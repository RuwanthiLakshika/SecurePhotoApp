using Microsoft.AspNetCore.Mvc;
using SecurePhotoApp.Models;
using Azure.Storage.Blobs;
using Azure.Identity;
using Azure.Storage.Blobs.Models;

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

        private string GenerateFileName(string fileName, string displayName)
        {
            try
            {
                string[] strName = fileName.Split('.');
                string extension = strName[^1]; 
                string newFileName = displayName + DateTime.UtcNow.ToString("yyyyMMdd\\THHmmssfff") + "." + extension;
                return newFileName;
            }
            catch (Exception)
            {
                return fileName; 
            }
        }

        public async Task<IActionResult> Gallery(string searchTerm = "", string sortOrder = "newest")
        {
            try
            {
                var username = User.Identity.Name ?? "user";
                var serviceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");

                BlobServiceClient blobServiceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());

                BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);

                var blobItems = new List<BlobItem>();
                await foreach (var blobItem in container.GetBlobsAsync())
                {
                    blobItems.Add(blobItem);
                }

                var userPhotos = blobItems.Where(b => b.Name.StartsWith(username));

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    userPhotos = userPhotos.Where(b => b.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                }

                switch (sortOrder.ToLower())
                {
                    case "oldest":
                        userPhotos = userPhotos.OrderBy(b => b.Properties.CreatedOn);
                        break;
                    case "name":
                        userPhotos = userPhotos.OrderBy(b => b.Name);
                        break;
                    case "size":
                        userPhotos = userPhotos.OrderByDescending(b => b.Properties.ContentLength);
                        break;
                    case "newest":
                    default:
                        userPhotos = userPhotos.OrderByDescending(b => b.Properties.CreatedOn);
                        break;
                }

                var photos = new List<PhotoItemVM>();
                foreach (var blobItem in userPhotos)
                {
                    var blobClient = container.GetBlobClient(blobItem.Name);

                    photos.Add(new PhotoItemVM
                    {
                        Name = blobItem.Name,
                        Url = blobClient.Uri.AbsoluteUri,
                        UploadDate = blobItem.Properties.CreatedOn?.DateTime ?? DateTime.MinValue,
                        Size = blobItem.Properties.ContentLength ?? 0
                    });
                }

                var model = new GalleryVM
                {
                    Photos = photos,
                    SearchTerm = searchTerm,
                    SortOrder = sortOrder
                };

                return View(model);
            }
            catch (Exception ex)
            {
                return View("Error");
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeletePhoto(string blobName)
        {
            try
            {
                var username = User.Identity.Name ?? "user";

                if (!blobName.StartsWith(username))
                {
                    return Forbid();
                }

                var serviceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");

                BlobServiceClient blobServiceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());

                BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);

                BlobClient blob = container.GetBlobClient(blobName);
                await blob.DeleteIfExistsAsync();

                return RedirectToAction("Gallery");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

    }
}
    
