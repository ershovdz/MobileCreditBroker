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
using Credits.Extensions;
using System.Linq;
using System.Collections.Generic;

namespace Credits.Effects
{
    public class TurnstileFeatherAnimator : AnimatorHelperBase
    {
        protected enum Directions
        {
            In,
            Out
        }

        protected int Duration { get; set; }
        protected int Angle { get; set; }
        protected int FeatherDelay { get; set; }
        protected Directions Direction { get; set; }
        protected int InitialDelay { get; set; }
        protected bool HoldSelectedItem { get; set; }

        public TurnstileFeatherAnimator()
            : base()
        {
            InitialDelay = 0;
        }

        public void AdjustPerspective(FrameworkElement element, FrameworkElement anchestor, bool verticalOnly = false)
        {
            if (anchestor.ActualHeight != 0.0)
            {
                PlaneProjection projection = new PlaneProjection();
                TranslateTransform translate = new TranslateTransform();
                element.Projection = projection;
                element.RenderTransform = translate;

                GeneralTransform gt = element.TransformToVisual(anchestor);
                Point pt;
                if (gt.TryTransform(new Point(), out pt))
                {
                    double ew = element.ActualWidth;
                    double eh = element.ActualHeight;

                    double dx = (anchestor.ActualWidth - ew) / 2 - pt.X;
                    double dy = (anchestor.ActualHeight - eh) / 2 - pt.Y;

                    double w = Math.Max(ew, pt.X);
                    double h = Math.Max(eh, pt.Y);

                    if (w > 0 && verticalOnly) projection.CenterOfRotationX = -pt.X / w; else projection.CenterOfRotationX = 0.0;

                    translate.X = dx;
                    projection.GlobalOffsetX = -dx;

                    translate.Y = dy;
                    projection.GlobalOffsetY = -dy;
                }
            }
        }

        public static bool GetIsElement(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsElementProperty);
        }

        public static void SetIsElement(DependencyObject obj, bool value)
        {
            obj.SetValue(IsElementProperty, value);
        }

        public static readonly DependencyProperty IsElementProperty =
            DependencyProperty.RegisterAttached("IsElement", typeof(bool), typeof(TurnstileFeatherAnimator), new PropertyMetadata(false));

        private static bool IsVisibleInContainer(FrameworkElement c, FrameworkElement container)
        {
            try
            {
                var gt = c.TransformToVisual(container);
                if (gt == null) return false;

                Point pt;
                if (!gt.TryTransform(new Point(), out pt)) return false;

                return !(((pt.Y + c.ActualHeight) < 0.0) || (pt.Y >= container.ActualHeight) || (pt.X >= container.ActualWidth) || (pt.X + c.ActualWidth < 0.0));

            }
            catch
            {
                return false;
            }
        }

        private static bool IsActiveElement(DependencyObject o, FrameworkElement container)
        {
            FrameworkElement e = o as FrameworkElement;

            bool isExcluded = e.GetVisualAncestors()
                .Cast<FrameworkElement>()
                .Any(c => c.IsHitTestVisible == false);

            if (!isExcluded) isExcluded = !IsVisibleInContainer(o as FrameworkElement, container);
            if (isExcluded)
            {
                e.Opacity = 1.0;
                PlaneProjection projection = e.GetPlaneProjection(false);
                if (projection != null) projection.RotationY = 0.0;
            }
            return !isExcluded;

        }

        private static List<DependencyObject> GetEffectiveItems(FrameworkElement container)
        {
            var effectiveElements = container
                .GetVisualDescendants()
                .Where(e => GetIsElement(e) && IsActiveElement(e, container))
                .ToList();

            return effectiveElements;
        }

