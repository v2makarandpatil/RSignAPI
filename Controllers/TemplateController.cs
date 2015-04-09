using eSign.Models.Data;
using eSign.Models.Domain;
using eSign.Models.Helpers;
using eSign.WebAPI.Models;
using eSign.WebAPI.Models.Domain;
using eSign.WebAPI.Models.Helpers;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Objects.DataClasses;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.IO;
using System.Threading;

namespace eSign.WebAPI.Controllers
{
    public class TemplateController : ApiController
    {
        IList<Envelope> Templates { get; set; }

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage InitializeTemplate(APITemplate template)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            const int passwordKeySize = 128;
            object _locker = new object();
            bool lockTaken = false;
            try
            {
                Monitor.Enter (_locker, ref lockTaken);
                lock (_locker)
                {
                    string completeEncodedKey = ModelHelper.GenerateKey(passwordKeySize);
                    System.Collections.Generic.IEnumerable<string> iHeader;
                    Request.Headers.TryGetValues("AuthToken", out iHeader);
                    string authToken = iHeader.ElementAt(0);
                    Guid newTemplateId = Guid.NewGuid();
                    string tempDirectory = ConfigurationManager.AppSettings["TempDirectory"].ToString();
                    string finalDirectoryPath = tempDirectory + Convert.ToString(newTemplateId);
                    using (var dbContext = new eSignEntities())
                    {
                        var helper = new eSignHelper();
                        UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                        EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                        MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                        UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                        if (template.DateFormatID != null && template.ExpiryTypeID != null)
                        {
                            if (!masterDataRepository.ValidateDateFormatId(new Guid(template.DateFormatID)))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.Forbidden;
                                responseMessage.StatusMessage = "Forbidden";
                                responseMessage.Message = ConfigurationManager.AppSettings["DateFormatIdInvalid"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                                return responseToClient;
                            }
                            if (!masterDataRepository.ValidateExpiryTypeId(new Guid(template.ExpiryTypeID)))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.Forbidden;
                                responseMessage.StatusMessage = "Forbidden";
                                responseMessage.Message = ConfigurationManager.AppSettings["ExpiryTypeIdInvalid"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                                return responseToClient;
                            }
                            if (template.PasswordRequiredToSign == true && string.IsNullOrEmpty(template.PasswordToSign))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                                responseMessage.StatusMessage = "NotAcceptable";
                                responseMessage.Message = ConfigurationManager.AppSettings["PasswordToSignAndOrOpenSignMissing"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                                return responseToClient;
                            }
                            if (template.PasswordRequiredToOpen == true && string.IsNullOrEmpty(template.PasswordToOpen))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                                responseMessage.StatusMessage = "NotAcceptable";
                                responseMessage.Message = ConfigurationManager.AppSettings["PasswordToSignAndOrOpenSignMissing"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                                return responseToClient;
                            }
                            if (template.PasswordRequiredToSign == false && !string.IsNullOrEmpty(template.PasswordToSign))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                                responseMessage.StatusMessage = "NotAcceptable";
                                responseMessage.Message = ConfigurationManager.AppSettings["PasswordReqdToSignMissing"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                                return responseToClient;
                            }
                            if (template.PasswordRequiredToOpen == false && !string.IsNullOrEmpty(template.PasswordToOpen))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                                responseMessage.StatusMessage = "NotAcceptable";
                                responseMessage.Message = ConfigurationManager.AppSettings["PasswordReqdToOpenMissing"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                                return responseToClient;
                            }
                            if (string.IsNullOrEmpty(template.TemplateName) || string.IsNullOrEmpty(template.TemplateDescription))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                                responseMessage.StatusMessage = "NotAcceptable";
                                responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                                return responseToClient;
                            }


                            if (string.IsNullOrWhiteSpace(template.TemplateName) || string.IsNullOrWhiteSpace(template.TemplateDescription))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                                responseMessage.StatusMessage = "NotAcceptable";
                                responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                                return responseToClient;
                            }

                            Directory.CreateDirectory(finalDirectoryPath);
                            EnvelopeHelperMain objEnvelope = new EnvelopeHelperMain();
                            string userEmailAddress = tokenRepository.GetUserEmailByToken(authToken);
                            Guid userID = tokenRepository.GetUserProfileUserIDByEmail(userEmailAddress);
                            helper.SetApiCallFlag();
                            int tempCode = GetMaxTemplateCode(dbContext) + 1;
                            eSign.Models.Domain.Envelope envlp = new Envelope();
                            envlp.ID = newTemplateId;
                            envlp.IsEnvelope = false;
                            envlp.EnvelopeCode = 0;
                            //envlp.TemplateCode = objEnvelope.GetMaxTemplateCode() + 1;
                            envlp.TemplateCode = tempCode;
                            envlp.UserID = userID;
                            envlp.DateFormatID = new Guid(template.DateFormatID);
                            envlp.ExpiryTypeID = new Guid(template.ExpiryTypeID);
                            envlp.PasswordReqdtoOpen = template.PasswordRequiredToOpen;
                            envlp.PasswordReqdtoSign = template.PasswordRequiredToSign;
                            envlp.PasswordtoOpen = template.PasswordToOpen;
                            envlp.PasswordtoSign = template.PasswordToSign;
                            envlp.StatusID = Constants.StatusCode.Envelope.Waiting_For_Signature;
                            envlp.SigningCertificateName = ConfigurationManager.AppSettings["SigningCertificateName"].ToString();
                            envlp.CreatedDateTime = DateTime.Now;
                            envlp.ModifiedDateTime = DateTime.Now;
                            envlp.IsTemplateDeleted = false;
                            envlp.IsTemplateEditable = template.IsTemplateEditable;
                            envlp.DisplayCode = 0;
                            envlp.TemplateName = template.TemplateName;
                            envlp.TemplateDescription = template.TemplateDescription;
                            envlp.IsEnvelopePrepare = false;
                            envlp.IsEnvelopeComplete = false;
                            eSign.Models.Domain.Recipients recipient = new Recipients();
                            recipient.ID = Guid.NewGuid();
                            recipient.EnvelopeID = newTemplateId;
                            recipient.RecipientTypeID = Constants.RecipientType.Sender;
                            recipient.Name = tokenRepository.GetUserProfileNameByEmail(userEmailAddress);
                            recipient.EmailAddress = userEmailAddress;
                            recipient.Order = null;
                            recipient.CreatedDateTime = DateTime.Now;
                            envlp.Recipients.Add(recipient);
                            envelopeRepository.SetInitializeEnvelopeFlag();
                            envelopeRepository.Save(envlp);
                            unitOfWork.SaveChanges();
                            bool isXmlCreate = helper.CreateEnvelopeXML(newTemplateId);
                            ResponseMessageWithTemplateGuid responseMessageWithEnvlpId = new ResponseMessageWithTemplateGuid();
                            responseMessageWithEnvlpId.StatusCode = HttpStatusCode.OK;
                            responseMessageWithEnvlpId.StatusMessage = "OK";
                            responseMessageWithEnvlpId.Message = ConfigurationManager.AppSettings["SuccessInitializeTemplate"].ToString();
                            responseMessageWithEnvlpId.TemplateId = Convert.ToString(newTemplateId);
                            responseMessageWithEnvlpId.TemplateCode = tempCode;
                            responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessageWithEnvlpId);
                            return responseToClient;
                        }
                        else
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
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
            finally { if (lockTaken) Monitor.Exit(_locker); } 
        }

