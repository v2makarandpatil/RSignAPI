using eSign.Models.Data;
using eSign.Models.Domain;
using eSign.Notification;
using eSign.WebAPI.Models.Domain;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;

namespace eSign.WebAPI.Controllers
{
    public class AuthenticationController : ApiController
    {        
        [HttpPost]
        public HttpResponseMessage AuthenticateUser(UserProfileToken objUser)
        {
            ResponseTokenWithEmailId respToken = new ResponseTokenWithEmailId();
            try
            {
                UserToken userProfile = new UserToken();
                userProfile.EmailId = objUser.EmailId;                

                var rpostServiceAPI = new RpostServiceAPI();

                using (var dbcontext = new eSignEntities())
                {
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbcontext);
                    eSign.Notification.RpostService.AuthorizationResponse response = rpostServiceAPI.AuthenticateUser(objUser.EmailId, objUser.Password);

                    if (!string.IsNullOrEmpty(response.AuthorizationKey))
                    {
                        UserToken objToken = new UserToken();

                        objToken.ID = tokenRepository.GetUserProfileIDByEmail(objUser.EmailId);
                        objToken.EmailId = objUser.EmailId;
                        objToken.AuthToken = response.AuthorizationKey;
                        objToken.LastUpdated = DateTime.Now;
                        objToken.ExpiresIn = DateTime.Now.AddDays(Convert.ToDouble(ConfigurationManager.AppSettings["ExpiryDays"]));                        

                        bool result = tokenRepository.Save(objToken);
                        dbcontext.SaveChanges();

                        respToken.AuthMessage = Convert.ToString(ConfigurationManager.AppSettings["TokenSuccess"].ToString());
                        respToken.AuthToken = response.AuthorizationKey;
                        respToken.EmailId = objUser.EmailId;
                        HttpResponseMessage responseToClient = Request.CreateResponse(HttpStatusCode.OK, respToken);
                        return responseToClient;
                    }
                    else
                    {
                        ResponseMessageWithEmailId responseMessage = new ResponseMessageWithEmailId();
                        responseMessage.StatusCode = HttpStatusCode.NotAcceptable;
                        responseMessage.StatusMessage = "NotAcceptable";
                        responseMessage.Message = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings["InvalidUser"].ToString());
                        responseMessage.EmailId = objUser.EmailId;
                        HttpResponseMessage responseToClient = Request.CreateResponse(HttpStatusCode.NotAcceptable, responseMessage);
                        return responseToClient;
                    }
                }
            }
            catch (Exception ex)
            {
                HttpResponseMessage responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }
        }
    }
}
