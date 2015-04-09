using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace eSign.WebAPI.Models.Helpers
{
    public class GetTimeZone
    {
        public static string GetTimeZoneAbbreviation(string timeZoneName)
        {
            string output = string.Empty;
            string[] timeZoneWords = timeZoneName.Split(' ');
            foreach (string timeZoneWord in timeZoneWords)
            {
                if (timeZoneWord[0] != '(')
                {
                    output += timeZoneWord[0];
                }
                else
                {
                    output += timeZoneWord;
                }
            }

            if (output == "CUT")
            {
                output = "UTC";
            }

            return output;            
        }
    }
}