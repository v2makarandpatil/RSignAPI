using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace eSign.WebAPI.Models.Domain
{
    public class EnvelopeGetEnvelopeHistoryByEnvelopeCode
    {
        public int EnvelopeCode { get; set; }
        public string Subject { get; set; }
        public string CurrentStatus { get; set; }
        public string Sent { get; set; }       
        public string Completed { get; set; }

        public List<string> DocumentList { get; set; }
        public string EnvelopeID { get; set; }
        public string Sender { get; set; }               
        public string DelegatedTo { get; set; }                            
        public string EnvelopeStatusDescription { get; set; }
        public List<DocumentStatus> DocumentHistory { get; set; }
        public string CompletedStatusDate { get; set; }        
    }

    public class DocumentStatus
    {
        public string StatusDate { get; set; }
        public string SignerStatusDescription { get; set; }
        public string Recipient { get; set; }
        public string RecipientEmailAddress { get; set; }
        public string IPAddress { get; set; }
    }
}