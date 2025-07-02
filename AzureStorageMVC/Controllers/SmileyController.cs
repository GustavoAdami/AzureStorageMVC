using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Azure.Storage.Blobs;
using Azure;
using AzureStorageMVC.Models;
using System.IO;

namespace AzureStorageMVC.Controllers
{
    public class SmileyController : Controller
    {
        /*
         * BlobServiceClient is the entry point for working with your Azure Blob Storage account.
         * It lets you manage containers (create, list, delete).
         */
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName = "smilies";

        public SmileyController(BlobServiceClient blobServiceClient)
        {
            _blobServiceClient = blobServiceClient;
        }

        /* 
         * Index action retrieves all blobs in the specified container and returns them to the view.
         * It creates a BlobContainerClient for the specified container, and if it does not exist, it creates it with public access.
         * The blobs are then mapped to a list of Smiley objects containing the file name and URL.
         */
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

        /* Performs the file upload operation.
         * It validates the file, uploads it to Azure Blob Storage, and redirects to the Index action.
         * Creates a random file name to avoid user malicious file name manipulation.
         */
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormFile file)
        {

            // Validate file is not null or empty
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("FileName", "Please select a file to upload.");
                return View();
            }

            // Validate file max size
            if (file.Length > 10 * 1024 * 1024) // 10 MB limit
            {
                ModelState.AddModelError("FileName", "File size must not exceed 10 MB.");
                return View();
            }

            // Validate file extension
            string[] permittedExtensions = { ".txt", ".jpg", ".png" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(fileExtension) || !permittedExtensions.Contains(fileExtension))
            {
                ModelState.AddModelError("FileName", "Only .txt, .jpg, and .png files are allowed.");
                return View();
            }

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
        //        containerClient = await _blobServiceClient.CreateBlobContainerAsync(_containerName);
        //        containerClient.SetAccessPolicy(Azure.Storage.Blobs.Models.PublicAccessType.BlobContainer);
        //    }
        //    catch (RequestFailedException e)
        //    {
        //        containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
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

        /*
         * Deletes all blobs in the specified container.
         */
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

        /* Gets a BlobContainerClient for the specified container. 
         * If the container does not exist, it creates the container with public access.
         */
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

        /* Uploads a file to Azure Blob Storage using the provided BlobClient.
         * If a blob with the same name already exists, it deletes the existing blob before uploading the new file.
         * The file is first copied into a MemoryStream (in-memory buffer), then uploaded to the blob storage.
         */
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

        /*
         * BlobContainerClient is focused on a single container.
         * It lets you manage blobs (files) inside that container—such as uploading, downloading, listing, and deleting blobs.
         * 
         * Think of BlobServiceClient as the manager of a storage facility, and BlobContainerClient as the key to a specific storage unit (container) inside that facility.
         * You use the manager to access a unit, and then use the key to work with items inside that unit
         */
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
