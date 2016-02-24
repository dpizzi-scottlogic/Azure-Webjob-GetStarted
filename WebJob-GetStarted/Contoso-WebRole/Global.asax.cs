using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;
using System.Diagnostics;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Contoso_WebRole
{
    public class MvcApplication : System.Web.HttpApplication
    {
        public static string storageConnectionStringKey = "AzureContosoStorage";
        public static string blobKey = "images";
        public static string queueKey = "thumbmailrequest";

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            InitializeStorage();
        }

        private void InitializeStorage()
        {
            Trace.TraceInformation("Opening storage account using credentials");
            var connectionString = ConfigurationManager.ConnectionStrings[storageConnectionStringKey].ToString();
            var storageAccount = CloudStorageAccount.Parse(connectionString);

            Trace.TraceInformation("Creating images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            var imagesBlobReference = blobClient.GetContainerReference(blobKey);
            if (imagesBlobReference.CreateIfNotExists())
            {
                Trace.TraceInformation("Enables public access to images blob container");
                imagesBlobReference.SetPermissions(
                    new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    });
            }

            Trace.TraceInformation("Create thumbmailrequest queue");
            var queueClient = storageAccount.CreateCloudQueueClient();
            var thumbmailQueueReference = queueClient.GetQueueReference(queueKey);
            thumbmailQueueReference.CreateIfNotExists();

            Trace.TraceInformation("Storage created");
        }
    }
}
