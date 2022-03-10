﻿/************************************************************************
 * Copyright (C) 2015 by themirage <mirage@secure-mail.biz>             *
 *                                                                      *
 * This program is free software: you can redistribute it and/or modify *
 * it under the terms of the GNU General Public License as published by *
 * the Free Software Foundation, either version 3 of the License, or    *
 * (at your option) any later version.                                  *
 *                                                                      *
 * This program is distributed in the hope that it will be useful,      *
 * but WITHOUT ANY WARRANTY; without even the implied warranty of       *
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the        *
 * GNU General Public License for more details.                         *
 *                                                                      *
 * You should have received a copy of the GNU General Public License    *
 * along with this program.  If not, see <http://www.gnu.org/licenses/> *
 ************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

// see Infinitechan.cs for explanation

namespace YChan
{
    internal class Fchan : Imageboard
    {
        public static string regThread = "boards.(4chan|4channel).org/[a-zA-Z0-9]*?/thread/[0-9]*";
        public static string regBoard = "boards.(4chan|4channel).org/[a-zA-Z0-9]*?/*$";

        public Fchan(string url, bool isBoard) : base(url, isBoard)
        {
            this.board = isBoard;
            this.imName = "4chan";
            if (!isBoard)
            {
                Match match = Regex.Match(url, @"boards.(4chan|4channel).org/[a-zA-Z0-9]*?/thread/\d*");
                this.URL = "http://" + match.Groups[0].Value;
                this.SaveTo = Properties.Settings.Default.path + "\\" + this.imName + "\\" + getURL().Split('/')[3] + "\\" + getURL().Split('/')[5];
            }
            else
            {
                this.URL = url;
                this.SaveTo = Properties.Settings.Default.path + "\\" + this.imName + "\\" + getURL().Split('/')[3];
            }
        }

        public new static bool urlIsThread(string url)
        {
            Regex urlMatcher = new Regex(regThread);
            return (urlMatcher.IsMatch(url));
        }

        public new static bool urlIsBoard(string url)
        {
            Regex urlMatcher = new Regex(regBoard);
            return (urlMatcher.IsMatch(url));
        }

        override protected FileInformation[] getLinks()
        {
            List<FileInformation> links = new List<FileInformation>();

            string boardNameSplit = getURL().Split('/')[3];
            string threadNumberSplit = getURL().Split('/')[5];

            string JSONUrl = "http://a.4cdn.org/" + boardNameSplit + "/thread/" + threadNumberSplit + ".json";
            string baseURL = "http://i.4cdn.org/" + boardNameSplit + "/";
            string str = "";
            XmlNodeList xmlName;
            XmlNodeList xmlTim;
            XmlNodeList xmlExt;
            XmlNodeList xmlMd5;

            try
            {
                string Content = new WebClient().DownloadString(JSONUrl);

                byte[] bytes = Encoding.ASCII.GetBytes(Content);
                using (var stream = new MemoryStream(bytes))
                {
                    var quotas = new XmlDictionaryReaderQuotas();
                    var jsonReader = JsonReaderWriterFactory.CreateJsonReader(stream, quotas);
                    var xml = XDocument.Load(jsonReader);
                    str = xml.ToString();
                }

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(str);
                if (boardNameSplit == "f")
                {
                    xmlName = xmlTim = doc.DocumentElement.SelectNodes("/root/posts/item/filename");
                }
                else
                {
                    xmlTim = doc.DocumentElement.SelectNodes("/root/posts/item/tim");
                    xmlName = doc.DocumentElement.SelectNodes("/root/posts/item/filename");
                }  

                xmlExt = doc.DocumentElement.SelectNodes("/root/posts/item/ext");
                xmlMd5 = doc.DocumentElement.SelectNodes("/root/posts/item/md5");
                for (int i = 0; i < xmlExt.Count; i++)
                {
                    links.Add(new FileInformation(baseURL + xmlTim[i].InnerText + xmlExt[i].InnerText, 
                        xmlName[i].InnerText + xmlExt[i].InnerText, xmlMd5[i].InnerText));
                }
            }
            catch
            {
                throw;
            }
            return links.ToArray();
        }

        override public string[] getThreads()
        {
            string URL = "http://a.4cdn.org/" + getURL().Split('/')[3] + "/catalog.json";
            List<string> Res = new List<string>();
            string str = "";
            XmlNodeList tNa;
            XmlNodeList tNo;

            string boardNameSplit = getURL().Split('/')[3];

            try
            {
                string json = new WebClient().DownloadString(URL);
                byte[] bytes = Encoding.ASCII.GetBytes(json);
                using (var stream = new MemoryStream(bytes))
                {
                    var quotas = new XmlDictionaryReaderQuotas();
                    var jsonReader = JsonReaderWriterFactory.CreateJsonReader(stream, quotas);
                    var xml = XDocument.Load(jsonReader);
                    str = xml.ToString();
                }

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(str);
                tNo = doc.DocumentElement.SelectNodes("/root/item/threads/item/no");
                tNa = doc.DocumentElement.SelectNodes("/root/item/threads/item/semantic_url");
                for (int i = 0; i < tNo.Count; i++)
                {
                    Res.Add("http://boards.4chan.org/" + boardNameSplit + "/thread/" + tNo[i].InnerText + "/" + tNa[i].InnerText);
                }
            }
            catch (WebException webEx)
            {
#if DEBUG
                MessageBox.Show("Connection Error: " + webEx.Message);
#endif
            }
            return Res.ToArray();
        }

        override public void download()
        {
            try
            {
                if (!Directory.Exists(this.SaveTo))
                    Directory.CreateDirectory(this.SaveTo);

                if (Properties.Settings.Default.loadHTML)
                    downloadHTMLPage();

                FileInformation[] URLs = getLinks();

                for (int y = 0; y < URLs.Length; y++)
                    General.DownloadToDir(URLs[y], this.SaveTo);
            }
            catch (WebException webEx)
            {
                if (webEx.Status == WebExceptionStatus.ProtocolError)
                    this.Gone = true;
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(ex.Message, "No Permission to access folder");
                throw;
            }
        }

        private void downloadHTMLPage()
        {
            List<string> thumbs = new List<string>();
            List<string> duplicateFileName = new List<string>();
            string xmlString;
            string boardNameSplit = getURL().Split('/')[3];
            string threadNumberSplit = getURL().Split('/')[5];
            string baseURL1 = "//i.4cdn.org/" + boardNameSplit + "/";
            string baseURL2 = "//is2.4chan.org/" + boardNameSplit + "/";
            string JURL = "http://a.4cdn.org/" + boardNameSplit + "/thread/" + threadNumberSplit + ".json";
            XmlDocument doc = new XmlDocument();

            try
            {
                //Add a UserAgent to prevent 403
                WebClient web = new WebClient();
                web.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0";

                string htmlPage = web.DownloadString(this.getURL());

                //Prevent the html from being destroyed by the anti adblock script
                htmlPage = htmlPage.Replace("f=\"to\"", "f=\"penis\"");

                //Normalize urls
                htmlPage = htmlPage.Replace("http:" + baseURL1, baseURL1);
                htmlPage = htmlPage.Replace("http:" + baseURL2, baseURL2);

                string json = web.DownloadString(JURL);
                byte[] bytes = Encoding.ASCII.GetBytes(json);
                using (var stream = new MemoryStream(bytes))
                {
                    var quotas = new XmlDictionaryReaderQuotas();
                    var jsonReader = JsonReaderWriterFactory.CreateJsonReader(stream, quotas);
                    xmlString = XDocument.Load(jsonReader).ToString();
                }

                doc.LoadXml(xmlString);
                XmlNodeList xmlImageFileTimestamp = doc.DocumentElement.SelectNodes("/root/posts/item/tim");
                XmlNodeList xmlImageFileName = doc.DocumentElement.SelectNodes("/root/posts/item/filename");
                XmlNodeList xmlImageFileExtension = doc.DocumentElement.SelectNodes("/root/posts/item/ext");

                for (int i = 0; i < xmlImageFileExtension.Count; i++)
                {
                    string imageFileTime = xmlImageFileTimestamp[i].InnerText + xmlImageFileExtension[i].InnerText;
                    string imageFileName = xmlImageFileName[i].InnerText + xmlImageFileExtension[i].InnerText;
                    string imageURL1 = baseURL1 + xmlImageFileTimestamp[i].InnerText + xmlImageFileExtension[i].InnerText;
                    string imageURL2 = baseURL2 + xmlImageFileTimestamp[i].InnerText + xmlImageFileExtension[i].InnerText;

                    while (duplicateFileName.Contains(imageFileName))
                    {
                        imageFileName = "_" + imageFileName;
                    }
                    duplicateFileName.Add(imageFileName);

                    htmlPage = htmlPage.Replace(imageURL1, imageFileName);
                    htmlPage = htmlPage.Replace(imageURL2, imageFileName);

                    //Save thumbs for files that need it
                    if (xmlImageFileExtension[i].InnerText == ".webm" /*|| xmlImageFileExtension[i].InnerText == ""*/)
                    {
                        string imageURL = "//t.4cdn.org/" + boardNameSplit + "/" + xmlImageFileTimestamp[i].InnerText + "s.jpg";
                        thumbs.Add("http:" + imageURL);

                        htmlPage = htmlPage.Replace(baseURL1 + xmlImageFileTimestamp[i].InnerText, "thumb/" + xmlImageFileTimestamp[i].InnerText);
                        htmlPage = htmlPage.Replace(baseURL2 + xmlImageFileTimestamp[i].InnerText, "thumb/" + xmlImageFileTimestamp[i].InnerText);
                    }
                    else
                    {
                        string thumbName = imageFileTime.Split('.')[0] + "s" + ".jpg";

                        htmlPage = htmlPage.Replace(baseURL1 + thumbName, System.Web.HttpUtility.UrlEncode(imageFileName));
                        htmlPage = htmlPage.Replace(baseURL2 + thumbName, System.Web.HttpUtility.UrlEncode(imageFileName));
                    }

                    htmlPage = htmlPage.Replace("/" + imageFileTime, imageFileName);
                }

                htmlPage = htmlPage.Replace("=\"//", "=\"http://");

                //Save thumbs for files that need it
                for (int i = 0; i < thumbs.Count; i++)
                    General.DownloadToDir(new FileInformation(thumbs[i]), this.SaveTo + "\\thumb");

                if (!string.IsNullOrWhiteSpace(htmlPage))
                    File.WriteAllText(this.SaveTo + "\\Thread.html", htmlPage);
            }
            catch
            {
                throw;
            }
        }

        public override void download(object callback)
        {
            Console.WriteLine("Downloading: " + URL);
            download();
        }
    }
}