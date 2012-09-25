using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace Credits
{
    public partial class PopupMessage : UserControl
    {
        public event EventHandler Completed;

        private DispatcherTimer m_timer;
        private static Popup m_popup;

        public PopupMessage()
        {
            InitializeComponent();

            m_popup = new Popup();
            m_popup.Child = this;

            m_timer = new DispatcherTimer();
            m_timer.Interval = new TimeSpan(0, 0, 3);
            m_timer.Tick += new EventHandler(closePopupMsg);            
        }

        public void Show()
        {
            var accentColor = Application.Current.Resources["PhoneAccentColor"];
            this.ContentPanel.Background = new SolidColorBrush((Color)accentColor);

            m_timer.Start();

            m_popup.IsOpen = true;
        }

        private void closePopupMsg(object o, EventArgs e)
        {
            m_popup.IsOpen = false;

            if (m_timer.IsEnabled)
            {
                m_timer.Stop();
            }

            if (Completed != null)
            {
                Completed(this, null);
            }
        }
    }
}
