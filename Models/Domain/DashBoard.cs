using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace eSign.WebAPI.Models.Domain
{
    public class DashBoard
    {
        public double AvereageTimeofCompletion { get; set; }
        public string Company { get; set; }
        public int Completed { get; set; }
        public int SignedDocuments { get; set; }
        public string EmailID { get; set; }
        public double Expired { get; set; }
        public int Expiring { get; set; }        
        public string FirstName { get; set; }
        public string FontClass { get; set; }
        public Guid ID { get; set; }
        public string Initials { get; set; }
        public string LastName { get; set; }
        public int SentForSignature { get; set; }
        public int Terminated { get; set; }
        public byte[] Photo { get; set; }
        public string PhotoString { get; set; }
        public byte[] SignatureImage { get; set; }
        public string SignatureString { get; set; }
        public string SignatureText { get; set; }
        public Guid SignatureTypeID { get; set; }
        public double Signed { get; set; }
        public double Pending { get; set; }
        public double Viewed { get; set; }        
        public string Title { get; set; }        
        public int SentDocuments { get; set; }
        public Guid UserID { get; set; }
        public string ProfilePicLocation { get; set; }        
    }
}