using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;

namespace eSign.WebAPI.Models.Domain
{
    public class ResponseMessage
    {
        public HttpStatusCode StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public string Message { get; set; }
    }

    public class ResponseMessagePDF
    {
        public HttpStatusCode StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public string FileName { get; set; }
        public string Message { get; set; }
        public byte[] PdfInBinary { get; set; }

    }

    public class ResponseMessageWithEmailId
    {
        public HttpStatusCode StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public string Message { get; set; }
        public string EmailId { get; set; }
    }

    public class ResponseMessageWithEnvID
    {
        public HttpStatusCode StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public string Message { get; set; }
        public int EnvId { get; set; }
    }

    public class ResponseMessageWithEnvlpGuid
    {
        public HttpStatusCode StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public string Message { get; set; }
        public string EnvelopeId { get; set; }
        public List<RecipientList> RecipientList { get; set; }
    }

    public class RecipientList
    {
        public string RecipientName { get; set; }
        public string RecipientId { get; set; }
    }

    public class RoleList
    {
        public string RoleName { get; set; }
        public string RecipientId { get; set; }
    }
    public class ResponseMessageWithTemplateGuid
    {
        public HttpStatusCode StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public string Message { get; set; }
        public string TemplateId { get; set; }
        public List<RoleList> RoleList { get; set; }
        public int? TemplateCode { get; set; }
    }

    public class ResponseMessageDocument
    {
        public HttpStatusCode StatusCode { get; set; }
        public string StatusMessage { get; set; }
        public string Message { get; set; }
        public string EnvelopeId { get; set; }
        public string DocumentId { get; set; }
        public string DocumentContentId { get; set; }
    }
}