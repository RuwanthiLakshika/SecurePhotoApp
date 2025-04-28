using Microsoft.AspNetCore.Mvc;
using SecurePhotoApp.Models;
using Azure.Storage.Blobs;
using Azure.Identity;

namespace SecurePhotoApp.Controllers
{
    public class PhotoController : Controller
    {
        private readonly string storageAccountName = "photostorageaccount";
        private readonly string containerName = "photocontainer";

        public IActionResult Add()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(PhotoVM model)
        {
            try
            {
                var username = User.Identity.Name ?? "user";  
                var uploadedUrls = new List<string>();

                var serviceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");

                BlobServiceClient blobServiceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());

                BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);
                await container.CreateIfNotExistsAsync();

                foreach (var file in model.myFiles)
                {
                    var filename = GenerateFileName(file.FileName, username);
                    BlobClient blob = container.GetBlobClient(filename);

                    using (var stream = file.OpenReadStream())
                    {
                        await blob.UploadAsync(stream);
                    }

                    uploadedUrls.Add(blob.Uri.AbsoluteUri);
                }

                return View("UploadSuccess", uploadedUrls);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private string GenerateFileName(string fileName, string customerName)
        {
            try
            {
                string[] strName = fileName.Split('.');
                string extension = strName[^1]; 
                string newFileName = customerName + DateTime.UtcNow.ToString("yyyyMMdd\\THHmmssfff") + "." + extension;
                return newFileName;
            }
            catch (Exception)
            {
                return fileName; 
            }
        }
    }
}
