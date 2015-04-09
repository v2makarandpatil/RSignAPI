using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace eSign.WebAPI.Models.Domain
{
    public class UserProfileToken
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string EmailId { get; set; }
        public Guid UserID { get; set; }
        public string Password { get; set; }
        public string AuthToken { get; set; }
        public DateTime issuedInTime { get; set; }
        public DateTime expiresInTime { get; set; }
        
    }
}