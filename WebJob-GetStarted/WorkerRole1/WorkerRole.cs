using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Webjob_GetStarted_Common;
using Microsoft.WindowsAzure.Storage.Blob;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {

        private CloudQueue imagesQueue;
        private CloudBlobContainer imagesBlobContainer;
        private ContosoAdsContext db;

        public override void Run()
        {
            Trace.TraceInformation("WorkerRole1 entry point called");
            CloudQueueMessage message = null;
            // To make the worker role more scalable, implement multi-threaded and 
            // asynchronous code. See:
            // http://msdn.microsoft.com/en-us/library/ck8bc5c6.aspx
            // http://www.asp.net/aspnet/overview/developing-apps-with-windows-azure/building-real-world-cloud-apps-with-windows-azure/web-development-best-practices#async
            while (true)
            {
                try
                {
                    message = imagesQueue.GetMessage();
                    if (message != null)
                    {
                        ProcessQueueMessage(message);
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (StorageException exc)
                {

                    if (message != null && message.DequeueCount > 5)
                    {
                        imagesQueue.DeleteMessage(message);
                        Trace.TraceError("Deleting poison queue item: '{0}'", message.AsString);
                    }
                    Trace.TraceError("Exception in ContosoAdsWorker: '{0}'", exc.Message);
                    Thread.Sleep(5000);
                }
            }

        }

        private void ProcessQueueMessage(CloudQueueMessage message)
        {
            Trace.TraceInformation("Processing queue message {0}", message);

            var adId = int.Parse(message.AsString);
            Trace.TraceInformation("Retrieving AdId {0}", adId);

            Ad ad = db.Ads.Find(adId);
            if (ad == null)
            {
                throw new Exception(string.Format("AdId {0} not found, can't create thumbnail", adId.ToString()));
            }

            Uri blobUri = new Uri(ad.ImageURL);
            string blobName = blobUri.Segments[blobUri.Segments.Length - 1];

            CloudBlockBlob inputBlob = imagesBlobContainer.GetBlockBlobReference(blobName);
            string thumbnailName = Path.GetFileNameWithoutExtension(inputBlob.Name) + "thumb.jpg";
            CloudBlockBlob outputBlob = imagesBlobContainer.GetBlockBlobReference(thumbnailName);

            using (Stream input = inputBlob.OpenRead())
            using (Stream output = outputBlob.OpenWrite())
            {
                ConvertImageToThumbnailJPG(input, output);
                outputBlob.Properties.ContentType = "image.jpeg";
            }

            Trace.TraceInformation("Generated thumbnail in blob {0}", thumbnailName);

            ad.ThumbnailURL = outputBlob.Uri.ToString();
            db.SaveChanges();
            Trace.TraceInformation("Updated thumbnail URL in database: {0}", ad.ThumbnailURL);

            // Remove message from queue.
            this.imagesQueue.DeleteMessage(message);
        }

        public void ConvertImageToThumbnailJPG(Stream input, Stream output)
        {
            int thumbnailsize = 80;
            int width;
            int height;
            var originalImage = new Bitmap(input);

            if (originalImage.Width > originalImage.Height)
            {
                width = thumbnailsize;
                height = thumbnailsize * originalImage.Height / originalImage.Width;
            }
            else
            {
                height = thumbnailsize;
                width = thumbnailsize * originalImage.Width / originalImage.Height;
            }

            Bitmap thumbnailImage = null;
            try
            {
                thumbnailImage = new Bitmap(width, height);

                using (Graphics graphics = Graphics.FromImage(thumbnailImage))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(originalImage, 0, 0, width, height);
                }

                thumbnailImage.Save(output, ImageFormat.Jpeg);
            }
            finally
            {
                if (thumbnailImage != null)
                {
                    thumbnailImage.Dispose();
                }
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections.
            ServicePointManager.DefaultConnectionLimit = 12;

            // Read database connection string and open database.
            var dbConnString = RoleEnvironment.GetConfigurationSettingValue("ContosoAdsDbConnectionString");
            db = new ContosoAdsContext(dbConnString);
            // Open storage account using credentials from .cscfg file.
            var storageAccount = CloudStorageAccount.Parse
                (RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));

            Trace.TraceInformation("Creating images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            imagesBlobContainer = blobClient.GetContainerReference("images");
            if (imagesBlobContainer.CreateIfNotExists())
            {
                // Enable public access on the newly created "images" container.
                imagesBlobContainer.SetPermissions(
                    new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    });
            }

            Trace.TraceInformation("Creating images queue");
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            imagesQueue = queueClient.GetQueueReference("images");
            imagesQueue.CreateIfNotExists();

            Trace.TraceInformation("Storage initialized");
            return base.OnStart();
        }
    }
}
