using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using eSign.WebAPI.Models;
using System.Configuration;
using System.IO;
using eSign.WebAPI.Models.Domain;
using System.Text;
using eSign.Models.Domain;
using eSign.Models.Data;
using eSign.Models.Helpers;
using eSign.WebAPI.Models.Helpers;
using System.Threading;

namespace eSign.WebAPI.Controllers
{    
    public class EnvelopeController : ApiController
    {
        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage InitializeEnvelope(APIEnvelope envelope)
        {
            object _locker = new object();
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            const int passwordKeySize = 128;
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
                    Guid newEnvelopeID = Guid.NewGuid();
                    //int newEnvelopeCode =
                    string tempDirectory = ConfigurationManager.AppSettings["TempDirectory"].ToString();
                    string finalDirectoryPath = tempDirectory + Convert.ToString(newEnvelopeID);
                    using (var dbContext = new eSignEntities())
                    {
                        var helper = new eSignHelper();
                        UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                        EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                        MasterDataRepository masterDataRepository = new MasterDataRepository(dbContext);
                        UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                        if (envelope.DateFormatID != null && envelope.ExpiryTypeID != null)
                        {
                            if (!masterDataRepository.ValidateDateFormatId(new Guid(envelope.DateFormatID)))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.Forbidden;
                                responseMessage.StatusMessage = "Forbidden";
                                responseMessage.Message = ConfigurationManager.AppSettings["DateFormatIdInvalid"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                                return responseToClient;
                            }
                            if (!masterDataRepository.ValidateExpiryTypeId(new Guid(envelope.ExpiryTypeID)))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.Forbidden;
                                responseMessage.StatusMessage = "Forbidden";
                                responseMessage.Message = ConfigurationManager.AppSettings["ExpiryTypeIdInvalid"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                                return responseToClient;
                            }
                            if (envelope.PasswordRequiredToSign == true && string.IsNullOrEmpty(envelope.PasswordToSign))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                                responseMessage.StatusMessage = "NotAcceptable";
                                responseMessage.Message = ConfigurationManager.AppSettings["PasswordToSignAndOrOpenSignMissing"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                                return responseToClient;
                            }
                            if (envelope.PasswordRequiredToOpen == true && string.IsNullOrEmpty(envelope.PasswordToOpen))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                                responseMessage.StatusMessage = "NotAcceptable";
                                responseMessage.Message = ConfigurationManager.AppSettings["PasswordToSignAndOrOpenSignMissing"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                                return responseToClient;
                            }
                            if (envelope.PasswordRequiredToSign == false && !string.IsNullOrEmpty(envelope.PasswordToSign))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                                responseMessage.StatusMessage = "NotAcceptable";
                                responseMessage.Message = ConfigurationManager.AppSettings["PasswordReqdToSignMissing"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                                return responseToClient;
                            }
                            if (envelope.PasswordRequiredToOpen == false && !string.IsNullOrEmpty(envelope.PasswordToOpen))
                            {
                                ResponseMessage responseMessage = new ResponseMessage();
                                responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                                responseMessage.StatusMessage = "NotAcceptable";
                                responseMessage.Message = ConfigurationManager.AppSettings["PasswordReqdToOpenMissing"].ToString();
                                responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                                return responseToClient;
                            }

                            



                           
                            Directory.CreateDirectory(finalDirectoryPath);
                            EnvelopeHelperMain objEnvelope = new EnvelopeHelperMain();
                            string userEmailAddress = tokenRepository.GetUserEmailByToken(authToken);
                            Guid userID = tokenRepository.GetUserProfileUserIDByEmail(userEmailAddress);
                            helper.SetApiCallFlag();
                            eSign.Models.Domain.Envelope envlp = new Envelope();
                            envlp.ID = newEnvelopeID;
                            //envlp.EnvelopeCode = objEnvelope.GetMaxEnvelopeCode() + 1;
                            envlp.EnvelopeCode = GetMaxEnvelopeCode(dbContext) + 1;
                            envlp.UserID = userID;
                            envlp.DateFormatID = new Guid(envelope.DateFormatID);
                            envlp.ExpiryTypeID = new Guid(envelope.ExpiryTypeID);
                            envlp.PasswordReqdtoOpen = envelope.PasswordRequiredToOpen;
                            envlp.PasswordReqdtoSign = envelope.PasswordRequiredToSign;
                            envlp.PasswordtoOpen = envelope.PasswordToOpen;
                            envlp.PasswordtoSign = envelope.PasswordToSign;
                            envlp.StatusID = Constants.StatusCode.Envelope.Waiting_For_Signature;
                            envlp.SigningCertificateName = ConfigurationManager.AppSettings["SigningCertificateName"].ToString();
                            envlp.CreatedDateTime = DateTime.Now;
                            envlp.ModifiedDateTime = DateTime.Now;
                            envlp.IsEnvelope = true;
                            envlp.IsTemplateDeleted = false;
                            envlp.IsTemplateEditable = false;
                            envlp.IsEnvelopeComplete = false;
                            envlp.DisplayCode = EnvelopeHelperMain.TakeUniqueDisplayCode();
                            envlp.IsEnvelopePrepare = false;
                            eSign.Models.Domain.Recipients recipient = new Recipients();
                            recipient.ID = Guid.NewGuid();
                            recipient.EnvelopeID = newEnvelopeID;
                            recipient.RecipientTypeID = Constants.RecipientType.Sender;
                            recipient.Name = tokenRepository.GetUserProfileNameByEmail(userEmailAddress);
                            recipient.EmailAddress = userEmailAddress;
                            recipient.Order = null;
                            recipient.CreatedDateTime = DateTime.Now;
                            envlp.Recipients.Add(recipient);
                            envelopeRepository.SetInitializeEnvelopeFlag();
                            envelopeRepository.Save(envlp);
                            unitOfWork.SaveChanges();
                            bool isXmlCreate = helper.CreateEnvelopeXML(newEnvelopeID);

                            lockTaken = true;
                            ResponseMessageWithEnvlpGuid responseMessageWithEnvlpId = new ResponseMessageWithEnvlpGuid();
                            responseMessageWithEnvlpId.StatusCode = HttpStatusCode.OK;
                            responseMessageWithEnvlpId.StatusMessage = "OK";
                            responseMessageWithEnvlpId.Message = ConfigurationManager.AppSettings["SuccessInitializeEnvelope"].ToString();
                            responseMessageWithEnvlpId.EnvelopeId = Convert.ToString(newEnvelopeID);
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
                } // lock end
            }
            catch (Exception e)
            {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(e.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }     
            finally { if (lockTaken) Monitor.Exit (_locker); } 
        }
        // temporary

        public int GetMaxEnvelopeCode(eSignEntities dbcontext)
        {

            var EnvelopeCodeMax = dbcontext.Envelope.OrderByDescending(x => x.EnvelopeCode).First().EnvelopeCode;
            return Convert.ToInt32(EnvelopeCodeMax);

        }

      

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage PrepareEnvelope(PrepareEnvelope prepareEnvelope)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageWithEnvlpGuid responseMessage = new ResponseMessageWithEnvlpGuid();
            responseMessage.RecipientList = new List<RecipientList>();
            
            try
            {
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), prepareEnvelope.EnvelopeID);
                string tempDocumentDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), prepareEnvelope.EnvelopeID, ConfigurationManager.AppSettings["UploadedDocuments"].ToString());

                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }
                if (string.IsNullOrEmpty(prepareEnvelope.EnvelopeID) || string.IsNullOrEmpty(prepareEnvelope.Subject) || prepareEnvelope.Recipients == null || string.IsNullOrEmpty(prepareEnvelope.Message))
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
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
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(prepareEnvelope.EnvelopeID));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage, Configuration.Formatters.JsonFormatter);
                        return responseToClient;
                    }

                    Envelope envelopeDetails = new Envelope();
                    envelopeDetails = envelopeRepository.GetEnvelopeDetails(new Guid(prepareEnvelope.EnvelopeID),UserId);
                    if (!envelopeDetails.IsEnvelope)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["PrepareEnvelopeWithTemplate"].ToString();
                        responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                  
                    

                    eSign.Models.Domain.Envelope envelope = new Envelope();
                    envelope.Recipients = new System.Data.Objects.DataClasses.EntityCollection<Recipients>();

                    EnvelopeHelperMain objEnvelope = new EnvelopeHelperMain();
                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(prepareEnvelope.EnvelopeID));
                    if (isEnvelopePrepare == true)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopePrepared"].ToString();
                        responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    var envelopeToConvert = envelopeRepository.GetEntity(new Guid(prepareEnvelope.EnvelopeID));
                    if (envelopeToConvert.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopePrepareFail"].ToString();
                        responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (!Directory.Exists(tempDocumentDirectory))
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["NoDocumentUploaded"].ToString();
                        responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    foreach (var recipient in prepareEnvelope.Recipients)
                    {
                        if (string.IsNullOrEmpty(recipient.RecipientName) || string.IsNullOrEmpty(recipient.RecipientType) || string.IsNullOrEmpty(recipient.Email) || string.IsNullOrEmpty(Convert.ToString(recipient.Order)))
                        {
                            responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                            responseMessage.StatusMessage = "NotAcceptable";
                            responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                            responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                            responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                            return responseToClient;
                        }

                        if (!masterDataRepository.ValidateRecipientTypeId(new Guid(recipient.RecipientType)))
                        {
                            responseMessage.StatusCode = HttpStatusCode.Forbidden;
                            responseMessage.StatusMessage = "Forbidden";
                            responseMessage.Message = ConfigurationManager.AppSettings["RecipientTypeIdInvalid"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                            return responseToClient;
                        }


                        if (!EnvelopeHelper.IsEmailValid(recipient.Email))
                        {
                            responseMessage.StatusCode = HttpStatusCode.Forbidden;
                            responseMessage.StatusMessage = "Forbidden";
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["Email"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                            return responseToClient;
                        }

                        if (!EnvelopeHelper.IsRecipientOrderValid(Convert.ToString(recipient.Order)) || recipient.Order == 0)
                        {
                            responseMessage.StatusCode = HttpStatusCode.Forbidden;
                            responseMessage.StatusMessage = "Forbidden";
                            responseMessage.Message = ConfigurationManager.AppSettings["InvalidOrder"].ToString();
                            responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                            return responseToClient;

                        }
                    }

                    foreach (var rec in prepareEnvelope.Recipients)
                    {                        

                        RecipientList recipientList = new RecipientList();
                        Recipients recipient = new Recipients();
                        recipient.ID = Guid.NewGuid();
                        recipient.EnvelopeID = new Guid(prepareEnvelope.EnvelopeID);
                        recipient.RecipientTypeID = new Guid(rec.RecipientType);
                        recipient.Name = rec.RecipientName;
                        recipient.EmailAddress = rec.Email;

                        if (new Guid(rec.RecipientType) == Constants.RecipientType.CC)
                            recipient.Order = null;
                        else
                        {
                            recipient.Order = rec.Order;

                            //if (EnvelopeHelper.IsRecipientOrderValid(Convert.ToString(rec.Order)))
                            //    recipient.Order = rec.Order;
                            //else
                            //    recipient.Order = 1;
                        }

                        recipient.CreatedDateTime = DateTime.Now;
                        envelope.Recipients.Add(recipient);
                        recipientList.RecipientName = rec.RecipientName;
                        recipientList.RecipientId = Convert.ToString(recipient.ID);
                        responseMessage.RecipientList.Add(recipientList);
                    }

                    envelope.ID = new Guid(prepareEnvelope.EnvelopeID);
                    envelope.ReminderDays = prepareEnvelope.SendReminderIn;
                    envelope.ReminderRepeatDays = prepareEnvelope.ThenSendReminderIn;
                    envelope.IsFinalCertificateReq = prepareEnvelope.SignatureCertificateRequired;
                    envelope.IsFinalDocLinkReq = prepareEnvelope.DownloadLinkRequired;
                    envelope.Subject = prepareEnvelope.Subject;
                    envelope.Message = prepareEnvelope.Message;
                    envelope.IsEnvelopePrepare = true;
                    
                    objEnvelope.SetApiCallFlag();
                    envelopeRepository.SetPrepareEnvelopeFlag();
                    envelopeRepository.SaveApiEnvelope(envelope);
                    unitOfWork.SaveChanges();
                    //dbContext.SaveChanges();
                   
                    objEnvelope.ConvertDocumentToImage(prepareEnvelope, envelopeToConvert);

                    responseMessage.StatusCode = HttpStatusCode.OK;
                    responseMessage.StatusMessage = "OK";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopePreparedSuccess"].ToString();
                    responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
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
        [HttpPost]
        public HttpResponseMessage SendEnvelope(PrepareEnvelope prepareEnvelope)
        {
           
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageWithEnvlpGuid responseMessage = new ResponseMessageWithEnvlpGuid();
            try
            {
                string envelopeID = prepareEnvelope.EnvelopeID;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), prepareEnvelope.EnvelopeID);
                if (string.IsNullOrEmpty(envelopeID))
                {
                    responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                    responseMessage.StatusMessage = "NotAcceptable";
                    responseMessage.Message = ConfigurationManager.AppSettings["RequiredFieldIsMissing"].ToString();
                    responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                    responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                    return responseToClient;
                }
                if (!Directory.Exists(tempEnvelopeDirectory))
                {
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString();
                    responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                    responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;
                }
                using (var dbContext = new eSignEntities())
                {
                    EnvelopeContent envelopeContent = new EnvelopeContent();
                    EnvelopeHelperMain objEnvelope = new EnvelopeHelperMain();
                    var helper = new eSignHelper();
                    UserTokenRepository userTokenRepository = new UserTokenRepository(dbContext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    string userEmail = userTokenRepository.GetUserEmailByToken(authToken);
                    Guid UserId = userTokenRepository.GetUserProfileUserIDByID(userTokenRepository.GetUserProfileIDByEmail(userEmail));
                    bool isEnvelopeExists = envelopeRepository.IsUserEnvelopeExists(UserId, new Guid(prepareEnvelope.EnvelopeID));
                    if (!isEnvelopeExists)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage, Configuration.Formatters.XmlFormatter);
                        return responseToClient;
                    }
                    Envelope envelope = envelopeRepository.GetEntity(new Guid(envelopeID));
                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeAlreadySent"].ToString());
                        responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage, Configuration.Formatters.XmlFormatter);
                        return responseToClient;
                    }


                    envelopeRepository.SetSendEnvelopeFlag();
                    helper.SetApiCallFlag();
                    objEnvelope.SetApiCallFlag();
                   
                    bool isCreateXml = objEnvelope.CreateXml(envelope, out envelopeContent);
                    //envelopeRepository.Save(envelopeContent);
                    //unitOfWork.SaveChanges();
                    //envelope = envelopeRepository.GetEntity(new Guid(envelopeID)); 
                    envelope.IsEnvelopeComplete = true;
                    envelopeRepository.Save(envelope);
                    unitOfWork.SaveChanges();
                    envelopeRepository.SaveApiEnvelope(envelope);
                    bool isEnvelopeSend = objEnvelope.SendApiEnvelope(envelope, userEmail);

                    
                        
                    if (isEnvelopeSend)
                    {
                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.Message = ConfigurationManager.AppSettings["EnvelopeSucess"].ToString();
                        responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;
                    }
                    else
                    {
                        responseMessage.StatusCode = HttpStatusCode.ExpectationFailed;
                        responseMessage.StatusMessage = "ExpectationFailed";
                        responseMessage.Message = ConfigurationManager.AppSettings["SomethingWentWrong"].ToString();
                        responseMessage.EnvelopeId = prepareEnvelope.EnvelopeID;
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
        [HttpGet]
        public HttpResponseMessage GetEnvelopeDetails(string envelopeCode)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageWithEnvlpGuid responseMessage = new ResponseMessageWithEnvlpGuid();
            Envelope envelope = new Envelope();
            //TemplateDetails templateDetails = new TemplateDetails();
            EnvelopeDetails envelopeDetails = new EnvelopeDetails();
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
                        responseMessage.EnvelopeId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    if (!envelope.IsEnvelope)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeDetailsFail"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.DateFormatID == Constants.DateFormat.US)
                        envelopeDetails.DateFormat = "US";
                    else
                        envelopeDetails.DateFormat = "EU";

                    string Password = string.Empty;

                    if (envelope.PasswordReqdtoSign)
                    {
                        Password = ModelHelper.Decrypt(envelope.PasswordtoSign, envelope.PasswordKey, (int)envelope.PasswordKeySize);
                        envelopeDetails.PasswordToSign = Password;
                        
                    }
                    else
                        envelopeDetails.PasswordToSign = null;


                    if (envelope.PasswordReqdtoOpen)
                    {
                        Password = ModelHelper.Decrypt(envelope.PasswordtoOpen, envelope.PasswordKey, (int)envelope.PasswordKeySize);
                        envelopeDetails.PasswordToOpen = Password;
                    }
                    else
                        envelopeDetails.PasswordToOpen = null;

                    envelopeDetails.ExpiryType = EnvelopeHelper.GetExpiryType(envelope.ExpiryTypeID);
                    envelopeDetails.CreatedDateTime = envelope.CreatedDateTime;
                    envelopeDetails.DisplayCode = envelope.DisplayCode;
                    envelopeDetails.DownloadLinkOnManageRequired = envelope.IsFinalDocLinkReq;
                    envelopeDetails.Subject = envelope.Subject;
                    envelopeDetails.EnvelopeID = envelope.ID;
                    envelopeDetails.Message = envelope.Message;
                    envelopeDetails.SignatureCertificateRequired = envelope.IsFinalCertificateReq;
                    envelopeDetails.PasswordKey = envelope.PasswordKey;
                    envelopeDetails.PasswordKeySize = envelope.PasswordKeySize;
                    envelopeDetails.PasswordReqdToOpen = envelope.PasswordReqdtoOpen;
                    envelopeDetails.PasswordReqdToSign = envelope.PasswordReqdtoSign;
                    envelopeDetails.RemainderDays = envelope.ReminderDays;
                    envelopeDetails.RemainderRepeatDays = envelope.ReminderRepeatDays;
                    envelopeDetails.StatusID = envelope.StatusID;
                    envelopeDetails.UserID = envelope.UserID;
                    envelopeDetails.DocumentDetails = new List<DocumentDetails>();


                    envelopeDetails.RecipientList = new List<RecipientDetails>();

                    foreach (var recipient in envelope.Recipients)
                    {
                        RecipientDetails recipientN = new RecipientDetails();

                        recipientN.ID = recipient.ID;
                        recipientN.CreatedDateTime = recipient.CreatedDateTime;
                        recipientN.RecipientName = recipient.Name;
                        recipientN.EnvelopeID = recipient.EnvelopeID;
                        recipientN.EmailID = recipient.EmailAddress;
                        recipientN.Order = recipient.Order;
                        recipientN.RecipientType = EnvelopeHelper.GetRecipentType(recipient.RecipientTypeID);

                        envelopeDetails.RecipientList.Add(recipientN);
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

                        envelopeDetails.DocumentDetails.Add(newDoc);

                    }


                    responseToClient = Request.CreateResponse(HttpStatusCode.OK, new { envelopeDetails });
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
        public HttpResponseMessage GetPageImage(string envelopeCode,string id) {
           
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageWithEnvlpGuid responseMessage = new ResponseMessageWithEnvlpGuid();
            Envelope envelope = new Envelope();

            try
            {
                string pageName = id;
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
                        responseMessage.EnvelopeId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(envelopeCode));
                    if (!isEnvelopePrepare)
                    {                       
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    PageDetails pageDetails = new PageDetails();
                    var imgPath = ConfigurationManager.AppSettings["TempLocation"] + envelopeCode + "\\Images\\" + pageName + ".jpg";
                    byte[] imgBytes = File.ReadAllBytes(imgPath);
                    string imgBase64 = Convert.ToBase64String(imgBytes);
                    pageDetails.EnvelopeID = envelopeCode;
                    pageDetails.PageName = id;
                    pageDetails.PageData = imgBase64;

                    responseToClient = Request.CreateResponse(HttpStatusCode.OK, new { pageDetails });
                    return responseToClient;

                }
            }
            catch (Exception ex) {

                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }
        }

        [AuthorizeAccess]
        [HttpGet]
        public HttpResponseMessage ViewPdf(string envelopeCode) {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageWithEnvlpGuid responseMessage = new ResponseMessageWithEnvlpGuid();
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
                        responseMessage.EnvelopeId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(envelopeCode));
                    if (!isEnvelopePrepare)
                    {                        
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    EnvelopeHelperMain envelopeHelperMain = new EnvelopeHelperMain();
                    ViewPdf viewPdf = new ViewPdf();
                    envelopeHelperMain.SetApiCallFlag();
                    var filestream = envelopeHelperMain.ViewPdfPreviewHelper(ConfigurationManager.AppSettings["WebLocation"].ToString(), envelope, false, envelope.ID);
                    var filePath = filestream.Name;
                    byte[] imgBytes = File.ReadAllBytes(filePath);
                    string imgBase64 = Convert.ToBase64String(imgBytes);
                    viewPdf.EnvelopeID = envelopeCode;
                    viewPdf.PageData = imgBase64;
                    responseToClient = Request.CreateResponse(HttpStatusCode.OK, new { viewPdf });
                    return responseToClient;

                }
            }
            catch (Exception ex) {
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }
        }
		
		[AuthorizeAccess]
        [HttpGet]
        public HttpResponseMessage GetPageCount(string envelopeCode)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageWithEnvlpGuid responseMessage = new ResponseMessageWithEnvlpGuid();
            Envelope envelope = new Envelope();
            DocumentPageDetails documentDetails = new DocumentPageDetails();
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
                        responseMessage.EnvelopeId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelopePrepare == false)
                    {                     
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeNotPrepared"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    var imageLocation = Convert.ToString(ConfigurationManager.AppSettings["TempDirectory"].ToString()) + envelopeCode + "\\" + Convert.ToString(ConfigurationManager.AppSettings["ImageFolder"].ToString());

                    if(!Directory.Exists(imageLocation))
                    {                     
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeIdMissing"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }
                    
                    documentDetails.ImageName = new List<string>();
                    string[] totalFiles = Directory.GetFiles(imageLocation);
                    
                    if (totalFiles.Length == 0)
                    {                        
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["PrepareEnvelope"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }


                    foreach (var file in totalFiles)
                    {
                        documentDetails.ImageName.Add(Path.GetFileName(file));
                    }

                    documentDetails.TotalPageCount = totalFiles.Length;
                    documentDetails.EnvelopeID = envelopeCode;
                    responseToClient = Request.CreateResponse(HttpStatusCode.OK, new { documentDetails });
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
        public HttpResponseMessage Discard(string envelopeCode)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            ResponseMessageDocument responseMessage = new ResponseMessageDocument();
            Envelope envelope = new Envelope();
            EnvelopeHelperMain envelopeHerlperMain = new EnvelopeHelperMain();

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

                    DocumentRepository documentRepository = new DocumentRepository(dbContext);
                    UnitOfWork unitOfWork = new UnitOfWork(dbContext);
                    DocumentContentsRepository documentContentsRepository = new DocumentContentsRepository(dbContext);
                    

                    Guid UserId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));
                    envelope = envelopeRepository.GetEnvelopeDetails(new Guid(envelopeCode), UserId);

                    if (envelope == null)
                    {
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseMessage.EnvelopeId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;
                    }


                    if (!envelope.IsEnvelope)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["DiscardTemplateFail"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    if (envelope.IsEnvelope && envelope.IsTemplateDeleted)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["DiscardEnvelopeFail"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }


                    if (envelope.IsEnvelopeComplete)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["DiscardCompleteFail"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }


                    bool isEnvelopePrepare = envelopeRepository.IsEnvelopePrepare(new Guid(envelopeCode));
                    if (!isEnvelopePrepare)
                    {
                        responseMessage.StatusCode = HttpStatusCode.BadRequest;
                        responseMessage.StatusMessage = "BadRequest";
                        responseMessage.Message = ConfigurationManager.AppSettings["DiscardOnPrepare"].ToString();
                        responseMessage.EnvelopeId = envelopeCode;
                        responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                        return responseToClient;
                    }

                    // Delete envelope folder from temporary location

                    string tempEnvelopeDirectory = Path.Combine(ConfigurationManager.AppSettings["TempDirectory"].ToString(), envelopeCode);

                    if (Directory.Exists(tempEnvelopeDirectory))
                        Directory.Delete(tempEnvelopeDirectory, true);

                    bool documentDelete = false;
                    var documents = envelope.Documents.ToList();
                    foreach (var document in documents)
                    {
                        if (document != null)
                        {
                            Documents doc = documentRepository.GetEntity(document.ID);

                            envelopeHerlperMain.SetApiCallFlag();
                            //envelopeHelperMain.DeleteFile(envelope.Documents.Where(d => d.ID == new Guid(documentCode)).FirstOrDefault().DocumentName, Convert.ToString(envelope.ID), envelope.Documents.Count, envelope);
                            bool documentContentDelete = documentContentsRepository.Delete(doc);
                            documentDelete = documentRepository.Delete(document.ID);
                        }
                    }

                    if (documentDelete)
                    {
                        envelope.IsEnvelopeComplete = false;
                        envelope.IsTemplateDeleted = true;
                        unitOfWork.SaveChanges();

                        responseMessage.StatusCode = HttpStatusCode.OK;
                        responseMessage.StatusMessage = "OK";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["DiscardSuccess"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessage);
                        return responseToClient;

                    }
                    else
                    {
                        responseMessage.StatusCode = HttpStatusCode.Ambiguous;
                        responseMessage.StatusMessage = "Ambiguous";
                        responseMessage.EnvelopeId = envelopeCode;
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["DiscardFail"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.Ambiguous, responseMessage);
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
        
    }
}
