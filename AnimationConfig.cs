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

        // 新增：交错延迟、缓动控制
        public static TimeSpan StaggerDelay { get; set; } = TimeSpan.FromMilliseconds(30);
        public static IEasingFunction ShowEase { get; set; } = new CircleEase { EasingMode = EasingMode.EaseOut };
        public static IEasingFunction HideEase { get; set; } = new CircleEase { EasingMode = EasingMode.EaseIn };

        // 原有无参出现动画（保持兼容）
        public static void PlayShowAnimation(this MenuItemNode node)
        {
            PlayShowAnimation(node, 0);
        }

        // 新重载：支持交错索引
        public static void PlayShowAnimation(this MenuItemNode node, int index)
        {
            if (node.UiButton == null) return;
            double dur = node.ShowDurationOverride ?? GlobalShowDuration;
            double stagger = StaggerDelay.TotalSeconds * index;
            var button = node.UiButton;

            button.Opacity = 0;
            button.RenderTransform = new ScaleTransform(0, 0);
            button.RenderTransformOrigin = new Point(0.5, 0.5);

            var opacityAnim = new DoubleAnimation(1, TimeSpan.FromSeconds(dur))
            {
                BeginTime = TimeSpan.FromSeconds(stagger),
                EasingFunction = ShowEase
            };
            var scaleAnimX = new DoubleAnimation(1, TimeSpan.FromSeconds(dur))
            {
                BeginTime = TimeSpan.FromSeconds(stagger),
                EasingFunction = ShowEase
            };
            var scaleAnimY = new DoubleAnimation(1, TimeSpan.FromSeconds(dur))
            {
                BeginTime = TimeSpan.FromSeconds(stagger),
                EasingFunction = ShowEase
            };

            button.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            if (button.RenderTransform is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimX);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimY);
            }
        }

        // 原有无参消失动画
        public static void PlayHideAnimation(this MenuItemNode node, Action onCompleted)
        {
            PlayHideAnimation(node, onCompleted, 0);
        }

        // 新重载：支持交错索引
        public static void PlayHideAnimation(this MenuItemNode node, Action onCompleted, int index)
        {
            if (node.UiButton == null)
            {
                onCompleted?.Invoke();
                return;
            }
            double dur = node.HideDurationOverride ?? GlobalHideDuration;
            double stagger = StaggerDelay.TotalSeconds * index;
            var button = node.UiButton;

            var opacityAnim = new DoubleAnimation(0, TimeSpan.FromSeconds(dur))
            {
                BeginTime = TimeSpan.FromSeconds(stagger),
                EasingFunction = HideEase
            };
            var scaleAnimX = new DoubleAnimation(0, TimeSpan.FromSeconds(dur))
            {
                BeginTime = TimeSpan.FromSeconds(stagger),
                EasingFunction = HideEase
            };
            var scaleAnimY = new DoubleAnimation(0, TimeSpan.FromSeconds(dur))
            {
                BeginTime = TimeSpan.FromSeconds(stagger),
                EasingFunction = HideEase
            };

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