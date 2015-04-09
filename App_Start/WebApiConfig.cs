using eSign.Models.Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.Routing;

namespace eSign.WebAPI
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            config.Routes.MapHttpRoute(
                name: "DocumentDelete",
                routeTemplate: "api/{controller}/{action}/{envelopeCode}/{id}",
                defaults: new { envelopeCode = RouteParameter.Optional, documentCode = RouteParameter.Optional }
            );
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{action}/{envelopeCode}",
                defaults: new { envelopeCode = RouteParameter.Optional }
            );
            config.Routes.MapHttpRoute(
               name: "ControllerAndAction",
               routeTemplate: "api/{controller}/{action}"
           );

           
            GlobalConfiguration.Configuration.Formatters.XmlFormatter.UseXmlSerializer = true;
            config.EnableSystemDiagnosticsTracing();
        }
    }
}
