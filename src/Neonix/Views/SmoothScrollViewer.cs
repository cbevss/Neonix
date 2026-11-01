using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Neonix.Views
{
    /// <summary>A ScrollViewer whose mouse wheel glides to the new position instead of jumping.</summary>
    public class SmoothScrollViewer : ScrollViewer
    {
        public static readonly DependencyProperty AnimatedOffsetProperty = DependencyProperty.Register(
            nameof(AnimatedOffset), typeof(double), typeof(SmoothScrollViewer),
            new PropertyMetadata(0.0, (d, e) => ((SmoothScrollViewer)d).ScrollToVerticalOffset((double)e.NewValue)));

        public double AnimatedOffset
        {
            get => (double)GetValue(AnimatedOffsetProperty);
            set => SetValue(AnimatedOffsetProperty, value);
        }

        double _target;
        bool _animating;
        int _version;

        public SmoothScrollViewer()
        {
            // If the user drags the scroll bar (or uses the keyboard), follow it.
            ScrollChanged += (_, _) => { if (!_animating) _target = VerticalOffset; };
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (ScrollableHeight <= 0)
            {
                base.OnMouseWheel(e);
                return;
            }

            e.Handled = true;
            if (!_animating) _target = VerticalOffset;
            _target = Math.Clamp(_target - e.Delta, 0, ScrollableHeight);

            int mine = ++_version;
            _animating = true;
            var anim = new DoubleAnimation(VerticalOffset, _target, TimeSpan.FromMilliseconds(340))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            anim.Completed += (_, _) => { if (mine == _version) _animating = false; };
            BeginAnimation(AnimatedOffsetProperty, anim);
        }
    }
}
