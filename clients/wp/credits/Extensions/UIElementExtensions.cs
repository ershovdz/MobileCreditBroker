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
using System.Collections.Generic;

namespace Credits
{
    public static class UIElementExtensions
    {
        #region Animation

        public static void Show(this UIElement element)
        {
            UIElementExtensions.Show(element, 0, null);
        }

        public static void Show(this UIElement element, int duration)
        {
            UIElementExtensions.Show(element, duration, null);
        }

        public static void Show(this UIElement element, int duration, Action<UIElement> callback)
        {
            element.Visibility = Visibility.Visible;
            UIElementExtensions.SetOpacity(element, duration, 1.0d, (e) =>
            {
                if (callback != null)
                {
                    callback(e);
                }
            });
        }

        public static void Hide(this UIElement element)
        {
            UIElementExtensions.Hide(element, 0, null);
        }

        public static void Hide(this UIElement element, int duration)
        {
            UIElementExtensions.Hide(element, duration, null);
        }

        public static void Hide(this UIElement element, int duration, Action<UIElement> callback)
        {
            UIElementExtensions.SetOpacity(element, duration, 0.0d, (e) =>
            {
                e.Visibility = Visibility.Collapsed;
                if (callback != null)
                {
                    callback(e);
                }
            });
        }

        private static void SetOpacity(this UIElement element, int duration, double opacity, Action<UIElement> callback)
        {
            UIElementExtensions.Animate(element, "Opacity", duration, opacity, callback);
        }

        public static void Animate(this UIElement element, string property, int duration, double value)
        {
            UIElementExtensions.Animate(element, property, duration, value, null);
        }

        public static void Animate(this UIElement element, string property, int duration, double value, Action<UIElement> callback)
        {
            Storyboard storyBoard = new Storyboard();

            DoubleAnimation animation = new DoubleAnimation();
            animation.To = value;
            animation.Duration = new Duration(TimeSpan.FromMilliseconds(duration));

            if (callback != null)
            {
                animation.Completed += delegate { callback(element); };
            }

            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));

            storyBoard.Children.Add(animation);
            storyBoard.Begin();
        }

        #endregion
    }
}
