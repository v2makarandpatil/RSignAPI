using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using eSign.Models;
using eSign.Models.Data;
using eSign.WebAPI.Models.Domain;
using eSign.WebAPI.Models;
using eSign.Models.Domain;
using eSign.Models.Helpers;
using eSign.Core.Enums;
using System.Data.Objects.DataClasses;
using System.Text.RegularExpressions;
using System.Configuration;
using System.Text;
using System.Net.Http.Headers;
using eSign.WebAPI.Models.Helpers;

namespace eSign.WebAPI.Controllers
{
    public class ManageEnvelopeController : ApiController
    {
        

        [AuthorizeAccess]
        [HttpPost]
        public HttpResponseMessage SendEnvelope(TemplateSend template)
        {
            HttpResponseMessage responseToClient = new HttpResponseMessage();
            try
            {                
                int displayCode = 0;
                bool flagRole = true;
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);
                Envelope envelope = new Envelope();
                Dictionary<string, string> roleDic = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                EnvelopeHelperMain objEnvelope = new EnvelopeHelperMain();
                EntityCollection<Recipients> recipients = new EntityCollection<Recipients>();
                Recipients recipint = new Recipients();

                using (var dbContext = new eSignEntities())
                {
                    List<Roles> roleList = new List<Roles>();
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                    EnvelopeRepository envelopeRepository = new EnvelopeRepository(dbContext);                    
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);

                    Guid UserId = tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail));
                    envelope = envelopeRepository.GetTemplateDetails(template.TemplateCode, UserId);
                    
                    if (envelope == null)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoContent"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient;                     
                    }

                    envelope.Message = template.MailBody;
                    envelope.Subject = template.MailSubject;
                    roleList = envelopeRepository.GetRoles(envelope.ID);
                    envelope.IsEnvelopeComplete = true;
                    envelope.IsEnvelopePrepare = true;
                    
                    if (template.Recipients.Count != roleList.Count)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.Forbidden;
                        responseMessage.StatusMessage = "Forbidden";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["RolesCount"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                        return responseToClient;   
                    }



                    foreach (var recip in template.Recipients)
                    {
                        flagRole = EnvelopeHelper.IsEmailValid(recip.EmailAddress);
                        if (!flagRole)
                        {
                            ResponseMessage responseMessage = new ResponseMessage();
                            responseMessage.StatusCode = HttpStatusCode.Forbidden;
                            responseMessage.StatusMessage = "Forbidden";
                            responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["Email"].ToString());
                            responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                            return responseToClient;   
                        }

                        foreach (var role in roleList)
                        {
                            if (role.Name.ToLower() == recip.Role.ToLower())
                            {
                                recipint.EmailAddress = recip.EmailAddress;
                                recipint.Name = recip.Role;
                                recipint.Order = role.Order;
                                recipint.RecipientTypeID = role.RecipientTypeID;
                                roleDic.Add(recipint.Name, recipint.EmailAddress);
                            }
                        }
                    }
                   


                    foreach (var role in roleList)
                    {
                        if (!roleDic.ContainsKey(role.Name))
                            flagRole = false;

                    }

                    if (!flagRole)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.Forbidden;
                        responseMessage.StatusMessage = "Forbidden";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["RolesCount"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                        return responseToClient;   
                    }

                    string Password = string.Empty;

                    if (envelope.PasswordReqdtoSign)
                    {
                        Password = ModelHelper.Decrypt(envelope.PasswordtoSign, envelope.PasswordKey, (int)envelope.PasswordKeySize);
                        envelope.PasswordtoSign = Password;
                    }
                    else
                        envelope.PasswordtoSign = null;


                    if (envelope.PasswordReqdtoOpen)
                    {
                        Password = ModelHelper.Decrypt(envelope.PasswordtoOpen, envelope.PasswordKey, (int)envelope.PasswordKeySize);
                        envelope.PasswordtoOpen = Password;
                    }
                    else
                        envelope.PasswordtoOpen = null;

                   roleDic.Add("Sender", userEmail);

                   objEnvelope.SetApiCallFlag();
                   bool status = objEnvelope.UpdatedEnvelope(envelope, template.TemplateCode, roleDic, userEmail, out displayCode);
                  if (status == false)
                  {                     
                      ResponseMessage responseMessageFail = new ResponseMessage();
                      responseMessageFail.StatusCode = HttpStatusCode.Ambiguous;
                      responseMessageFail.StatusMessage = "Ambiguous";
                      responseMessageFail.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeFail"].ToString());
                      responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessageFail);
                      return responseToClient;
                  }
                }
                ResponseMessageWithEnvID responseMessageSuccess = new ResponseMessageWithEnvID();
                responseMessageSuccess.StatusCode = HttpStatusCode.OK;
                responseMessageSuccess.StatusMessage = "OK";
                responseMessageSuccess.EnvId = displayCode;
                responseMessageSuccess.Message = Convert.ToString(ConfigurationManager.AppSettings["EnvelopeSucess"].ToString());
                responseToClient = Request.CreateResponse(HttpStatusCode.OK, responseMessageSuccess);
                return responseToClient;
            }
            catch (Exception ex)
            {                
                responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(Convert.ToString(ConfigurationManager.AppSettings["EnvelopeFail"].ToString()), Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }
        }


      

    }
}
