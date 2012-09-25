using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;
using Microsoft.Phone.Controls;
using Credits.Extensions;

namespace Credits.Effects
{
    public class TiltEffect : DependencyObject
    {
        #region Constructor and Static Constructor
        private TiltEffect()
        {
        }

        static TiltEffect()
        {
            // The tiltable items list.
            TiltableItems = new List<Type>() { typeof(ButtonBase), typeof(ListBoxItem) };
            UseLogarithmicEase = false;
        }

        #endregion

        #region Fields and simple properties
        const double MaxAngle = 0.3;
        const double MaxDepression = 25;
        static readonly TimeSpan TiltReturnAnimationDelay = TimeSpan.FromMilliseconds(200);
        static readonly TimeSpan TiltReturnAnimationDuration = TimeSpan.FromMilliseconds(100);
        static FrameworkElement currentTiltElement;
        static Storyboard tiltReturnStoryboard;
        static DoubleAnimation tiltReturnXAnimation;
        static DoubleAnimation tiltReturnYAnimation;
        static DoubleAnimation tiltReturnZAnimation;
        static Point currentTiltElementCenter;
        static bool wasPauseAnimation = false;

        public static bool UseLogarithmicEase { get; set; }
        public static List<Type> TiltableItems { get; private set; }
        #endregion


        #region Dependency properties
        public static readonly DependencyProperty IsTiltEnabledProperty = DependencyProperty.RegisterAttached(
          "IsTiltEnabled",
          typeof(bool),
          typeof(TiltEffect),
          new PropertyMetadata(OnIsTiltEnabledChanged)
          );

        public static bool GetIsTiltEnabled(DependencyObject source) { return (bool)source.GetValue(IsTiltEnabledProperty); }
        public static void SetIsTiltEnabled(DependencyObject source, bool value) { source.SetValue(IsTiltEnabledProperty, value); }
        public static readonly DependencyProperty SuppressTiltProperty = DependencyProperty.RegisterAttached(
          "SuppressTilt",
          typeof(bool),
          typeof(TiltEffect),
          null
          );

        public static bool GetSuppressTilt(DependencyObject source) { return (bool)source.GetValue(SuppressTiltProperty); }
        public static void SetSuppressTilt(DependencyObject source, bool value) { source.SetValue(SuppressTiltProperty, value); }
        static void OnIsTiltEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (target is FrameworkElement)
            {
                // Add / remove the event handler if necessary
                if ((bool)args.NewValue == true)
                {
                    (target as FrameworkElement).ManipulationStarted += TiltEffect_ManipulationStarted;
                }
                else
                {
                    (target as FrameworkElement).ManipulationStarted -= TiltEffect_ManipulationStarted;
                }
            }
        }
        #endregion

