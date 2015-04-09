using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace eSign.WebAPI.Models.Domain
{
    public class UploadLocalDocument
    {
        public string FileName { get; set; }
        public string EnvelopeId { get; set; }                
        public string DocumentBase64Data { get; set; }
    }
    public class UploadGoogleDocument
    {
        public string FileName { get; set; }
        public string EnvelopeId { get; set; }
        public string DownloadUrl { get; set; }
        public string AccessToken { get; set; }
    }
    public class UploadDropboxDocument
    {
        public string FileName { get; set; }
        public string EnvelopeId { get; set; }
        public string DownloadUrl { get; set; }        
    }
    public class UploadSkydriveDocument
    {
        public string FileName { get; set; }
        public string EnvelopeId { get; set; }
        public string DownloadUrl { get; set; }
    }
}