using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Net.NetworkInformation;
using System.Xml.Linq;
using System.IO;
using System.IO.IsolatedStorage;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using Microsoft.Phone.Shell;
using System.ComponentModel;
using System.Windows.Threading;
using System.Collections;
using System.Threading;
using Credits.Loaders;
using System.Text.RegularExpressions;


namespace Credits
{
    public class LoadContentEventArgs : EventArgs
    {
        public LoadContentEventArgs(Exception error, object data, int progress)
        { Error = error; Data = data as XElement; Progress = progress; }
        public Exception Error { get; set; }
        public int Progress { get; set; }
        public XElement Data { get; set; }
    }

    public class DataManager
    {
        public event EventHandler<LoadContentEventArgs> LoadProgress;
        public event EventHandler<LoadContentEventArgs> LoadCompleted;
        public event EventHandler<LoadContentEventArgs> UpdateProgress;
        public event EventHandler<LoadContentEventArgs> UpdateCompleted;

        private IContentLoader m_loader;
        private ManualResetEvent m_event;
        private LoadContentEventArgs m_args = new LoadContentEventArgs(null, null, 0);
        private bool m_loadInProgress = false;
        private bool m_loadCancelled = false;

        public void LoadCollectionContent(string link, int page)
        {
            if (m_loadInProgress)
            {
                if (this.LoadCompleted != null)
                {
                    m_args.Error = new Exception("Already in use");
                    LoadCompleted(this, m_args);
                }

                return;
            }

            m_loadInProgress = true;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                link += "?page=" + Convert.ToString(page);
                XElement res = LoadFromLocal(link);
                if (res == null)
                {
                    if (m_event == null)
                    {
                        m_event = new ManualResetEvent(false);
                    }
                    m_event.Reset();
                    m_args.Data = null;
                    m_args.Error = null;

                    LoadCollectionFromRemote(link);
                    if (m_args.Error == null)
                    {
                        m_event.WaitOne();

                        if (m_args.Error == null && m_args.Data.Attribute("last_update_time") == null)
                        {
                            m_args.Data.Add(new XAttribute("last_update_time", DateTime.Now.ToString()));
                            SaveContent(link, m_args.Data);
                        }
                    }

                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        m_loadInProgress = false;
                        if (this.LoadCompleted != null)
                        {
                            LoadCompleted(this, m_args);
                        }
                    });

