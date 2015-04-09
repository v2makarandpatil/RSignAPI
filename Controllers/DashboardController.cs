using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using eSign.Models;
using eSign.Models.Helpers;
using eSign.Models.Data;
using eSign.Models.Domain;
using eSign.Models.Interfaces;
using eSign.Web;
using eSign.Web.Helpers;
using eSign.Models.Interfaces.Data;
using eSign.Core.Enums;
using eSign.Notification.RpostService;
using eSign.Notification;
using System.Web;
using System.Web.Http.WebHost;
using System.Web.SessionState;
using System.Web.Routing;
using eSign.WebAPI.Models;
using System.Web.Helpers;
using System.Web.Script.Serialization;
using eSign.WebAPI.Models.Domain;
using System.IO;
using System.Web.UI.WebControls;
using System.Text;
using System.ComponentModel;
using System.Drawing;
using System.Configuration;
using eSign.WebAPI.Models.Helpers;




namespace eSign.WebAPI.Controllers
{    
    public class DashboardController : ApiController
    {
        const int passwordKeySize = 128;
        // Generating key
        string completeEncodedKey = ModelHelper.GenerateKey(passwordKeySize);
        Guid userId;

       [AuthorizeAccess]
       [HttpGet]
        public HttpResponseMessage GetDashboardData()
        {                     
           var userProfile = new UserProfile();
           DashBoard dashBoard = new DashBoard();
           HttpResponseMessage responseToClient = new HttpResponseMessage();
           try
            {               
                System.Collections.Generic.IEnumerable<string> iHeader;
                Request.Headers.TryGetValues("AuthToken", out iHeader);
                string authToken = iHeader.ElementAt(0);

                using (var dbContext = new eSignEntities())
                {
                    UserTokenRepository tokenRepository = new UserTokenRepository(dbContext);
                    string userEmail = tokenRepository.GetUserEmailByToken(authToken);
                    UserRepository objRepository = new UserRepository(dbContext);
                    userProfile = objRepository.GetUserProfile(tokenRepository.GetUserProfileUserIDByID(tokenRepository.GetUserProfileIDByEmail(userEmail)), userEmail, true);
                    
                    if(userProfile == null)
                    {
                        ResponseMessage responseMessage = new ResponseMessage();
                        responseMessage.StatusCode = HttpStatusCode.NoContent;
                        responseMessage.StatusMessage = "NoContent";
                        responseMessage.Message = Convert.ToString(ConfigurationManager.AppSettings["NoDataFound"].ToString());
                        responseToClient = Request.CreateResponse(HttpStatusCode.NoContent, responseMessage);
                        return responseToClient; 
                    }
                    userId = userProfile.UserID;
                    dashBoard = Assignment.DashboardAssignment(dashBoard, userProfile);                    
                    dashBoard.ProfilePicLocation = PictureUrl(userProfile);                    
                    responseToClient = Request.CreateResponse(HttpStatusCode.OK, dashBoard);
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


    

       private static string PictureUrl(UserProfile userProfile)
       {
         
           string photoFile = userProfile.Photo != null
                                  ? string.Format("{0}/{1}.jpg", Convert.ToString(ConfigurationManager.AppSettings["domain"].ToString()) + Convert.ToString(ConfigurationManager.AppSettings["ProfilePicLocation"].ToString()), userProfile.UserID.ToString())
                                  : Convert.ToString(ConfigurationManager.AppSettings["domain"] +Convert.ToString(ConfigurationManager.AppSettings["defaultPic"].ToString()));

           return photoFile;
       }       
    }
}
