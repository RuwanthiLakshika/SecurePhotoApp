using Microsoft.AspNetCore.Mvc;
using SecurePhotoApp.Models;
using Azure.Storage.Blobs;

namespace SecurePhotoApp.Controllers
{
    public class PhotoController : Controller
    {
        private string BlobConnectionString = "DefaultEndpointsProtocol=https;AccountName=photostorageaccount;AccountKey=HwyYcCSBPPygXUjgM76mvv3hmGS0JuZTAK5cPBy/inRE1N2/TMEodavuPUsbVIDp2nas2aFmrrXA+AStAzs20g==;EndpointSuffix=core.windows.net";
        private string ContainerName = "photocontainer";

        public IActionResult Add()
        {
            return View();
        }

        [HttpPost]
        public IActionResult UploadFile(PhotoVM model)
        {
            try
            {
                var filename = GenerateFileName(model.myFile.FileName, model.myFile.FileName);
                var fileUrl = "";
                BlobContainerClient container = new BlobContainerClient(BlobConnectionString, ContainerName);
                try
                {
                    BlobClient blob = container.GetBlobClient(filename);
                    using (Stream stream = model.myFile.OpenReadStream())
                    {
                        blob.Upload(stream);
                    }
                    fileUrl = blob.Uri.AbsoluteUri;
                }
                catch (Exception ex) { }
                var result = fileUrl;
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest();
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