        [AuthorizeAccess]
        [HttpGet]
        public HttpResponseMessage GetTemplateList(HttpRequestMessage Request)
        {
            var TemplateList = new List<Template>();
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
                    Templates = envelopeRepository.GetAll(UserId).OrderByDescending(r => r.TemplateCode).Where(r => r.TemplateCode != null && r.TemplateCode != 0 && !r.IsTemplateDeleted && r.IsEnvelopeComplete == true).ToList();
                }
                Template mngTemplate = new Template();
                foreach (var template in Templates)
                {
                    mngTemplate = new Template();
                    mngTemplate.TemplateCode = (int)template.TemplateCode;
                    mngTemplate.TemplateName = template.TemplateName;
                    mngTemplate.TemplateDescription = template.TemplateDescription;
                    mngTemplate.CreatedDateTime = template.CreatedDateTime;
                    mngTemplate.IsTemplateEditable = template.IsTemplateEditable;
                    mngTemplate.IsTemplateDeleted = template.IsTemplateDeleted;

                    TemplateList.Add(mngTemplate);
                }
                if (TemplateList.Count == 0)
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
                    responseToClient = Request.CreateResponse(HttpStatusCode.OK, new { TemplateList });
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
        public HttpResponseMessage GetTemplateByCode(int envelopeCode)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            Envelope envelope = new Envelope();
            TemplateDetails templateDetails = new TemplateDetails();
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

