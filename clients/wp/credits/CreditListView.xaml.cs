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
using System.Windows.Navigation;
using Credits.Effects;
using Credits.Extensions;
using Credits.Converters;
using System.Threading;
using System.Xml.Linq;
using System.IO;
using System.IO.IsolatedStorage;

namespace Credits
{
    public partial class CreditListView : AnimatedBasePage
    {
        DataManager m_dataManager = null;
        string m_creditType;

        // Constructor
        public CreditListView()
            : base()
        {
            InitializeComponent();
            AnimationContext = LayoutRoot;

            m_dataManager = new DataManager();
            m_dataManager.LoadCompleted += new EventHandler<LoadContentEventArgs>(OnLoadCompleted);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            m_creditType = this.NavigationContext.QueryString["Type"];

            m_dataManager.LoadCollectionContent("http://lab3d.ru/credit_broker/info.xml", 1);
        }

        void OnListBoxLoaded(object sender, RoutedEventArgs e)
        {
            ListBox lb = (sender as ListBox);
            lb.SelectedIndex = -1;
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
            CreditList.SelectedIndex = -1;
            base.AnimationsComplete(animationType);
        }

        private void OnLoadCompleted(object sender, LoadContentEventArgs args)
        {
            XElement data = args.Data;
            foreach (var elem in data.Descendants(m_creditType))
            {
                Credit credit = new Credit
                {
                    Name = HtmlToXamlConverter.ConvertHtmlToXml(elem.Element("name").Value).Value,
                    Id = elem.Element("id").Value,
                    Link = elem.Element("link").Value,
                    Bank = elem.Element("bank").Element("name").Value,
                };

                this.CreditList.Items.Add(credit);
            }
        }
    }
}