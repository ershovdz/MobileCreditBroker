using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Linq;
using System.Windows.Media.Imaging;
using System.Text;
using System.Net.NetworkInformation;
using System.IO.IsolatedStorage;

namespace Credits
{
    public partial class Informer : UserControl
    {
        public static readonly DependencyProperty TitleTextProperty =
      DependencyProperty.Register("TitleText", typeof(String), typeof(Informer), null);

        public Informer()
        {
            InitializeComponent();
        }

        public String TitleText
        {
            get 
            { 
                return base.GetValue(TitleTextProperty) as String; 
            }

            set 
            { 
                base.SetValue(TitleTextProperty, value);

                if (TitleText != null && TitleText.Length != 0)
                {
                    Text.Text = TitleText;
                }
            }
        }
    }
}