        #region Top-level manipulation event handlers
        static void TiltEffect_ManipulationStarted(object sender, ManipulationStartedEventArgs e)
        {
            TryStartTiltEffect(sender as FrameworkElement, e);
        }
        static void TiltEffect_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            ContinueTiltEffect(sender as FrameworkElement, e);
        }
        static void TiltEffect_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            EndTiltEffect(currentTiltElement);
        }
        #endregion

        #region Core tilt logic
        public static void TryStartTiltEffect(FrameworkElement source, ManipulationStartedEventArgs e)
        {
            foreach (FrameworkElement ancestor in (e.OriginalSource as FrameworkElement).GetVisualAncestors())
            {
                foreach (Type t in TiltableItems)
                {
                    if (t.IsAssignableFrom(ancestor.GetType()))
                    {
                        if ((bool)ancestor.GetValue(SuppressTiltProperty) != true)
                        {
                            // Use first child of the control, so that you can add transforms and not
                            // impact any transforms on the control itself
                            FrameworkElement element = VisualTreeHelper.GetChild(ancestor, 0) as FrameworkElement;
                            FrameworkElement container = e.ManipulationContainer as FrameworkElement;

                            if (element == null || container == null)
                                return;

                            // Touch point relative to the element being tilted
                            Point tiltTouchPoint = container.TransformToVisual(element).Transform(e.ManipulationOrigin);

                            // Center of the element being tilted
                            Point elementCenter = new Point(element.ActualWidth / 2, element.ActualHeight / 2);

                            // Camera adjustment
                            Point centerToCenterDelta = GetCenterToCenterDelta(element, source);

                            BeginTiltEffect(element, tiltTouchPoint, elementCenter, centerToCenterDelta);
                            return;
                        }
                    }
                }
            }
        }

        static Point GetCenterToCenterDelta(FrameworkElement element, FrameworkElement container)
        {
            Point elementCenter = new Point(element.ActualWidth / 2, element.ActualHeight / 2);
            Point containerCenter;

            // Need to special-case the frame to handle different orientations
            if (container is PhoneApplicationFrame)
            {
                PhoneApplicationFrame frame = container as PhoneApplicationFrame;

                // Switch width and height in landscape mode
                if ((frame.Orientation & PageOrientation.Landscape) == PageOrientation.Landscape)
                {
                    containerCenter = new Point(container.ActualHeight / 2, container.ActualWidth / 2);
                }
                else
                {
                    containerCenter = new Point(container.ActualWidth / 2, container.ActualHeight / 2);
                }
            }
            else
            {
                containerCenter = new Point(container.ActualWidth / 2, container.ActualHeight / 2);
            }

            Point transformedElementCenter = element.TransformToVisual(container).Transform(elementCenter);
            Point result = new Point(containerCenter.X - transformedElementCenter.X, containerCenter.Y - transformedElementCenter.Y);
           
            return result;
        }

        static void BeginTiltEffect(FrameworkElement element, Point touchPoint, Point centerPoint, Point centerDelta)
        {
            if (tiltReturnStoryboard != null)
            {
                StopTiltReturnStoryboardAndCleanup();
            }

            if (PrepareControlForTilt(element, centerDelta) == false)
            {
                return;
            }

            currentTiltElement = element;
            currentTiltElementCenter = centerPoint;
            PrepareTiltReturnStoryboard(element);

            ApplyTiltEffect(currentTiltElement, touchPoint, currentTiltElementCenter);
        }

        static bool PrepareControlForTilt(FrameworkElement element, Point centerDelta)
        {
            // Prevents interference with any existing transforms
            if (element.Projection != null || (element.RenderTransform != null && element.RenderTransform.GetType() != typeof(MatrixTransform)))
                return false;

            TranslateTransform transform = new TranslateTransform();
            transform.X = centerDelta.X;
            transform.Y = centerDelta.Y;
            element.RenderTransform = transform;

            PlaneProjection projection = new PlaneProjection();
            projection.GlobalOffsetX = -1 * centerDelta.X;
            projection.GlobalOffsetY = -1 * centerDelta.Y;
            element.Projection = projection;

            element.ManipulationDelta += TiltEffect_ManipulationDelta;
            element.ManipulationCompleted += TiltEffect_ManipulationCompleted;

            return true;
        }

        static void RevertPrepareControlForTilt(FrameworkElement element)
        {
            element.ManipulationDelta -= TiltEffect_ManipulationDelta;
            element.ManipulationCompleted -= TiltEffect_ManipulationCompleted;
            element.Projection = null;
            element.RenderTransform = null;
        }

        static void PrepareTiltReturnStoryboard(FrameworkElement element)
        {
            if (tiltReturnStoryboard == null)
            {
                tiltReturnStoryboard = new Storyboard();
                tiltReturnStoryboard.Completed += TiltReturnStoryboard_Completed;

                tiltReturnXAnimation = new DoubleAnimation();
                Storyboard.SetTargetProperty(tiltReturnXAnimation, new PropertyPath(PlaneProjection.RotationXProperty));
                tiltReturnXAnimation.BeginTime = TiltReturnAnimationDelay;
                tiltReturnXAnimation.To = 0;
                tiltReturnXAnimation.Duration = TiltReturnAnimationDuration;

                tiltReturnYAnimation = new DoubleAnimation();
                Storyboard.SetTargetProperty(tiltReturnYAnimation, new PropertyPath(PlaneProjection.RotationYProperty));
                tiltReturnYAnimation.BeginTime = TiltReturnAnimationDelay;
                tiltReturnYAnimation.To = 0;
                tiltReturnYAnimation.Duration = TiltReturnAnimationDuration;

                tiltReturnZAnimation = new DoubleAnimation();
                Storyboard.SetTargetProperty(tiltReturnZAnimation, new PropertyPath(PlaneProjection.GlobalOffsetZProperty));
                tiltReturnZAnimation.BeginTime = TiltReturnAnimationDelay;
                tiltReturnZAnimation.To = 0;
                tiltReturnZAnimation.Duration = TiltReturnAnimationDuration;

                if (UseLogarithmicEase)
                {
                    tiltReturnXAnimation.EasingFunction = new LogarithmicEase();
                    tiltReturnYAnimation.EasingFunction = new LogarithmicEase();
                    tiltReturnZAnimation.EasingFunction = new LogarithmicEase();
                }

                tiltReturnStoryboard.Children.Add(tiltReturnXAnimation);
                tiltReturnStoryboard.Children.Add(tiltReturnYAnimation);
                tiltReturnStoryboard.Children.Add(tiltReturnZAnimation);
            }

            Storyboard.SetTarget(tiltReturnXAnimation, element.Projection);
            Storyboard.SetTarget(tiltReturnYAnimation, element.Projection);
            Storyboard.SetTarget(tiltReturnZAnimation, element.Projection);
        }

        static void ContinueTiltEffect(FrameworkElement element, ManipulationDeltaEventArgs e)
        {
            FrameworkElement container = e.ManipulationContainer as FrameworkElement;
            if (container == null || element == null)
            {
                return;
            }

            Point tiltTouchPoint = container.TransformToVisual(element).Transform(e.ManipulationOrigin);

            // If touch moved outside bounds of element, then pause the tilt (but don't cancel it)
            if (new Rect(0, 0, currentTiltElement.ActualWidth, currentTiltElement.ActualHeight).Contains(tiltTouchPoint) != true)
            {
                PauseTiltEffect();
                return;
            }

            // Apply the updated tilt effect
            ApplyTiltEffect(currentTiltElement, e.ManipulationOrigin, currentTiltElementCenter);
        }

        static void EndTiltEffect(FrameworkElement element)
        {
            if (element != null)
            {
                element.ManipulationCompleted -= TiltEffect_ManipulationCompleted;
                element.ManipulationDelta -= TiltEffect_ManipulationDelta;
            }

            if (tiltReturnStoryboard != null)
            {
                wasPauseAnimation = false;
                if (tiltReturnStoryboard.GetCurrentState() != ClockState.Active)
                {
                    tiltReturnStoryboard.Begin();
                }
            }
            else
            {
                StopTiltReturnStoryboardAndCleanup();
            }
        }

        static void TiltReturnStoryboard_Completed(object sender, EventArgs e)
        {
            if (wasPauseAnimation)
            {
                ResetTiltEffect(currentTiltElement);
            }
            else
            {
                StopTiltReturnStoryboardAndCleanup();
            }
        }

        static void ResetTiltEffect(FrameworkElement element)
        {
            PlaneProjection projection = element.Projection as PlaneProjection;
            projection.RotationY = 0;
            projection.RotationX = 0;
            projection.GlobalOffsetZ = 0;
        }

        static void StopTiltReturnStoryboardAndCleanup()
        {
            if (tiltReturnStoryboard != null)
            {
                tiltReturnStoryboard.Stop();
            }

            RevertPrepareControlForTilt(currentTiltElement);
        }

        static void PauseTiltEffect()
        {
            if ((tiltReturnStoryboard != null) && !wasPauseAnimation)
            {
                tiltReturnStoryboard.Stop();
                wasPauseAnimation = true;
                tiltReturnStoryboard.Begin();
            }
        }

        private static void ResetTiltReturnStoryboard()
        {
            tiltReturnStoryboard.Stop();
            wasPauseAnimation = false;
        }

        static void ApplyTiltEffect(FrameworkElement element, Point touchPoint, Point centerPoint)
        {
            // Stop any active animation
            ResetTiltReturnStoryboard();

            // Get relative point of the touch in percentage of container size
            Point normalizedPoint = new Point(
                Math.Min(Math.Max(touchPoint.X / (centerPoint.X * 2), 0), 1),
                Math.Min(Math.Max(touchPoint.Y / (centerPoint.Y * 2), 0), 1));

            // Shell values
            double xMagnitude = Math.Abs(normalizedPoint.X - 0.5);
            double yMagnitude = Math.Abs(normalizedPoint.Y - 0.5);
            double xDirection = -Math.Sign(normalizedPoint.X - 0.5);
            double yDirection = Math.Sign(normalizedPoint.Y - 0.5);
            double angleMagnitude = xMagnitude + yMagnitude;
            double xAngleContribution = xMagnitude + yMagnitude > 0 ? xMagnitude / (xMagnitude + yMagnitude) : 0;

            double angle = angleMagnitude * MaxAngle * 180 / Math.PI;
            double depression = (1 - angleMagnitude) * MaxDepression;

            // RotationX and RotationY are the angles of rotations about the x- or y-*axis*;
            // to achieve a rotation in the x- or y-*direction*, we need to swap the two.
            // That is, a rotation to the left about the y-axis is a rotation to the left in the x-direction,
            // and a rotation up about the x-axis is a rotation up in the y-direction.
            PlaneProjection projection = element.Projection as PlaneProjection;
            projection.RotationY = angle * xAngleContribution * xDirection;
            projection.RotationX = angle * (1 - xAngleContribution) * yDirection;
            projection.GlobalOffsetZ = -depression;
        }

        #endregion


        #region Custom easing function
        private class LogarithmicEase : EasingFunctionBase
        {
            protected override double EaseInCore(double normalizedTime)
            {
                return Math.Log(normalizedTime + 1) / 0.693147181; // ln(t + 1) / ln(2)
            }
        }

        #endregion
    }
}

