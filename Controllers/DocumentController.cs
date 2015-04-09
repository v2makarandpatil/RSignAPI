using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using eSign.Models;
using eSign.WebAPI.Models;
using eSign.WebAPI.Models.Domain;
using System.Configuration;
using System.IO;
using eSign.Models.Domain;
using System.Text;
using eSign.Models.Data;
using eSign.Models.Helpers;
using GoogleDriveDownload;

namespace eSign.WebAPI.Controllers
{
    public class DocumentController : ApiController
    {
        private GoogleDrive gDrive = new GoogleDrive();
        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage UploadLocalDocument(UploadLocalDocument documentLocalDrive)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            bool fileDuplicateFlag = false;
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                string tempDirectory = ConfigurationManager.AppSettings["TempDirectory"].ToString() + documentLocalDrive.EnvelopeId;
                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(documentLocalDrive.EnvelopeId));
                    if (!Directory.Exists(tempDirectory))
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                        responseMessage.EnvelopeId = documentLocalDrive.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else if (isEnvelopePrepare == true)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopePrepared"].ToString();
                        responseMessage.EnvelopeId = documentLocalDrive.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else
                    {
                        UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                        string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                        Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                        bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(documentLocalDrive.EnvelopeId));
                        if (!isEnvelopeExists)
                        {
                            responseMessage.StatusCode = HttpStatusCode.NoContent;
                            responseMessage.StatusMessage = "NoContent";
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                            responseMessage.EnvelopeId = documentLocalDrive.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage, Configuration.Formatters.XmlFormatter);
                            return responseToClient;
                        }
                        string documentUploadPath = Path.Combine(tempDirectory, ConfigurationManager.AppSettings["UploadedDocuments"].ToString());
                        string docFinalPath = Path.Combine(documentUploadPath, documentLocalDrive.FileName);
                        if (!Directory.Exists(documentUploadPath))
                            Directory.CreateDirectory(documentUploadPath);

                        string[] listOfFiles = Directory.GetFiles(documentUploadPath);
                        foreach (var file in listOfFiles)
                        {
                            if (file.Contains(documentLocalDrive.FileName))
                            {
                                fileDuplicateFlag = true;
                                break;
                            }
                        }
                        if (fileDuplicateFlag)
                        {
                            responseMessage.StatusCode = HttpStatusCode.Ambiguous;
                            responseMessage.StatusMessage = "Ambiguous";
                            responseMessage.Message = ConfigurationManager.AppSettings["FileDuplicate"].ToString();
                            responseMessage.EnvelopeId = documentLocalDrive.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.Ambiguous, responseMessage);
                            return responseToClient;
                        }
                        //string[] validFileTypes = { "docx", "pdf", "doc", "xls", "xlsx", "ppt", "pptx", "DOCX", "PDF", "DOC", "XLS", "XLSX", "PPT", "PPTX" };
                        //string ext = Path.GetExtension(documentDropbox.FileName);
                        //bool isValidType = false;
                        //for (int j = 0; j < validFileTypes.Length; j++)
                        //{
                        //    if (ext == "." + validFileTypes[j])
                        //    {
                        //        isValidType = true;
                        //        break;
                        //    }
                        //}
                        //if (!isValidType)
                        //{
                        //    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                        //    responseMessage.StatusMessage = "NotAcceptable";
                        //    responseMessage.Message = ConfigurationManager.AppSettings["InvalidFileExtension"].ToString();
                        //    responseMessage.EnvelopeId = documentDropbox.EnvelopeId;
                        //    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                        //    return responseToClient;  
                        //}
                        try
                        {
                            File.WriteAllBytes(docFinalPath, Convert.FromBase64String(documentLocalDrive.DocumentBase64Data));
                        }
                        catch (Exception ex)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["InvalidLocalDocumentData"].ToString();
                            responseMessage.EnvelopeId = documentLocalDrive.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }
                        Guid documentId = Guid.NewGuid();

                        DocumentRepository documentRepository = new DocumentRepository(dbContext);
                        UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                        Documents doc = new Documents();
                        doc.ID = documentId;
                        doc.EnvelopeID = new Guid(documentLocalDrive.EnvelopeId);
                        doc.DocumentName = documentLocalDrive.FileName;
                        doc.UploadedDateTime = DateTime.Now;
                        int docCount = Directory.GetFiles(documentUploadPath).Length;
                        doc.Order = (short)(docCount);
                        documentRepository.Save(doc);
                        unitOfWork.SaveChanges();

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["SuccessDocumentUpload"].ToString();
                        responseMessage.EnvelopeId = documentLocalDrive.EnvelopeId;
                        responseMessage.DocumentId = Convert.ToString(documentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                }
            }
            catch (Exception e)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(e.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }  
        }  
        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage UploadGoogleDocument(UploadGoogleDocument documentGoogle)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            bool fileDuplicateFlag = false;
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                string tempDirectory = ConfigurationManager.AppSettings["TempDirectory"].ToString() + documentGoogle.EnvelopeId;
                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(documentGoogle.EnvelopeId));
                    if (!Directory.Exists(tempDirectory))
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                        responseMessage.EnvelopeId = documentGoogle.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else if (isEnvelopePrepare == true)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopePrepared"].ToString();
                        responseMessage.EnvelopeId = documentGoogle.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else
                    {
                        UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                        string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                        Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                        bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(documentGoogle.EnvelopeId));
                        if (!isEnvelopeExists)
                        {
                            responseMessage.StatusCode = HttpStatusCode.NoContent;
                            responseMessage.StatusMessage = "NoContent";
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                            responseMessage.EnvelopeId = documentGoogle.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage, Configuration.Formatters.XmlFormatter);
                            return responseToClient;
                        }
                        string documentUploadPath = Path.Combine(tempDirectory, ConfigurationManager.AppSettings["UploadedDocuments"].ToString());
                        string docFinalPath = Path.Combine(documentUploadPath, documentGoogle.FileName);
                        if (!Directory.Exists(documentUploadPath))
                            Directory.CreateDirectory(documentUploadPath);

                        string[] listOfFiles = Directory.GetFiles(documentUploadPath);
                        foreach (var file in listOfFiles)
                        {
                            if (file.Contains(documentGoogle.FileName))
                            {
                                fileDuplicateFlag = true;
                                break;
                            }
                        }
                        if (fileDuplicateFlag)
                        {
                            responseMessage.StatusCode = HttpStatusCode.Ambiguous;
                            responseMessage.StatusMessage = "Ambiguous";
                            responseMessage.Message = ConfigurationManager.AppSettings["FileDuplicate"].ToString();
                            responseMessage.EnvelopeId = documentGoogle.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.Ambiguous, responseMessage);
                            return responseToClient;
                        }
                        //string[] validFileTypes = { "docx", "pdf", "doc", "xls", "xlsx", "ppt", "pptx", "DOCX", "PDF", "DOC", "XLS", "XLSX", "PPT", "PPTX" };
                        //string ext = Path.GetExtension(documentDropbox.FileName);
                        //bool isValidType = false;
                        //for (int j = 0; j < validFileTypes.Length; j++)
                        //{
                        //    if (ext == "." + validFileTypes[j])
                        //    {
                        //        isValidType = true;
                        //        break;
                        //    }
                        //}
                        //if (!isValidType)
                        //{
                        //    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                        //    responseMessage.StatusMessage = "NotAcceptable";
                        //    responseMessage.Message = ConfigurationManager.AppSettings["InvalidFileExtension"].ToString();
                        //    responseMessage.EnvelopeId = documentDropbox.EnvelopeId;
                        //    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                        //    return responseToClient;  
                        //}
                        string tocheck = string.Empty;
                        try
                        {
                            tocheck = gDrive.DownloadFile(documentGoogle.AccessToken, documentGoogle.DownloadUrl, documentGoogle.FileName, documentUploadPath);
                        }
                        catch (WebException ex)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["InvalidDownloadUri"].ToString();
                            responseMessage.EnvelopeId = documentGoogle.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }
                        Guid documentId = Guid.NewGuid();

                        DocumentRepository documentRepository = new DocumentRepository(dbContext);
                        UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                        Documents doc = new Documents();
                        doc.ID = documentId;
                        doc.EnvelopeID = new Guid(documentGoogle.EnvelopeId);
                        doc.DocumentName = documentGoogle.FileName;
                        doc.UploadedDateTime = DateTime.Now;
                        int docCount = Directory.GetFiles(documentUploadPath).Length;
                        doc.Order = (short)(docCount);
                        documentRepository.Save(doc);
                        unitOfWork.SaveChanges();

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["SuccessDocumentUpload"].ToString();
                        responseMessage.EnvelopeId = documentGoogle.EnvelopeId;
                        responseMessage.DocumentId = Convert.ToString(documentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                }
            }
            catch (Exception e)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(e.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }  
        }         
        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage UploadDropboxDocument(UploadDropboxDocument documentDropbox)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            bool fileDuplicateFlag = false;
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                string tempDirectory = ConfigurationManager.AppSettings["TempDirectory"].ToString() + documentDropbox.EnvelopeId;
                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(documentDropbox.EnvelopeId));
                    if (!Directory.Exists(tempDirectory))
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                        responseMessage.EnvelopeId = documentDropbox.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else if (isEnvelopePrepare == true)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopePrepared"].ToString();
                        responseMessage.EnvelopeId = documentDropbox.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else
                    {
                        UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                        string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                        Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                        bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId,new Guid(documentDropbox.EnvelopeId));
                        if (!isEnvelopeExists)
                        {
                            responseMessage.StatusCode = HttpStatusCode.NoContent;
                            responseMessage.StatusMessage = "NoContent";
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                            responseMessage.EnvelopeId = documentDropbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage, Configuration.Formatters.XmlFormatter);
                            return responseToClient;
                        }
                        string documentUploadPath = Path.Combine(tempDirectory, ConfigurationManager.AppSettings["UploadedDocuments"].ToString());
                        string docFinalPath = Path.Combine(documentUploadPath, documentDropbox.FileName);
                        if (!Directory.Exists(documentUploadPath))
                            Directory.CreateDirectory(documentUploadPath);

                        string[] listOfFiles = Directory.GetFiles(documentUploadPath);
                        foreach (var file in listOfFiles)
                        {
                            if (file.Contains(documentDropbox.FileName))
                            {
                                fileDuplicateFlag = true;
                                break;
                            }
                        }
                        if (fileDuplicateFlag)
                        {
                            responseMessage.StatusCode = HttpStatusCode.Ambiguous;
                            responseMessage.StatusMessage = "Ambiguous";
                            responseMessage.Message = ConfigurationManager.AppSettings["FileDuplicate"].ToString();
                            responseMessage.EnvelopeId = documentDropbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.Ambiguous, responseMessage);
                            return responseToClient;
                        }
                        //string[] validFileTypes = { "docx", "pdf", "doc", "xls", "xlsx", "ppt", "pptx", "DOCX", "PDF", "DOC", "XLS", "XLSX", "PPT", "PPTX" };
                        //string ext = Path.GetExtension(documentDropbox.FileName);
                        //bool isValidType = false;
                        //for (int j = 0; j < validFileTypes.Length; j++)
                        //{
                        //    if (ext == "." + validFileTypes[j])
                        //    {
                        //        isValidType = true;
                        //        break;
                        //    }
                        //}
                        //if (!isValidType)
                        //{
                        //    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                        //    responseMessage.StatusMessage = "NotAcceptable";
                        //    responseMessage.Message = ConfigurationManager.AppSettings["InvalidFileExtension"].ToString();
                        //    responseMessage.EnvelopeId = documentDropbox.EnvelopeId;
                        //    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                        //    return responseToClient;  
                        //}
                        try
                        {
                            using (WebClient webClient = new WebClient())
                            {
                                webClient.DownloadFile(new Uri(documentDropbox.DownloadUrl), docFinalPath);
                            }
                        }
                        catch (WebException ex)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["InvalidDownloadUri"].ToString();
                            responseMessage.EnvelopeId = documentDropbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }
                        Guid documentId = Guid.NewGuid();

                        DocumentRepository documentRepository = new DocumentRepository(dbContext);
                        UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                        Documents doc = new Documents();
                        doc.ID = documentId;
                        doc.EnvelopeID = new Guid(documentDropbox.EnvelopeId);
                        doc.DocumentName = documentDropbox.FileName;
                        doc.UploadedDateTime = DateTime.Now;
                        int docCount = Directory.GetFiles(documentUploadPath).Length;
                        doc.Order = (short)(docCount);
                        documentRepository.Save(doc);
                        unitOfWork.SaveChanges();

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["SuccessDocumentUpload"].ToString();
                        responseMessage.EnvelopeId = documentDropbox.EnvelopeId;
                        responseMessage.DocumentId = Convert.ToString(documentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                }
            }
            catch (Exception e)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(e.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);                
            }            
        }

        [AuthorizeAccess]        
        [HttpPost]
        public HttpResponseMessage UploadSkydriveDocument(UploadSkydriveDocument documentSkyDrive)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            bool fileDuplicateFlag = false;
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                string tempDirectory = ConfigurationManager.AppSettings["TempDirectory"].ToString() + documentSkyDrive.EnvelopeId;
                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(documentSkyDrive.EnvelopeId));
                    if (!Directory.Exists(tempDirectory))
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                        responseMessage.EnvelopeId = documentSkyDrive.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else if (isEnvelopePrepare == true)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopePrepared"].ToString();
                        responseMessage.EnvelopeId = documentSkyDrive.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    else
                    {
                        UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                        string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                        Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                        bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(documentSkyDrive.EnvelopeId));
                        if (!isEnvelopeExists)
                        {
                            responseMessage.StatusCode = HttpStatusCode.NoContent;
                            responseMessage.StatusMessage = "NoContent";
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                            responseMessage.EnvelopeId = documentSkyDrive.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage, Configuration.Formatters.XmlFormatter);
                            return responseToClient;
                        }
                        string documentUploadPath = Path.Combine(tempDirectory, ConfigurationManager.AppSettings["UploadedDocuments"].ToString());
                        string docFinalPath = Path.Combine(documentUploadPath, documentSkyDrive.FileName);
                        if (!Directory.Exists(documentUploadPath))
                            Directory.CreateDirectory(documentUploadPath);
                        string[] listOfFiles = Directory.GetFiles(documentUploadPath);
                        foreach (var file in listOfFiles)
                        {
                            if (file.Contains(documentSkyDrive.FileName))
                            {
                                fileDuplicateFlag = true;
                                break;
                            }
                        }
                        if (fileDuplicateFlag)
                        {
                            responseMessage.StatusCode = HttpStatusCode.Ambiguous;
                            responseMessage.StatusMessage = "Ambiguous";
                            responseMessage.Message = ConfigurationManager.AppSettings["FileDuplicate"].ToString();
                            responseMessage.EnvelopeId = documentSkyDrive.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.Ambiguous, responseMessage);
                            return responseToClient;
                        }
                        //string[] validFileTypes = { "docx", "pdf", "doc", "xls", "xlsx", "ppt", "pptx", "DOCX", "PDF", "DOC", "XLS", "XLSX", "PPT", "PPTX" };
                        //string ext = Path.GetExtension(documentSkyDrive.FileName);
                        //bool isValidType = false;
                        //for (int j = 0; j < validFileTypes.Length; j++)
                        //{
                        //    if (ext == "." + validFileTypes[j])
                        //    {
                        //        isValidType = true;
                        //        break;
                        //    }
                        //}
                        //if (!isValidType)
                        //{
                        //    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                        //    responseMessage.StatusMessage = "NotAcceptable";
                        //    responseMessage.Message = ConfigurationManager.AppSettings["InvalidFileExtension"].ToString();
                        //    responseMessage.EnvelopeId = documentSkyDrive.EnvelopeId;
                        //    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                        //    return responseToClient;
                        //}
                        try
                        {
                            using (WebClient webClient = new WebClient())
                            {
                                webClient.DownloadFile(new Uri(documentSkyDrive.DownloadUrl), docFinalPath);
                            }
                        }
                        catch (WebException ex)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["InvalidDownloadUri"].ToString();
                            responseMessage.EnvelopeId = documentSkyDrive.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }
                        Guid documentId = Guid.NewGuid();

                        DocumentRepository documentRepository = new DocumentRepository(dbContext);
                        UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                        Documents doc = new Documents();
                        doc.ID = documentId;
                        doc.EnvelopeID = new Guid(documentSkyDrive.EnvelopeId);
                        doc.DocumentName = documentSkyDrive.FileName;
                        doc.UploadedDateTime = DateTime.Now;
                        int docCount = Directory.GetFiles(documentUploadPath).Length;
                        doc.Order = (short)(docCount);
                        documentRepository.Save(doc);
                        unitOfWork.SaveChanges();

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["SuccessDocumentUpload"].ToString();
                        responseMessage.EnvelopeId = documentSkyDrive.EnvelopeId;
                        responseMessage.DocumentId = Convert.ToString(documentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                }
            }
            catch (Exception e)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(e.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }
        }

        [AuthorizeAccess]
        [HttpDelete]
        public HttpResponseMessage DeleteDocument(string envelopeCode, string id)
        {
            string documentCode = id;
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            string documentName = string.Empty;
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                string tempDirectory = ConfigurationManager.AppSettings["TempDirectory"].ToString();
                string documentUploadPath = Path.Combine(tempDirectory, envelopeCode, ConfigurationManager.AppSettings["UploadedDocuments"].ToString());                
                using (var dbContext = new eSignEntities())
                {
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    var envelopeRepository = new EnvelopeRepository(dbContext);
                    if (!Directory.Exists(Path.Combine(tempDirectory,envelopeCode)))
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                        responseMessage.EnvelopeId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(envelopeCode));
                    if (isEnvelopePrepare == true)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopePrepared"].ToString();
                        responseMessage.EnvelopeId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    DocumentRepository documentRepository = new DocumentRepository(dbContext);                    
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    Documents doc = documentRepository.GetEntity(new Guid(documentCode));
                    string documentPath = string.Empty;
                    if (doc != null)
                    {
                        documentName = doc.DocumentName;
                        documentPath = Path.Combine(documentUploadPath, documentName);
                    }                    
                    if (doc == null)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdMissing"].ToString();
                        responseMessage.EnvelopeId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }                                       
                    else
                    {
                        UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                        string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                        Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                        bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(envelopeCode));
                        if (!isEnvelopeExists)
                        {
                            responseMessage.StatusCode = HttpStatusCode.NoContent;
                            responseMessage.StatusMessage = "NoContent";
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                            responseMessage.EnvelopeId = envelopeCode;
                            responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage, Configuration.Formatters.XmlFormatter);
                            return responseToClient;
                        }
                        Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeCode));
                        envelopeHelperMain.SetApiCallFlag();
                        envelopeHelperMain.DeleteFile(envelope.Documents.Where(d => d.ID == new Guid(documentCode)).FirstOrDefault().DocumentName, Convert.ToString(envelope.ID), envelope.Documents.Count, envelope);
                        bool documentContentDelete = documentContentsRepository.Delete(doc);
                        bool documentDelete = documentRepository.Delete(new Guid(documentCode));
                        unitOfWork.SaveChanges();                        
                        if (documentDelete == true)
                        {
                            responseMessage.StatusCode = HttpStatusCode.OK;
                            responseMessage.StatusMessage = "OK";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentDeleted"].ToString();
                            responseMessage.EnvelopeId = envelopeCode;
                            responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                            return responseToClient;
                        }
                        else
                        {
                            responseMessage.StatusCode = HttpStatusCode.OK;
                            responseMessage.StatusMessage = "OK";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentDeleted"].ToString();
                            responseMessage.EnvelopeId = envelopeCode;
                            responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                            return responseToClient;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }            
        }
    }
}
