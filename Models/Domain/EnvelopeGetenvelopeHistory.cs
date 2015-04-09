using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace eSign.WebAPI.Models.Domain
{
    public class EnvelopeGetenvelopeHistory
    {
        public int EnvelopeCode { get; set; }
        public string Subject { get; set; }
        public string CurrentStatus { get; set; }
        public string Sent { get; set; }        
        public string Completed { get; set; }
    }
}