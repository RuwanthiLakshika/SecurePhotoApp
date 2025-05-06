using Microsoft.AspNetCore.Mvc;
using SecurePhotoApp.Models;
using Azure.Storage.Blobs;
using Azure.Identity;
using Azure.Storage.Blobs.Models;
using System.Text.Json;

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

                    // Create metadata with privacy setting
                    var metadata = new Dictionary<string, string>
                    {
                        { "privacy", model.PrivacySetting ?? "Private - Only me" }
                    };

                    // Set the blob's metadata
                    var blobHttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = file.ContentType
                    };

                    using (var stream = file.OpenReadStream())
                    {
                        await blob.UploadAsync(stream, new BlobUploadOptions
                        {
                            HttpHeaders = blobHttpHeaders,
                            Metadata = metadata
                        });
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

                var photos = new List<PhotoItemVM>();
                await foreach (var blobItem in container.GetBlobsAsync(BlobTraits.Metadata))
                {
                    if (blobItem.Name.StartsWith(username))
                    {
                        var blobClient = container.GetBlobClient(blobItem.Name);

                        // Get privacy setting from metadata
                        string privacySetting = "Private - Only me"; // Default
                        if (blobItem.Metadata.ContainsKey("privacy"))
                        {
                            privacySetting = blobItem.Metadata["privacy"];
                        }

                        photos.Add(new PhotoItemVM
                        {
                            Name = blobItem.Name,
                            Url = blobClient.Uri.AbsoluteUri,
                            UploadDate = blobItem.Properties.CreatedOn?.DateTime ?? DateTime.MinValue,
                            Size = blobItem.Properties.ContentLength ?? 0,
                            PrivacySetting = privacySetting
                        });
                    }
                }

                // Apply search filter if provided
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    photos = photos.Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                // Apply sorting
                switch (sortOrder.ToLower())
                {
                    case "oldest":
                        photos = photos.OrderBy(p => p.UploadDate).ToList();
                        break;
                    case "name":
                        photos = photos.OrderBy(p => p.Name).ToList();
                        break;
                    case "size":
                        photos = photos.OrderByDescending(p => p.Size).ToList();
                        break;
                    case "newest":
                    default:
                        photos = photos.OrderByDescending(p => p.UploadDate).ToList();
                        break;
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

        [HttpPost]
        public async Task<IActionResult> UpdatePrivacy(string blobName, string privacySetting)
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

                // Get current metadata
                var properties = await blob.GetPropertiesAsync();
                IDictionary<string, string> metadata = properties.Value.Metadata;

                // Update privacy setting
                if (metadata.ContainsKey("privacy"))
                {
                    metadata["privacy"] = privacySetting;
                }
                else
                {
                    metadata.Add("privacy", privacySetting);
                }

                // Set the updated metadata
                await blob.SetMetadataAsync(metadata);

                TempData["SuccessMessage"] = "Privacy settings updated successfully.";
                return RedirectToAction("Gallery");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to update privacy settings: " + ex.Message;
                return RedirectToAction("Gallery");
            }
        }
    }
}