using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using eSign.WebAPI.Models.Domain;
using System.Text;
using eSign.Models.Domain;
using eSign.Models.Data;
using eSign.Models.Helpers;
using eSign.WebAPI.Models.Helpers;
using eSign.WebAPI.Models;
using System.Configuration;
using System.IO;

namespace eSign.WebAPI.Controllers
{
    public class ControlController : ApiController
    {

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage SignatureControl(Signature signature)
        {
            
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            int pageNumber = 0;
            int documentPageNumber = 0;
            bool docIdPresent = false;
            bool docContentIdPresent = false;
            int docContentCount = 0;
            bool recPresent = false;
            string recName = "";
            try
            {
                string envelopeID = signature.EnvelopeId;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), signature.EnvelopeId);
                if (string.IsNullOrEmpty(envelopeID) || string.IsNullOrEmpty(signature.RecipientId) || string.IsNullOrEmpty(signature.DocumentId) || string.IsNullOrEmpty(signature.PageName)  || signature.XCordinate == 0 ||  signature.YCordinate == 0 || signature.ZCordinate == 0)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = signature.EnvelopeId;
                    responseMessage.DocumentId = signature.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }                

                if (signature.Height < 50 || signature.Width < 180)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["NotValidHeightWidth"].ToString();
                    responseMessage.EnvelopeId = signature.EnvelopeId;
                    responseMessage.DocumentId = signature.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = signature.EnvelopeId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }
                if (string.IsNullOrEmpty(signature.RecipientId) || string.IsNullOrEmpty(signature.DocumentId))
                {

                }

                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    RecipientRepository recipientRepository = new RecipientRepository(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    
                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid userId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(userId, new Guid(signature.EnvelopeId));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = signature.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }
                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = signature.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = signature.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(signature.EnvelopeId));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString();
                        responseMessage.EnvelopeId = signature.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (signature.DocumentContentId == "" || signature.DocumentContentId == null)
                    {

                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(signature.DocumentId));

                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = signature.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        
                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(signature.RecipientId));
                            if(recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(signature.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(signature.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(signature.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = signature.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                           isCCRecipient =  envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(signature.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(signature.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;

                         
                         if (isCCRecipient)
                         {
                             responseMessage.StatusCode = HttpStatusCode.BadRequest;
                             responseMessage.StatusMessage = "BadRequest";
                             responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                             responseMessage.EnvelopeId = signature.EnvelopeId;
                             responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                             return responseToClient;
                         }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == signature.PageName)
                            {
                                if (docImage.Document.Id == new Guid(signature.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = signature.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            docContentCount = docContentCount + doc.DocumentContents.Count;
                        }

                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Signature);
                        controlTemplate = controlTemplate.Replace("#SignControlId", "signControl" + docContentCount);
                        controlTemplate = controlTemplate.Replace("#left", signature.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", signature.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", signature.Height + "px");
                        controlTemplate = controlTemplate.Replace("#width", signature.Width + "px");
                        controlTemplate = controlTemplate.Replace("#Required", signature.Required.ToString());
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Signature));
                        controlTemplate = controlTemplate.Replace("#recipientId", signature.RecipientId);

                        Guid docContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = docContentId;
                        documentContents.DocumentID = new Guid(signature.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = "signControl" + docContentCount;
                        documentContents.Label = "Sign control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Signature;
                        documentContents.Height = signature.Height;
                        documentContents.Width = signature.Width;
                        documentContents.XCoordinate = signature.XCordinate;
                        documentContents.YCoordinate = signature.YCordinate;
                        documentContents.ZCoordinate = signature.ZCordinate;
                        documentContents.RecipientID = new Guid(signature.RecipientId);
                        documentContents.Required = signature.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = docContentId;
                        controlStyle.FontID = Guid.Parse("1875C58D-52BD-498A-BE6D-433A8858357E");
                        controlStyle.FontSize = 14;
                        controlStyle.FontColor = "#000000";
                        controlStyle.IsBold = false;
                        controlStyle.IsItalic = false;
                        controlStyle.IsUnderline = false;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(signature.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = docContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["AddSignatureSuccess"].ToString();
                        responseMessage.EnvelopeId = signature.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(docContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {

                        if (envelope.IsEnvelopeComplete)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.EnvelopeId = signature.EnvelopeId;
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        bool isSignControl = false;
                        //check if document is provide for provided document id in the envelope
                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(signature.DocumentId));
                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = signature.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            //Check whether document content id is present in the envelope
                            docContentIdPresent = doc.DocumentContents.Any(dc => dc.ID == new Guid(signature.DocumentContentId));
                            if (docContentIdPresent)
                            {
                                //Check if the provided document content id to edit is of signature control or not
                                isSignControl = doc.DocumentContents.SingleOrDefault(dc => dc.ID == new Guid(signature.DocumentContentId)).ControlID == Constants.Control.Signature ? true : false;
                                break;
                            }
                        }
                        if (!docContentIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentContentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = signature.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (!isSignControl)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["NotSignatureControl"].ToString();
                            responseMessage.EnvelopeId = signature.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(signature.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(signature.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(signature.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(signature.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = signature.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }



                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(signature.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(signature.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = signature.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == signature.PageName)
                            {
                                if (docImage.Document.Id == new Guid(signature.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = signature.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        DocumentContents existingDocContents = documentContentsRepository.GetEntity(new Guid(signature.DocumentContentId));
                        existingDocContents.IsControlDeleted = true;
                        documentContentsRepository.Save(existingDocContents);

                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Signature);
                        controlTemplate = controlTemplate.Replace("#SignControlId", existingDocContents.ControlHtmlID);
                        controlTemplate = controlTemplate.Replace("#left", signature.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", signature.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", signature.Height + "px");
                        controlTemplate = controlTemplate.Replace("#width", signature.Width + "px");
                        controlTemplate = controlTemplate.Replace("#Required", signature.Required.ToString());
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Signature));
                        controlTemplate = controlTemplate.Replace("#recipientId", signature.RecipientId);
                       
                        Guid newDocContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = newDocContentId;
                        documentContents.DocumentID = new Guid(signature.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = existingDocContents.ControlHtmlID;
                        documentContents.Label = "Sign control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Signature;
                        documentContents.Height = signature.Height;
                        documentContents.Width = signature.Width;
                        documentContents.XCoordinate = signature.XCordinate;
                        documentContents.YCoordinate = signature.YCordinate;
                        documentContents.ZCoordinate = signature.ZCordinate;
                        documentContents.RecipientID = new Guid(signature.RecipientId);
                        documentContents.Required = signature.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = newDocContentId;
                        controlStyle.FontID = Guid.Parse("1875C58D-52BD-498A-BE6D-433A8858357E");
                        controlStyle.FontSize = 14;
                        controlStyle.FontColor = "#000000";
                        controlStyle.IsBold = false;
                        controlStyle.IsItalic = false;
                        controlStyle.IsUnderline = false;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(signature.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = newDocContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["EditSignatureSuccess"].ToString();
                        responseMessage.EnvelopeId = signature.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(newDocContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
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

        [AuthorizeAccess]
        [HttpDelete]
        public HttpResponseMessage ClearControls(string envelopeCode)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            Envelope envelope = new Envelope();

            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                using (var dbContext = new eSignEntities())
                {
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    DocumentContentsRepository documentContentRepository = new DocumentContentsRepository(dbContext);
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);

                    Guid userId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));

                    envelope = envelopeRepository.GetEnvelopeDetails(new Guid(envelopeCode), userId);


                    if (envelope == null)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }


                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["DeleteControlInvaild"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    // Envelope should have atleast one document
                    if (envelope.Documents.Count == 0)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["ClearControlDocMissing"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    
                    // Delete each control from each document
                    var documents = envelope.Documents;
                    var documentContent = documents.SelectMany(x => x.DocumentContents).ToList();
                    var documentContentDeleted = documents.SelectMany(x => x.DocumentContents).Where(d => d.IsControlDeleted == false).ToList();

                    if (documentContentDeleted.Count == 0)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoControlToDelete"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    foreach (var contents in documentContent)
                    {
                        foreach (var document in documents)
                        {
                            documentContentRepository.DeleteDocumentControl(contents.ID);
                        }
                    }

                    dbContext.SaveChanges();


                    responseMessage.StatusCode = HttpStatusCode.OK;
                    responseMessage.StatusMessage = "OK";
                    responseMessage.EnvelopeId = envelopeCode;
                    responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["ClearControlSuccess"].ToString());
                    responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
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
        [HttpDelete]
        public HttpResponseMessage DeleteDropDownOption(string envelopeCode, string id)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            Envelope envelope = new Envelope();
            bool selectControlOptionFlag = false;
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                using (var dbContext = new eSignEntities())
                {
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    DocumentContentsRepository documentContentRepository = new DocumentContentsRepository(dbContext);
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);

                    Guid userId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));

                    envelope = envelopeRepository.GetEnvelopeDetails(new Guid(envelopeCode), userId);

                    
                    if (envelope == null)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["DeleteControlInvaild"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    foreach (var doc in envelope.Documents)
                    {
                        foreach (var docContent in doc.DocumentContents)
                        {
                            foreach (var selectControl in docContent.SelectControlOptions)
                            {
                                if (selectControl.ID == new Guid(id))
                                    selectControlOptionFlag = true;
                            }
                        }
                    }


                    if (!selectControlOptionFlag)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["SelectControlNotExist"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool deleteResult = documentContentRepository.DeleteSelectControlOption(new Guid(id));
                    dbContext.SaveChanges();
                    if (deleteResult)
                    {
                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.DocumentContentId = id;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["SelectControlOptionDeleteSuccess"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }

                }

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
        [HttpDelete]
        public HttpResponseMessage DeleteControl(string envelopeCode, string id)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            Envelope envelope = new Envelope();
            bool docControlFlag = false;
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                using (var dbContext = new eSignEntities())
                {
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    DocumentContentsRepository documentContentRepository = new DocumentContentsRepository(dbContext);
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);

                    Guid userId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));

                    envelope = envelopeRepository.GetEnvelopeDetails(new Guid(envelopeCode), userId);


                    if (envelope == null)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["DeleteControlInvaild"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    foreach (var doc in envelope.Documents)
                    {
                        foreach (var docContent in doc.DocumentContents)
                        {
                            if (docContent.ID == new Guid(id) && !docContent.IsControlDeleted)                            
                                docControlFlag = true;
                            
                        }
                    }


                    if (!docControlFlag)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.DocumentContentId = id;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["ControlNotExist"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool deleteResult = documentContentRepository.DeleteDocumentControl(new Guid(id));
                    dbContext.SaveChanges();

                    if (deleteResult)
                    {
                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.DocumentContentId = id;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["ControlDeleteSuccess"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }

                }

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
        [HttpPost]
        public HttpResponseMessage DateControl(Date date)
        {

            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            int pageNumber = 0;
            int documentPageNumber = 0;
            bool docIdPresent = false;
            bool docContentIdPresent = false;
            int docContentCount = 0;
            bool recPresent = false;
            string recName = "";
            try
            {
                string envelopeID = date.EnvelopeId;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), date.EnvelopeId);
                if (string.IsNullOrEmpty(envelopeID) || string.IsNullOrEmpty(date.RecipientId) || string.IsNullOrEmpty(date.DocumentId) || string.IsNullOrEmpty(date.PageName) || date.XCordinate == 0 || date.YCordinate == 0 || date.ZCordinate == 0)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = date.EnvelopeId;
                    responseMessage.DocumentId = date.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                //if (date.Height != 50 || date.Width != 100)
                //{
                //    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                //    responseMessage.StatusMessage = "NotAcceptable";
                //    responseMessage.Message = ConfigurationManager.AppSettings["NotValidHeightWidth"].ToString();
                //    responseMessage.EnvelopeId = date.EnvelopeId;
                //    responseMessage.DocumentId = date.DocumentId;
                //    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                //    return responseToClient;
                //}

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = date.EnvelopeId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }
                //if (string.IsNullOrEmpty(signature.RecipientId) || string.IsNullOrEmpty(signature.DocumentId))
                //{

                //}

                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    RecipientRepository recipientRepository = new RecipientRepository(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();

                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid userId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(userId, new Guid(date.EnvelopeId));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = date.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }
                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = date.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = date.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(date.EnvelopeId));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString();
                        responseMessage.EnvelopeId = date.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (date.DocumentContentId == "" || date.DocumentContentId == null)
                    {

                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(date.DocumentId));

                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = date.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(date.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(date.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(date.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(date.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = date.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(date.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(date.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = date.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == date.PageName)
                            {
                                if (docImage.Document.Id == new Guid(date.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = date.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            docContentCount = docContentCount + doc.DocumentContents.Count;
                        }

                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Date);
                        controlTemplate = controlTemplate.Replace("#DateControlId", "dateControl" + docContentCount);
                        controlTemplate = controlTemplate.Replace("#left", date.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", date.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", "100px");
                        controlTemplate = controlTemplate.Replace("#Required", date.Required.ToString());
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Date));
                        controlTemplate = controlTemplate.Replace("#recipientId", date.RecipientId);

                        Guid docContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = docContentId;
                        documentContents.DocumentID = new Guid(date.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = "dateControl" + docContentCount;
                        documentContents.Label = "Date control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Date;
                        documentContents.Height = 22;
                        documentContents.Width = 100;
                        documentContents.XCoordinate = date.XCordinate;
                        documentContents.YCoordinate = date.YCordinate;
                        documentContents.ZCoordinate = date.ZCordinate;
                        documentContents.RecipientID = new Guid(date.RecipientId);
                        documentContents.Required = date.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = docContentId;
                        controlStyle.FontID = Guid.Parse("1875C58D-52BD-498A-BE6D-433A8858357E");
                        controlStyle.FontSize = 14;
                        controlStyle.FontColor = "#000000";
                        controlStyle.IsBold = false;
                        controlStyle.IsItalic = false;
                        controlStyle.IsUnderline = false;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(date.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = docContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["AddDateSuccess"].ToString();
                        responseMessage.EnvelopeId = date.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(docContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {

                        if (envelope.IsEnvelopeComplete)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.EnvelopeId = date.EnvelopeId;
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        bool isSignControl = false;
                        //check if document is provide for provided document id in the envelope
                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(date.DocumentId));
                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = date.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            //Check whether document content id is present in the envelope
                            docContentIdPresent = doc.DocumentContents.Any(dc => dc.ID == new Guid(date.DocumentContentId));
                            if (docContentIdPresent)
                            {
                                //Check if the provided document content id to edit is of signature control or not
                                isSignControl = doc.DocumentContents.SingleOrDefault(dc => dc.ID == new Guid(date.DocumentContentId)).ControlID == Constants.Control.Date ? true : false;
                                break;
                            }
                        }
                        if (!docContentIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentContentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = date.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (!isSignControl)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["NotDateControl"].ToString();
                            responseMessage.EnvelopeId = date.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(date.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(date.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(date.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(date.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = date.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }



                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(date.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(date.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = date.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == date.PageName)
                            {
                                if (docImage.Document.Id == new Guid(date.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = date.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        DocumentContents existingDocContents = documentContentsRepository.GetEntity(new Guid(date.DocumentContentId));
                        existingDocContents.IsControlDeleted = true;
                        documentContentsRepository.Save(existingDocContents);

                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Date);
                        controlTemplate = controlTemplate.Replace("#DateControlId", existingDocContents.ControlHtmlID);
                        controlTemplate = controlTemplate.Replace("#left", date.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", date.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", "100px");
                        controlTemplate = controlTemplate.Replace("#Required", date.Required.ToString());
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Date));
                        controlTemplate = controlTemplate.Replace("#recipientId", date.RecipientId);

                        Guid newDocContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = newDocContentId;
                        documentContents.DocumentID = new Guid(date.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = existingDocContents.ControlHtmlID;
                        documentContents.Label = "Date control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Date;
                        documentContents.Height = 22;
                        documentContents.Width = 100;
                        documentContents.XCoordinate = date.XCordinate;
                        documentContents.YCoordinate = date.YCordinate;
                        documentContents.ZCoordinate = date.ZCordinate;
                        documentContents.RecipientID = new Guid(date.RecipientId);
                        documentContents.Required = date.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = newDocContentId;
                        controlStyle.FontID = Guid.Parse("1875C58D-52BD-498A-BE6D-433A8858357E");
                        controlStyle.FontSize = 14;
                        controlStyle.FontColor = "#000000";
                        controlStyle.IsBold = false;
                        controlStyle.IsItalic = false;
                        controlStyle.IsUnderline = false;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(date.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = newDocContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["EditDateSuccess"].ToString();
                        responseMessage.EnvelopeId = date.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(newDocContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
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

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage NameControl(Name name)
        {

            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            int pageNumber = 0;
            int documentPageNumber = 0;
            bool docIdPresent = false;
            bool docContentIdPresent = false;
            int docContentCount = 0;
            bool recPresent = false;
            string recName = "";
            try
            {
                string envelopeID = name.EnvelopeId;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), name.EnvelopeId);
                if (string.IsNullOrEmpty(envelopeID) || name.fontSize == 0 || string.IsNullOrEmpty(name.fontFamilyID) || string.IsNullOrWhiteSpace(name.Color) || string.IsNullOrEmpty(name.Color) || string.IsNullOrEmpty(name.RecipientId) || string.IsNullOrEmpty(name.DocumentId) || string.IsNullOrEmpty(name.PageName) || name.XCordinate == 0 || name.YCordinate == 0 || name.ZCordinate == 0)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = name.EnvelopeId;
                    responseMessage.DocumentId = name.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!name.Color.StartsWith("#") || name.Color.Length != 7)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["ColorInvalid"].ToString();
                    responseMessage.EnvelopeId = name.EnvelopeId;
                    responseMessage.DocumentId = name.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                
                if (name.Width < 100)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["NotValidWidth"].ToString();
                    responseMessage.EnvelopeId = name.EnvelopeId;
                    responseMessage.DocumentId = name.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = name.EnvelopeId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }

                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    RecipientRepository recipientRepository = new RecipientRepository(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    GenericRepository genericRepository = new GenericRepository();
                    
                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid userId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(userId, new Guid(name.EnvelopeId));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = name.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    if (!masterDataRepository.ValidateFontFamilyId(new Guid(name.fontFamilyID)))
                    {
                        responseMessage.StatusCode = HttpStatusCode.Forbidden;
                        responseMessage.StatusMessage = "Forbidden";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["FontFamilyInvalid"].ToString());
                        responseMessage.EnvelopeId = name.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                        return responseToClient;                       
                    }

                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = name.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = name.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(name.EnvelopeId));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString();
                        responseMessage.EnvelopeId = name.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (name.DocumentContentId == "" || name.DocumentContentId == null)
                    {

                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(name.DocumentId));

                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = name.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(name.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(name.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(name.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(name.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = name.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(name.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(name.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = name.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == name.PageName)
                            {
                                if (docImage.Document.Id == new Guid(name.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = name.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            docContentCount = docContentCount + doc.DocumentContents.Count;
                        }


                        FontList loFontList = new FontList();
                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Name);
                        controlTemplate = controlTemplate.Replace("#nameControl", "nameControl" + docContentCount);
                        controlTemplate = controlTemplate.Replace("#left", name.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", name.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", "100px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Name));
                        controlTemplate = controlTemplate.Replace("#recipientId", name.RecipientId);
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(name.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", name.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(name.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", name.fontSize + "px");
                        controlTemplate = controlTemplate.Replace("#Required", name.Required.ToString());

                        controlTemplate = controlTemplate.Replace("#fontStyle", name.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", name.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", name.Underline ? "underline" : "normal");

                        Guid docContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = docContentId;
                        documentContents.DocumentID = new Guid(name.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = "nameControl" + docContentCount;
                        documentContents.Label = "Name control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Name;
                        documentContents.Height = 22;
                        documentContents.Width = name.Width;
                        documentContents.XCoordinate = name.XCordinate;
                        documentContents.YCoordinate = name.YCordinate;
                        documentContents.ZCoordinate = name.ZCordinate;
                        documentContents.RecipientID = new Guid(name.RecipientId);
                        documentContents.Required = name.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = docContentId;
                        controlStyle.FontID = Guid.Parse(name.fontFamilyID);
                        controlStyle.FontSize = name.fontSize;
                        controlStyle.FontColor = name.Color;
                        controlStyle.IsBold = name.Bold;
                        controlStyle.IsItalic = name.Italic;
                        controlStyle.IsUnderline = name.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(name.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = docContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["AddNameSuccess"].ToString();
                        responseMessage.EnvelopeId = name.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(docContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {
                        
                        if (envelope.IsEnvelopeComplete)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.EnvelopeId = name.EnvelopeId;
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        bool isNameControl = false;
                        //check if document is provide for provided document id in the envelope
                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(name.DocumentId));
                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = name.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            //Check whether document content id is present in the envelope
                            docContentIdPresent = doc.DocumentContents.Any(dc => dc.ID == new Guid(name.DocumentContentId));
                            if (docContentIdPresent)
                            {
                                //Check if the provided document content id to edit is of signature control or not
                                isNameControl = doc.DocumentContents.SingleOrDefault(dc => dc.ID == new Guid(name.DocumentContentId)).ControlID == Constants.Control.Name ? true : false;
                                break;
                            }
                        }
                        if (!docContentIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentContentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = name.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (!isNameControl)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["NotNameControl"].ToString();
                            responseMessage.EnvelopeId = name.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(name.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(name.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(name.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(name.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = name.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }



                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(name.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(name.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = name.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == name.PageName)
                            {
                                if (docImage.Document.Id == new Guid(name.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = name.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        DocumentContents existingDocContents = documentContentsRepository.GetEntity(new Guid(name.DocumentContentId));
                        existingDocContents.IsControlDeleted = true;
                        documentContentsRepository.Save(existingDocContents);

                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Name);
                        controlTemplate = controlTemplate.Replace("#nameControl", existingDocContents.ControlHtmlID);
                        controlTemplate = controlTemplate.Replace("#left", name.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", name.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", name.Width + "px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Name));
                        controlTemplate = controlTemplate.Replace("#recipientId", name.RecipientId);
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(name.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", name.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(name.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", name.fontSize + "px");
                        controlTemplate = controlTemplate.Replace("#Required", name.Required.ToString());

                        controlTemplate = controlTemplate.Replace("#fontStyle", name.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", name.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", name.Underline ? "underline" : "normal");

                        Guid newDocContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = newDocContentId;
                        documentContents.DocumentID = new Guid(name.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = existingDocContents.ControlHtmlID;
                        documentContents.Label = "Name control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Name;
                        documentContents.Height = 22;
                        documentContents.Width = name.Width;
                        documentContents.XCoordinate = name.XCordinate;
                        documentContents.YCoordinate = name.YCordinate;
                        documentContents.ZCoordinate = name.ZCordinate;
                        documentContents.RecipientID = new Guid(name.RecipientId);
                        documentContents.Required = name.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = newDocContentId;
                        controlStyle.FontID = Guid.Parse(name.fontFamilyID);
                        controlStyle.FontSize = name.fontSize;
                        controlStyle.FontColor = name.Color;
                        controlStyle.IsBold = name.Bold;
                        controlStyle.IsItalic = name.Italic;
                        controlStyle.IsUnderline = name.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(name.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = newDocContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["EditNameSuccess"].ToString();
                        responseMessage.EnvelopeId = name.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(newDocContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
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

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage TextControl(Text text)
        {

            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            int pageNumber = 0;
            int documentPageNumber = 0;
            bool docIdPresent = false;
            bool docContentIdPresent = false;
            int docContentCount = 0;
            bool recPresent = false;
            string recName = "";
            try
            {
                string envelopeID = text.EnvelopeId;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), text.EnvelopeId);
                if (string.IsNullOrEmpty(envelopeID) || string.IsNullOrEmpty(text.maxcharID) || string.IsNullOrEmpty(text.LabelText) || string.IsNullOrWhiteSpace(text.Color) || string.IsNullOrEmpty(text.textTypeID) || string.IsNullOrEmpty(text.RecipientId) || text.fontSize == 0 || string.IsNullOrEmpty(text.fontFamilyID) || string.IsNullOrEmpty(text.Color) || string.IsNullOrEmpty(text.DocumentId) || string.IsNullOrEmpty(text.PageName) || text.XCordinate == 0 || text.YCordinate == 0 || text.ZCordinate == 0)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = text.EnvelopeId;
                    responseMessage.DocumentId = text.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (text.Width < 100)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["NotValidWidth"].ToString();
                    responseMessage.EnvelopeId = text.EnvelopeId;
                    responseMessage.DocumentId = text.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!text.Color.StartsWith("#") || text.Color.Length != 7)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["ColorInvalid"].ToString();
                    responseMessage.EnvelopeId = text.EnvelopeId;
                    responseMessage.DocumentId = text.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = text.EnvelopeId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }

                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    RecipientRepository recipientRepository = new RecipientRepository(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    GenericRepository genericRepository = new GenericRepository();

                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(text.EnvelopeId));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = text.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    if (!masterDataRepository.ValidateFontFamilyId(new Guid(text.fontFamilyID)))
                    {
                        responseMessage.StatusCode = HttpStatusCode.Forbidden;
                        responseMessage.StatusMessage = "Forbidden";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["FontFamilyInvalid"].ToString());
                        responseMessage.EnvelopeId = text.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                        return responseToClient;
                    }

                    if (!masterDataRepository.ValidateMaxCharId(new Guid(text.maxcharID)))
                    {
                        responseMessage.StatusCode = HttpStatusCode.Forbidden;
                        responseMessage.StatusMessage = "Forbidden";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["MaxCharInvalid"].ToString());
                        responseMessage.EnvelopeId = text.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                        return responseToClient;
                    }

                    if (!masterDataRepository.ValidateTextTypeId(new Guid(text.textTypeID)))
                    {
                        responseMessage.StatusCode = HttpStatusCode.Forbidden;
                        responseMessage.StatusMessage = "Forbidden";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TextTypeInvalid"].ToString());
                        responseMessage.EnvelopeId = text.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                        return responseToClient;
                    }
                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = text.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = text.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(text.EnvelopeId));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString();
                        responseMessage.EnvelopeId = text.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (text.DocumentContentId == "" || text.DocumentContentId == null)
                    {

                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(text.DocumentId));

                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = text.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(text.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(text.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(text.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(text.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = text.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(text.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(text.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = text.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == text.PageName)
                            {
                                if (docImage.Document.Id == new Guid(text.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = text.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            docContentCount = docContentCount + doc.DocumentContents.Count;
                        }

                        FontList loFontList = new FontList();
                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Name);
                        controlTemplate = controlTemplate.Replace("#textControl", "textControl" + docContentCount);
                        controlTemplate = controlTemplate.Replace("#left", text.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", text.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", text.Width + "px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Text));
                        controlTemplate = controlTemplate.Replace("#recipientId", text.RecipientId);
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(text.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", text.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(text.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", text.fontSize + "px");
                        controlTemplate = controlTemplate.Replace("#Required", text.Required.ToString());

                        controlTemplate = controlTemplate.Replace("#fontStyle", text.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", text.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", text.Underline ? "underline" : "normal");

                        controlTemplate = controlTemplate.Replace("#maxcharID", text.maxcharID);
                        controlTemplate = controlTemplate.Replace("#textTypeid", text.textTypeID);

                        Guid docContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = docContentId;
                        documentContents.DocumentID = new Guid(text.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = "textControl" + docContentCount;
                        documentContents.Label = text.LabelText;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Text;
                        documentContents.Height = 22;
                        documentContents.Width = text.Width;
                        documentContents.XCoordinate = text.XCordinate;
                        documentContents.YCoordinate = text.YCordinate;
                        documentContents.ZCoordinate = text.ZCordinate;
                        documentContents.RecipientID = new Guid(text.RecipientId);
                        documentContents.Required = text.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContents.MaxLength = new Guid(text.maxcharID);
                        documentContents.ControlType = new Guid(text.textTypeID);
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = docContentId;
                        controlStyle.FontID = Guid.Parse(text.fontFamilyID);
                        controlStyle.FontSize = text.fontSize;
                        controlStyle.FontColor = text.Color;
                        controlStyle.IsBold = text.Bold;
                        controlStyle.IsItalic = text.Italic;
                        controlStyle.IsUnderline = text.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(text.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = docContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["AddTextSuccess"].ToString();
                        responseMessage.EnvelopeId = text.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(docContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {
                        if (envelope.IsEnvelopeComplete)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.EnvelopeId = text.EnvelopeId;
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        bool isTextControl = false;
                        //check if document is provide for provided document id in the envelope
                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(text.DocumentId));
                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = text.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            //Check whether document content id is present in the envelope
                            docContentIdPresent = doc.DocumentContents.Any(dc => dc.ID == new Guid(text.DocumentContentId));
                            if (docContentIdPresent)
                            {
                                //Check if the provided document content id to edit is of signature control or not
                                isTextControl = doc.DocumentContents.SingleOrDefault(dc => dc.ID == new Guid(text.DocumentContentId)).ControlID == Constants.Control.Text ? true : false;
                                break;
                            }
                        }
                        if (!docContentIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentContentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = text.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (!isTextControl)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["NotTextControl"].ToString();
                            responseMessage.EnvelopeId = text.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(text.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(text.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(text.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(text.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = text.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }



                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(text.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(text.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = text.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == text.PageName)
                            {
                                if (docImage.Document.Id == new Guid(text.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = text.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        DocumentContents existingDocContents = documentContentsRepository.GetEntity(new Guid(text.DocumentContentId));
                        existingDocContents.IsControlDeleted = true;
                        documentContentsRepository.Save(existingDocContents);

                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Name);
                        controlTemplate = controlTemplate.Replace("#textControl", existingDocContents.ControlHtmlID);
                        controlTemplate = controlTemplate.Replace("#left", text.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", text.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", text.Width + "px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Text));
                        controlTemplate = controlTemplate.Replace("#recipientId", text.RecipientId);
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(text.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", text.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(text.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", text.fontSize + "px");
                        controlTemplate = controlTemplate.Replace("#Required", text.Required.ToString());

                        controlTemplate = controlTemplate.Replace("#fontStyle", text.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", text.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", text.Underline ? "underline" : "normal");

                        controlTemplate = controlTemplate.Replace("#maxcharID", text.maxcharID);
                        controlTemplate = controlTemplate.Replace("#textTypeid", text.textTypeID);

                        Guid newDocContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = newDocContentId;
                        documentContents.DocumentID = new Guid(text.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = existingDocContents.ControlHtmlID;
                        documentContents.Label = text.LabelText;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Text;
                        documentContents.Height = 22;
                        documentContents.Width = text.Width;
                        documentContents.XCoordinate = text.XCordinate;
                        documentContents.YCoordinate = text.YCordinate;
                        documentContents.ZCoordinate = text.ZCordinate;
                        documentContents.RecipientID = new Guid(text.RecipientId);
                        documentContents.Required = text.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContents.MaxLength = new Guid(text.maxcharID);
                        documentContents.ControlType = new Guid(text.textTypeID);
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = newDocContentId;
                        controlStyle.FontID = Guid.Parse(text.fontFamilyID);
                        controlStyle.FontSize = text.fontSize;
                        controlStyle.FontColor = text.Color;
                        controlStyle.IsBold = text.Bold;
                        controlStyle.IsItalic = text.Italic;
                        controlStyle.IsUnderline = text.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(text.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = newDocContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["EditTextSuccess"].ToString();
                        responseMessage.EnvelopeId = text.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(newDocContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
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

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage TitleControl(Title title)
        {

            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            int pageNumber = 0;
            int documentPageNumber = 0;
            bool docIdPresent = false;
            bool docContentIdPresent = false;
            int docContentCount = 0;
            bool recPresent = false;
            string recName = "";
            try
            {
                string envelopeID = title.EnvelopeId;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), title.EnvelopeId);
                if (string.IsNullOrEmpty(envelopeID) || string.IsNullOrEmpty(title.Color) || string.IsNullOrWhiteSpace(title.Color) || title.fontSize == 0 || string.IsNullOrEmpty(title.fontFamilyID) || string.IsNullOrEmpty(title.Color) || string.IsNullOrEmpty(title.RecipientId) || string.IsNullOrEmpty(title.DocumentId) || string.IsNullOrEmpty(title.PageName) || title.XCordinate == 0 || title.YCordinate == 0 || title.ZCordinate == 0)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = title.EnvelopeId;
                    responseMessage.DocumentId = title.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!title.Color.StartsWith("#") || title.Color.Length != 7)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["ColorInvalid"].ToString();
                    responseMessage.EnvelopeId = title.EnvelopeId;
                    responseMessage.DocumentId = title.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (title.Width < 100)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["NotValidWidth"].ToString();
                    responseMessage.EnvelopeId = title.EnvelopeId;
                    responseMessage.DocumentId = title.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = title.EnvelopeId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }

                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    RecipientRepository recipientRepository = new RecipientRepository(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    GenericRepository genericRepository = new GenericRepository();

                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(title.EnvelopeId));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = title.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    if (!masterDataRepository.ValidateFontFamilyId(new Guid(title.fontFamilyID)))
                    {
                        responseMessage.StatusCode = HttpStatusCode.Forbidden;
                        responseMessage.StatusMessage = "Forbidden";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["FontFamilyInvalid"].ToString());
                        responseMessage.EnvelopeId = title.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                        return responseToClient;
                    }

                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = title.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = title.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(title.EnvelopeId));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString();
                        responseMessage.EnvelopeId = title.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (title.DocumentContentId == "" || title.DocumentContentId == null)
                    {

                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(title.DocumentId));

                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = title.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(title.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(title.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(title.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(title.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = title.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(title.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(title.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = title.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == title.PageName)
                            {
                                if (docImage.Document.Id == new Guid(title.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = title.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            docContentCount = docContentCount + doc.DocumentContents.Count;
                        }

                        FontList loFontList = new FontList();
                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Title);
                        controlTemplate = controlTemplate.Replace("#titleControl", "titleControl" + docContentCount);
                        controlTemplate = controlTemplate.Replace("#left", title.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", title.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", title.Width + "px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Title));
                        controlTemplate = controlTemplate.Replace("#recipientId", title.RecipientId);
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(title.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", title.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(title.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", title.fontSize + "px");
                        controlTemplate = controlTemplate.Replace("#Required", title.Required.ToString());

                        controlTemplate = controlTemplate.Replace("#fontStyle", title.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", title.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", title.Underline ? "underline" : "normal");

                        Guid docContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = docContentId;
                        documentContents.DocumentID = new Guid(title.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = "titleControl" + docContentCount;
                        documentContents.Label = "Title control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Title;
                        documentContents.Height = 22;
                        documentContents.Width = title.Width;
                        documentContents.XCoordinate = title.XCordinate;
                        documentContents.YCoordinate = title.YCordinate;
                        documentContents.ZCoordinate = title.ZCordinate;
                        documentContents.RecipientID = new Guid(title.RecipientId);
                        documentContents.Required = title.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = docContentId;
                        controlStyle.FontID = Guid.Parse(title.fontFamilyID);
                        controlStyle.FontSize = title.fontSize;
                        controlStyle.FontColor = title.Color;
                        controlStyle.IsBold = title.Bold;
                        controlStyle.IsItalic = title.Italic;
                        controlStyle.IsUnderline = title.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(title.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = docContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["AddTitleSuccess"].ToString();
                        responseMessage.EnvelopeId = title.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(docContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {

                        if (envelope.IsEnvelopeComplete)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.EnvelopeId = title.EnvelopeId;
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        bool istitleControl = false;
                        //check if document is provide for provided document id in the envelope
                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(title.DocumentId));
                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = title.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            //Check whether document content id is present in the envelope
                            docContentIdPresent = doc.DocumentContents.Any(dc => dc.ID == new Guid(title.DocumentContentId));
                            if (docContentIdPresent)
                            {
                                //Check if the provided document content id to edit is of signature control or not
                                istitleControl = doc.DocumentContents.SingleOrDefault(dc => dc.ID == new Guid(title.DocumentContentId)).ControlID == Constants.Control.Title ? true : false;
                                break;
                            }
                        }
                        if (!docContentIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentContentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = title.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (!istitleControl)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["NotTitleControl"].ToString();
                            responseMessage.EnvelopeId = title.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(title.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(title.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(title.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(title.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = title.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }



                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(title.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(title.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = title.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == title.PageName)
                            {
                                if (docImage.Document.Id == new Guid(title.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = title.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        DocumentContents existingDocContents = documentContentsRepository.GetEntity(new Guid(title.DocumentContentId));
                        existingDocContents.IsControlDeleted = true;
                        documentContentsRepository.Save(existingDocContents);

                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Title);
                        controlTemplate = controlTemplate.Replace("#titleControl", existingDocContents.ControlHtmlID);
                        controlTemplate = controlTemplate.Replace("#left", title.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", title.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", title.Width + "px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Title));
                        controlTemplate = controlTemplate.Replace("#recipientId", title.RecipientId);
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(title.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", title.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(title.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", title.fontSize + "px");
                        controlTemplate = controlTemplate.Replace("#Required", title.Required.ToString());

                        controlTemplate = controlTemplate.Replace("#fontStyle", title.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", title.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", title.Underline ? "underline" : "normal");

                        Guid newDocContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = newDocContentId;
                        documentContents.DocumentID = new Guid(title.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = existingDocContents.ControlHtmlID;
                        documentContents.Label = "Title control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Title;
                        documentContents.Height = 22;
                        documentContents.Width = title.Width;
                        documentContents.XCoordinate = title.XCordinate;
                        documentContents.YCoordinate = title.YCordinate;
                        documentContents.ZCoordinate = title.ZCordinate;
                        documentContents.RecipientID = new Guid(title.RecipientId);
                        documentContents.Required = title.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = newDocContentId;
                        controlStyle.FontID = Guid.Parse(title.fontFamilyID);
                        controlStyle.FontSize = title.fontSize;
                        controlStyle.FontColor = title.Color;
                        controlStyle.IsBold = title.Bold;
                        controlStyle.IsItalic = title.Italic;
                        controlStyle.IsUnderline = title.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(title.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = newDocContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["EditTitleSuccess"].ToString();
                        responseMessage.EnvelopeId = title.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(newDocContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
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

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage CompanyControl(Company company)
        {

            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            int pageNumber = 0;
            int documentPageNumber = 0;
            bool docIdPresent = false;
            bool docContentIdPresent = false;
            int docContentCount = 0;
            bool recPresent = false;
            string recName = "";
            try
            {
                string envelopeID = company.EnvelopeId;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), company.EnvelopeId);
                if (string.IsNullOrEmpty(envelopeID) || string.IsNullOrEmpty(company.RecipientId) || string.IsNullOrWhiteSpace(company.Color) || company.fontSize == 0 || string.IsNullOrEmpty(company.fontFamilyID) || string.IsNullOrEmpty(company.Color) || string.IsNullOrEmpty(company.DocumentId) || string.IsNullOrEmpty(company.PageName) || company.XCordinate == 0 || company.YCordinate == 0 || company.ZCordinate == 0)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = company.EnvelopeId;
                    responseMessage.DocumentId = company.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (company.Width < 100)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["NotValidWidth"].ToString();
                    responseMessage.EnvelopeId = company.EnvelopeId;
                    responseMessage.DocumentId = company.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!company.Color.StartsWith("#") || company.Color.Length != 7)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["ColorInvalid"].ToString();
                    responseMessage.EnvelopeId = company.EnvelopeId;
                    responseMessage.DocumentId = company.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = company.EnvelopeId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }

                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    RecipientRepository recipientRepository = new RecipientRepository(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    GenericRepository genericRepository = new GenericRepository();

                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(company.EnvelopeId));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = company.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    if (!masterDataRepository.ValidateFontFamilyId(new Guid(company.fontFamilyID)))
                    {
                        responseMessage.StatusCode = HttpStatusCode.Forbidden;
                        responseMessage.StatusMessage = "Forbidden";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["FontFamilyInvalid"].ToString());
                        responseMessage.EnvelopeId = company.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                        return responseToClient;
                    }

                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = company.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = company.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(company.EnvelopeId));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString();
                        responseMessage.EnvelopeId = company.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (company.DocumentContentId == "" || company.DocumentContentId == null)
                    {

                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(company.DocumentId));

                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = company.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(company.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(company.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(company.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(company.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = company.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(company.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(company.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = company.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == company.PageName)
                            {
                                if (docImage.Document.Id == new Guid(company.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = company.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            docContentCount = docContentCount + doc.DocumentContents.Count;
                        }

                        FontList loFontList = new FontList();
                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Company);
                        controlTemplate = controlTemplate.Replace("#companyControl", "companyControl" + docContentCount);
                        controlTemplate = controlTemplate.Replace("#left", company.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", company.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", company.Width + "px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Company));
                        controlTemplate = controlTemplate.Replace("#recipientId", company.RecipientId);
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(company.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", company.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(company.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", company.fontSize + "px");
                        controlTemplate = controlTemplate.Replace("#Required", company.Required.ToString());

                        controlTemplate = controlTemplate.Replace("#fontStyle", company.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", company.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", company.Underline ? "underline" : "normal");

                        Guid docContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = docContentId;
                        documentContents.DocumentID = new Guid(company.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = "companyControl" + docContentCount;
                        documentContents.Label = "Company control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Company;
                        documentContents.Height = 22;
                        documentContents.Width = company.Width;
                        documentContents.XCoordinate = company.XCordinate;
                        documentContents.YCoordinate = company.YCordinate;
                        documentContents.ZCoordinate = company.ZCordinate;
                        documentContents.RecipientID = new Guid(company.RecipientId);
                        documentContents.Required = company.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = docContentId;
                        controlStyle.FontID = Guid.Parse(company.fontFamilyID);
                        controlStyle.FontSize = company.fontSize;
                        controlStyle.FontColor = company.Color;
                        controlStyle.IsBold = company.Bold;
                        controlStyle.IsItalic = company.Italic;
                        controlStyle.IsUnderline = company.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(company.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = docContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["AddCompanySuccess"].ToString();
                        responseMessage.EnvelopeId = company.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(docContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {

                        if (envelope.IsEnvelopeComplete)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.EnvelopeId = company.EnvelopeId;
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        bool isCompanyControl = false;
                        //check if document is provide for provided document id in the envelope
                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(company.DocumentId));
                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = company.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            //Check whether document content id is present in the envelope
                            docContentIdPresent = doc.DocumentContents.Any(dc => dc.ID == new Guid(company.DocumentContentId));
                            if (docContentIdPresent)
                            {
                                //Check if the provided document content id to edit is of signature control or not
                                isCompanyControl = doc.DocumentContents.SingleOrDefault(dc => dc.ID == new Guid(company.DocumentContentId)).ControlID == Constants.Control.Company ? true : false;
                                break;
                            }
                        }
                        if (!docContentIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentContentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = company.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (!isCompanyControl)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["NotCompanyControl"].ToString();
                            responseMessage.EnvelopeId = company.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(company.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(company.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(company.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(company.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = company.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }



                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(company.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(company.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = company.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == company.PageName)
                            {
                                if (docImage.Document.Id == new Guid(company.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = company.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        DocumentContents existingDocContents = documentContentsRepository.GetEntity(new Guid(company.DocumentContentId));
                        existingDocContents.IsControlDeleted = true;
                        documentContentsRepository.Save(existingDocContents);

                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Company);
                        controlTemplate = controlTemplate.Replace("#companyControl", existingDocContents.ControlHtmlID);
                        controlTemplate = controlTemplate.Replace("#left", company.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", company.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", company.Width + "px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Company));
                        controlTemplate = controlTemplate.Replace("#recipientId", company.RecipientId);
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(company.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", company.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(company.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", company.fontSize + "px");
                        controlTemplate = controlTemplate.Replace("#Required", company.Required.ToString());

                        controlTemplate = controlTemplate.Replace("#fontStyle", company.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", company.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", company.Underline ? "underline" : "normal");

                        Guid newDocContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = newDocContentId;
                        documentContents.DocumentID = new Guid(company.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = existingDocContents.ControlHtmlID; // "companyControl" + docContentCount;
                        documentContents.Label = "Company control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Company;
                        documentContents.Height = 22;
                        documentContents.Width = company.Width;
                        documentContents.XCoordinate = company.XCordinate;
                        documentContents.YCoordinate = company.YCordinate;
                        documentContents.ZCoordinate = company.ZCordinate;
                        documentContents.RecipientID = new Guid(company.RecipientId);
                        documentContents.Required = company.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = newDocContentId;
                        controlStyle.FontID = Guid.Parse(company.fontFamilyID);
                        controlStyle.FontSize = company.fontSize;
                        controlStyle.FontColor = company.Color;
                        controlStyle.IsBold = company.Bold;
                        controlStyle.IsItalic = company.Italic;
                        controlStyle.IsUnderline = company.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(company.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = newDocContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["EditCompanySuccess"].ToString();
                        responseMessage.EnvelopeId = company.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(newDocContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
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

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage InitialsControl(Initials initials)
        {

            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            int pageNumber = 0;
            int documentPageNumber = 0;
            bool docIdPresent = false;
            bool docContentIdPresent = false;
            int docContentCount = 0;
            bool recPresent = false;
            string recName = "";
            try
            {
                string envelopeID = initials.EnvelopeId;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), initials.EnvelopeId);
                if (string.IsNullOrEmpty(envelopeID) || string.IsNullOrEmpty(initials.RecipientId) || string.IsNullOrWhiteSpace(initials.Color) || initials.fontSize == 0 || string.IsNullOrEmpty(initials.fontFamilyID) || string.IsNullOrEmpty(initials.Color) || string.IsNullOrEmpty(initials.DocumentId) || string.IsNullOrEmpty(initials.PageName) || initials.XCordinate == 0 || initials.YCordinate == 0 || initials.ZCordinate == 0)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = initials.EnvelopeId;
                    responseMessage.DocumentId = initials.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!initials.Color.StartsWith("#") || initials.Color.Length != 7)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["ColorInvalid"].ToString();
                    responseMessage.EnvelopeId = initials.EnvelopeId;
                    responseMessage.DocumentId = initials.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                //if (initials.Width < 100)
                //{
                //    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                //    responseMessage.StatusMessage = "NotAcceptable";
                //    responseMessage.Message = ConfigurationManager.AppSettings["NotValidWidth"].ToString();
                //    responseMessage.EnvelopeId = initials.EnvelopeId;
                //    responseMessage.DocumentId = initials.DocumentId;
                //    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                //    return responseToClient;
                //}

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = initials.EnvelopeId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }

                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    RecipientRepository recipientRepository = new RecipientRepository(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    GenericRepository genericRepository = new GenericRepository();

                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(initials.EnvelopeId));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = initials.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    if (!masterDataRepository.ValidateFontFamilyId(new Guid(initials.fontFamilyID)))
                    {
                        responseMessage.StatusCode = HttpStatusCode.Forbidden;
                        responseMessage.StatusMessage = "Forbidden";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["FontFamilyInvalid"].ToString());
                        responseMessage.EnvelopeId = initials.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                        return responseToClient;
                    }

                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = initials.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = initials.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(initials.EnvelopeId));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString();
                        responseMessage.EnvelopeId = initials.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (initials.DocumentContentId == "" || initials.DocumentContentId == null)
                    {

                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(initials.DocumentId));

                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = initials.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(initials.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(initials.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = initials.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = initials.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == initials.PageName)
                            {
                                if (docImage.Document.Id == new Guid(initials.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = initials.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            docContentCount = docContentCount + doc.DocumentContents.Count;
                        }

                        FontList loFontList = new FontList();
                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Initials);
                        controlTemplate = controlTemplate.Replace("#initialsControl", "initialsControl" + docContentCount);
                        controlTemplate = controlTemplate.Replace("#left", initials.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", initials.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", "100px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Initials));
                        controlTemplate = controlTemplate.Replace("#recipientId", initials.RecipientId);
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(initials.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", initials.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(initials.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", initials.fontSize + "px");
                        controlTemplate = controlTemplate.Replace("#Required", initials.Required.ToString());

                        controlTemplate = controlTemplate.Replace("#fontStyle", initials.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", initials.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", initials.Underline ? "underline" : "normal");

                        Guid docContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = docContentId;
                        documentContents.DocumentID = new Guid(initials.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = "initialsControl" + docContentCount;
                        documentContents.Label = "Initials control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Initials;
                        documentContents.Height = 22;
                        documentContents.Width = 100;
                        documentContents.XCoordinate = initials.XCordinate;
                        documentContents.YCoordinate = initials.YCordinate;
                        documentContents.ZCoordinate = initials.ZCordinate;
                        documentContents.RecipientID = new Guid(initials.RecipientId);
                        documentContents.Required = initials.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = docContentId;
                        controlStyle.FontID = Guid.Parse(initials.fontFamilyID);
                        controlStyle.FontSize = initials.fontSize;
                        controlStyle.FontColor = initials.Color;
                        controlStyle.IsBold = initials.Bold;
                        controlStyle.IsItalic = initials.Italic;
                        controlStyle.IsUnderline = initials.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(initials.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = docContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["AddInitialsSuccess"].ToString();
                        responseMessage.EnvelopeId = initials.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(docContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {

                        if (envelope.IsEnvelopeComplete)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.EnvelopeId = initials.EnvelopeId;
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        bool isInitialsControl = false;
                        //check if document is provide for provided document id in the envelope
                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(initials.DocumentId));
                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = initials.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            //Check whether document content id is present in the envelope
                            docContentIdPresent = doc.DocumentContents.Any(dc => dc.ID == new Guid(initials.DocumentContentId));
                            if (docContentIdPresent)
                            {
                                //Check if the provided document content id to edit is of signature control or not
                                isInitialsControl = doc.DocumentContents.SingleOrDefault(dc => dc.ID == new Guid(initials.DocumentContentId)).ControlID == Constants.Control.Initials ? true : false;
                                break;
                            }
                        }
                        if (!docContentIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentContentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = initials.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (!isInitialsControl)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["NotInitialsControl"].ToString();
                            responseMessage.EnvelopeId = initials.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(initials.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(initials.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = initials.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }



                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = initials.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == initials.PageName)
                            {
                                if (docImage.Document.Id == new Guid(initials.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = initials.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        DocumentContents existingDocContents = documentContentsRepository.GetEntity(new Guid(initials.DocumentContentId));
                        existingDocContents.IsControlDeleted = true;
                        documentContentsRepository.Save(existingDocContents);


                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Initials);
                        controlTemplate = controlTemplate.Replace("#initialsControl", existingDocContents.ControlHtmlID);
                        controlTemplate = controlTemplate.Replace("#left", initials.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", initials.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", "100px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Initials));
                        controlTemplate = controlTemplate.Replace("#recipientId", initials.RecipientId);
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(initials.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", initials.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(initials.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", initials.fontSize + "px");
                        controlTemplate = controlTemplate.Replace("#Required", initials.Required.ToString());

                        controlTemplate = controlTemplate.Replace("#fontStyle", initials.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", initials.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", initials.Underline ? "underline" : "normal");

                        Guid newDocContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = newDocContentId;
                        documentContents.DocumentID = new Guid(initials.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = existingDocContents.ControlHtmlID; // "companyControl" + docContentCount;
                        documentContents.Label = "Initials control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Initials;
                        documentContents.Height = 22;
                        documentContents.Width = 100;
                        documentContents.XCoordinate = initials.XCordinate;
                        documentContents.YCoordinate = initials.YCordinate;
                        documentContents.ZCoordinate = initials.ZCordinate;
                        documentContents.RecipientID = new Guid(initials.RecipientId);
                        documentContents.Required = initials.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = newDocContentId;
                        controlStyle.FontID = Guid.Parse(initials.fontFamilyID);
                        controlStyle.FontSize = initials.fontSize;
                        controlStyle.FontColor = initials.Color;
                        controlStyle.IsBold = initials.Bold;
                        controlStyle.IsItalic = initials.Italic;
                        controlStyle.IsUnderline = initials.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(initials.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = newDocContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["EditInitialsSuccess"].ToString();
                        responseMessage.EnvelopeId = initials.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(newDocContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
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

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage LabelControl(Label label)
        {

            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            int pageNumber = 0;
            int documentPageNumber = 0;
            bool docIdPresent = false;
            bool docContentIdPresent = false;
            int docContentCount = 0;
            //bool recPresent = false;
            //string recName = "";
            try
            {
                string envelopeID = label.EnvelopeId;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), label.EnvelopeId);
                if (string.IsNullOrEmpty(envelopeID) || string.IsNullOrEmpty(Convert.ToString(label.Color)) || string.IsNullOrWhiteSpace(Convert.ToString(label.Color)) || label.fontSize == 0 || string.IsNullOrEmpty(Convert.ToString(label.fontFamilyID)) || string.IsNullOrEmpty(label.DocumentId) || string.IsNullOrEmpty(label.PageName) || string.IsNullOrEmpty(label.Text) || label.XCordinate == 0 || label.YCordinate == 0 || label.ZCordinate == 0)
                {
                    
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = label.EnvelopeId;
                    responseMessage.DocumentId = label.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!label.Color.StartsWith("#") || label.Color.Length != 7)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["ColorInvalid"].ToString();
                    responseMessage.EnvelopeId = label.EnvelopeId;
                    responseMessage.DocumentId = label.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (label.Width < 100)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["NotValidWidth"].ToString();
                    responseMessage.EnvelopeId = label.EnvelopeId;
                    responseMessage.DocumentId = label.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = label.EnvelopeId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }

                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    RecipientRepository recipientRepository = new RecipientRepository(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    GenericRepository genericRepository = new GenericRepository();

                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(label.EnvelopeId));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = label.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    if (!masterDataRepository.ValidateFontFamilyId(new Guid(label.fontFamilyID)))
                    {
                        responseMessage.StatusCode = HttpStatusCode.Forbidden;
                        responseMessage.StatusMessage = "Forbidden";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["FontFamilyInvalid"].ToString());
                        responseMessage.EnvelopeId = label.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                        return responseToClient;
                    }

                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = label.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = label.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(label.EnvelopeId));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString();
                        responseMessage.EnvelopeId = label.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (label.DocumentContentId == "" || label.DocumentContentId == null)
                    {

                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(label.DocumentId));

                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = label.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        //recPresent = envelope.Recipients.Any(r => r.ID == new Guid(initials.RecipientId));
                        //if (!recPresent)
                        //{
                        //    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        //    responseMessage.StatusMessage = "BadRequest";
                        //    responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                        //    responseMessage.EnvelopeId = initials.EnvelopeId;
                        //    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        //    return responseToClient;
                        //}

                        //recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).Name;

                        //bool isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        //if (isCCRecipient)
                        //{
                        //    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        //    responseMessage.StatusMessage = "BadRequest";
                        //    responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                        //    responseMessage.EnvelopeId = initials.EnvelopeId;
                        //    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        //    return responseToClient;
                        //}

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == label.PageName)
                            {
                                if (docImage.Document.Id == new Guid(label.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = label.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            docContentCount = docContentCount + doc.DocumentContents.Count;
                        }

                        FontList loFontList = new FontList();
                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Label);
                        controlTemplate = controlTemplate.Replace("#labelControl", "labelControl" + docContentCount);
                        controlTemplate = controlTemplate.Replace("#left", label.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", label.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", label.Width + "px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Label));
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(label.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", label.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(label.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", label.fontSize + "px");

                        controlTemplate = controlTemplate.Replace("#fontStyle", label.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", label.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", label.Underline ? "underline" : "normal");

                        Guid docContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = docContentId;
                        documentContents.DocumentID = new Guid(label.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = "labelControl" + docContentCount;
                        documentContents.Label = label.Text;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Label;
                        documentContents.Height = 22;
                        documentContents.Width = label.Width;
                        documentContents.XCoordinate = label.XCordinate;
                        documentContents.YCoordinate = label.YCordinate;
                        documentContents.ZCoordinate = label.ZCordinate;
                        //documentContents.Required = label.Required;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = docContentId;
                        controlStyle.FontID = Guid.Parse(label.fontFamilyID);
                        controlStyle.FontSize = label.fontSize;
                        controlStyle.FontColor = label.Color;
                        controlStyle.IsBold = label.Bold;
                        controlStyle.IsItalic = label.Italic;
                        controlStyle.IsUnderline = label.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(label.EnvelopeId);
                        response.DocumentContentId = docContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["AddLabelSuccess"].ToString();
                        responseMessage.EnvelopeId = label.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(docContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {

                        if (envelope.IsEnvelopeComplete)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.EnvelopeId = label.EnvelopeId;
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        bool isLabelControl = false;
                        //check if document is provide for provided document id in the envelope
                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(label.DocumentId));
                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = label.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            //Check whether document content id is present in the envelope
                            docContentIdPresent = doc.DocumentContents.Any(dc => dc.ID == new Guid(label.DocumentContentId));
                            if (docContentIdPresent)
                            {
                                //Check if the provided document content id to edit is of signature control or not
                                isLabelControl = doc.DocumentContents.SingleOrDefault(dc => dc.ID == new Guid(label.DocumentContentId)).ControlID == Constants.Control.Label ? true : false;
                                break;
                            }
                        }
                        if (!docContentIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentContentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = label.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (!isLabelControl)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["NotLabelControl"].ToString();
                            responseMessage.EnvelopeId = label.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        //recPresent = envelope.Recipients.Any(r => r.ID == new Guid(initials.RecipientId));
                        //if (!recPresent)
                        //{
                        //    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        //    responseMessage.StatusMessage = "BadRequest";
                        //    responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                        //    responseMessage.EnvelopeId = initials.EnvelopeId;
                        //    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        //    return responseToClient;
                        //}

                        //recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).Name;

                        //bool isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(initials.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        //if (isCCRecipient)
                        //{
                        //    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        //    responseMessage.StatusMessage = "BadRequest";
                        //    responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                        //    responseMessage.EnvelopeId = initials.EnvelopeId;
                        //    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        //    return responseToClient;
                        //}

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == label.PageName)
                            {
                                if (docImage.Document.Id == new Guid(label.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = label.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        DocumentContents existingDocContents = documentContentsRepository.GetEntity(new Guid(label.DocumentContentId));
                        existingDocContents.IsControlDeleted = true;
                        documentContentsRepository.Save(existingDocContents);

                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Label);
                        controlTemplate = controlTemplate.Replace("#labelControl", existingDocContents.ControlHtmlID);
                        controlTemplate = controlTemplate.Replace("#left", label.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", label.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "22px");
                        controlTemplate = controlTemplate.Replace("#width", label.Width + "px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Label));
                        // controlTemplate = controlTemplate.Replace("#recipientId", label.RecipientId);
                        controlTemplate = controlTemplate.Replace("#color", RGBConverter(label.Color));
                        controlTemplate = controlTemplate.Replace("#fontFamilyID", label.fontFamilyID);
                        controlTemplate = controlTemplate.Replace("#fontFamily", genericRepository.GetFontName(new Guid(label.fontFamilyID)));
                        controlTemplate = controlTemplate.Replace("#fontSize", label.fontSize + "px");

                        controlTemplate = controlTemplate.Replace("#fontStyle", label.Italic ? "italic" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontWeight", label.Bold ? "bold" : "normal");
                        controlTemplate = controlTemplate.Replace("#fontDecoration;", label.Underline ? "underline" : "normal");

                        Guid newDocContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = newDocContentId;
                        documentContents.DocumentID = new Guid(label.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = existingDocContents.ControlHtmlID; // "companyControl" + docContentCount;
                        documentContents.Label = label.Text;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Label;
                        documentContents.Height = 22;
                        documentContents.Width = label.Width;
                        documentContents.XCoordinate = label.XCordinate;
                        documentContents.YCoordinate = label.YCordinate;
                        documentContents.ZCoordinate = label.ZCordinate;
                        // documentContents.RecipientID = new Guid(label.RecipientId);
                        //documentContents.Required = label.Required;
                        // documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = newDocContentId;
                        controlStyle.FontID = Guid.Parse(label.fontFamilyID);
                        controlStyle.FontSize = label.fontSize;
                        controlStyle.FontColor = label.Color;
                        controlStyle.IsBold = label.Bold;
                        controlStyle.IsItalic = label.Italic;
                        controlStyle.IsUnderline = label.Underline;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(label.EnvelopeId);
                        response.DocumentContentId = newDocContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["EditlabelSuccess"].ToString();
                        responseMessage.EnvelopeId = label.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(newDocContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
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

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage RadioControl(Radio radio)
        {

            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            int pageNumber = 0;
            int documentPageNumber = 0;
            bool docIdPresent = false;
            bool docContentIdPresent = false;
            int docContentCount = 0;
            bool recPresent = false;
            string recName = "";
            try
            {
                string envelopeID = radio.EnvelopeId;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), radio.EnvelopeId);
                if (string.IsNullOrEmpty(envelopeID) || string.IsNullOrEmpty(radio.RecipientId) || string.IsNullOrEmpty(radio.DocumentId) || string.IsNullOrEmpty(radio.GroupName) || string.IsNullOrEmpty(radio.PageName) || radio.XCordinate == 0 || radio.YCordinate == 0 || radio.ZCordinate == 0)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = radio.EnvelopeId;
                    responseMessage.DocumentId = radio.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = radio.EnvelopeId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }

                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    RecipientRepository recipientRepository = new RecipientRepository(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    GenericRepository genericRepository = new GenericRepository();

                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(radio.EnvelopeId));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = radio.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }
                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = radio.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = radio.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(radio.EnvelopeId));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString();
                        responseMessage.EnvelopeId = radio.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (radio.DocumentContentId == "" || radio.DocumentContentId == null)
                    {

                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(radio.DocumentId));

                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = radio.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(radio.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(radio.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(radio.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(radio.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = radio.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(radio.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(radio.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = radio.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == radio.PageName)
                            {
                                if (docImage.Document.Id == new Guid(radio.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = radio.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            docContentCount = docContentCount + doc.DocumentContents.Count;
                        }

                        FontList loFontList = new FontList();
                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Radio);
                        controlTemplate = controlTemplate.Replace("#radioControl", "radioControl" + docContentCount);
                        controlTemplate = controlTemplate.Replace("#left", radio.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", radio.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "23px");
                        controlTemplate = controlTemplate.Replace("#width", "25px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Radio));
                        controlTemplate = controlTemplate.Replace("#recipientId", radio.RecipientId);
                        controlTemplate = controlTemplate.Replace("#groupName", radio.GroupName);

                        Guid docContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = docContentId;
                        documentContents.DocumentID = new Guid(radio.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = "radioControl" + docContentCount;
                        documentContents.Label = "radio control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Radio;
                        documentContents.Height = 23;
                        documentContents.Width = 25;
                        documentContents.GroupName = radio.GroupName;
                        documentContents.XCoordinate = radio.XCordinate;
                        documentContents.YCoordinate = radio.YCordinate;
                        documentContents.ZCoordinate = radio.ZCordinate;
                        documentContents.RecipientID = new Guid(radio.RecipientId);
                        documentContents.Required = false;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(radio.EnvelopeId);
                        response.DocumentContentId = docContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["AddRadioSuccess"].ToString();
                        responseMessage.EnvelopeId = radio.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(docContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {

                        if (envelope.IsEnvelopeComplete)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.EnvelopeId = radio.EnvelopeId;
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        bool isRadioControl = false;
                        //check if document is provide for provided document id in the envelope
                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(radio.DocumentId));
                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = radio.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            //Check whether document content id is present in the envelope
                            docContentIdPresent = doc.DocumentContents.Any(dc => dc.ID == new Guid(radio.DocumentContentId));
                            if (docContentIdPresent)
                            {
                                //Check if the provided document content id to edit is of signature control or not
                                isRadioControl = doc.DocumentContents.SingleOrDefault(dc => dc.ID == new Guid(radio.DocumentContentId)).ControlID == Constants.Control.Radio ? true : false;
                                break;
                            }
                        }
                        if (!docContentIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentContentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = radio.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (!isRadioControl)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["NotRadioControl"].ToString();
                            responseMessage.EnvelopeId = radio.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(radio.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(radio.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(radio.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(radio.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = radio.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }



                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(radio.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(radio.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = radio.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == radio.PageName)
                            {
                                if (docImage.Document.Id == new Guid(radio.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = radio.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        DocumentContents existingDocContents = documentContentsRepository.GetEntity(new Guid(radio.DocumentContentId));
                        existingDocContents.IsControlDeleted = true;
                        documentContentsRepository.Save(existingDocContents);

                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Radio);
                        controlTemplate = controlTemplate.Replace("#radioControl", existingDocContents.ControlHtmlID);
                        controlTemplate = controlTemplate.Replace("#left", radio.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", radio.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "23px");
                        controlTemplate = controlTemplate.Replace("#width", "25px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Radio));
                        controlTemplate = controlTemplate.Replace("#recipientId", radio.RecipientId);
                        controlTemplate = controlTemplate.Replace("#groupName", radio.GroupName);

                        Guid newDocContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = newDocContentId;
                        documentContents.DocumentID = new Guid(radio.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = existingDocContents.ControlHtmlID; // "radioControl" + docContentCount;
                        documentContents.Label = "radio control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Radio;
                        documentContents.Height = 23;
                        documentContents.Width = 25;
                        documentContents.GroupName = radio.GroupName;
                        documentContents.XCoordinate = radio.XCordinate;
                        documentContents.YCoordinate = radio.YCordinate;
                        documentContents.ZCoordinate = radio.ZCordinate;
                        documentContents.RecipientID = new Guid(radio.RecipientId);
                        documentContents.Required = false;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(radio.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = newDocContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["EditRadioSuccess"].ToString();
                        responseMessage.EnvelopeId = radio.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(newDocContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
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


        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage CheckboxControl(Checkbox checkbox)
        {

            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            int pageNumber = 0;
            int documentPageNumber = 0;
            bool docIdPresent = false;
            bool docContentIdPresent = false;
            int docContentCount = 0;
            bool recPresent = false;
            string recName = "";
            try
            {
                string envelopeID = checkbox.EnvelopeId;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), checkbox.EnvelopeId);
                if (string.IsNullOrEmpty(envelopeID) || string.IsNullOrEmpty(checkbox.RecipientId) || string.IsNullOrEmpty(checkbox.DocumentId) || string.IsNullOrEmpty(checkbox.PageName) || checkbox.XCordinate == 0 || checkbox.YCordinate == 0 || checkbox.ZCordinate == 0)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = checkbox.EnvelopeId;
                    responseMessage.DocumentId = checkbox.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = checkbox.EnvelopeId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }

                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    RecipientRepository recipientRepository = new RecipientRepository(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    GenericRepository genericRepository = new GenericRepository();

                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(checkbox.EnvelopeId));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = checkbox.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }
                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = checkbox.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = checkbox.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(checkbox.EnvelopeId));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString();
                        responseMessage.EnvelopeId = checkbox.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (checkbox.DocumentContentId == "" || checkbox.DocumentContentId == null)
                    {

                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(checkbox.DocumentId));

                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = checkbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(checkbox.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(checkbox.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(checkbox.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(checkbox.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = checkbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(checkbox.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(checkbox.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = checkbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == checkbox.PageName)
                            {
                                if (docImage.Document.Id == new Guid(checkbox.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = checkbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            docContentCount = docContentCount + doc.DocumentContents.Count;
                        }

                        FontList loFontList = new FontList();
                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Checkbox);
                        controlTemplate = controlTemplate.Replace("#checkBoxControl", "checkBoxControl" + docContentCount);
                        controlTemplate = controlTemplate.Replace("#left", checkbox.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", checkbox.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "23px");
                        controlTemplate = controlTemplate.Replace("#width", "25px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Checkbox));
                        controlTemplate = controlTemplate.Replace("#recipientId", checkbox.RecipientId);
                        controlTemplate = controlTemplate.Replace("#Required", checkbox.Required.ToString());

                        Guid docContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = docContentId;
                        documentContents.DocumentID = new Guid(checkbox.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = "checkBoxControl" + docContentCount;
                        documentContents.Label = "checkBox control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Checkbox;
                        documentContents.Height = 23;
                        documentContents.Width = 25;
                        documentContents.XCoordinate = checkbox.XCordinate;
                        documentContents.YCoordinate = checkbox.YCordinate;
                        documentContents.ZCoordinate = checkbox.ZCordinate;
                        documentContents.RecipientID = new Guid(checkbox.RecipientId);
                        documentContents.Required = checkbox.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(checkbox.EnvelopeId);
                        response.DocumentContentId = docContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["AddCheckboxSuccess"].ToString();
                        responseMessage.EnvelopeId = checkbox.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(docContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {

                        if (envelope.IsEnvelopeComplete)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.EnvelopeId = checkbox.EnvelopeId;
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        bool isCheckboxControl = false;
                        //check if document is provide for provided document id in the envelope
                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(checkbox.DocumentId));
                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = checkbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            //Check whether document content id is present in the envelope
                            docContentIdPresent = doc.DocumentContents.Any(dc => dc.ID == new Guid(checkbox.DocumentContentId));
                            if (docContentIdPresent)
                            {
                                //Check if the provided document content id to edit is of signature control or not
                                isCheckboxControl = doc.DocumentContents.SingleOrDefault(dc => dc.ID == new Guid(checkbox.DocumentContentId)).ControlID == Constants.Control.Checkbox ? true : false;
                                break;
                            }
                        }
                        if (!docContentIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentContentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = checkbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (!isCheckboxControl)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["NotCheckboxControl"].ToString();
                            responseMessage.EnvelopeId = checkbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(checkbox.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(checkbox.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(checkbox.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(checkbox.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = checkbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }



                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(checkbox.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(checkbox.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = checkbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == checkbox.PageName)
                            {
                                if (docImage.Document.Id == new Guid(checkbox.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = checkbox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        DocumentContents existingDocContents = documentContentsRepository.GetEntity(new Guid(checkbox.DocumentContentId));
                        existingDocContents.IsControlDeleted = true;
                        documentContentsRepository.Save(existingDocContents);

                        //string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Radio);
                        //controlTemplate = controlTemplate.Replace("#radioControl", existingDocContents.ControlHtmlID);
                        //controlTemplate = controlTemplate.Replace("#left", radio.XCordinate + "px");
                        //controlTemplate = controlTemplate.Replace("#top", radio.YCordinate + "px");
                        //controlTemplate = controlTemplate.Replace("#height", "23px");
                        //controlTemplate = controlTemplate.Replace("#width", "25px");
                        //controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Radio));
                        //controlTemplate = controlTemplate.Replace("#recipientId", radio.RecipientId);
                        //controlTemplate = controlTemplate.Replace("#groupName", radio.GroupName);
                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.Checkbox);
                        controlTemplate = controlTemplate.Replace("#checkBoxControl", existingDocContents.ControlHtmlID);
                        controlTemplate = controlTemplate.Replace("#left", checkbox.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", checkbox.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "23px");
                        controlTemplate = controlTemplate.Replace("#width", "25px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.Checkbox));
                        controlTemplate = controlTemplate.Replace("#recipientId", checkbox.RecipientId);
                        controlTemplate = controlTemplate.Replace("#Required", checkbox.Required.ToString());

                        Guid newDocContentId = Guid.NewGuid();

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = newDocContentId;
                        documentContents.DocumentID = new Guid(checkbox.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = existingDocContents.ControlHtmlID; // "checkBoxControl" + docContentCount;
                        documentContents.Label = "checkBox control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.Checkbox;
                        documentContents.Height = 23;
                        documentContents.Width = 25;
                        documentContents.XCoordinate = checkbox.XCordinate;
                        documentContents.YCoordinate = checkbox.YCordinate;
                        documentContents.ZCoordinate = checkbox.ZCordinate;
                        documentContents.RecipientID = new Guid(checkbox.RecipientId);
                        documentContents.Required = checkbox.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        //ControlStyle controlStyle = new ControlStyle();
                        //controlStyle.DocumentContentId = newDocContentId;
                        //controlStyle.FontID = Guid.Parse("1875C58D-52BD-498A-BE6D-433A8858357E");
                        //controlStyle.FontSize = 14;
                        //controlStyle.FontColor = "#000000";
                        //controlStyle.IsBold = false;
                        //controlStyle.IsItalic = false;
                        //controlStyle.IsUnderline = false;
                        //documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(checkbox.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = newDocContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["EditCheckboxSuccess"].ToString();
                        responseMessage.EnvelopeId = checkbox.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(newDocContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
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

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage DropdownControl(DropDownBox dropdownBox)
        {

            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            int pageNumber = 0;
            int documentPageNumber = 0;
            bool docIdPresent = false;
            bool docContentIdPresent = false;
            int docContentCount = 0;
            bool recPresent = false;
            string recName = "";
            try
            {
                string envelopeID = dropdownBox.EnvelopeId;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), dropdownBox.EnvelopeId);
                if (string.IsNullOrEmpty(envelopeID) || string.IsNullOrEmpty(dropdownBox.RecipientId) || string.IsNullOrEmpty(dropdownBox.DocumentId) || string.IsNullOrEmpty(dropdownBox.PageName) || dropdownBox.XCordinate == 0 || dropdownBox.YCordinate == 0 || dropdownBox.ZCordinate == 0)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                    responseMessage.DocumentId = dropdownBox.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (dropdownBox.SelectOption == null || dropdownBox.SelectOption.Count == 0)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["DropdownOptionNotAdded"].ToString();
                    responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                    responseMessage.DocumentId = dropdownBox.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                foreach (var option in dropdownBox.SelectOption)
                {
                    if(string.IsNullOrEmpty(option.Option) || (string.IsNullOrWhiteSpace(option.Option)))
                    {
                        responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                        responseMessage.StatusMessage = "NotAcceptable";
                        responseMessage.Message = ConfigurationManager.AppSettings["DropdownOptionNotAdded"].ToString();
                        responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                        responseMessage.DocumentId = dropdownBox.DocumentId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                        return responseToClient;
                    }
                }

                if (dropdownBox.Width < 40)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["NotValidDropdownWidth"].ToString();
                    responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                    responseMessage.DocumentId = dropdownBox.DocumentId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }

                using (var dbContext = new eSignEntities())
                {
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    RecipientRepository recipientRepository = new RecipientRepository(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    GenericRepository genericRepository = new GenericRepository();

                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(dropdownBox.EnvelopeId));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }
                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(dropdownBox.EnvelopeId));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString();
                        responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (dropdownBox.DocumentContentId == "" || dropdownBox.DocumentContentId == null)
                    {

                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(dropdownBox.DocumentId));

                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(dropdownBox.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(dropdownBox.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(dropdownBox.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(dropdownBox.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }


                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(dropdownBox.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(dropdownBox.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == dropdownBox.PageName)
                            {
                                if (docImage.Document.Id == new Guid(dropdownBox.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            docContentCount = docContentCount + doc.DocumentContents.Count;
                        }


                        FontList loFontList = new FontList();
                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.DropDown);
                        controlTemplate = controlTemplate.Replace("#dropdownControl", "dropdownControl" + docContentCount);
                        controlTemplate = controlTemplate.Replace("#left", dropdownBox.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", dropdownBox.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "23px");
                        controlTemplate = controlTemplate.Replace("#width", dropdownBox.Width + "px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.DropDown));
                        controlTemplate = controlTemplate.Replace("#recipientId", dropdownBox.RecipientId);

                        Guid docContentId = Guid.NewGuid();

                        string selectOption = string.Empty;
                        int index = 0;
                        foreach (var item in dropdownBox.SelectOption)
                        {
                            SelectControlOptions selectControlOption = new SelectControlOptions();
                            selectControlOption.ID = Guid.NewGuid();
                            selectControlOption.DocumentContentID = docContentId;
                            selectControlOption.OptionText = item.Option;
                            selectControlOption.Order = Convert.ToByte(index);
                            documentContentsRepository.Save(selectControlOption);

                            selectOption = selectOption + "<option value='" + index + "'>" + item.Option + "</option>";
                            index++;
                        }

                        controlTemplate = controlTemplate.Replace("#selectOption", selectOption);

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = docContentId;
                        documentContents.DocumentID = new Guid(dropdownBox.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = "dropdownControl" + docContentCount;
                        documentContents.Label = "dropdownBox control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.DropDown;
                        documentContents.Height = 23;
                        documentContents.Width = dropdownBox.Width;
                        documentContents.XCoordinate = dropdownBox.XCordinate;
                        documentContents.YCoordinate = dropdownBox.YCordinate;
                        documentContents.ZCoordinate = dropdownBox.ZCordinate;
                        documentContents.RecipientID = new Guid(dropdownBox.RecipientId);
                        documentContents.Required = dropdownBox.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(dropdownBox.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = docContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["AddDropdownSuccess"].ToString();
                        responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(docContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {

                        if (envelope.IsEnvelopeComplete)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeComplted"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        bool isDropdownControl = false;
                        //check if document is provide for provided document id in the envelope
                        docIdPresent = envelope.Documents.Any(d => d.ID == new Guid(dropdownBox.DocumentId));
                        if (!docIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        foreach (var doc in envelope.Documents)
                        {
                            //Check whether document content id is present in the envelope
                            docContentIdPresent = doc.DocumentContents.Any(dc => dc.ID == new Guid(dropdownBox.DocumentContentId));
                            if (docContentIdPresent)
                            {
                                //Check if the provided document content id to edit is of signature control or not
                                isDropdownControl = doc.DocumentContents.SingleOrDefault(dc => dc.ID == new Guid(dropdownBox.DocumentContentId)).ControlID == Constants.Control.DropDown ? true : false;
                                break;
                            }
                        }
                        if (!docContentIdPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentContentIdNotFound"].ToString();
                            responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (!isDropdownControl)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["NotDropdownControl"].ToString();
                            responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        if (envelope.IsEnvelope)
                        {
                            recPresent = envelope.Recipients.Any(r => r.ID == new Guid(dropdownBox.RecipientId));
                            if (recPresent)
                                recName = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(dropdownBox.RecipientId)).Name;
                        }
                        else
                        {
                            recPresent = envelope.Roles.Any(r => r.ID == new Guid(dropdownBox.RecipientId));
                            if (recPresent)
                                recName = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(dropdownBox.RecipientId)).Name;
                        }

                        if (!recPresent)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientIdNotFound"].ToString();
                            responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }



                        bool isCCRecipient = false;
                        if (envelope.IsEnvelope)
                            isCCRecipient = envelope.Recipients.SingleOrDefault(r => r.ID == new Guid(dropdownBox.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;
                        else
                            isCCRecipient = envelope.Roles.SingleOrDefault(r => r.ID == new Guid(dropdownBox.RecipientId)).RecipientTypeID == Constants.RecipientType.CC;


                        if (isCCRecipient)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["CCNOtValid"].ToString();
                            responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        List<EnvelopeImageInformationDetails> documentImageList = envelopeHelperMain.GetDocumentInfo(envelope);
                        foreach (var docImage in documentImageList)
                        {
                            if (docImage.ImagePath.Split('/').Last() == dropdownBox.PageName)
                            {
                                if (docImage.Document.Id == new Guid(dropdownBox.DocumentId))
                                {
                                    pageNumber = docImage.Id;
                                    documentPageNumber = docImage.DocPageNo;
                                    break;
                                }
                            }
                        }
                        if (pageNumber == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.BadRequest;
                            responseMessage.StatusMessage = "BadRequest";
                            responseMessage.Message = ConfigurationManager.AppSettings["DocumentPageNotFound"].ToString();
                            responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                            responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                            return responseToClient;
                        }

                        DocumentContents existingDocContents = documentContentsRepository.GetEntity(new Guid(dropdownBox.DocumentContentId));
                        existingDocContents.IsControlDeleted = true;
                        documentContentsRepository.Save(existingDocContents);


                        string controlTemplate = masterDataRepository.GetControlTemplate(Constants.Control.DropDown);
                        controlTemplate = controlTemplate.Replace("#dropdownControl", existingDocContents.ControlHtmlID);
                        controlTemplate = controlTemplate.Replace("#left", dropdownBox.XCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#top", dropdownBox.YCordinate + "px");
                        controlTemplate = controlTemplate.Replace("#height", "23px");
                        controlTemplate = controlTemplate.Replace("#width", dropdownBox.Width + "px");
                        controlTemplate = controlTemplate.Replace("#controlGuid", Convert.ToString(Constants.Control.DropDown));
                        controlTemplate = controlTemplate.Replace("#recipientId", dropdownBox.RecipientId);
                        Guid newDocContentId = Guid.NewGuid();
                        string selectOption = string.Empty;
                        int index = 0;


                        foreach (var item in dropdownBox.SelectOption)
                        {
                            SelectControlOptions selectControlOption = new SelectControlOptions();
                            selectControlOption.ID = Guid.NewGuid();
                            selectControlOption.DocumentContentID = newDocContentId;
                            selectControlOption.OptionText = item.Option;
                            selectControlOption.Order = Convert.ToByte(index);
                            documentContentsRepository.Save(selectControlOption);

                            selectOption = selectOption + "<option value='" + index + "'>" + item.Option + "</option>";
                            index++;
                        }

                        controlTemplate = controlTemplate.Replace("#selectOption", selectOption);

                        DocumentContents documentContents = new DocumentContents();
                        documentContents.ID = newDocContentId;
                        documentContents.DocumentID = new Guid(dropdownBox.DocumentId);
                        documentContents.DocumentPageNo = documentPageNumber;
                        documentContents.ControlHtmlID = existingDocContents.ControlHtmlID; // "dropdownBoxControl" + docContentCount;
                        documentContents.Label = "Dropdown control assigned to " + recName;
                        documentContents.PageNo = pageNumber;
                        documentContents.ControlID = Constants.Control.DropDown;
                        documentContents.Height = 23;
                        documentContents.Width = dropdownBox.Width;
                        documentContents.XCoordinate = dropdownBox.XCordinate;
                        documentContents.YCoordinate = dropdownBox.YCordinate;
                        documentContents.ZCoordinate = dropdownBox.ZCordinate;
                        documentContents.RecipientID = new Guid(dropdownBox.RecipientId);
                        documentContents.Required = dropdownBox.Required;
                        documentContents.RecName = recName;
                        documentContents.ControHtmlData = controlTemplate;
                        documentContents.ControlHtmlData = controlTemplate;
                        documentContents.IsControlDeleted = false;
                        documentContentsRepository.Save(documentContents);

                        ControlStyle controlStyle = new ControlStyle();
                        controlStyle.DocumentContentId = newDocContentId;
                        controlStyle.FontID = Guid.Parse("1875C58D-52BD-498A-BE6D-433A8858357E");
                        controlStyle.FontSize = 14;
                        controlStyle.FontColor = "#000000";
                        controlStyle.IsBold = false;
                        controlStyle.IsItalic = false;
                        controlStyle.IsUnderline = false;
                        documentContentsRepository.Save(controlStyle);
                        unitOfWork.SaveChanges();

                        ControlResponse response = new ControlResponse();
                        response.EnvelopeID = new Guid(dropdownBox.EnvelopeId);
                        //response.RecipientId = new Guid(signature.RecipientId);
                        //response.DocumentId = new Guid(signature.DocumentId);
                        response.DocumentContentId = newDocContentId;

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["DropdownEditSuccess"].ToString();
                        responseMessage.EnvelopeId = dropdownBox.EnvelopeId;
                        responseMessage.DocumentContentId = Convert.ToString(newDocContentId);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
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

       

        private static String RGBConverter(string fontColor)
        {
            String rtn = String.Empty;
            try
            {
                System.Drawing.Color c = System.Drawing.ColorTranslator.FromHtml(fontColor);
                rtn = "RGB(" + c.R.ToString() + "," + c.G.ToString() + "," + c.B.ToString() + ")";
            }
            catch (Exception ex)
            {
                //doing nothing
            }

            return rtn;
        }

        //[HttpPost]
        //[AuthorizeAccess]
        //public HttpResponseMessage TextControl(Text text) {
        //    string envelopeID = text.EnvelopeId;
        //    HttpResponseMessage responseToClient = new HttpResponseMessage();
        //    ResponseMessageDocument responseMessage = new ResponseMessageDocument();
        //    int pageNumber = 0;
        //    int documentPageNumber = 0;
        //    try {
        //        System.Collections.Generic.IEnumerable<string> iHeader;
        //        Request.Headers.TryGetValues("AuthToken", out iHeader);
        //        string authToken = iHeader.ElementAt(0);
        //        string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), text.EnvelopeId);
        //        if (string.IsNullOrEmpty(envelopeID))
        //        {
        //            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
        //            responseMessage.StatusMessage = "NotAcceptable";
        //            responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
        //            responseMessage.EnvelopeId = text.EnvelopeId;
        //            responseMessage.DocumentId = text.DocumentId;
        //            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
        //            return responseToClient;
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        responseToClient = Request.CreateResponse((HttpStatusCode)422);
        //        responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
        //        throw new HttpResponseException(responseToClient);
        //    }



        //    return null;
        //}
    }
}