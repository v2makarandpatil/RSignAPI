using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using eSign.Models.Domain;

namespace eSign.WebAPI.Models.Domain
{    

    public class EnvelopeDetails
    {
        public Guid EnvelopeID { get; set; }
        public Guid UserID { get; set; }
        public string DateFormat { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public bool PasswordReqdToOpen { get; set; }
        public bool PasswordReqdToSign { get; set; }
        public string PasswordToOpen { get; set; }
        public string PasswordToSign { get; set; }
        public int? RemainderDays { get; set; }
        public int? RemainderRepeatDays { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string ExpiryType { get; set; }
        public Guid StatusID { get; set; }
        public bool? SignatureCertificateRequired { get; set; }
        public bool? DownloadLinkOnManageRequired { get; set; }
        public List<DocumentDetails> DocumentDetails { get; set; }
        public List<RecipientDetails> RecipientList { get; set; }
        public int DisplayCode { get; set; }
        public string PasswordKey { get; set; }
        public int? PasswordKeySize { get; set; }
    }

    public class RecipientDetails
    {
        public Guid ID { get; set; }
        public Guid EnvelopeID { get; set; }
        public string RecipientName { get; set; }
        public string EmailID { get; set; }
        public int? Order { get; set; }
        public DateTime? CreatedDateTime { get; set; }
        public string RecipientType { get; set; }
    }

    public class PageDetails {
        public string EnvelopeID { get; set; }
        public string PageName { get; set; }
        public string PageData { get; set; }
    }

    public class ViewPdf {
        public string EnvelopeID { get; set; }
        public string PageData { get; set; }
    }
	
	public class DocumentPageDetails
    {
        public int TotalPageCount { get; set; }
        public List<string> ImageName { get; set; }
        public string EnvelopeID { get; set; }

    }
}