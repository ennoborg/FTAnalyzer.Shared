﻿using FTAnalyzer.Utilities;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

namespace FTAnalyzer.Exports
{
    public static class ExportToLostCousins
    {
        static List<CensusIndividual> ToProcess { get; set; }
        static NetworkCredential Credentials { get; set; }
        static CookieCollection CookieJar { get; set; }
        static List<LostCousin> Website { get; set; }
        static List<LostCousin> SessionList { get; set; }
        
        public static int ProcessList(List<CensusIndividual> individuals, IProgress<string> outputText)
        {
            ToProcess = individuals;
            int recordsAdded = 0;
            int recordsFailed = 0;
            int recordsPresent = 0;
            int sessionDuplicates = 0;
            int count = 0;
            if (Website == null)
                Website = LoadWebsiteAncestors(outputText);
            if (SessionList == null)
                SessionList = new List<LostCousin>();
            foreach (CensusIndividual ind in ToProcess)
            {
                if (ind.CensusReference != null && ind.CensusReference.IsValidLostCousinsReference())
                {
                    LostCousin lc = new LostCousin($"{ind.Surname}, {ind.Forenames}", ind.BirthDate.BestYear, GetCensusSpecificFields(ind), ind.CensusDate.BestYear, ind.CensusCountry); ;
                    if (Website.Contains(lc))
                    {
                        outputText.Report($"Record {++count} of {ToProcess.Count}: {ind.CensusDate} - Already Present {ind.ToString()}, {ind.CensusReference}.\n");
                        DatabaseHelper.Instance.StoreLostCousinsFact(ind);
                        recordsPresent++;
                    }
                    else
                    {
                        if (SessionList.Contains(lc))
                        {
                            outputText.Report($"Record {++count} of {ToProcess.Count}: {ind.CensusDate} - Already submitted this session {ind.ToString()}, {ind.CensusReference}. Possible duplicate Individual\n");
                            sessionDuplicates++;
                        }
                        else
                        { 
                            if (AddIndividualToWebsite(ind, outputText))
                            {
                                outputText.Report($"Record {++count} of {ToProcess.Count}: {ind.CensusDate} - {ind.ToString()}, {ind.CensusReference} added.\n");
                                recordsAdded++;
                                SessionList.Add(lc);
                                DatabaseHelper.Instance.StoreLostCousinsFact(ind);
                            }
                            else
                            {
                                outputText.Report($"Record {++count} of {ToProcess.Count}: {ind.CensusDate} - Failed to add {ind.ToString()}, {ind.CensusReference}.\n");
                                recordsFailed++;
                            }
                        }
                    }
                }
                else
                {
                    outputText.Report($"Record {++count} of {ToProcess.Count}: {ind.CensusDate} - Failed to add {ind.ToString()}, {ind.CensusReference}. Census Reference problem.\n");
                    recordsFailed++;
                }
            }
            outputText.Report($"\nFinished writing Entries to Lost Cousins website. {recordsAdded} successfully added, {recordsPresent} already present, {sessionDuplicates} possible duplicates and {recordsFailed} failed.\nView Lost Cousins Report tab to see current status.");
            return recordsAdded;
        }

        public static bool CheckLostCousinsLogin(string email, string password)
        {
            HttpWebResponse resp = null;
            try
            {
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                    return false;
                string formParams = $"stage=submit&email={HttpUtility.UrlEncode(email)}&password={password}{Suffix()}";
                HttpWebRequest req = WebRequest.Create("https://www.lostcousins.com/pages/login/") as HttpWebRequest;
                req.Referer = "https://www.lostcousins.com/pages/login/";
                req.ContentType = "application/x-www-form-urlencoded";
                req.Method = "POST";
                Credentials = new NetworkCredential(email, password);
                req.Credentials = Credentials;
                req.CookieContainer = new CookieContainer();
                req.AllowAutoRedirect = false;
                byte[] bytes = Encoding.ASCII.GetBytes(formParams);
                req.ContentLength = bytes.Length;
                using (Stream os = req.GetRequestStream())
                {
                    os.Write(bytes, 0, bytes.Length);
                }
                resp = req.GetResponse() as HttpWebResponse;
                CookieJar = resp.Cookies;
                return CookieJar.Count == 2 && (CookieJar[0].Name=="lostcousins_user_login" || CookieJar[1].Name == "lostcousins_user_login");
            }
            catch(Exception e)
            {
                UIHelpers.ShowMessage($"Problem accessing Lost Cousins Website. Check you are connected to internet. Error message is: {e.Message}");
                return false;
            }
            finally
            {
                resp?.Close();
            }
        }

