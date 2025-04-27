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
                var filename = GenerateFileName(model.myFile.FileName, username);

                var fileUrl = "";

                var serviceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
                BlobServiceClient blobServiceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());

                BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);
                await container.CreateIfNotExistsAsync();

                BlobClient blob = container.GetBlobClient(filename);

                using (Stream stream = model.myFile.OpenReadStream())
                {
                    await blob.UploadAsync(stream);
                }

                fileUrl = blob.Uri.AbsoluteUri;
                return Ok(fileUrl);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        private string GenerateFileName(string fileName, string CustomerName)
        {
            try
            {
                string strFileName = string.Empty;
                string[] strName = fileName.Split('.');
                strFileName = CustomerName + DateTime.Now.ToUniversalTime().ToString("yyyyMMdd\\THHmmssfff") + "." + strName[strName.Length - 1];
                return strFileName;
            }
            catch (Exception ex)
            {
                return fileName;
            }
        }
    }
}
