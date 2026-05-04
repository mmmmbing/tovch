using full_AI_tovch;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace full_AI_tovch
{
    public static class AnimationConfig
    {
        public static double GlobalShowDuration { get; set; } = 0.3;
        public static double GlobalHideDuration { get; set; } = 0.2;

        // 为指定按钮播放出现动画
        public static void PlayShowAnimation(this MenuItemNode node)
        {
            if (node.UiButton == null) return;
            double dur = node.ShowDurationOverride ?? GlobalShowDuration;
            var button = node.UiButton;

            button.Opacity = 0;
            button.RenderTransform = new ScaleTransform(0, 0);

            var opacityAnim = new DoubleAnimation(1, TimeSpan.FromSeconds(dur)) { EasingFunction = new QuadraticEase() };
            var scaleAnimX = new DoubleAnimation(1, TimeSpan.FromSeconds(dur)) { EasingFunction = new BackEase() };
            var scaleAnimY = new DoubleAnimation(1, TimeSpan.FromSeconds(dur)) { EasingFunction = new BackEase() };

            button.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            button.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimX);
            button.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimY);
        }

        // 播放消失动画，完成后执行回调（用于从Canvas移除）
        public static void PlayHideAnimation(this MenuItemNode node, Action onCompleted)
        {
            if (node.UiButton == null)
            {
                onCompleted?.Invoke();
                return;
            }
            double dur = node.HideDurationOverride ?? GlobalHideDuration;
            var button = node.UiButton;

            var opacityAnim = new DoubleAnimation(0, TimeSpan.FromSeconds(dur));
            var scaleAnimX = new DoubleAnimation(0, TimeSpan.FromSeconds(dur));
            var scaleAnimY = new DoubleAnimation(0, TimeSpan.FromSeconds(dur));

            opacityAnim.Completed += (s, e) => onCompleted?.Invoke();

            button.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            if (button.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimX);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimY);
            }
        }
    }
}
