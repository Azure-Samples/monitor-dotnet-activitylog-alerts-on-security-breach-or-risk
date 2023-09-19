// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Storage.Models;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Monitor;
using Azure.ResourceManager.Monitor.Models;

namespace SecurityBreachOrRiskActivityLogAlerts
{
    public class Program
    {
        /**
         * This sample shows examples of configuring Activity Log Alerts for potential security breach or risk notifications.
         *  - Create a storage account
         *  - Setup an action group to trigger a notification to the security teams
         *  - Create an activity log alerts for storage account access key retrievals
         *  - List Storage account keys to trigger an alert.
         *  - Retrieve and show all activity logs that contains "List Storage Account Keys" operation name in the resource group for the past 7 days for the same Storage account.
         */
        private static ResourceIdentifier? _resourceGroupId = null;
        public static async Task RunSample(ArmClient client)
        {
           
            try
            {
                // ============================================================

                // Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();
                var rgName = Utilities.CreateRandomName("rgMonitor");
                Utilities.Log($"creating a resource group with name : {rgName}...");
                var rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS2));
                var resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log("Created a resource group with name: " + resourceGroup.Data.Name);

                //Create a storage account
                Utilities.Log("Creating a storage account...");
                var storageCollection = resourceGroup.GetStorageAccounts();
                var accountName = Utilities.CreateRandomName("samonitor");
                var sku = new StorageSku(StorageSkuName.StandardGrs);
                var kind = StorageKind.BlobStorage;
                var content = new StorageAccountCreateOrUpdateContent(sku, kind, AzureLocation.EastUS2)
                {
                    AccessTier = StorageAccountAccessTier.Cool
                };
                var storageAccountLro = await storageCollection.CreateOrUpdateAsync(WaitUntil.Completed, accountName, content);
                var storageAccount = storageAccountLro.Value;
                Utilities.Log("Created a storage account with name : " + storageAccount.Data.Name);

                // ============================================================

                // Create an action group to send notifications in case activity log alert condition will be triggered
                Utilities.Log("Creating actionGroup...");
                var actionGroupName = Utilities.CreateRandomName("securityBreachActionGroup");
                var actionGroupCollection = resourceGroup.GetActionGroups();
                Uri uri = new Uri("https://www.weseemstobehacked.securecorporation.com");
                var actionGroupData = new ActionGroupData(AzureLocation.NorthCentralUS)
                {
                    GroupShortName = "AG",
                    IsEnabled = true,
                    AzureAppPushReceivers =
                    {
                        new MonitorAzureAppPushReceiver("MAAPRtierOne","security_on_duty@securecorporation.com")
                    },
                    EmailReceivers =
                    {
                        new MonitorEmailReceiver("MERtierOne","security_guards@securecorporation.com"),
                        new MonitorEmailReceiver("MERtierTwo","ceo@securecorporation.com")
                    },
                    SmsReceivers =
                    {
                        new MonitorSmsReceiver("MSRtierOne","1","4255655665")
                    },
                    VoiceReceivers =
                    {
                        new MonitorVoiceReceiver("MVRtierOne","1","2062066050")
                    },
                    WebhookReceivers =
                    {
                        new MonitorWebhookReceiver("MWRtierOne",uri)
                    }
                };
                var actionGroup =(await actionGroupCollection.CreateOrUpdateAsync(WaitUntil.Completed,actionGroupName,actionGroupData)).Value;
                Utilities.Log("Created actionGroup with name:" + actionGroup.Data.Name);
           
                // ============================================================

                // Set a trigger to fire each time
                Utilities.Log("Creating activityLogAlert...");
                var alertRuleCollection = resourceGroup.GetActivityLogAlerts();
                var ruleName = Utilities.CreateRandomName("alertRule");
                var alertData = new ActivityLogAlertData("global")
                {
                    ConditionAllOf = new List<ActivityLogAlertAnyOfOrLeafCondition>()
                    {
                        new()
                        {
                            Field = "category",
                            EqualsValue = "Security",
                        },
                        new() 
                        {
                            Field = "resourceId",
                            EqualsValue = storageAccount.Id
                        },
                        new()
                        { 
                            Field = "operationName",
                            EqualsValue = "Microsoft.Storage/storageAccounts/listkeys/action"
                        }
                    },
                    IsEnabled = true,
                    Description = "Security StorageAccounts ListAccountKeys trigger",
                    ActionsActionGroups =
                    {
                        new ActivityLogAlertActionGroup(actionGroup.Id)
                    },
                    Scopes =
                    {
                        "/subscriptions/"+Environment.GetEnvironmentVariable("SUBSCRIPTION_ID")
                    }
                };
                var activityLogAlert = (await alertRuleCollection.CreateOrUpdateAsync(WaitUntil.Completed, ruleName,alertData)).Value;
                Utilities.Log("Created activityLogAlert with name : " + activityLogAlert.Data.Name);
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group: {_resourceGroupId}");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId}");
                    }
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Utilities.Log(g);
                }
            }
        }
        public static async Task Main(string[] args)
        {
            try
            {
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);
                await RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}
