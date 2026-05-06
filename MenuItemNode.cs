using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace full_AI_tovch
{
    public class MenuItemNode 
    {
        public string Text { get; set; }
        public List<MenuItemNode> Children { get; set; } = new List<MenuItemNode>();

        // 用于子节点排布的参数
        public double SelfTrackRadius { get; set; }
        public double TrackRadius { get; set; }
        public double ButtonSize { get; set; }

        // 动画时间覆盖（null 表示用全局值）
        public double? ShowDurationOverride { get; set; }
        public double? HideDurationOverride { get; set; }

        // 外观覆盖（null 表示用全局值）
        public Brush BackgroundOverride { get; set; }
        public Brush ForegroundOverride { get; set; }

        // 运行时字段
        public Button UiButton { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }


        /// <summary>所有标签，第一个为主标签，其余为隐藏标签</summary>
        public List<string> Labels { get; set; }

        /// <summary>当前显示的标签索引</summary>
        public int CurrentLabelIndex { get; set; } = 0;

        /// <summary>当前应显示的文字（优先使用标签，否则回退到 Text）</summary>
        public string DisplayText
        {
            get
            {
                if (Labels != null && Labels.Count > 0)
                    return Labels[CurrentLabelIndex % Labels.Count];
                return Text;
            }
        }

        /// <summary>自定义切换动作，若不为 null 则优先调用</summary>
        public Action CustomToggleAction { get; set; }

        /// <summary>切换到下一个标签</summary>
        public void SwitchToNextLabel()
        {
            if (Labels == null || Labels.Count == 0) return;
            CurrentLabelIndex = (CurrentLabelIndex + 1) % Labels.Count;
            UpdateButtonContent();
        }

        /// <summary>切换到指定索引的标签</summary>
        public void SwitchToLabel(int index)
        {
            if (Labels == null || Labels.Count == 0) return;
            CurrentLabelIndex = index % Labels.Count;
            UpdateButtonContent();
        }

        /// <summary>执行切换：先尝试自定义动作，若无则轮转标签</summary>
        public void ToggleLabel()
        {
            if (CustomToggleAction != null)
            {
                CustomToggleAction.Invoke();
            }
            else
            {
                SwitchToNextLabel();
            }
        }

        private void UpdateButtonContent()
        {
            if (UiButton != null)
                UiButton.Content = DisplayText;
        }

    }
}