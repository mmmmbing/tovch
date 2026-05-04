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
    }
}