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
    public partial class StartView : CreditsBaseView
    {
        SplashScreen m_splash = new SplashScreen();

        // Constructor
        public StartView()
            : base()
        {
            InitializeComponent();

            this.Loaded += new RoutedEventHandler(OnLoaded);

            m_splash.Show();
            ApplicationBar.IsVisible = false;

            AnimationContext = LayoutRoot;
        }

        // Load data for the ViewModel Items
        protected void OnLoaded(object sender, RoutedEventArgs e)
        {
            PhoneApplicationFrame parent = Parent as PhoneApplicationFrame;
            this.DataContext = parent.DataContext;
        }

        void OnListBoxLoaded(object sender, RoutedEventArgs e)
        {
            (sender as ListBox).SelectedIndex = -1;

            if (this.m_isForwardNavigation)
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    MessageBox.Show(AppResources.OfflineStartupText, AppResources.Attention, MessageBoxButton.OK);
                }

                m_splash.Hide();
                ApplicationBar.IsVisible = true;
            }
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox lb = (sender as ListBox);
            if (lb.SelectedIndex == 0)
            {
                NavigationService.Navigate(new Uri("/CalculatorView.xaml", UriKind.Relative));
            }
            else if (lb.SelectedIndex == 1)
            {
                NavigationService.Navigate(new Uri("/CreditSelectionView.xaml", UriKind.Relative));
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

        protected override void AnimationsComplete(AnimationType animationType)
        {
            switch (animationType)
            {
                case AnimationType.NavigateForwardIn:
                    break;
                case AnimationType.NavigateBackwardIn:
                    CategoriesList.SelectedIndex = -1;
                    break;
            }

            base.AnimationsComplete(animationType);
        }
    }
}