        public override void Begin(Action completionAction)
        {
            Storyboard = new Storyboard();

            double liCounter = 0;
            var listBoxItems = GetEffectiveItems(RootElement as FrameworkElement);

            foreach (FrameworkElement li in listBoxItems)
            {
                DependencyObject obj = RootElement.Parent;
                AdjustPerspective(li, (obj as FrameworkElement), true);

                var beginTime = TimeSpan.FromMilliseconds((FeatherDelay * liCounter) + InitialDelay);

                if (Direction == Directions.In)
                {
                    li.Opacity = 0;

                    DoubleAnimationUsingKeyFrames daukf = new DoubleAnimationUsingKeyFrames();

                    EasingDoubleKeyFrame edkf1 = new EasingDoubleKeyFrame();
                    edkf1.KeyTime = beginTime;
                    edkf1.Value = Angle;
                    daukf.KeyFrames.Add(edkf1);

                    EasingDoubleKeyFrame edkf2 = new EasingDoubleKeyFrame();
                    edkf2.KeyTime = TimeSpan.FromMilliseconds(Duration).Add(beginTime);
                    edkf2.Value = 0;

                    ExponentialEase ee = new ExponentialEase();
                    ee.EasingMode = EasingMode.EaseOut;
                    ee.Exponent = 6;

                    edkf2.EasingFunction = ee;
                    daukf.KeyFrames.Add(edkf2);

                    Storyboard.SetTarget(daukf, li);
                    Storyboard.SetTargetProperty(daukf, new PropertyPath("(UIElement.Projection).(PlaneProjection.RotationY)"));
                    Storyboard.Children.Add(daukf);

                    DoubleAnimation da = new DoubleAnimation();
                    da.Duration = TimeSpan.FromMilliseconds(0);
                    da.BeginTime = beginTime;
                    da.To = 1;

                    Storyboard.SetTarget(da, li);
                    Storyboard.SetTargetProperty(da, new PropertyPath("(UIElement.Opacity)"));
                    Storyboard.Children.Add(da);
                }
                else
                {
                    li.Opacity = 1;

                    DoubleAnimation da = new DoubleAnimation();
                    da.BeginTime = beginTime;
                    da.Duration = TimeSpan.FromMilliseconds(Duration);
                    da.To = Angle;

                    ExponentialEase ee = new ExponentialEase();
                    ee.EasingMode = EasingMode.EaseIn;
                    ee.Exponent = 6;

                    da.EasingFunction = ee;

                    Storyboard.SetTarget(da, li);
                    Storyboard.SetTargetProperty(da, new PropertyPath("(UIElement.Projection).(PlaneProjection.RotationY)"));
                    Storyboard.Children.Add(da);

                    da = new DoubleAnimation();
                    da.Duration = TimeSpan.FromMilliseconds(10);
                    da.To = 0;
                    da.BeginTime = TimeSpan.FromMilliseconds(Duration).Add(beginTime);

                    Storyboard.SetTarget(da, li);
                    Storyboard.SetTargetProperty(da, new PropertyPath("(UIElement.Opacity)"));
                    Storyboard.Children.Add(da);
                }

                liCounter++;
            }

            base.Begin(completionAction);
        }
    }

    public class TurnstileFeatherForwardInAnimator : TurnstileFeatherAnimator
    {
        public TurnstileFeatherForwardInAnimator()
            : base()
        {
            Duration = 250;
            Angle = -100;
            FeatherDelay = 50;
            Direction = Directions.In;
        }
    }

    public class TurnstileFeatherForwardOutAnimator : TurnstileFeatherAnimator
    {
        public TurnstileFeatherForwardOutAnimator()
            : base()
        {
            Duration = 150;
            Angle = 70;
            FeatherDelay = 50;
            Direction = Directions.Out;
            HoldSelectedItem = true;
        }
    }

    public class TurnstileFeatherBackwardInAnimator : TurnstileFeatherAnimator
    {
        public TurnstileFeatherBackwardInAnimator()
            : base()
        {
            Duration = 250;
            Angle = 70;
            FeatherDelay = 50;
            Direction = Directions.In;
        }
    }

    public class TurnstileFeatherBackwardOutAnimator : TurnstileFeatherAnimator
    {
        public TurnstileFeatherBackwardOutAnimator()
            : base()
        {
            Duration = 150;
            Angle = -100;
            FeatherDelay = 50;
            Direction = Directions.Out;
        }
    }
}
