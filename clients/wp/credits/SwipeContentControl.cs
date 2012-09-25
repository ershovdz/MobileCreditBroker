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
using Microsoft.Phone.Controls;
using Credits;

namespace Credits
{
    public class SwipeContentControl : ContentControl
    {
        private bool m_cancelled = false;
        private bool m_canSwipe = true;
        #region Events
        public event EventHandler SwipeLeft;
        protected virtual void OnSwipeLeft()
        {
            if (this.SwipeLeft != null)
            {
                this.SwipeLeft(this, new EventArgs());
            }
        }

        public event EventHandler SwipeRight;
        protected virtual void OnSwipeRight()
        {
            if (this.SwipeRight != null)
            {
                this.SwipeRight(this, new EventArgs());
            }
        }

        #endregion

        public bool CanSwipe
        {
            get { return m_canSwipe; }
            set { m_canSwipe = value; }
        }

        public SwipeContentControl()
        {
            this.RenderTransform = new TranslateTransform();

            this.DefaultStyleKey = typeof(ContentControl);

            GestureListener listener = GestureService.GetGestureListener(this);
            listener.DragStarted += DragStarted;
            listener.DragDelta += DragDelta;
            listener.DragCompleted += DragCompleted;
        }

        #region Swipe

        private void DragStarted(object sender, Microsoft.Phone.Controls.DragStartedGestureEventArgs e)
        {
            m_cancelled = false;
        }

        private void DragDelta(object sender, Microsoft.Phone.Controls.DragDeltaGestureEventArgs e)
        {
            if (!m_canSwipe || m_cancelled)
            {
                return;
            }
            
            if (Math.Abs(e.VerticalChange) <= 3)
            {
                if (Math.Abs(e.HorizontalChange) > 0)
                {
                    TranslateTransform transform = (TranslateTransform)this.RenderTransform;
                    if (transform.X == 0)
                    {
                        if ((e.HorizontalChange > -5) && (e.HorizontalChange < 5))
                        {
                            return;
                        }
                    }
                    transform.X += e.HorizontalChange;
                    //transform.Y += e.VerticalChange;
                }
            }
            else
            {
                m_cancelled = true;
            }
        }

        private void DragCompleted(object sender, Microsoft.Phone.Controls.DragCompletedGestureEventArgs e)
        {
            if(!m_canSwipe)
            {
                return;
            }

            TranslateTransform transform = (TranslateTransform)this.RenderTransform;
            if (transform.X > 20.0d)
            {
                double offset = this.ActualWidth + 20.0d;

                this.Animate("Opacity", 200, 0.0d);
                this.Animate("(RenderTransform).(TranslateTransform.X)", 200, offset, (element) =>
                {
                    this.OnSwipeRight();

                    this.Animate("(RenderTransform).(TranslateTransform.X)", 200, offset, (element2) =>
                    {
                        transform = (TranslateTransform)this.RenderTransform;
                        transform.X = -offset;

                        this.Animate("Opacity", 200, 1.0d);
                        this.Animate("(RenderTransform).(TranslateTransform.X)", 250, 0.0d);
                    });
                });
            }
            else if (transform.X < -20.0d)
            {
                double offset = this.ActualWidth + 20.0d;
                this.Animate("Opacity", 200, 0.0d);
                this.Animate("(RenderTransform).(TranslateTransform.X)", 200, -offset, (element) =>
                {
                    this.OnSwipeLeft();
                    this.Animate("(RenderTransform).(TranslateTransform.X)", 200, -offset, (element2) =>
                    {
                        transform = (TranslateTransform)this.RenderTransform;
                        transform.X = offset;

                        this.Animate("Opacity", 200, 1.0d);
                        this.Animate("(RenderTransform).(TranslateTransform.X)", 250, 0.0d);
                    });
                });
            }
            else if (transform.X != 0.0d)
            {
                this.Animate("(RenderTransform).(TranslateTransform.X)", 300, 0.0d);
            }
        }

        #endregion
    }
}
