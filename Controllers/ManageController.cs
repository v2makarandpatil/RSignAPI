using eSign.Models.Data;
using eSign.Models.Domain;
using eSign.Models.Helpers;
using eSign.Notification;
using eSign.Web.Helpers;
using eSign.WebAPI.Models;
using eSign.WebAPI.Models.Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web.Http;
using System.Xml;
using eSign.WebAPI.Models.Domain;
using System.IO;

namespace eSign.WebAPI.Controllers
{
    public class ManageController : ApiController
    {
        IList<Envelope> Envelopes { get; set; }

        [AuthorizeAccess]
        [HttpGet]
        public HttpResponseMessage GetEnvelopeList()
        {
            var manageEnvelope = new List<EnvelopeGetenvelopeHistory>();
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                using (var dbContext = new eSignEntities())
                {
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);                    
                    Guid UserId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));
                    Envelopes = envelopeRepository.GetAll(UserId).OrderByDescending(r => r.CreatedDateTime).Where(r => r.DisplayCode != null && r.DisplayCode != 0 && r.IsEnvelopeComplete == true).ToList();
                }
                EnvelopeGetenvelopeHistory mngEnvlop = new EnvelopeGetenvelopeHistory();
                foreach (var env in Envelopes)
                {
                    mngEnvlop = new EnvelopeGetenvelopeHistory();
                    mngEnvlop.EnvelopeCode = env.DisplayCode;
                    mngEnvlop.Subject = env.Subject;
                    mngEnvlop.CurrentStatus = env.EnvelopeStatusDescription;
                    mngEnvlop.Sent = String.Format("{0:MM/dd/yyyy HH:mm tt}", env.CreatedDateTime);
                    if (env.StatusID != Constants.StatusCode.Envelope.Waiting_For_Signature)
                    {                        
                        mngEnvlop.Completed = String.Format("{0:MM/dd/yyyy HH:mm tt}", env.ModifiedDateTime);
                    }                    
                    manageEnvelope.Add(mngEnvlop);
                }
                if (manageEnvelope.Count == 0)
                {
                    ResponseMessage responseMessage = new ResponseMessage();
                    responseMessage.StatusCode = HttpStatusCode.NoContent;
                    responseMessage.StatusMessage = "NoContent";
                    responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                    responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                    return responseToClient;
                }
                else
                {
                    responseToClient = Request.CreateResponse(HttpStatusCode.OK, manageEnvelope);
                    return responseToClient;
                }
            }
            catch (Exception ex)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }
        }

        [AuthorizeAccess]
        [HttpGet]
        public HttpResponseMessage GetEnvelopeHistoryByCode(int envelopeCode)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            EnvelopeGetEnvelopeHistoryByEnvelopeCode newEnvelope = new EnvelopeGetEnvelopeHistoryByEnvelopeCode();
            string userName = string.Empty;
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                using (var dbContext = new eSignEntities())
                {
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));
                    userName = envelopeRepository.GetUserNameByAuthToken(authToken);
                    Envelopes = envelopeRepository.GetAll(UserId).OrderByDescending(r => r.CreatedDateTime).Where(r => r.DisplayCode != null && r.DisplayCode != 0 && r.IsEnvelopeComplete == true).ToList();
                    foreach (var env in Envelopes)
                    {
                        if (env.DisplayCode == envelopeCode)//envelope.EnvelopeCode)
                        {
                            newEnvelope.DocumentList = new List<string>();
                            newEnvelope.DocumentHistory = new List<DocumentStatus>();
                            var documentList = env.Documents.Select(m => m.DocumentName).ToList();
                            string recipients = string.Empty;

                            newEnvelope.EnvelopeCode = env.DisplayCode;
                            newEnvelope.Subject = env.Subject;
                            newEnvelope.CurrentStatus = env.EnvelopeStatusDescription;
                            newEnvelope.Sent = String.Format("{0:MM/dd/yyyy HH:mm tt}", env.CreatedDateTime);
                            if (env.StatusID != Constants.StatusCode.Envelope.Waiting_For_Signature)
                            {
                                newEnvelope.Completed = String.Format("{0:MM/dd/yyyy HH:mm tt}", env.ModifiedDateTime);
                            }                            

                            for (int i = 0; i < documentList.Count; i++)
                            {                                
                                newEnvelope.DocumentList.Add(documentList[i]);
                            }
                            newEnvelope.EnvelopeID = env.EnvelopeCodeDisplay;
                            newEnvelope.Sender = userName;
                            foreach (var recipient in env.Recipients.OrderByDescending(r => r.RecipientTypeDescription).ToList())
                            {
                                if (recipient.RecipientTypeID == Constants.RecipientType.Sender)  // Sender
                                {
                                    continue;
                                }
                                if (recipient.RecipientTypeID == Constants.RecipientType.CC) // CC
                                {
                                    recipients += recipient.Name + "(CC), ";
                                }
                                else if (recipient.RecipientTypeID == Constants.RecipientType.Signer) // Signer
                                {
                                    recipients += recipient.Name + ", ";
                                }
                            }
                            if (!string.IsNullOrEmpty(recipients))
                            {
                                recipients = recipients.Substring(0, recipients.Length - 2);
                            }
                            

                            foreach (var recipient in env.Recipients.OrderBy(r => r.StatusDate))
                            {
                                DocumentStatus docHistory = new DocumentStatus();
                                if (recipient.RecipientTypeID == Constants.RecipientType.CC ||
                                                (recipient.StatusID == null || recipient.StatusID == Guid.Empty))
                                {
                                    continue;
                                }
                                if (recipient.RecipientTypeID == Constants.RecipientType.Signer)
                                {
                                    docHistory.StatusDate = String.Format("{0:MM/dd/yyyy HH:mm tt}", recipient.StatusDate) + " " + GetTimeZone.GetTimeZoneAbbreviation(TimeZone.CurrentTimeZone.StandardName);                                   
                                    if (recipient.StatusID == Constants.StatusCode.Signer.Delegated)
                                    {
                                        newEnvelope.DelegatedTo = recipient.DelegatedTo;
                                    }
                                    else
                                    {
                                        docHistory.SignerStatusDescription = recipient.SignerStatusDescription;                                        
                                        docHistory.Recipient = recipient.Name;
                                        docHistory.RecipientEmailAddress = recipient.EmailAddress;                                       
                                        if (recipient.StatusID == Constants.StatusCode.Signer.Pending)
                                            docHistory.IPAddress = "-.-.-.-";                                        
                                        else
                                            docHistory.IPAddress = recipient.SignerIPAddress;                                        
                                    }
                                }
                                newEnvelope.DocumentHistory.Add(docHistory);
                            }
                            if (env.StatusID == Constants.StatusCode.Envelope.Completed)
                            {
                                var recipient = env.Recipients.Where(r => r.StatusID == Constants.StatusCode.Signer.Signed).OrderByDescending(r => r.StatusDate).FirstOrDefault();
                                newEnvelope.CompletedStatusDate = String.Format("{0:MM/dd/yyyy HH:mm tt}", recipient.StatusDate) + " " + GetTimeZone.GetTimeZoneAbbreviation(TimeZone.CurrentTimeZone.StandardName);
                                newEnvelope.EnvelopeStatusDescription = env.EnvelopeStatusDescription;
                            }
                            if (env.StatusID == Constants.StatusCode.Envelope.Terminated)
                            {
                                var recipient = env.Recipients.Where(r => r.StatusID == Constants.StatusCode.Signer.Rejected).OrderByDescending(r => r.StatusDate).First();
                                newEnvelope.CompletedStatusDate = String.Format("{0:MM/dd/yyyy HH:mm tt}", recipient.StatusDate) + " " + GetTimeZone.GetTimeZoneAbbreviation(TimeZone.CurrentTimeZone.StandardName);
                                newEnvelope.EnvelopeStatusDescription = env.EnvelopeStatusDescription;
                            }
                        }
                    }
                }
                if (newEnvelope.EnvelopeCode == 0)
                {
                    ResponseMessage responseMessage = new ResponseMessage();
                    responseMessage.StatusCode = HttpStatusCode.NoContent;
                    responseMessage.StatusMessage = "NoContent";
                    responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                    responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                    return responseToClient;
                }
                else
                {
                    responseToClient = Request.CreateResponse(HttpStatusCode.OK, newEnvelope);
                    return responseToClient;
                }
            }
            catch (Exception ex)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }
        }

        [AuthorizeAccess]
        [HttpGet]
        public HttpResponseMessage GetEnvelopeXMLByCode(int envelopeCode)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            Byte[] info;
            Guid envelopeId;
            var doc = new XmlDocument();
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                using (var dbContext = new eSignEntities())
                {
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));
                    string eId = envelopeRepository.GetEnvelopeIdByDisplayCode(envelopeCode, UserId);
                    if (string.IsNullOrEmpty(eId))
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage, Configuration.Formatters.XmlFormatter);
                        return responseToClient;
                    }
                    envelopeId = new Guid(eId);                                                           
                    Envelope newEnvelope = envelopeRepository.GetEntity(envelopeId);
                    if (newEnvelope == null)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    info = new UTF8Encoding(true).GetBytes(newEnvelope.EnvelopeContent.First().ContentXML);
                    string xml = Encoding.UTF8.GetString(info);
                    doc.LoadXml(xml);

                    responseToClient = Request.CreateResponse(HttpStatusCode.OK, doc, Configuration.Formatters.XmlFormatter);
                    return responseToClient;
                }

            }
            catch (Exception ex)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }
        }

        //[AuthorizeAccess]
        [HttpGet]
        public HttpResponseMessage GetFinalSignedPDFDocument(int envelopeCode, string AuthToken)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            var base64String = string.Empty;
            byte[] binaryData = new Byte[102222];
            //eSign.Web.Controllers.DocumentPackageController doc = new Web.Controllers.DocumentPackageController();
            EnvelopeHelperMain objEnvelope = new EnvelopeHelperMain();
            Guid envelopeId;
            string finalDocumentName = string.Empty;
            try
            {                               
                string authToken = AuthToken;
                using (var objectcontext = new eSignEntities())
                {
                    //var helper = new eSign.Web.Helpers.EnvelopeHelper();
                    UserTokenRepository tokenRepository = new UserTokenRepository(objectcontext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(objectcontext);
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));
                    string eId = envelopeRepository.GetEnvelopeIdByDisplayCode(envelopeCode, UserId);
                    finalDocumentName = envelopeRepository.GetFinalDocumentName(new Guid(eId));
                    if (string.IsNullOrEmpty(eId))
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    envelopeId = new Guid(eId);
                    Envelope envelopeObject = envelopeRepository.GetEntity(envelopeId);
                    if (envelopeObject == null)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    if (envelopeObject.StatusID == Constants.StatusCode.Envelope.Waiting_For_Signature)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["PartialComplete"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else if (envelopeObject.StatusID == Constants.StatusCode.Envelope.Incomplete_and_Expired)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["IncompleteAndExpired"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else if (envelopeObject.StatusID == Constants.StatusCode.Envelope.Terminated)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["Terminated"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    string permanentPDFFilePath = ConfigurationManager.AppSettings["PermanentPDFPathStart"].ToString() + envelopeId.ToString() + ConfigurationManager.AppSettings["PermanentPDFPathEnd"].ToString();
                    string finalPdfFilePath = ConfigurationManager.AppSettings["PDFPathStart"].ToString() + envelopeId.ToString() + ConfigurationManager.AppSettings["PDFPathEnd"].ToString();

                    if (System.IO.File.Exists(permanentPDFFilePath))
                    {
                        System.IO.FileStream inFile;
                        inFile = new System.IO.FileStream(permanentPDFFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                        binaryData = new Byte[inFile.Length];
                        long bytesRead = inFile.Read(binaryData, 0, (int)inFile.Length);
                        base64String = Convert.ToBase64String(binaryData, 0, (int)bytesRead);
                        inFile.Close();
                    }
                    else if (System.IO.File.Exists(finalPdfFilePath))
                    {
                        System.IO.FileStream inFile;
                        inFile = new System.IO.FileStream(finalPdfFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                        binaryData = new Byte[inFile.Length];
                        long bytesRead = inFile.Read(binaryData, 0, (int)inFile.Length);
                        base64String = Convert.ToBase64String(binaryData, 0, (int)bytesRead);
                        inFile.Close();
                    }
                    else
                    {
                        var result = objEnvelope.DownloadPDFApi(envelopeObject);
                        System.IO.FileStream inFile;
                        inFile = new System.IO.FileStream(result, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                        binaryData = new Byte[inFile.Length];
                        long bytesRead = inFile.Read(binaryData, 0, (int)inFile.Length);
                        base64String = Convert.ToBase64String(binaryData, 0, (int)bytesRead);
                        inFile.Close();
                    }
                }
                responseToClient.Content = new ByteArrayContent(binaryData);
                responseToClient.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                responseToClient.Content.Headers.ContentDisposition.FileName = finalDocumentName+".pdf";
                responseToClient.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                return responseToClient;
            }
            catch (Exception ex)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }
        }
        
        [AuthorizeAccess]
        [HttpGet]
        public HttpResponseMessage GetFinalSignedPDFDocumentHeader(int envelopeCode)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            var base64String = string.Empty;
            byte[] binaryData = new Byte[102222];
            //eSign.Web.Controllers.DocumentPackageController doc = new Web.Controllers.DocumentPackageController();
            EnvelopeHelperMain objEnvelope = new EnvelopeHelperMain();
            Guid envelopeId;
            string finalDocumentName = string.Empty;
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);                                
                using (var objectcontext = new eSignEntities())
                {
                    //var helper = new eSign.Web.Helpers.EnvelopeHelper();
                    UserTokenRepository tokenRepository = new UserTokenRepository(objectcontext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(objectcontext);
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));
                    string eId = envelopeRepository.GetEnvelopeIdByDisplayCode(envelopeCode, UserId);
                    finalDocumentName = envelopeRepository.GetFinalDocumentName(new Guid(eId));
                    if (string.IsNullOrEmpty(eId))
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    envelopeId = new Guid(eId);
                    Envelope envelopeObject = envelopeRepository.GetEntity(envelopeId);
                    if (envelopeObject == null)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    if (envelopeObject.StatusID == Constants.StatusCode.Envelope.Waiting_For_Signature)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["PartialComplete"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else if (envelopeObject.StatusID == Constants.StatusCode.Envelope.Incomplete_and_Expired)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["IncompleteAndExpired"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else if (envelopeObject.StatusID == Constants.StatusCode.Envelope.Terminated)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["Terminated"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }

                    string permanentPDFFilePath = ConfigurationManager.AppSettings["PermanentPDFPathStart"].ToString() + envelopeId.ToString() + ConfigurationManager.AppSettings["PermanentPDFPathEnd"].ToString();
                    string finalPdfFilePath = ConfigurationManager.AppSettings["PDFPathStart"].ToString() + envelopeId.ToString() + ConfigurationManager.AppSettings["PDFPathEnd"].ToString();

                    if (System.IO.File.Exists(permanentPDFFilePath))
                    {
                        System.IO.FileStream inFile;
                        inFile = new System.IO.FileStream(permanentPDFFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                        binaryData = new Byte[inFile.Length];
                        long bytesRead = inFile.Read(binaryData, 0, (int)inFile.Length);
                        base64String = Convert.ToBase64String(binaryData, 0, (int)bytesRead);
                        inFile.Close();
                    }
                    else if (System.IO.File.Exists(finalPdfFilePath))
                    {
                        System.IO.FileStream inFile;
                        inFile = new System.IO.FileStream(finalPdfFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                        binaryData = new Byte[inFile.Length];
                        long bytesRead = inFile.Read(binaryData, 0, (int)inFile.Length);
                        base64String = Convert.ToBase64String(binaryData, 0, (int)bytesRead);
                        inFile.Close();
                    }
                    else
                    {
                        var result = objEnvelope.DownloadPDFApi(envelopeObject);
                        System.IO.FileStream inFile;
                        inFile = new System.IO.FileStream(result, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                        binaryData = new Byte[inFile.Length];
                        long bytesRead = inFile.Read(binaryData, 0, (int)inFile.Length);
                        base64String = Convert.ToBase64String(binaryData, 0, (int)bytesRead);
                        inFile.Close();
                    }
                }                

                ResponseMessagePDF responseMessagePDF = new ResponseMessagePDF();
                responseMessagePDF.StatusCode = HttpStatusCode.OK;
                responseMessagePDF.StatusMessage = "OK";
                responseMessagePDF.FileName = finalDocumentName+".pdf";
                responseMessagePDF.Message = "Following is the binary data of pdf.";
                responseMessagePDF.PdfInBinary = binaryData;
                responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessagePDF);
                return responseToClient;
            }
            catch (Exception ex)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }                        
        }
       

        [AuthorizeAccess]
        [HttpGet]
        public HttpResponseMessage ResendEmail(int envelopeCode)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            //eSign.Web.Controllers.DocumentPackageController doc = new Web.Controllers.DocumentPackageController();
            EnvelopeHelperMain objEnvelope = new EnvelopeHelperMain();
            Guid envelopeId;
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                using (var dbContext = new eSignEntities())
                {
                    //var helper = new eSign.Web.Helpers.EnvelopeHelper();
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));
                    string eId = envelopeRepository.GetEnvelopeIdByDisplayCode(envelopeCode, UserId);
                    if (string.IsNullOrEmpty(eId))
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }
                    envelopeId = new Guid(eId);
                    Envelope envelopeObject = envelopeRepository.GetEntity(envelopeId);
                    if (envelopeObject == null)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    if (envelopeObject.StatusID == Constants.StatusCode.Envelope.Incomplete_and_Expired)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["IncompleteAndExpired"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else if (envelopeObject.StatusID == Constants.StatusCode.Envelope.Terminated)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["Terminated"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    var JsonResult = objEnvelope.SendmailReminderAPI(Convert.ToString(envelopeId), tokenRepository.GetUserEmailByToken(authToken));
                    if (JsonResult == "All signers has signed the document")
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["AllSigned"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else if(JsonResult == "The email has been resent")
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["MailResend"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {
                        responseToClient = Request.CreateResponse((HttpStatusCode)422);
                        responseToClient.Content = new StringContent(JsonResult, Encoding.Unicode);
                        throw new HttpResponseException(responseToClient);
                    }
                    
                }
                
            }
            catch(Exception ex)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }                        
        }

    }
}
