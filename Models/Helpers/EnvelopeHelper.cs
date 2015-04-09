using eSign.Models.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;

namespace eSign.WebAPI.Models.Helpers
{
    public class EnvelopeHelper
    {
        public static string GetExpiryType(Guid ExpiryTypeID)
        {
            if (ExpiryTypeID == Constants.ExpiryType.One_Weeks)
                return "One Week";
            else if (ExpiryTypeID == Constants.ExpiryType.Two_Weeks)
                return "Two Weeks";
            else if (ExpiryTypeID == Constants.ExpiryType.Thirty_Days)
                return "One Month";
            else if (ExpiryTypeID == Constants.ExpiryType.Three_Months)
                return "Three Months";

            return string.Empty;

        }

        public const string matchEmailPattern = @"^(([\w-]+\.)+[\w-]+|([a-zA-Z]{1}|[\w-]{2,}))@" + @"((([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\.([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\." + @"([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])\.([0-1]?[0-9]{1,2}|25[0-5]|2[0-4][0-9])){1}|" + @"([a-zA-Z]+[\w-]+\.)+[a-zA-Z]{2,4})$";

        public static bool IsEmailValid(string email)
        {
            if (email != null)
                return Regex.IsMatch(email, matchEmailPattern);
            else return false;
        }

        public static bool IsRecipientOrderValid(string number)
        {
            int returnNumber;
            bool isNumeric = int.TryParse(number, out returnNumber);
            if (isNumeric)
            {
                bool positive = returnNumber > 0;
                return positive;
            }
            return false;
        }


        public static string GetFontName(Guid FontID)
        {
            if (FontID == new Guid("1AB25FA7-A294-405E-A04A-3B731AD795AC"))
                return "Arial";
            else if (FontID == new Guid("1875C58D-52BD-498A-BE6D-433A8858357E"))
                return "Cambria";
            else if (FontID == new Guid("D4A45ECD-3865-448A-92FA-929C2295EA34"))
                return "Courier";
            else if (FontID == new Guid("956D8FD3-BB0F-4E30-8E55-D860DEABB346"))
                return "Times New Roman";

            return string.Empty;

        }

        public static string GetRecipentType(Guid RecipintID)
        {
            if (RecipintID == new Guid("63EA73C2-4B64-4974-A7D5-0312B49D29D0"))
                return "CC";
            else if (RecipintID == new Guid("C20C350E-1C6D-4C03-9154-2CC688C099CB"))
                return "Signer";
            else if (RecipintID == new Guid("26E35C91-5EE1-4ABF-B421-3B631A34F677"))
                return "Sender";
            
            return string.Empty;

        }


    }
}