                    m_loadInProgress = false;
                }
                else
                {
                    m_loadInProgress = false;

                    m_args.Error = null;
                    m_args.Data = res;

                    SaveContent(link, m_args.Data);
                    //                    ThreadPool.QueueUserWorkItem((state) =>
                    //                    {
                    //                        Thread.Sleep(TimeSpan.FromSeconds(0.25));
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (this.LoadCompleted != null)
                        {
                            LoadCompleted(this, m_args);
                        }
                    });
                }
            });
        }

        public void UpdateCollectionContent(XElement digest)
        {
            if (m_loadInProgress)
            {
                if (this.UpdateCompleted != null)
                {
                    m_args.Error = new Exception("Already in use");
                    UpdateCompleted(this, m_args);
                }

                return;
            }

            m_loadInProgress = true;

            m_loadCancelled = false;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                if (m_event == null)
                {
                    m_event = new ManualResetEvent(false);
                }

                string[] files = null;
                IsolatedStorageFile myIsolatedStorage = IsolatedStorageFile.GetUserStoreForApplication();
                string localLink = GetLocalLink(digest.Attribute("link").Value);
                string localDir = localLink.Substring(0, localLink.LastIndexOf("/"));
                if (myIsolatedStorage.DirectoryExists(localDir))
                {
                    files = myIsolatedStorage.GetFileNames(localDir + "/*");
                }
                else
                {
                    files = new string[1];
                }

                for (int i = 0; i < files.Count() && !m_loadCancelled; i++)
                {
                    m_event.Reset();
                    m_args.Error = null;
                    m_args.Progress = i + 1;
                    m_args.Data = null;

                    string link = digest.Attribute("link").Value + "?page=" + Convert.ToString(i + 1);
                    LoadCollectionFromRemote(link);

                    m_event.WaitOne();

                    LoadContentEventArgs tmpArgs = new LoadContentEventArgs(m_args.Error, m_args.Data, m_args.Progress);
                    Deployment.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (this.UpdateProgress != null)
                        {
                            UpdateProgress(this, tmpArgs);
                        }
                    });

                    if (m_args.Error != null)
                    {
                        break;
                    }

                    m_args.Data.Add(new XAttribute("last_update_time", DateTime.Now.ToString()));
                    SaveContent(link, m_args.Data);
                }

                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (this.UpdateCompleted != null)
                    {
                        UpdateCompleted(this, m_args);
                    }
                });

                m_loadInProgress = false;
            });
        }

        public XElement LoadFromLocal(string link)
        {
            XElement res = null;
            try
            {
                IsolatedStorageFile myIsolatedStorage = IsolatedStorageFile.GetUserStoreForApplication();
                string localLink = GetLocalLink(link);
                if (myIsolatedStorage.FileExists(localLink))
                {
                    IsolatedStorageFileStream isoStream = new IsolatedStorageFileStream(localLink, FileMode.Open, myIsolatedStorage);
                    res = XElement.Load(isoStream);
                    isoStream.Close();
                }
                else
                {
                    string resourceLink = GetResourceLink(link);
                    res = XElement.Load(resourceLink);
                }
            }
            catch (Exception /*ex*/) {}

            return res;
        }

        public void SaveContent(string link, XElement element)
        {
            try
            {
                IsolatedStorageFile myIsolatedStorage = IsolatedStorageFile.GetUserStoreForApplication();
                string localLink = GetLocalLink(link);
                string localDir = localLink.Substring(0, localLink.LastIndexOf("/"));
                if (!myIsolatedStorage.DirectoryExists(localDir))
                {
                    myIsolatedStorage.CreateDirectory(localDir);
                }

                string[] files = myIsolatedStorage.GetFileNames(localDir + "/*");

                IsolatedStorageFileStream isoStream = null;
                if (myIsolatedStorage.FileExists(localLink))
                {
                    myIsolatedStorage.DeleteFile(localLink);
                }

                isoStream = new IsolatedStorageFileStream(localLink, FileMode.OpenOrCreate, myIsolatedStorage);
                element.Save(isoStream);
                isoStream.Close();
            }
            catch (Exception /*ex*/) { /* [YE] Isolated storage is very capricious*/ }
        }

        public void CancelOperation()
        {
            m_loadCancelled = true;
            m_loadInProgress = false;
            if (m_loader != null)
            {
                m_loader.Cancel();
            }
            LoadCompleted = null;
            LoadProgress = null;
        }

        private void LoadCollectionFromRemote(string url)
        {
            m_loader = new CollectionContentLoader();
            m_loader.LoadCompleted += OnLoadCollectionCompleted;
            m_loader.LoadAsync(new LoadContext() { Url = url });
        }

        private void OnLoadCollectionCompleted(object sender, ContentLoadAsyncEventArgs e)
        {
            m_loader.LoadCompleted -= OnLoadCollectionCompleted;

            m_args.Error = e.m_error;
            if (e.m_error == null)
            {
                m_args.Data = e.m_data as XElement;
            }

            m_event.Set();
        }

        private string GetLocalLink(string link)
        {
            string path = link;
            if (!IsLocal(link))
            {
                int startPos = link.LastIndexOf("/");
                string name = link.Substring(startPos + 1);
                Regex regEx = new Regex("(.*).xml");
                MatchCollection m = regEx.Matches(name);

                if (m.Count == 0)
                {
                    return Constants.m_cacheDir + Constants.m_globalSeparator + "tmp";
                }

                string localName = m[0].Groups[1].Value;
                string dir = Constants.m_cacheDir + Constants.m_globalSeparator + localName;

                Regex regEx1 = new Regex("page=(.*)");
                m = regEx1.Matches(link);
                int page = 1;
                if (m.Count != 0)
                {
                    string val = m[0].Groups[1].Value;
                    int index = val.IndexOf("&");
                    page = Convert.ToInt16(index < 0 ? val : val.Substring(0, index));
                }

                path = dir + "/" + String.Format("page={0}", page);
            }
            return path;
        }

        private string GetResourceLink(string link)
        {
            string path = link;
            if (!IsLocal(link))
            {
                int startPos = link.LastIndexOf("lab3d.ru/credit_broker/") == -1 ? link.LastIndexOf("/") : link.LastIndexOf("lab3d.ru/credit_broker/") + 22;
                string name = link.Substring(startPos + 1);
                Regex regEx = new Regex("(.*).xml");
                MatchCollection m = regEx.Matches(name);

                if (m.Count == 0)
                {
                    return Constants.m_resourceDir + Constants.m_globalSeparator + "tmp";
                }

                string localName = m[0].Groups[1].Value + (link.IndexOf(".xml") == -1 ? ".html" : ".xml");
                path = Constants.m_resourceDir + Constants.m_globalSeparator + localName;
            }

            return path;
        }

        private bool IsLocal(string link)
        {
            return link.IndexOf(Constants.m_cacheDir) >= 0;
        }
    }
}
