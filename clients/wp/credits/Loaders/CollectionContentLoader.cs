using System;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.NetworkInformation;


namespace Credits.Loaders
{
    public class CollectionContentLoader : IContentLoader
    {
        public event ContentLoadAsyncHandler LoadCompleted;
        WebClient m_downloader = new WebClient();
        private LoadContext m_context;

        public CollectionContentLoader() 
        {
            m_downloader.OpenReadCompleted += OnLoadAsyncCompleted;
        }

        public void LoadAsync(LoadContext context)
        {
            m_context = context;

            if (NetworkInterface.GetIsNetworkAvailable())
            {
                if (m_context != null && m_context.Url != "")
                {
                    Uri serviceUri = new Uri(m_context.Url + "&" + DateTime.Now.ToString());
                    m_downloader.OpenReadAsync(serviceUri);
                }
                else
                {
                    ContentLoadAsyncEventArgs args = new ContentLoadAsyncEventArgs();
                    args.m_error = new Exception("Url is invalid!");

                    if (this.LoadCompleted != null)
                    {
                        this.LoadCompleted(this, args);
                    }
                }
            }
            else
            {
                ContentLoadAsyncEventArgs args = new ContentLoadAsyncEventArgs();
                args.m_error = new Exception("No network available");

                if (this.LoadCompleted != null)
                {
                    this.LoadCompleted(this, args);
                }
            }
        }

        public void OnLoadAsyncCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            ContentLoadAsyncEventArgs args = new ContentLoadAsyncEventArgs();
            if (null == e.Error)
            {
                try
                {
                    args.m_data = XElement.Load(e.Result);
                }
                catch (Exception /*ex*/) {}
            }
            else
            {
                if (e.Error.Message == "WebException")
                {
                    LoadAsync(m_context);
                    return;
                }
                else
                {
                    args.m_error = e.Error;
                }
            }

            if (this.LoadCompleted != null)
            {
                this.LoadCompleted(this, args);
            }
        }

        public void Cancel()
        {
            if(m_downloader != null)
            {
                m_downloader.CancelAsync();
            }
        }
    }
}
