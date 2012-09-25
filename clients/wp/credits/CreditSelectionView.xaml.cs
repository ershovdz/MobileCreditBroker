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
using System.Threading;
using System.Xml.Linq;
using System.IO;
using System.IO.IsolatedStorage;

namespace Credits
{
    public partial class CreditSelectionView : CreditsBaseView
    {
        // Constructor
        public CreditSelectionView()
            : base()
        {
            InitializeComponent();
            AnimationContext = LayoutRoot;
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
    }
}