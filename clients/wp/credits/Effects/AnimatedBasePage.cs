using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Credits.Extensions;
using System.Diagnostics;
using System.Windows.Controls;
using Microsoft.Phone.Shell;
using System.Windows.Input;
using System.ComponentModel;

namespace Credits.Effects
{
    public class AnimatedBasePage : PhoneApplicationPage
    {
        private static readonly Uri ExternalUri = new Uri(@"app://external/");

        public static readonly DependencyProperty AnimationContextProperty = DependencyProperty.Register("AnimationContext", typeof(FrameworkElement), typeof(AnimatedBasePage), new PropertyMetadata(null));
        public FrameworkElement AnimationContext
        {
            get
            {
                return (FrameworkElement)GetValue(AnimationContextProperty);
            }
            set
            {
                SetValue(AnimationContextProperty, value);
            }
        }

        private static Uri m_fromUri;

        protected bool m_isAnimating;
        private static bool m_isNavigating;
        protected bool m_needsOutroAnimation;
        protected Uri m_nextUri;
        protected Uri m_arrivedFromUri;
        private AnimationType m_currentAnimationType;
        protected NavigationMode? m_currentNavigationMode;
        protected bool m_isActive;
        protected bool m_isForwardNavigation;
        private bool m_loadingAndAnimatingIn;
        protected static bool m_isSystemTrayVisible = false;
        private Point m_manipulationStartPoint;

        public AnimatedBasePage()
            : base()
        {
            m_isActive = true;

            m_isForwardNavigation = true;
        }

        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            base.OnBackKeyPress(e);

            if (m_isNavigating)
            {
                e.Cancel = true;
                return;
            }

            if (!CanAnimate())
                return;

            if (m_isAnimating)
            {
                e.Cancel = true;
                return;
            }

            if (m_loadingAndAnimatingIn)
            {
                e.Cancel = true;
                return;
            }

            if (!this.NavigationService.CanGoBack)
                return;

            if (!IsPopupOpen())
            {
                m_isNavigating = true;
                e.Cancel = true;
                m_needsOutroAnimation = false;
                m_currentAnimationType = AnimationType.NavigateBackwardOut;
                m_currentNavigationMode = NavigationMode.Back;

                RunAnimation();
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            m_nextUri = e.Uri;

            base.OnNavigatingFrom(e);

            if (m_isAnimating)
            {
                e.Cancel = true;
                return;
            }

            if (m_loadingAndAnimatingIn)
            {
                e.Cancel = true;
                return;
            }

            m_fromUri = this.NavigationService.CurrentSource;

            if (m_needsOutroAnimation)
            {
                m_needsOutroAnimation = false;

                if (!CanAnimate())
                    return;

                if (m_isNavigating)
                {
                    e.Cancel = true;
                    return;
                }

                if (!this.NavigationService.CanGoBack && e.NavigationMode == NavigationMode.Back)
                    return;

                if (IsPopupOpen())
                {
                    return;
                }

                e.Cancel = true;
                m_nextUri = e.Uri;

                switch (e.NavigationMode)
                {
                    case NavigationMode.New:
                        m_currentAnimationType = AnimationType.NavigateForwardOut;
                        break;

                    case NavigationMode.Back:
                        m_currentAnimationType = AnimationType.NavigateBackwardOut;
                        break;

                    case NavigationMode.Forward:
                        m_currentAnimationType = AnimationType.NavigateForwardOut;
                        break;
                }
                m_currentNavigationMode = e.NavigationMode;

                if (e.Uri != ExternalUri)
                    RunAnimation();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            SystemTray.IsVisible = m_isSystemTrayVisible;

            m_currentNavigationMode = null;

            //Debug.WriteLine("OnNavigatedTo: {0}", this);

            if (m_nextUri != ExternalUri)
            {
                //this.InvokeOnLayoutUpdated(() => OnLayoutUpdated(this, null));

                m_loadingAndAnimatingIn = true;
                this.Loaded += new RoutedEventHandler(AnimatedBasePage_Loaded);

                if (AnimationContext != null)
                    AnimationContext.Opacity = 0;
            }
            else
            {
                this.InvokeOnLayoutUpdated(() => OnLayoutUpdated(this, null));
            }

            m_needsOutroAnimation = true;
        }

        void AnimatedBasePage_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= new RoutedEventHandler(AnimatedBasePage_Loaded);
            OnLayoutUpdated(this, null);

            ManipulationStarted += new EventHandler<ManipulationStartedEventArgs>(OnManipulationStarted);
            ManipulationCompleted += new EventHandler<ManipulationCompletedEventArgs>(OnManipulationCompleted);
        }

