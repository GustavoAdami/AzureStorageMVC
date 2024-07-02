using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Azure.Storage.Blobs;
using Azure;
using Models;
using System.IO;

namespace Lab5.Controllers
{
    public class SmileyController : Controller
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName = "smilies";

        public SmileyController(BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient;
        }

        public async Task<IActionResult> Index()
        {
            var containerClient = await GetOrCreateContainerClientAsync();
            if (containerClient == null)
            {
                return View("Error");
            }

            var smilies = containerClient.GetBlobs().Select(blob => new Smiley
            {
                FileName = blob.Name,
                Url = containerClient.GetBlobClient(blob.Name).Uri.AbsoluteUri
            }).ToList();

            return View(smilies);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormFile file)
        {
            var containerClient = await GetOrCreateContainerClientAsync();
            if (containerClient == null)
            {
                return View("Error");
            }

            var randomFileName = Path.GetRandomFileName();
            var blockBlob = containerClient.GetBlobClient(randomFileName);

            try
            {
                await UploadFileToBlobAsync(file, blockBlob);
            }
            catch (RequestFailedException)
            {
                return View("Error");
            }

            return RedirectToAction("Index");
        }

        // For multiple files, use this
        //public async Task<IActionResult> Create(ICollection<IFormFile> files)
        //{

        //    BlobContainerClient containerClient;
        //    // Create the container and return a container client object
        //    try
        //    {
        //        containerClient = await _blobServiceClient.CreateBlobContainerAsync(containerName);
        //        containerClient.SetAccessPolicy(Azure.Storage.Blobs.Models.PublicAccessType.BlobContainer);
        //    }
        //    catch (RequestFailedException e)
        //    {
        //        containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        //    }

        //    foreach (var file in files)
        //    {
        //        try
        //        {
        //            // create the blob to hold the data
        //            var blockBlob = containerClient.GetBlobClient(file.FileName);
        //            if (await blockBlob.ExistsAsync())
        //            {
        //                await blockBlob.DeleteAsync();
        //            }

        //            using (var memoryStream = new MemoryStream())
        //            {
        //                // copy the file data into memory
        //                await file.CopyToAsync(memoryStream);

        //                // navigate back to the beginning of the memory stream
        //                memoryStream.Position = 0;

        //                // send the file to the cloud
        //                await blockBlob.UploadAsync(memoryStream);
        //                memoryStream.Close();
        //            }

        //        }
        //        catch (RequestFailedException e)
        //        {

        //        }
        //    }
        //    return RedirectToAction("Index");
        //}

        public async Task<IActionResult> Delete()
        {
            return View();
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed()
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            try
            {
                await DeleteAllBlobsAsync(containerClient);
            }
            catch (RequestFailedException)
            {
                return View("Error");
            }

            return RedirectToAction("Index");
        }

        private async Task<BlobContainerClient> GetOrCreateContainerClientAsync()
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            if (await containerClient.ExistsAsync())
            {
                return containerClient;
            }
            else
            {
                containerClient = await _blobServiceClient.CreateBlobContainerAsync(_containerName, Azure.Storage.Blobs.Models.PublicAccessType.BlobContainer);
                return containerClient;
            }
        }

        private async Task UploadFileToBlobAsync(IFormFile file, BlobClient blobClient)
        {
            if (await blobClient.ExistsAsync())
            {
                await blobClient.DeleteAsync();
            }

            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                await blobClient.UploadAsync(memoryStream);
            }
        }

        private async Task DeleteAllBlobsAsync(BlobContainerClient containerClient)
        {
            await foreach (var blob in containerClient.GetBlobsAsync())
            {
                var blobClient = containerClient.GetBlobClient(blob.Name);
                if (await blobClient.ExistsAsync())
                {
                    await blobClient.DeleteAsync();
                }
            }
        }
    }
}
