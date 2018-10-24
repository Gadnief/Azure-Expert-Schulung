// Version 0.3.1

namespace SampleModule
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
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    class Program
    {
        static int counter;

        //Windpowerstation Data
        static string stationID = "default";
        static int messageID = 0;
        static bool generating = false;
        static bool service = false;
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

            // Register callback to be called when a message is received by the module
            // await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);

            //Register Direct Method for Service
            await ioTHubModuleClient.SetMethodHandlerAsync("ServiceOn",ServiceOn,null);
            await ioTHubModuleClient.SetMethodHandlerAsync("ServiceOff",ServiceOff,null);

            //Register Direct Method for Emergency
            await ioTHubModuleClient.SetMethodHandlerAsync("TurnOn",TurnOn,null);
            await ioTHubModuleClient.SetMethodHandlerAsync("TurnOff",TurnOff,null);

            //Register Direct Method for Windspeed
            await ioTHubModuleClient.SetMethodHandlerAsync("SetWindspeed",SetWindspeed,null);
            await ioTHubModuleClient.SetMethodHandlerAsync("ServiceRequest",ServiceRequest,null);

            await ioTHubModuleClient.SetMethodHandlerAsync("AnomalyOn",AnomalyOn,null);
            await ioTHubModuleClient.SetMethodHandlerAsync("AnomalyOff",AnomalyOff,null);

            return ioTHubModuleClient;
        }

        /// <summary>
        /// Handles changes in device twin
        /// </summary>
        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            var moduleClient = userContext as ModuleClient;

            string inputWind = JsonConvert.SerializeObject(desiredProperties, Formatting.Indented);
            Wind wind = JsonConvert.DeserializeObject<Wind>(inputWind);

            Console.WriteLine("windspeed");
            speed = Int32.Parse(JsonConvert.SerializeObject(wind.newWindspeed));
            Console.WriteLine(speed);
            TwinCollection reportedProperties = new TwinCollection();

            await moduleClient.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
        }

        /// <summary>
        /// Enables loggin in module
        /// </summary>
        private static async Task<MethodResponse> ServiceOn (MethodRequest request, object userContext)
        {
            Console.WriteLine("Turned on log for service!");
            service = true;
            return new MethodResponse(200);
        }

        private static async Task<MethodResponse> ServiceOff (MethodRequest request, object userContext)
        {
            Console.WriteLine("Turned off log for service!");
            service = false;
            return new MethodResponse(200);
        }

        /// <summary>
        /// Turns datageneration off
        /// </summary>
        private static async Task<MethodResponse> TurnOff (MethodRequest request, object userContext)
        {
            Console.WriteLine("GENERATOR TURNED OFF");
            generating = false;
            return new MethodResponse(200);
        }

        private static async Task<MethodResponse> TurnOn (MethodRequest request, object userContext)
        {
            Console.WriteLine("GENERATOR TURNED ON");
            generating = true;
            return new MethodResponse(200);
        }
        
        /// <summary>
        /// Turns anomalygeneration on
        /// </summary>
        private static async Task<MethodResponse> AnomalyOn (MethodRequest request, object userContext)
        {
            anomalie = true;
            Console.WriteLine("GENERATING ANOMALY");
            return new MethodResponse(200);
        }

        private static async Task<MethodResponse> AnomalyOff (MethodRequest request, object userContext)
        {
            anomalie = false;
            Console.WriteLine("GENERATING NORMAL DATA");
            return new MethodResponse(200);
        }

        /// <summary>
        /// Method to set windspeed
        /// </summary>
        private static async Task<MethodResponse> SetWindspeed (MethodRequest request, object userContext)
        {
            Wind windObj = new Wind();
            windObj = JsonConvert.DeserializeObject<Wind>(request.DataAsJson);

            speed = windObj.newWindspeed;
            Console.WriteLine("New Windspeed: " + speed);
            return new MethodResponse(200);
        }   

        /// <summary>
        /// Method to set general device information
        /// </summary>
        private static async Task<MethodResponse> ServiceRequest (MethodRequest request, object userContext)
        {
            ServiceRequestObj requestObject = new ServiceRequestObj();
            requestObject = JsonConvert.DeserializeObject<ServiceRequestObj>(request.DataAsJson);

            Console.WriteLine("New Device Name: " + requestObject.newDeviceName);
            stationID = requestObject.newDeviceName;

            

            return new MethodResponse(200);
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
                    else
                    {
                        //Anomaly
                        data = new
                        {
                            messageID = messageID,
                            eventTime = DateTime.Now,
                            stationID = stationID,
                            rotorSpeed = rnd.Next(19, 21),
                            gearTemp = rnd.Next(84, 86),
                            generatorRotation = rnd.Next(1550, 1600),
                            envTemp = rnd.Next(4, 6),
                            voltage = rnd.Next(390, 400),
                            windspeed = speed,
                            pitch = speed + 42,
                            output = Math.Round((double)rnd.Next(speed * 12, speed * 12), 1)
                        };
                    }
                    
                    messageID++;        

                    //Moduletwin Update
                    TwinCollection reportedProps = new TwinCollection();
                    reportedProps["messageID"] = data;

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

                    moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null).Wait();

                } 
                else
                {
                await Task.Delay(1000);
                }                  
            }  
        }        
    }
}
