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
                        { "privacy", model.PrivacySetting ?? "Private - Only me" },
                        { "owner", username }
                    };

                    // If friend emails are provided, store them in metadata
                    if (model.PrivacySetting == "Friends - Only people I choose" && model.FriendEmails != null)
                    {
                        // Store as JSON array in metadata
                        metadata.Add("authorized_friends", JsonSerializer.Serialize(model.FriendEmails));
                    }

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
                    // Check if the user has access to this photo based on privacy settings
                    bool hasAccess = false;
                    string privacySetting = "Private - Only me"; // Default

                    if (blobItem.Metadata.ContainsKey("privacy"))
                    {
                        privacySetting = blobItem.Metadata["privacy"];
                    }

                    // Check access permissions
                    if (privacySetting == "Public - Anyone with the link")
                    {
                        // Public photos are visible to everyone
                        hasAccess = true;
                    }
                    else if (privacySetting == "Private - Only me")
                    {
                        // Private photos are only visible to the owner
                        if (blobItem.Metadata.ContainsKey("owner") && blobItem.Metadata["owner"] == username)
                        {
                            hasAccess = true;
                        }
                    }
                    else if (privacySetting == "Friends - Only people I choose")
                    {
                        // Check if current user is the owner
                        if (blobItem.Metadata.ContainsKey("owner") && blobItem.Metadata["owner"] == username)
                        {
                            hasAccess = true;
                        }
                        // Check if current user is in the authorized friends list
                        else if (blobItem.Metadata.ContainsKey("authorized_friends"))
                        {
                            try
                            {
                                var authorizedFriends = JsonSerializer.Deserialize<List<string>>(blobItem.Metadata["authorized_friends"]);
                                if (authorizedFriends != null && authorizedFriends.Contains(username))
                                {
                                    hasAccess = true;
                                }
                            }
                            catch { /* If JSON parsing fails, skip this blob */ }
                        }
                    }

                    // Only add photos the user has access to
                    if (hasAccess)
                    {
                        var blobClient = container.GetBlobClient(blobItem.Name);

                        // Get friends list for owner view
                        List<string> friendsList = new List<string>();
                        if (blobItem.Metadata.ContainsKey("authorized_friends"))
                        {
                            try
                            {
                                friendsList = JsonSerializer.Deserialize<List<string>>(blobItem.Metadata["authorized_friends"]) ?? new List<string>();
                            }
                            catch { /* If JSON parsing fails, use empty list */ }
                        }

                        photos.Add(new PhotoItemVM
                        {
                            Name = blobItem.Name,
                            Url = blobClient.Uri.AbsoluteUri,
                            UploadDate = blobItem.Properties.CreatedOn?.DateTime ?? DateTime.MinValue,
                            Size = blobItem.Properties.ContentLength ?? 0,
                            PrivacySetting = privacySetting,
                            IsOwner = blobItem.Metadata.ContainsKey("owner") && blobItem.Metadata["owner"] == username,
                            AuthorizedFriends = friendsList
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

                var serviceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
                BlobServiceClient blobServiceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());
                BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);
                BlobClient blob = container.GetBlobClient(blobName);

                // Get blob metadata to check ownership
                var properties = await blob.GetPropertiesAsync();
                if (properties.Value.Metadata.ContainsKey("owner") && properties.Value.Metadata["owner"] != username)
                {
                    return Forbid();
                }

                await blob.DeleteIfExistsAsync();

                return RedirectToAction("Gallery");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePrivacy(string blobName, string privacySetting, string friendEmails)
        {
            try
            {
                var username = User.Identity.Name ?? "user";

                var serviceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
                BlobServiceClient blobServiceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());
                BlobContainerClient container = blobServiceClient.GetBlobContainerClient(containerName);
                BlobClient blob = container.GetBlobClient(blobName);

                // Get current metadata
                var properties = await blob.GetPropertiesAsync();
                IDictionary<string, string> metadata = properties.Value.Metadata;

                // Check ownership
                if (!metadata.ContainsKey("owner") || metadata["owner"] != username)
                {
                    return Forbid();
                }

                // Update privacy setting
                if (metadata.ContainsKey("privacy"))
                {
                    metadata["privacy"] = privacySetting;
                }
                else
                {
                    metadata.Add("privacy", privacySetting);
                }

                // Remove old friends list if exists
                if (metadata.ContainsKey("authorized_friends"))
                {
                    metadata.Remove("authorized_friends");
                }

                // Add new friends list if applicable
                if (privacySetting == "Friends - Only people I choose" && !string.IsNullOrWhiteSpace(friendEmails))
                {
                    // Parse comma-separated emails
                    var emails = friendEmails.Split(',', ';')
                        .Select(e => e.Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .ToList();

                    if (emails.Any())
                    {
                        metadata.Add("authorized_friends", JsonSerializer.Serialize(emails));
                    }
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