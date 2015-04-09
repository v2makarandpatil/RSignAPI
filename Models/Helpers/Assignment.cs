using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using eSign.WebAPI.Models.Domain;
using eSign.Models.Domain;

namespace eSign.WebAPI.Models.Helpers
{
    public class Assignment
    {
        public static DashBoard DashboardAssignment(DashBoard dashBoard, UserProfile userProfile)
        {
            dashBoard.AvereageTimeofCompletion = userProfile.AverageTimeValue;
            dashBoard.Company = userProfile.Company;
            dashBoard.Completed = userProfile.CompletedValue;
            dashBoard.SignedDocuments = userProfile.CompletedValue;
            dashBoard.EmailID = userProfile.EmailID;
            dashBoard.Expired = userProfile.ExpiredValue;
            dashBoard.Expiring = userProfile.ExpiringSoonValue;
            dashBoard.FirstName = userProfile.FirstName;
            dashBoard.FontClass = userProfile.FontClass;
            dashBoard.ID = userProfile.ID;
            dashBoard.Initials = userProfile.Initials;
            dashBoard.LastName = userProfile.LastName;
            dashBoard.SentForSignature= userProfile.SentforSignatureValue;
            dashBoard.Terminated = userProfile.Terminated;
            dashBoard.Photo = userProfile.Photo;
            dashBoard.PhotoString = userProfile.PhotoString;
            dashBoard.SignatureImage = userProfile.SignatureImage;
            dashBoard.SignatureString = userProfile.SignatureString;
            dashBoard.SignatureText = userProfile.SignatureText;
            dashBoard.SignatureTypeID = userProfile.SignatureTypeID;
            dashBoard.Signed = userProfile.SignedValue;
            dashBoard.Viewed = userProfile.ViewedValue;
            dashBoard.Title = userProfile.Title;
            dashBoard.SentDocuments = userProfile.TotalEnvelopeCount;
            dashBoard.UserID = userProfile.UserID;
            dashBoard.Pending = userProfile.SentforSignatureValue;
            return dashBoard;
        }
    }
}