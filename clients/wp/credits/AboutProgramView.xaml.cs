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
using Microsoft.Phone.Controls;
using System.Windows.Navigation;
using System.IO;
using System.IO.IsolatedStorage;
using Credits.Effects;
using System.Xml.Linq;
using System.Windows.Media.Imaging;
using Microsoft.Phone.Tasks;


namespace Credits
{
    public partial class AboutProgramView : AnimatedBasePage
    {
        public AboutProgramView()
        {
            InitializeComponent();

            AnimationContext = LayoutRoot;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.New)
            {
                XElement article = XElement.Load("Resources/about_program.xml");

                XElement Credits = article.Element("Credits");
                CreditsAbout.Text = Credits.Element("text").Value;

                CreditsCopyright.Text = "\u00A9" + CreditsCopyright.Text;
                CreditsText.Content = "\"" + CreditsText.Content + "\"";

                Version.Text = AppResources.Version + ": " + article.Element("version").Value;

                XElement developer = article.Element("developer");
                Stream stream = Application.GetResourceStream(new Uri(developer.Element("logo").Attribute("src").Value, UriKind.Relative)).Stream;

                BitmapImage bmp = new BitmapImage();
                bmp.SetSource(stream);
                stream.Close();

                DeveloperLogo.Source = bmp;
                DeveloperLogo.Stretch = Stretch.Uniform;
            }
        }

        protected override AnimatorHelperBase GetAnimation(AnimationType animationType, Uri toOrFrom)
        {
            if (toOrFrom != null)
            {
                if (animationType == AnimationType.NavigateForwardOut)
                {
                    return new TurnstileFeatherForwardOutAnimator() { RootElement = LayoutRoot };
                }
                else if (animationType == AnimationType.NavigateForwardIn)
                {
                    return new TurnstileFeatherForwardInAnimator() { RootElement = LayoutRoot };
                }
                else if (animationType == AnimationType.NavigateBackwardIn)
                {
                    return new TurnstileFeatherBackwardInAnimator() { RootElement = LayoutRoot };
                }
                else
                {
                    return new TurnstileFeatherBackwardOutAnimator() { RootElement = LayoutRoot };
                }
            }

            return base.GetAnimation(animationType, toOrFrom);
        }

        private void OnHyperlinkClick(object sender, RoutedEventArgs e)
        {
            HyperlinkButton btn = sender as HyperlinkButton;
            if (btn != null)
            {
                string url = String.Empty;

                if (btn.Name == "CreditsText")
                {
                    url = "http://www.Credits.ru";
                }
                else if (btn.Name == "DeveloperCite")
                {
                    url = "http://www.teleca.ru";
                }

                if (MessageBox.Show(AppResources.BrowserQuery, AppResources.Attention, MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    WebBrowserTask browserTask = new WebBrowserTask();
                    browserTask.Uri = new Uri(url, UriKind.Absolute);
                    browserTask.Show();
                }
            }
        }
    }
}