                    envelope = envelopeRepository.GetTemplateDetails(envelopeCode, UserId);


                    if (envelope == null)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.DateFormatID == Constants.DateFormat.US)
                        templateDetails.DateFormat = "US";
                    else
                        templateDetails.DateFormat = "EU";

                    string Password = string.Empty;

                    if (envelope.PasswordReqdtoSign)
                    {
                        Password = ModelHelper.Decrypt(envelope.PasswordtoSign, envelope.PasswordKey, (int)envelope.PasswordKeySize);
                        templateDetails.PasswordToSign = Password;
                    }
                    else
                        templateDetails.PasswordToSign = null;


                    if (envelope.PasswordReqdtoOpen)
                    {
                        Password = ModelHelper.Decrypt(envelope.PasswordtoOpen, envelope.PasswordKey, (int)envelope.PasswordKeySize);
                        templateDetails.PasswordToOpen = Password;
                    }
                    else
                        templateDetails.PasswordToOpen = null;

                    templateDetails.ExpiryType = EnvelopeHelper.GetExpiryType(envelope.ExpiryTypeID);
                    templateDetails.CreatedDateTime = envelope.CreatedDateTime;
                    templateDetails.DateFormatID = envelope.DateFormatID;
                    templateDetails.ExpiryDateTime = envelope.ExpiryDate;
                    templateDetails.ExpiryTypeID = envelope.ExpiryTypeID;
                    templateDetails.ID = envelope.ID;
                    templateDetails.IsTemplateDeleted = envelope.IsTemplateDeleted;
                    templateDetails.IsTemplateEditable = envelope.IsTemplateEditable;
                    templateDetails.ModifiedDateTime = envelope.ModifiedDateTime;
                    templateDetails.PasswordKey = envelope.PasswordKey;
                    templateDetails.PasswordKeySize = envelope.PasswordKeySize;
                    templateDetails.PasswordReqdToOpen = envelope.PasswordReqdtoOpen;
                    templateDetails.PasswordReqdToSign = envelope.PasswordReqdtoSign;
                    templateDetails.RemainderDays = envelope.ReminderDays;
                    templateDetails.RemainderRepeatDays = envelope.ReminderRepeatDays;
                    templateDetails.StatusID = envelope.StatusID;
                    templateDetails.TemplateCode = (int)envelope.TemplateCode;
                    templateDetails.TemplateDescription = envelope.TemplateDescription;
                    templateDetails.TemplateName = envelope.TemplateName;
                    templateDetails.UserID = envelope.UserID;
                    templateDetails.documentDetails = new List<DocumentDetails>();

                    templateDetails.RoleList = new List<RolesDetails>();

                    foreach (var role in envelope.Roles)
                    {
                        RolesDetails roleN = new RolesDetails();

                        roleN.ID = role.ID;
                        roleN.RoleName = role.Name;
                        roleN.CreatedDateTime = role.CreatedDateTime;
                        roleN.Order = role.Order;
                        roleN.TemplateID = role.TemplateID;
                        roleN.RoleType = EnvelopeHelper.GetRecipentType(role.RecipientTypeID);

                        templateDetails.RoleList.Add(roleN);
                    }

                    foreach (var document in envelope.Documents)
                    {
                        DocumentDetails newDoc = new DocumentDetails();
                        newDoc.DocumentName = document.DocumentName;
                        newDoc.EnveopeID = document.EnvelopeID;
                        newDoc.ID = document.ID;
                        newDoc.UploadedDateTime = document.UploadedDateTime;
                        newDoc.Order = document.Order;


                        newDoc.documentContentDetails = new List<DocumentContentDetails>();


                        foreach (var documentContent in document.DocumentContents)
                        {
                            if (documentContent.IsControlDeleted)
                                continue;

                            DocumentContentDetails newDocContent = new DocumentContentDetails();
                            newDocContent.ID = documentContent.ID;
                            newDocContent.DocumentID = document.ID;
                            newDocContent.Label = documentContent.Label;
                            newDocContent.ControlID = documentContent.ControlID;
                            newDocContent.RecipientID = documentContent.RecipientID; ;
                            newDocContent.ControlHtmlID = documentContent.ControlHtmlID;
                            newDocContent.Required = documentContent.Required;
                            newDocContent.DocumentPageNo = documentContent.DocumentPageNo;
                            newDocContent.PageNo = documentContent.PageNo;
                            newDocContent.XCoordinate = documentContent.XCoordinate;
                            newDocContent.YCoordinate = documentContent.YCoordinate;
                            newDocContent.ZCoordinate = documentContent.ZCoordinate;
                            newDocContent.ControlValue = documentContent.ControlValue;
                            newDocContent.Height = documentContent.Height;
                            newDocContent.Width = documentContent.Width;
                            newDocContent.GroupName = documentContent.GroupName;
                            newDocContent.ControlHtmlData = documentContent.ControHtmlData;
                            newDocContent.RecipientName = documentContent.RecName;

                            if (documentContent.ControlStyle != null)
                            {
                                newDocContent.controlStyleDetails = new List<ControlStyleDetails>();
                                ControlStyleDetails controlStyle = new ControlStyleDetails();
                                controlStyle.FontColor = documentContent.ControlStyle.FontColor;
                                controlStyle.FontName = EnvelopeHelper.GetFontName(documentContent.ControlStyle.FontID);
                                controlStyle.FontSize = documentContent.ControlStyle.FontSize;
                                controlStyle.IsBold = documentContent.ControlStyle.IsBold;
                                controlStyle.IsItalic = documentContent.ControlStyle.IsItalic;
                                controlStyle.IsUnderline = documentContent.ControlStyle.IsUnderline;
                                newDocContent.ControlStyle = controlStyle;
                            }



                            if (documentContent.SelectControlOptions.Count != 0)
                            {
                                List<SelectControlOptionDetails> selectControlOptionDetails = new List<SelectControlOptionDetails>();
                                List<SelectControlOptionDetails> SelectControlOptionsEnt = new List<SelectControlOptionDetails>();

                                foreach (var opt in documentContent.SelectControlOptions)
                                {
                                    SelectControlOptionDetails selectControlOptions = new SelectControlOptionDetails();
                                    selectControlOptions.DocumentContentID = opt.DocumentContentID;
                                    selectControlOptions.ID = opt.ID;
                                    selectControlOptions.OptionText = opt.OptionText;
                                    selectControlOptions.Order = opt.Order;
                                    selectControlOptionDetails.Add(selectControlOptions);
                                    SelectControlOptionsEnt.Add(selectControlOptions);
                                }

                                newDocContent.SelectControlOptions = SelectControlOptionsEnt;
                            }

                            newDoc.documentContentDetails.Add(newDocContent);
                        }

                        templateDetails.documentDetails.Add(newDoc);

                    }


                    responseToClient = Request.CreateResponse(HttpStatusCode.OK, new { templateDetails });
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
        [HttpPost]
        public HttpResponseMessage EditTemplate(APITemplate template, string envelopeCode)
        {
            ResponseMessageWithTemplateGuid responseMessageWithId = new ResponseMessageWithTemplateGuid();
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            const int passwordKeySize = 128;            
            Envelope envelope = new Envelope();

            try
            {
                string completeEncodedKey = ModelHelper.GenerateKey(passwordKeySize);
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                Guid templateId = new Guid(envelopeCode);

                if (string.IsNullOrEmpty(envelopeCode))
                {
                    responseMessageWithId.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessageWithId.StatusMessage = "NotAcceptable";
                    responseMessageWithId.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessageWithId.TemplateId = envelopeCode;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessageWithId);
                    return responseToClient;
                }

                if (string.IsNullOrEmpty(template.TemplateName) || string.IsNullOrEmpty(template.TemplateDescription))
                {
                    ResponseMessage responseMessage = new ResponseMessage();
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }
                string tempDirectory = ConfigurationManager.AppSettings["TempDirectory"].ToString();
                string finalDirectoryPath = tempDirectory + Convert.ToString(templateId);

                using (var dbContext = new eSignEntities())
                {
                    var helper = new eSignHelper();
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);


                    if (template.DateFormatID != null && template.ExpiryTypeID != null)
                    {
                        if (!masterDataRepository.ValidateDateFormatId(new Guid(template.DateFormatID)))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.Forbidden;
                            responseMessage.StatusMessage = "Forbidden";
                            responseMessage.Message = ConfigurationManager.AppSettings["DateFormatIdInvalid"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                            return responseToClient;
                        }
                        if (!masterDataRepository.ValidateExpiryTypeId(new Guid(template.ExpiryTypeID)))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.Forbidden;
                            responseMessage.StatusMessage = "Forbidden";
                            responseMessage.Message = ConfigurationManager.AppSettings["ExpiryTypeIdInvalid"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                            return responseToClient;
                        }
                        if (template.PasswordRequiredToSign == true && string.IsNullOrEmpty(template.PasswordToSign))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["PasswordToSignAndOrOpenSignMissing"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                            return responseToClient;
                        }
                        if (template.PasswordRequiredToOpen == true && string.IsNullOrEmpty(template.PasswordToOpen))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["PasswordToSignAndOrOpenSignMissing"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                            return responseToClient;
                        }
                        if (template.PasswordRequiredToSign == false && !string.IsNullOrEmpty(template.PasswordToSign))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["PasswordReqdToSignMissing"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                            return responseToClient;
                        }
                        if (template.PasswordRequiredToOpen == false && !string.IsNullOrEmpty(template.PasswordToOpen))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["PasswordReqdToOpenMissing"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                            return responseToClient;
                        }
                        if (string.IsNullOrEmpty(template.TemplateName) || string.IsNullOrEmpty(template.TemplateDescription))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                            return responseToClient;
                        }
                    }

                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));

                    envelope = envelopeRepository.GetEnvelopeDetails(new Guid(envelopeCode), UserId);
                    if (envelope == null)
                    {
                        responseMessageWithId.StatusCode = HttpStatusCode.NoContent;
                        responseMessageWithId.StatusMessage = "NoContent";
                        responseMessageWithId.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessageWithId.TemplateId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessageWithId);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelope)
                    {
                        responseMessageWithId.StatusCode = HttpStatusCode.BadRequest;
                        responseMessageWithId.StatusMessage = "BadRequest";
                        responseMessageWithId.TemplateId = envelopeCode;
                        responseMessageWithId.TemplateCode = null;
                        responseMessageWithId.Message = Convert.ToString(ConfigurationManager.AppSettings["EditTemplateWithEnvelopeId"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessageWithId);
                        return responseToClient;
                    }

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessageWithId.StatusCode = HttpStatusCode.BadRequest;
                        responseMessageWithId.StatusMessage = "BadRequest";
                        responseMessageWithId.TemplateId = envelopeCode;
                        responseMessageWithId.TemplateCode = null;
                        responseMessageWithId.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessageWithId);
                        return responseToClient;
                    }
                    if (!envelope.IsEnvelopeComplete)
                    {
                        responseMessageWithId.StatusCode = HttpStatusCode.NotAcceptable;
                        responseMessageWithId.StatusMessage = "NotAcceptable";
                        responseMessageWithId.TemplateId = envelopeCode;
                        responseMessageWithId.TemplateCode = envelope.TemplateCode;
                        responseMessageWithId.Message = Convert.ToString(ConfigurationManager.AppSettings["EditTemplateSave"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessageWithId);
                        return responseToClient;
                    }


                    if (template.DateFormatID != null && template.ExpiryTypeID != null)
                    {
                        if (!masterDataRepository.ValidateDateFormatId(new Guid(template.DateFormatID)))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.Forbidden;
                            responseMessage.StatusMessage = "Forbidden";
                            responseMessage.Message = ConfigurationManager.AppSettings["DateFormatIdInvalid"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                            return responseToClient;
                        }
                        if (!masterDataRepository.ValidateExpiryTypeId(new Guid(template.ExpiryTypeID)))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.Forbidden;
                            responseMessage.StatusMessage = "Forbidden";
                            responseMessage.Message = ConfigurationManager.AppSettings["ExpiryTypeIdInvalid"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                            return responseToClient;
                        }
                        if (template.PasswordRequiredToSign == true && string.IsNullOrEmpty(template.PasswordToSign))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["PasswordToSignAndOrOpenSignMissing"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                            return responseToClient;
                        }
                        if (template.PasswordRequiredToOpen == true && string.IsNullOrEmpty(template.PasswordToOpen))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["PasswordToSignAndOrOpenSignMissing"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                            return responseToClient;
                        }
                        if (template.PasswordRequiredToSign == false && !string.IsNullOrEmpty(template.PasswordToSign))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["PasswordReqdToSignMissing"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                            return responseToClient;
                        }
                        if (template.PasswordRequiredToOpen == false && !string.IsNullOrEmpty(template.PasswordToOpen))
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["PasswordReqdToOpenMissing"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                            return responseToClient;
                        }

                        //Directory.CreateDirectory(finalDirectoryPath);
                        //EnvelopeHelperMain objEnvelope = new EnvelopeHelperMain();
                        
                        helper.SetApiCallFlag();
                        eSign.Models.Domain.Envelope envlp = new Envelope();
                        envelope.ID = templateId;
                        envelope.IsEnvelope = false;
                        envelope.EnvelopeCode = 0;
                        envelope.TemplateCode = envelope.TemplateCode;
                        envelope.UserID = UserId;
                        envelope.DateFormatID = new Guid(template.DateFormatID);
                        envelope.ExpiryTypeID = new Guid(template.ExpiryTypeID);
                        envelope.PasswordReqdtoOpen = template.PasswordRequiredToOpen;
                        envelope.PasswordReqdtoSign = template.PasswordRequiredToSign;
                        envelope.PasswordtoOpen = ModelHelper.Encrypt(template.PasswordToOpen, completeEncodedKey, passwordKeySize); ;
                        envelope.PasswordtoSign = ModelHelper.Encrypt(template.PasswordToSign, completeEncodedKey, passwordKeySize); ;
                        envelope.PasswordKey = completeEncodedKey;
                        envelope.PasswordKeySize = passwordKeySize;
                        envelope.StatusID = Constants.StatusCode.Envelope.Waiting_For_Signature;
                        envelope.SigningCertificateName = ConfigurationManager.AppSettings["SigningCertificateName"].ToString();
                        envelope.CreatedDateTime = envelope.CreatedDateTime;
                        envelope.ModifiedDateTime = DateTime.Now;
                        envelope.IsTemplateDeleted = false;
                        envelope.IsTemplateEditable = template.IsTemplateEditable;
                        envelope.DisplayCode = 0;
                        envelope.IsEnvelopePrepare = false;
                        envelope.IsEnvelopeComplete = true;
                        envelope.TemplateName = template.TemplateName;
                        envelope.TemplateDescription = template.TemplateDescription;
                        

                       


                        envelopeRepository.SetInitializeEnvelopeFlag();
                        //envelopeRepository.Save(envlp);

                        if (envelope.EntityState == System.Data.EntityState.Unchanged)
                            dbContext.ObjectStateManager.ChangeObjectState(envelope, System.Data.EntityState.Modified);

                        unitOfWork.SaveChanges();
                        bool isXmlCreate = helper.CreateEnvelopeXML(templateId);
                        ResponseMessageWithTemplateGuid responseMessageWithEnvlpId = new ResponseMessageWithTemplateGuid();
                        responseMessageWithEnvlpId.StatusCode = HttpStatusCode.OK;
                        responseMessageWithEnvlpId.StatusMessage = "OK";
                        responseMessageWithEnvlpId.Message = ConfigurationManager.AppSettings["SuccessEditTemplate"].ToString();
                        responseMessageWithEnvlpId.TemplateId = Convert.ToString(templateId);
                        responseMessageWithEnvlpId.TemplateCode = Convert.ToInt32(envelope.TemplateCode);
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessageWithEnvlpId);
                        return responseToClient;
                    }
                    else
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                        responseMessage.StatusMessage = "NotAcceptable";
                        responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                        responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
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
        public HttpResponseMessage DeleteTemplate(string envelopeCode)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageWithTemplateGuid responseMessage = new ResponseMessageWithTemplateGuid();
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
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));

                    envelope = envelopeRepository.GetEnvelopeDetails(new Guid(envelopeCode), UserId);
                    if (envelope == null)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.TemplateId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }
                    if (envelope.IsEnvelope)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.TemplateId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["DeleteTemplateWithEnvelope"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                        responseMessage.StatusMessage = "NotAcceptable";
                        responseMessage.TemplateId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                        return responseToClient;
                    }

                    bool deleteResult = envelopeRepository.DeleteTemplate(new Guid(envelopeCode));
                    dbContext.SaveChanges();
                    if (deleteResult)
                    {
                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.TemplateId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["DeleteTemplateSuccess"].ToString());
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


        public int GetMaxTemplateCode(eSignEntities dbcontext)
        {

            var TemplateCodeMax = dbcontext.Envelope.OrderByDescending(x => x.TemplateCode).First().TemplateCode;
            return Convert.ToInt32(TemplateCodeMax);

        }

        [AuthorizeAccess]
        [HttpGet]
        public HttpResponseMessage SaveTemplate(string envelopeCode)
        {
            EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageWithTemplateGuid responseMessage = new ResponseMessageWithTemplateGuid();
            Envelope envelope = new Envelope();

            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);


                if (string.IsNullOrEmpty(envelopeCode))
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.TemplateId = envelopeCode;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }


                using (var dbContext = new eSignEntities())
                {
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));

                    envelope = envelopeRepository.GetEnvelopeDetails(new Guid(envelopeCode), UserId);
                    if (envelope == null)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.TemplateId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelope)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.TemplateId = envelopeCode;
                        responseMessage.TemplateCode = envelope.TemplateCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["SaveTemplateWithEnvelope"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    if (envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.TemplateId = envelopeCode;
                        responseMessage.TemplateCode = null;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(envelopeCode));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.TemplateId = envelopeCode;
                        responseMessage.TemplateCode = envelope.TemplateCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateNotPrepared"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    bool templateSaved = false;
                    //bool templateSaved = envelopeHelperMain.SaveTemplate(envelope, Convert.ToInt32(envelope.TemplateCode), UserId, dbContext);

                    envelope.IsEnvelopeComplete = true;
                    if (envelope.EntityState == System.Data.EntityState.Unchanged)
                        dbContext.ObjectStateManager.ChangeObjectState(envelope, System.Data.EntityState.Modified);
                    dbContext.SaveChanges();
                    templateSaved = true;
                    if (templateSaved)
                    {
                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["SaveTemplateSuccess"].ToString();
                        responseMessage.TemplateId = envelopeCode;
                        responseMessage.TemplateCode = envelope.TemplateCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {
                        responseMessage.StatusCode = HttpStatusCode.ExpectationFailed;
                        responseMessage.StatusMessage = "ExpectationFailed";
                        responseMessage.Message = ConfigurationManager.AppSettings["SaveTemplateFail"].ToString();
                        responseMessage.TemplateId = envelopeCode;
                        responseMessage.TemplateCode = envelope.TemplateCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.ExpectationFailed, responseMessage);
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
        public HttpResponseMessage PrepareTemplate(PrepareTemplate prepareTemplate)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageWithTemplateGuid responseMessage = new ResponseMessageWithTemplateGuid();
            responseMessage.RoleList = new List<RoleList>();
            
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), prepareTemplate.TemplateID);
                string tempDocumentDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), prepareTemplate.TemplateID, ConfigurationManager.AppSettings["UploadedDocuments"].ToString());

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["TemplateIdMissing"].ToString();
                    responseMessage.TemplateId = prepareTemplate.TemplateID;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }
                if (string.IsNullOrEmpty(prepareTemplate.TemplateID) || prepareTemplate.Roles == null)
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.TemplateId = prepareTemplate.TemplateID;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }
               

                
                using (var dbContext = new eSignEntities())
                {
                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(prepareTemplate.TemplateID));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.TemplateId = prepareTemplate.TemplateID;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage, Configuration.Formatters.JsonFormatter);
                        return responseToClient;
                    }

                    foreach (var role in prepareTemplate.Roles)
                    {
                        

                        if (string.IsNullOrEmpty(role.RoleName) || string.IsNullOrEmpty(role.RoleType) || string.IsNullOrEmpty(Convert.ToString(role.Order)))
                        {
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                            responseMessage.TemplateId = prepareTemplate.TemplateID;                            
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                            return responseToClient;
                        }
                        if (!masterDataRepository.ValidateRecipientTypeId(new Guid(role.RoleType)))
                        {
                            responseMessage.StatusCode = HttpStatusCode.Forbidden;
                            responseMessage.StatusMessage = "Forbidden";
                            responseMessage.Message = ConfigurationManager.AppSettings["RoleTypeIdInvalid"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                            return responseToClient;
                        }

                        if (!EnvelopeHelper.IsRecipientOrderValid(Convert.ToString(role.Order)) || role.Order == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.Forbidden;
                            responseMessage.StatusMessage = "Forbidden";
                            responseMessage.Message = ConfigurationManager.AppSettings["InvalidOrder"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                            return responseToClient;

                        }
                    }

                    eSign.Models.Domain.Envelope envelope = new Envelope();
                    envelope.Recipients = new System.Data.Objects.DataClasses.EntityCollection<Recipients>();
                    var envelopeToConvert = envelopeRepository.GetEnvelopeDetails(new Guid(prepareTemplate.TemplateID),UserId);

                    if (envelopeToConvert.IsEnvelope)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["PrepareTemplateWithEnvelope"].ToString();
                        responseMessage.TemplateId = prepareTemplate.TemplateID;
                        responseMessage.TemplateCode = envelopeToConvert.TemplateCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelopeToConvert.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.TemplateId = prepareTemplate.TemplateID;
                        responseMessage.TemplateCode = null;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["TemplateAlreadyDeleted"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (Convert.ToBoolean(envelopeToConvert.IsEnvelopePrepare))
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["TemplatePrepared"].ToString();
                        responseMessage.TemplateId = prepareTemplate.TemplateID;
                        responseMessage.TemplateCode = envelopeToConvert.TemplateCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (!Directory.Exists(tempDocumentDirectory))
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["NoDocumentUploaded"].ToString();
                        responseMessage.TemplateId = prepareTemplate.TemplateID;
                        responseMessage.TemplateCode = envelopeToConvert.TemplateCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    envelope.ID = new Guid(prepareTemplate.TemplateID);
                    envelope.ReminderDays = prepareTemplate.SendReminderIn;
                    envelope.ReminderRepeatDays = prepareTemplate.ThenSendReminderIn;
                    envelope.IsFinalCertificateReq = prepareTemplate.SignatureCertificateRequired;
                    envelope.IsFinalDocLinkReq = prepareTemplate.DownloadLinkRequired;
                    envelope.IsEnvelopePrepare = true;
                    envelope.IsEnvelopeComplete = false;
                    

                    // update from DB
                    envelope.TemplateName = envelopeToConvert.TemplateName;
                    envelope.TemplateDescription = envelopeToConvert.TemplateDescription;
                    envelope.DateFormatID = envelopeToConvert.DateFormatID;
                    envelope.ExpiryTypeID = envelopeToConvert.ExpiryTypeID;
                    envelope.ReminderDays = envelopeToConvert.ReminderDays;
                    envelope.ReminderRepeatDays = envelopeToConvert.ReminderRepeatDays;                    
                    envelope.SigningCertificateName = "EsignApplication";
                    envelope.SignerCount = envelopeToConvert.SignerCount;
                    envelope.ModifiedDateTime = DateTime.Now;
                    envelope.DocumentHash = envelopeToConvert.DocumentHash;
                    envelope.IsFinalCertificateReq = envelopeToConvert.IsFinalCertificateReq;
                    envelope.IsFinalDocLinkReq = envelopeToConvert.IsFinalDocLinkReq;
                    envelope.TemplateCode = envelopeToConvert.TemplateCode;
                    envelope.PasswordReqdtoOpen = envelopeToConvert.PasswordReqdtoOpen;
                    envelope.PasswordReqdtoSign = envelopeToConvert.PasswordReqdtoSign;
                    envelope.PasswordtoOpen = envelopeToConvert.PasswordtoOpen;
                    envelope.PasswordtoOpen = envelopeToConvert.PasswordtoOpen;


                    EnvelopeHelperMain objEnvelope = new EnvelopeHelperMain();
                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(prepareTemplate.TemplateID));
                    if (isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["TemplatePrepared"].ToString();
                        responseMessage.TemplateId = prepareTemplate.TemplateID;
                        responseMessage.TemplateCode = envelopeToConvert.TemplateCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    foreach (var rec in prepareTemplate.Roles)
                    {
                        
                        RoleList roleList = new RoleList();
                        Roles role = new Roles();
                        role.ID = Guid.NewGuid();
                        role.TemplateID = new Guid(prepareTemplate.TemplateID);
                        role.RecipientTypeID = new Guid(rec.RoleType);
                        role.Name = rec.RoleName;

                        if (new Guid(rec.RoleType) == Constants.RecipientType.CC)
                            role.Order = null;
                        else
                        {
                            role.Order = rec.Order;

                            //if (EnvelopeHelper.IsRecipientOrderValid(Convert.ToString(rec.Order)))
                            //    role.Order = rec.Order;
                            //else
                            //    role.Order = 1;
                        }

                        role.CreatedDateTime = DateTime.Now;
                        envelope.Roles.Add(role);
                        roleList.RoleName = rec.RoleName;
                        roleList.RecipientId = Convert.ToString(role.ID);
                        responseMessage.RoleList.Add(roleList);
                    }
                   
                    objEnvelope.SetApiCallFlag();
                    envelopeRepository.SetPrepareEnvelopeFlag();
                    envelopeRepository.SaveApiEnvelope(envelope);
                    unitOfWork.SaveChanges();
                   
                    objEnvelope.ConvertDocumentToImage(prepareTemplate, envelopeToConvert);

                    responseMessage.StatusCode = HttpStatusCode.OK;
                    responseMessage.StatusMessage = "OK";
                    responseMessage.Message = ConfigurationManager.AppSettings["TemplatePreparedSuccess"].ToString();
                    responseMessage.TemplateId = prepareTemplate.TemplateID;
                    responseMessage.TemplateCode = envelopeToConvert.TemplateCode;
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



    }
}