        void OnLayoutUpdated(object sender, EventArgs e)
        {
            //Debug.WriteLine("OnLayoutUpdated: {0}", this);

            if (m_isForwardNavigation)
            {
                m_currentAnimationType = AnimationType.NavigateForwardIn;
                m_arrivedFromUri = m_fromUri != null ? new Uri(m_fromUri.OriginalString, UriKind.Relative) : null;
            }
            else
            {
                m_currentAnimationType = AnimationType.NavigateBackwardIn;
            }

            if (CanAnimate())
            {
                RunAnimation();
            }
            else
            {
                if (AnimationContext != null)
                    AnimationContext.Opacity = 1;

                OnTransitionAnimationCompleted();
            }

            //OnFirstLayoutUpdated(!_isForwardNavigation, _fromUri);

            if (m_isForwardNavigation)
                m_isForwardNavigation = false;
        }

        protected virtual void OnFirstLayoutUpdated(bool isBackNavigation, Uri from) { }

        protected void RunAnimation()
        {
            m_isAnimating = true;

            AnimatorHelperBase animation = null;

            switch (m_currentAnimationType)
            {
                case AnimationType.NavigateForwardIn:
                    animation = GetAnimation(m_currentAnimationType, m_fromUri);
                    break;
                case AnimationType.NavigateBackwardOut:
                    animation = GetAnimation(m_currentAnimationType, m_arrivedFromUri);
                    break;
                default:
                    animation = GetAnimation(m_currentAnimationType, m_nextUri);
                    break;
            }

            Dispatcher.BeginInvoke(() =>
            {
                AnimatorHelperBase transitionAnimation;

                if (animation == null)
                {
                    AnimationContext.Opacity = 1;
                    OnTransitionAnimationCompleted();
                }
                else
                {
                    transitionAnimation = animation;
                    AnimationContext.Opacity = 1;
                    transitionAnimation.Begin(OnTransitionAnimationCompleted);
                }

                //Debug.WriteLine("{0} - {1} - {2} - {3}", this, _currentAnimationType, _currentAnimationType == AnimationType.NavigateForwardOut || _currentAnimationType == AnimationType.NavigateBackwardIn ? _nextUri : _fromUri, transitionAnimation);
            });
        }

        protected virtual AnimatorHelperBase GetAnimation(AnimationType animationType, Uri toOrFrom)
        {
            return null;
        }

        private bool CanAnimate()
        {
            return (m_isActive /*&& !m_isNavigating*/ && AnimationContext != null);
        }

        void OnTransitionAnimationCompleted()
        {
            m_isAnimating = false;
            m_loadingAndAnimatingIn = false;

            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    switch (m_currentNavigationMode)
                    {
                        case NavigationMode.Forward:
                            Application.Current.GoForward();
                            break;

                        case NavigationMode.Back:
                            Application.Current.GoBack();
                            break;

                        case NavigationMode.New:
                            Application.Current.Navigate(m_nextUri);
                            break;
                    }
                    m_isNavigating = false;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("OnTransitionAnimationCompleted Exception on {0}: {1}", this, ex);
            }

            AnimationsComplete(m_currentAnimationType);
        }

        protected virtual void AnimationsComplete(AnimationType animationType) { }

        protected virtual bool IsPopupOpen()
        {
            return false;
        }

        public void CancelAnimation()
        {
            m_isActive = false;
        }

        public void ResumeAnimation()
        {
            m_isActive = true;
        }

        protected virtual void OnManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            m_manipulationStartPoint = e.ManipulationOrigin;
        }

        protected virtual void OnManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            if (Math.Abs(m_manipulationStartPoint.Y) < 100 && Math.Abs(e.TotalManipulation.Translation.Y) > 10)
            {
                m_isSystemTrayVisible = (e.ManipulationOrigin.Y >= 0);
                SystemTray.IsVisible = m_isSystemTrayVisible;
            }
        }

        protected virtual void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
        }
    }
}
