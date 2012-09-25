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
using Microsoft.Phone.Controls;
using System.Collections.ObjectModel;
using Microsoft.Phone.Shell;
using System.Net.NetworkInformation;
using System.ComponentModel;
using System.Windows.Threading;
using System.Collections;
using Microsoft.Phone.Controls.Primitives;
using Credits.Effects;


namespace Credits
{
    public class CreditsBaseView : AnimatedBasePage
    {
        // Constructor
        public CreditsBaseView()
            : base()
        {
            BuildApplicationBar();
        }

        private void OnSettingsButtonClick(object sender, EventArgs args)
        {
            this.NavigationService.Navigate(new Uri("/SettingsView.xaml", UriKind.Relative));
        }

        private void OnSearchButtonClick(object sender, EventArgs args)
        {
            NavigationService.Navigate(new Uri("/SearchPage.xaml", UriKind.Relative));
        }

        protected virtual void BuildApplicationBar()
        {
            ApplicationBar = new ApplicationBar();

            ApplicationBarIconButton settingsBtn = new ApplicationBarIconButton(new Uri("/Images/settings.png", UriKind.Relative));
            settingsBtn.Text = AppResources.SettingsAppBarText;
            settingsBtn.Click += new EventHandler(OnSettingsButtonClick);
            ApplicationBar.Buttons.Add(settingsBtn);

            /*ApplicationBarIconButton searchBtn = new ApplicationBarIconButton(new Uri("/Images/search.png", UriKind.Relative));
            searchBtn.Text = AppResources.SearchAppBarText;
            searchBtn.Click += new EventHandler(OnSearchButtonClick);
            ApplicationBar.Buttons.Add(searchBtn);
             */
        }
    }
}