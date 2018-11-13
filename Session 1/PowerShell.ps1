#Donwload Azure CLI and PowerShell
#CLI Download MSI-Installation Program: https://docs.microsoft.com/de-de/cli/azure/install-azure-cli-windows?view=azure-cli-latest
#PowerShell: Install-Module -Name AzureRM


#Login
Connect-AzureRmAccount

#Set Variables

$location = "westeurope"
$resourceGroup = "RG-Edge-Schulung-Automatisierung"
$iHName = "IH-Edge-TI2009"
$deviceName = "WindpowerStation"
$saName = "MSA-SA-TI2009"
$storageAccountName = "msaskti2009"
$containerName = "msascti2009"
$jobInputName = "IHInput"
$jobOutputName = "MyBlobOutput"
$jobTransformationName = "MyJobTransformation"

$jobDefinition = "C:\Users\tom-marvin.ihme\Documents\GitHub\EdgeSchulung\Automatisierung\Skript aktuell (Tom)\SAFiles\JobDefinition.json"
$jobInputDefinitionFile = "C:\Users\tom-marvin.ihme\Documents\GitHub\EdgeSchulung\Automatisierung\Skript aktuell (Tom)\SAFiles\JobInputDefinition.json"
$jobOutputDefinitionFile = "C:\Users\tom-marvin.ihme\Documents\GitHub\EdgeSchulung\Automatisierung\Skript aktuell (Tom)\SAFiles\JobOutputDefinition.json"
$jobTransformationDefinitionFile = "C:\Users\tom-marvin.ihme\Documents\GitHub\EdgeSchulung\Automatisierung\Skript aktuell (Tom)\SAFiles\JobTransformationDefinition.json"


#Create Resource Group
New-AzureRmResourceGroup -Name $resourceGroup -Location $location

#Create IoT-Hub
New-AzureRmIotHub -ResourceGroupName $resourceGroup -Name $iHName -SkuName "S1" -Units 1 -Location $location

#Create Device (new Command)
az iot hub device-identity create --device-id $deviceName --hub-name $iHName


#Create Storage Account
$storageAccount = New-AzureRmStorageAccount -ResourceGroupName $resourceGroup -Name $storageAccountName -Location $location -SkuName "Standard_LRS" -Kind "Storage"
$ctx = $storageAccount.Context

#Create Storage Container
New-AzureStorageContainer -Name $containerName -Context $ctx

#Variable Storage Account Key
$storageAccountKey = (Get-AzureRmStorageAccountKey -ResourceGroupName $resourceGroup -Name $storageAccountName).Value[0]



#Create new SA Job
New-AzureRmStreamAnalyticsJob -ResourceGroupName $resourceGroup -File $jobDefinition -Name $saName

#Configure Input Json for SA
$a = Get-Content $jobInputDefinitionFile -raw | ConvertFrom-Json
$a.Properties.DataSource.Properties.iotHubNamespace = $iHName
$a.Properties.DataSource.Properties.sharedAccessPolicyKey = (Get-AzureRmIotHubKey -ResourceGroupName $resourceGroup -Name $iHName -KeyName "iothubowner").PrimaryKey
$a | ConvertTo-Json -Depth 5 | set-content $jobInputDefinitionFile

#Create IH Input for SA
New-AzureRmStreamAnalyticsInput -ResourceGroupName $resourceGroup -JobName $saName -File $jobInputDefinitionFile -Name $jobInputName

#Configure Output Json for SA
$a = Get-Content $jobOutputDefinitionFile -raw | ConvertFrom-Json
$a.Properties.DataSource.Properties.storageAccounts[0].accountName = $storageAccountName
$a.Properties.DataSource.Properties.storageAccounts[0].accountKey = (Get-AzureRmStorageAccountKey -ResourceGroupName $resourceGroup -AccountName $storageAccountName).Value[0]
$a.Properties.DataSource.Properties.container = $containerName
$a | ConvertTo-Json -Depth 5 | set-content $jobOutputDefinitionFile

#Create BLOB Output for SA
New-AzureRmStreamAnalyticsOutput -ResourceGroupName $resourceGroup -JobName $saName -File $jobOutputDefinitionFile -Name $jobOutputName

#Create Query for SA Job
New-AzureRmStreamAnalyticsTransformation -ResourceGroupName $resourceGroup -JobName $saName -File $jobTransformationDefinitionFile -Name $jobTransformationName -Force

#Start SA Job
Start-AzureRmStreamAnalyticsJob -ResourceGroupName $resourceGroup -Name $saName -OutputStartMode "JobStartTime"

#Print ConnectionString for Device
Write-Host "ConnectionString for Device: " -ForegroundColor Magenta (az iot device show-connection-string --hub-name $iHName --device-id $deviceName | ConvertFrom-Json).connectionString














