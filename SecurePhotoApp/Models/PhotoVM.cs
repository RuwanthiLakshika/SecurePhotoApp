using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace SecurePhotoApp.Models
{
    public class PhotoVM
    {
        public List<IFormFile> myFiles { get; set; }
        public string PrivacySetting { get; set; } = "Private - Only me"; // Default privacy setting
        public List<string> FriendEmails { get; set; } = new List<string>(); // For friend-based access
    }

    public class PhotoItemVM
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public DateTime UploadDate { get; set; }
        public long Size { get; set; }
        public string PrivacySetting { get; set; } = "Private - Only me";
        public bool IsOwner { get; set; } = false; // To determine if current user is the owner
        public List<string> AuthorizedFriends { get; set; } = new List<string>(); // List of authorized friend emails

        // Helper properties
        public string FileName => System.IO.Path.GetFileName(Name);
        public string FormattedSize
        {
            get
            {
                if (Size < 1024)
                    return $"{Size} B";
                else if (Size < 1024 * 1024)
                    return $"{Size / 1024:F1} KB";
                else
                    return $"{Size / (1024 * 1024):F1} MB";
            }
        }
        public string FormattedDate => UploadDate.ToString("MMMM dd, yyyy");
        public string TimeAgo
        {
            get
            {
                var span = DateTime.Now - UploadDate;
                if (span.TotalDays > 365)
                    return $"{(int)(span.TotalDays / 365)} year{((int)(span.TotalDays / 365) == 1 ? "" : "s")} ago";
                if (span.TotalDays > 30)
                    return $"{(int)(span.TotalDays / 30)} month{((int)(span.TotalDays / 30) == 1 ? "" : "s")} ago";
                if (span.TotalDays > 1)
                    return $"{(int)span.TotalDays} day{((int)span.TotalDays == 1 ? "" : "s")} ago";
                if (span.TotalHours > 1)
                    return $"{(int)span.TotalHours} hour{((int)span.TotalHours == 1 ? "" : "s")} ago";
                if (span.TotalMinutes > 1)
                    return $"{(int)span.TotalMinutes} minute{((int)span.TotalMinutes == 1 ? "" : "s")} ago";
                return "Just now";
            }
        }
    }

    public class PhotoDetailVM : PhotoItemVM
    {
        public string ContentType { get; set; }
    }

    public class GalleryVM
    {
        public List<PhotoItemVM> Photos { get; set; }
        public string SearchTerm { get; set; }
        public string SortOrder { get; set; }
    }
}