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
            string sum = this.NavigationContext.QueryString["Sum"];
            string period = this.NavigationContext.QueryString["Period"];

            String link = "http://api.uslugi.yandex.ru/1.0/banks/" + m_creditType + "s/search?key=Lc1XBwACAAABOfd5Qj7Zg8EfLLjqDFu9esRUVkAFCoWX3g&region=moscow&currency=RUB&sum=" + sum + "&period=" + period + " months";
            m_dataManager.LoadCollectionContent(link, 1);
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
            if (data != null)
            {
                foreach (var elem in data.Descendants(m_creditType))
                {
                    Credit credit = new Credit
                    {
                        Name = HtmlToXamlConverter.ConvertHtmlToXml(elem.Element("name").Value).Value,
                        Id = elem.Element("id").Value,
                        Link = elem.Element("link").Value,
                        Bank = elem.Element("bank").Element("name").Value,
                        Rates = InitializeRates(elem.Element("short"))
                    };

                    this.CreditList.Items.Add(credit);
                }
            }
        }

        private List<Rate> InitializeRates(XElement rates)
        {
            List<Rate> res = new List<Rate>();
            if (rates != null)
            {
                foreach (var node in rates.Elements())
                {
                    Rate rate = new Rate
                    {
                        Currency = node.Element("currency") == null ? String.Empty : node.Element("currency").Value,
                        MinValue = node.Element("min-value") == null ? 0 : Convert.ToDouble(node.Element("min-value").Value),
                        MaxValue = node.Element("max-value") == null ? 0 : Convert.ToDouble(node.Element("max-value").Value),
                        BaseCoefficient = node.Element("base-coefficient") == null ? String.Empty : node.Element("base-coefficient").Value,
                        MinInitialInstalment = node.Element("min-initial-instalment") == null ? 0 : Convert.ToDouble(node.Element("min-initial-instalment").Value),
                        MaxInitialInstalment = node.Element("max-initial-instalment") == null ? 0 : Convert.ToDouble(node.Element("max-initial-instalment").Value),
                        MinPeriod = node.Element("min-period") == null ? 0 : Convert.ToInt16(node.Element("min-period").Value),
                        MaxPeriod = node.Element("max-period") == null ? 0 : Convert.ToInt16(node.Element("max-period").Value),
                        MinSum = node.Element("min-sum") == null ? 0 : Convert.ToInt32(node.Element("min-sum").Value),
                        MaxSum = node.Element("max-sum") == null ? 0 : Convert.ToInt32(node.Element("max-sum").Value),
                        StateProgramDiscount = node.Element("state-program-discount") == null ? 0 : Convert.ToDouble(node.Element("state-program-discount").Value)
                    };

                    res.Add(rate);
                }
            }

            return res;
        }
    }
}