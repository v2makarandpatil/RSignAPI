using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace eSign.WebAPI.Models.Domain
{
    public class ResponseToken
    {
        public string AuthMessage { get; set; }
        public string AuthToken { get; set; }
    }
    public class ResponseTokenWithEmailId
    {
        public string AuthMessage { get; set; }
        public string AuthToken { get; set; }
        public string EmailId { get; set; }
    }
}