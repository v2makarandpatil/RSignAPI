using eSign.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web;
using System.Threading.Tasks;
using eSign.Models.Domain;
using eSign.WebAPI.Models.Domain;
using eSign.WebAPI.RpostService;
using eSign.Models.Data;
using System.Text;
using System.Configuration;
using eSign.Notification.RpostService;
using eSign.Models;
using System.Text.RegularExpressions;
using eSign.WebAPI.Models;
using eSign.WebAPI.Models.Helpers;

namespace eSign.WebAPI.Controllers
{    
    public class AccountController : ApiController
    {
       

        [HttpPost]
        public HttpResponseMessage RegisterUser(UserProfileToken objUser)
        {            
            try
            {
                var rpostServiceAPI = new RpostServiceAPI();

                if (!EnvelopeHelper.IsEmailValid(objUser.EmailId))
                {
                    ResponseMessage responseMessage = new ResponseMessage();
                    responseMessage.StatusCode = HttpStatusCode.Forbidden;
                    responseMessage.StatusMessage = "Forbidden";
                    responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["EmailWrong"].ToString());
                    HttpResponseMessage responseToClient = Request.CreateResponse(HttpStatusCode.Forbidden, responseMessage);
                    return responseToClient;
                }

                eSign.Notification.RpostService.ServiceResponse response = rpostServiceAPI.RegisterUser(objUser.EmailId, objUser.Password, "1", "en-US", objUser.FirstName + " " + objUser.LastName);

                

                if (response.Response == "Registration Successfully Processed")
                {
                    var userProfile = new UserProfile
                               {
                                   ID = Guid.NewGuid(),
                                   UserID = Guid.NewGuid(),
                                   EmailID = objUser.EmailId,
                                   FirstName = objUser.FirstName,
                                   LastName = objUser.LastName
                               };

                    using (var dbContext = new eSignEntities())
                    {
                        UserRepository userRepository = new UserRepository(dbContext);
                        userRepository.Save(userProfile);
                        dbContext.SaveChanges();
                    }
                    ResponseMessage responseMessage = new ResponseMessage();
                    responseMessage.StatusCode = HttpStatusCode.Created;
                    responseMessage.StatusMessage = "Created";
                    responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["Success"].ToString());
                    HttpResponseMessage responseToClient = Request.CreateResponse(HttpStatusCode.Created, responseMessage);
                    return responseToClient;

                }
                else if (response.Response == "The email address that entered is already registered in the system.")
                {
                    ResponseMessage responseMessage = new ResponseMessage();
                    responseMessage.StatusCode = HttpStatusCode.Conflict;
                    responseMessage.StatusMessage = "Conflict";
                    responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["AlreadyRegistered"].ToString());
                    HttpResponseMessage responseToClient = Request.CreateResponse(HttpStatusCode.Conflict, responseMessage);
                    return responseToClient;
                }
                else
                {
                    ResponseMessage responseMessage = new ResponseMessage();
                    responseMessage.StatusCode = HttpStatusCode.BadRequest;
                    responseMessage.StatusMessage = "BadRequest";
                    responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["Failed"].ToString());
                    HttpResponseMessage responseToClient = Request.CreateResponse(HttpStatusCode.BadRequest, responseMessage);
                    return responseToClient;  
                }

            }
            catch (Exception ex)
            {
                HttpResponseMessage responseToClient = Request.CreateResponse((HttpStatusCode)422);
                responseToClient.Content = new StringContent(ex.Message, Encoding.Unicode);
                throw new HttpResponseException(responseToClient);
            }
        }
        [HttpGet]
        public string Test()
        {
            return "Test";
        }        
    }
}
