using FTAnalyzer.Properties;
using FTAnalyzer.Utilities;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace FTAnalyzer.Exports
{
    class CookieAwareWebClient : WebClient
    {
        readonly CookieContainer _cookieJar = new CookieContainer();

        internal CookieAwareWebClient(CookieCollection cookies)
        {
            _cookieJar.Add(cookies);
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            if (request is HttpWebRequest webRequest)
                webRequest.CookieContainer = _cookieJar;
            return request;
        }
    }
}
