using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Webjob_GetStarted_Common;

namespace Contoso_WebRole.Controllers
{
    public class AdController : Controller
    {
        private ContosoAdsContext db = new ContosoAdsContext();
        private CloudQueue thumbmailRequestQueue;
        private static CloudBlobContainer imagesBlobContainer;

        public AdController()
        {
            InitializeStorage();
        }

        private void InitializeStorage()
        {
            var connectionString = ConfigurationManager.ConnectionStrings[MvcApplication.storageConnectionStringKey].ToString();

            var storageAccount = CloudStorageAccount.Parse(connectionString);

            var blobClient = storageAccount.CreateCloudBlobClient();
            blobClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(new TimeSpan(0, 0, 3), 3);

            imagesBlobContainer = blobClient.GetContainerReference(MvcApplication.blobKey);

            var queueClient = storageAccount.CreateCloudQueueClient();
            queueClient.DefaultRequestOptions.RetryPolicy = new LinearRetry(new TimeSpan(0, 0, 3), 3);

            thumbmailRequestQueue = queueClient.GetQueueReference(MvcApplication.queueKey);
        }

        // GET: Ad
        public async Task<ActionResult> Index(int? category)
        {
            // This code executes an unbounded query; don't do this in a production app,
            // it could return too many rows for the web app to handle. For an example
            // of paging code, see:
            // http://www.asp.net/mvc/tutorials/getting-started-with-ef-using-mvc/sorting-filtering-and-paging-with-the-entity-framework-in-an-asp-net-mvc-application
            var adsList = db.Ads.AsQueryable();
            if (category != null)
            {
                adsList = adsList.Where(a => a.Category == (Category)category);
            }
            return View(await adsList.ToListAsync());
        }

        // GET: Ad/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Ad ad = await db.Ads.FindAsync(id);
            if (ad == null)
            {
                return HttpNotFound();
            }
            return View(ad);
        }

        // GET: Ad/Create
        public ActionResult Create()
        {
            return View();
        }

        //POST: Ad/Create
        public async Task<ActionResult> Create(
            [Bind(Include ="Title,Proce,Description,Category,Phone")]Ad ad,
            HttpPostedFileBase imageFile)
        {
            if (ModelState.IsValid)
            {
                CloudBlockBlob imageBlob = null;

                if(imageFile != null && imageFile.ContentLength != 0)
                {
                    Trace.TraceInformation("Uploading imagefile {0}", imageFile.FileName);
                    imageBlob = await UploadAndSaveBlobAsync(imageFile);
                    ad.ImageURL = imageBlob.Uri.ToString();
                }
                ad.PostedDate = DateTime.Now;
                db.Ads.Add(ad);
                await db.SaveChangesAsync();
                Trace.TraceInformation("Created AdId {0} in database", ad.AdId);

                if(imageBlob != null)
                {
                    Trace.TraceInformation("Creating queue message for AdId {0}", ad.AdId);
                    BlobInformation blobInfo = new BlobInformation { AdId = ad.AdId, BlobUri = imageBlob.Uri };
                    await thumbmailRequestQueue.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(blobInfo)));
                    Trace.TraceInformation("Created queue message for AdId {0}", ad.AdId);
                }

                return RedirectToAction("Index");
            }

            return View(ad);
        }

        private Task<CloudBlockBlob> UploadAndSaveBlobAsync(HttpPostedFileBase imageFile)
        {
            throw new NotImplementedException();
        }

        // GET: Ad/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Ad ad = await db.Ads.FindAsync(id);
            if (ad == null)
            {
                return HttpNotFound();
            }
            return View(ad);
        }
    }
}