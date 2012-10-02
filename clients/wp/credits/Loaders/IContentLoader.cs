using System;
using System.Threading;

namespace Credits
{
    public class LoadContext
    {
        public string Url
        {
            get;
            set;
        }

        public bool Forced
        {
            get;
            set;
        }
    }

    public delegate void ContentLoadAsyncHandler(object sender, ContentLoadAsyncEventArgs e);
    public class ContentLoadAsyncEventArgs : EventArgs
    {
        public EventWaitHandle m_waitHandle;
        public Exception m_error;
        public object m_data;
    }

    public interface IContentLoader
    {
        event ContentLoadAsyncHandler LoadCompleted;
        void LoadAsync(LoadContext context);
        void Cancel();
    }
}
