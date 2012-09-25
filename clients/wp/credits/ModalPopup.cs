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
using System.Windows.Controls.Primitives;
using Credits.Extensions;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;


namespace Credits
{
    public class ModalPopup : UserControl
    {
        private Popup m_popup;

        public ModalPopup()
        {
            m_popup = new Popup();
            m_popup.Child = this;
        }

        public void Show()
        {
            DisableCurrentPage();
            m_popup.IsOpen = true;

            this.Margin = new Thickness(0, SystemTray.IsVisible ? 20 : 0, 0, 0);
        }

        public void Hide()
        {
            EnableCurrentPage();

            m_popup.IsOpen = false;
        }

        public bool IsShown()
        {
            return m_popup.IsOpen == true;
        }

        protected void EnableCurrentPage()
        {
            var page = Application.Current.GetActivePage();
            page.IsEnabled = true;

            SetApplicationBarState(page, true);
        }

        void DisableCurrentPage()
        {
            var page = Application.Current.GetActivePage();
            page.IsEnabled = false;

            SetApplicationBarState(page, false);
        }

        void SetApplicationBarState(PhoneApplicationPage page, bool isEnabled)
        {
            foreach (var button in page.ApplicationBar.Buttons)
            {
                (button as ApplicationBarIconButton).IsEnabled = isEnabled;
            }
            page.ApplicationBar.IsMenuEnabled = isEnabled;
        }
    }
}
