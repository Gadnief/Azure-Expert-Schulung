namespace Telemetriedata
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    class Program
    {
        static int counter;

        //Windpowerstation Data
        static string stationID = "default";
        static int messageID = 0;
        static bool generating = true;
        static bool service = true;
        static bool anomalie = false;
        static int speed = 25;
        
        static void Main(string[] args)
        {
            Run().Wait();
        }
        static async Task Run(){
            var moduleClient = await Init();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            GenerateData(moduleClient);
            await WhenCancelled(cts.Token);
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task<ModuleClient> Init()
        {
            AmqpTransportSettings amqpSetting = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
            ITransportSettings[] settings = { amqpSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            return ioTHubModuleClient;
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                var pipeMessage = new Message(messageBytes);
                foreach (var prop in message.Properties)
                {
                    pipeMessage.Properties.Add(prop.Key, prop.Value);
                }
                await moduleClient.SendEventAsync("output1", pipeMessage);
                Console.WriteLine("Received message sent");
            }
            return MessageResponse.Completed;
        }

        /// <summary>
        /// Data generation
        /// </summary>
        private static async void GenerateData(object userContext)
        {
            Console.WriteLine("STARTING DATA GENERATION...");
            var moduleClient = userContext as ModuleClient;
            while(true)
            {                                            
                if(generating == true)
                {                   
                    Random rnd = new Random();
                    var data = new Object();

                    if (anomalie == false)
                    {
                        //No Anomaly
                        data = new
                        {                        
                            messageID = messageID,
                            eventTime = DateTime.Now,
                            stationID = stationID,
                            rotorSpeed = rnd.Next(24, 27),
                            gearTemp = rnd.Next(68, 70),
                            generatorRotation = rnd.Next(940, 1000),
                            envTemp = rnd.Next(9, 10),
                            voltage = rnd.Next(398, 401),
                            windspeed = speed,
                            pitch = speed + 42,
                            output = Math.Round((double)rnd.Next(speed * 12, speed * 12), 1)
                        };
                    }
                     messageID++;                          

                    //Send Data to output1
                    var jsonData = JsonConvert.SerializeObject(data);
                    if(service==true) Console.WriteLine($"SUBMIT: {jsonData}");
                    var dataBytes = Encoding.UTF8.GetBytes(jsonData);

                    try{
                        await moduleClient.SendEventAsync("output1",new Message(dataBytes));
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine("Error sending message: " + e);
                    }
                    
                    await Task.Delay(1000);                       
                } 
                else
                {
                await Task.Delay(1000);
                }                  
            }  
        }        
    }
}
