using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using eSign.Models.Domain;

namespace eSign.WebAPI.Models.Domain
{
    public class ManageEnvelope
    {
        public int EnvelopeCode { get; set; }
        public string Subject { get; set; }
        public string CurrentStatus { get; set; }
        public DateTime Sent { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime Completed { get; set; }
    }
}