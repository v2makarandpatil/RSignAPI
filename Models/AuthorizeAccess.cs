using System;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http;
using System.Net;
using eSign.Models.Data;
using eSign.Models.Domain;
using eSign.WebAPI.Models.Domain;


namespace eSign.WebAPI.Models
{
    public class AuthorizeAccess : System.Web.Http.AuthorizeAttribute
    {
        bool requireSsl = false;
        private string tokenInformation { get; set; }

        public bool RequireSsl
        {
            get { return requireSsl; }
            set { requireSsl = value; }
        }

        bool requireAuthentication = true;

        public bool RequireAuthentication
        {
            get { return requireAuthentication; }
            set { requireAuthentication = value; }
        }



        /// <summary>
        /// For logging with Log4net.
        /// </summary>
        //private static readonly ILog log = LogManager.GetLogger(typeof(BasicHttpAuthorizeAttribute));
        public override void OnAuthorization(System.Web.Http.Controllers.HttpActionContext actionContext)
        {
            if (Authenticate(actionContext) || !RequireAuthentication)
            {
                return;
            }
            else
            {
                if (tokenInformation == "User token is expired")
                {
                    //actionContext.Response = new HttpResponseMessage(System.Net.HttpStatusCode.ResetContent);                                        
                    ResponseMessage responseMessage = new ResponseMessage();
                    responseMessage.StatusCode = System.Net.HttpStatusCode.ResetContent;
                    responseMessage.StatusMessage = "ResetContent";
                    responseMessage.Message = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings["TokenExpired"].ToString());
                    var response = actionContext.Request.CreateResponse(System.Net.HttpStatusCode.ResetContent,responseMessage);
                    actionContext.Response = response;
                }                    
                else
                    HandleUnauthorizedRequest(actionContext);
            }
        }


        public bool Authorize(HttpRequestMessage Request)
        {
            try
            {
                
                System.Collections.Generic.IEnumerable<string> iHeader;
                if (!Request.Headers.TryGetValues("AuthToken", out iHeader))
                {
                    //ErrorMessage = "missing auth_token header";
                    return false;
                }

                string authToken = iHeader.ElementAt(0);
                if (authToken == string.Empty || authToken == null)
                    return false;

                using (var dbcontext = new eSignEntities())
                {
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbcontext);
                    tokenInformation = tokenRepository.IsUserAuthenticated(authToken);
                    if (tokenInformation == "User is authenticated")
                        return true;
                    else
                        return false;
                }
                
            }
            catch (Exception ex)
            {
               var responseToClient = new HttpResponseMessage()
                {
                    StatusCode = (HttpStatusCode)422,       //Unprocessable Entity
                    ReasonPhrase = ex.Message
                };
                throw new HttpResponseException(responseToClient);
            }
        }

        private bool Authenticate(HttpActionContext actionContext) //HttpRequestMessage input)
        {

            try
            {
                if (RequireSsl && !HttpContext.Current.Request.IsSecureConnection && !HttpContext.Current.Request.IsLocal)
                {
                    //log.Error("Failed to login: SSL:" + HttpContext.Current.Request.IsSecureConnection);
                    return false;
                }

                bool isAuthorize = Authorize(actionContext.Request);

                return isAuthorize;
            }
            catch (Exception ex)
            {

                var responseToClient = new HttpResponseMessage()
                {
                    StatusCode = (HttpStatusCode)422,       //Unprocessable Entity
                    ReasonPhrase = ex.Message
                };
                throw new HttpResponseException(responseToClient);
            }
        }        
    }
}