        static List<LostCousin> LoadWebsiteAncestors(IProgress<string> outputText)
        {
            List<LostCousin> websiteList = new List<LostCousin>();
            try
            {
                CookieAwareWebClient wc = new CookieAwareWebClient(CookieJar);
                HtmlDocument doc = new HtmlDocument();
                string webData = wc.DownloadString("https://www.lostcousins.com/pages/members/ancestors/");
                doc.LoadHtml(webData);
                HtmlNodeCollection rows = doc.DocumentNode.SelectNodes("//table[@class='data_table']/tr");
                if (rows != null)
                {
                    foreach (HtmlNode node in rows)
                    {
                        HtmlNodeCollection columns = node.SelectNodes("td");
                        if (columns != null && columns.Count == 8 && columns[0].InnerText != "Name") // ignore header row
                        {
                            string name = columns[0].InnerText.ClearWhiteSpace();
                            string birthYear = columns[2].InnerText.ClearWhiteSpace();
                            string reference = columns[4].InnerText.ClearWhiteSpace();
                            string census = columns[5].InnerText.ClearWhiteSpace();
                            LostCousin lc = new LostCousin(name, birthYear, reference, census);
                            websiteList.Add(lc);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                outputText.Report($"Problem accessing Lost Cousins Website to read current ancestor list. Error message is: {e.Message}\n");
                return null;
            }
            return websiteList;
        }

        static bool AddIndividualToWebsite(CensusIndividual ind, IProgress<string> outputText)
        {
            HttpWebResponse resp = null;
            try
            {
                string formParams = BuildParameterString(ind);
                HttpWebRequest req = WebRequest.Create("https://www.lostcousins.com/pages/members/ancestors/add_ancestor.mhtml") as HttpWebRequest;
                req.Referer = "https://www.lostcousins.com/pages/members/ancestors/add_ancestor.mhtml";
                req.ContentType = "application/x-www-form-urlencoded";
                req.Method = "POST";
                req.Credentials = Credentials;
                req.CookieContainer = new CookieContainer();
                req.CookieContainer.Add(CookieJar);
                byte[] bytes = Encoding.ASCII.GetBytes(formParams);
                req.ContentLength = bytes.Length;
                req.Timeout = 10000;
                using (Stream os = req.GetRequestStream())
                {
                    os.Write(bytes, 0, bytes.Length);
                }
                resp = req.GetResponse() as HttpWebResponse;
                return resp.ResponseUri.Query.Length > 0;
            }
            catch (Exception e)
            {
                outputText.Report($"Problem accessing Lost Cousins Website to send record below. Error message is: {e.Message}\n");
                return false;
            }
            finally
            {
                resp?.Close();
            }
        }

        static string BuildParameterString(CensusIndividual ind)
        {
            StringBuilder output = new StringBuilder("stage=submit&similar=");
            output.Append(GetCensusSpecificFields(ind));
            output.Append($"&surname={ind.SurnameAtDate(ind.CensusDate)}");
            output.Append($"&forename={ind.Forename}");
            output.Append($"&other_names={ind.OtherNames}");
            output.Append($"&age={ind.LCAge}");
            output.Append($"&relation_type={GetLCDescendantStatus(ind)}");
            if (!ind.IsMale && ind.Surname != ind.SurnameAtDate(ind.CensusDate))
                output.Append($"&maiden_name={ind.Surname}");
            else
                output.Append("&maiden_name=");
            output.Append($"&corrected_surname=&corrected_forename=&corrected_other_names=");
            if (ind.BirthDate.IsExact)
                output.Append($"&corrected_birth_day={ind.BirthDate.StartDate.Day}&corrected_birth_month={ind.BirthDate.StartDate.Month}&corrected_birth_year={ind.BirthDate.StartDate.Year}");
            else
                output.Append($"&corrected_birth_day=&corrected_birth_month=&corrected_birth_year=");
            output.Append("&baptism_day=&baptism_month=&baptism_year=");
            output.Append($"&piece_number=&notes=Added_By_FTAnalyzer{Suffix()}"); 
            return output.ToString();
        }

        static string Suffix()
        {
            Random random = new Random();
            int x = random.Next(1,99);
            int y = random.Next(1, 9);
            return $"&x={x}&y={y}";
        }

        static string GetCensusSpecificFields(CensusIndividual ind)
        {
            CensusReference censusRef = ind.CensusReference;
            if (ind.CensusDate.Overlaps(CensusDate.EWCENSUS1841) && Countries.IsEnglandWales(ind.CensusCountry))
                return $"&census_code=1841&ref1={censusRef.Piece}&ref2={censusRef.Book}&ref3={censusRef.Folio}&ref4={censusRef.Page}&ref5=";
            if (ind.CensusDate.Overlaps(CensusDate.EWCENSUS1881) && Countries.IsEnglandWales(ind.CensusCountry))
                return $"&census_code=RG11&ref1={censusRef.Piece}&ref2={censusRef.Folio}&ref3={censusRef.Page}&ref4=&ref5=";
            if (ind.CensusDate.Overlaps(CensusDate.SCOTCENSUS1881) && ind.CensusCountry == Countries.SCOTLAND)
                return $"&census_code=SCT1&ref1={censusRef.RD}&ref2={censusRef.ED}&ref3={censusRef.Page}&ref4=&ref5=";
            if (ind.CensusDate.Overlaps(CensusDate.CANADACENSUS1881) && ind.CensusCountry == Countries.CANADA)
                return $"&census_code=CAN1&ref1={censusRef.ED}&ref2={censusRef.SD}&ref3=&ref4={censusRef.Page}&ref5={censusRef.Family}";
            //if (ind.CensusDate.Overlaps(CensusDate.IRELANDCENSUS1911) && ind.CensusCountry == Countries.IRELAND)
            //    return $"&census_code=0IRL&ref1=&ref2=&ref3=&ref4=&ref5=";
            if (ind.CensusDate.Overlaps(CensusDate.EWCENSUS1911) && Countries.IsEnglandWales(ind.CensusCountry))
                return $"&census_code=0ENG&ref1={censusRef.Piece}&ref2={censusRef.Schedule}&ref3=&ref4=&ref5=";
            if (ind.CensusDate.Overlaps(CensusDate.USCENSUS1880) && ind.CensusCountry == Countries.UNITED_STATES)
                return $"&census_code=USA1&ref1={censusRef.Roll}&ref2={censusRef.Page}&ref3=&ref4=&ref5=";
            if (ind.CensusDate.Overlaps(CensusDate.USCENSUS1940) && ind.CensusCountry == Countries.UNITED_STATES)
                return $"&census_code=USA4&ref1={censusRef.Roll}&ref2={censusRef.ED}&ref3={censusRef.Page}&ref4=&ref5=";
            return string.Empty;
        }

        static string GetLCDescendantStatus(CensusIndividual ind)
        {
            switch(ind.RelationType)
            {
                case Individual.DIRECT:
                    return $"Direct+ancestor&descent={ind.Ahnentafel}";
                case Individual.BLOOD: 
                case Individual.DESCENDANT:
                    return "Blood+relative&descent=";
                case Individual.MARRIAGE:
                case Individual.MARRIEDTODB:
                    return "Marriage&descent=";
                case Individual.UNKNOWN:
                case Individual.UNSET:
                case Individual.LINKED:
                default:
                    return "Unknown&descent=";
            }
        }
    }

    class CookieAwareWebClient : WebClient
    {
        CookieContainer _cookieJar = new CookieContainer();

        internal CookieAwareWebClient(CookieCollection cookies)
        {
            _cookieJar.Add(cookies);
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            if (request is HttpWebRequest webRequest)
            {
                webRequest.CookieContainer = _cookieJar;
            }
            return request;
        }
    }
}
