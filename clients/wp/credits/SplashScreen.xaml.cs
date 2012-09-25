using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Windows.Controls.Primitives;


namespace Credits
{
    public partial class SplashScreen : UserControl
    {
        private static Popup m_popup;

        public SplashScreen()
        {
            InitializeComponent();

            m_popup = new Popup();
            m_popup.Child = this;
        }

        public void Show()
        {
            m_popup.IsOpen = true;
        }

        public void Hide()
        {
            m_popup.IsOpen = false;
        }
    }
}
