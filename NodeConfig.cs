using full_AI_tovch;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

//注，这个文件用来控制节点的通用属性，以及通过路径来修改节点的方法




namespace full_AI_tovch
{
    public static class NodeConfig
    {
        // 全局外观默认值
        public static Brush GlobalBackground { get; set; } = Brushes.LightBlue;
        public static Brush GlobalForeground { get; set; } = Brushes.Black;
        public static FontFamily GlobalFontFamily { get; set; } = new FontFamily("Segoe UI");
        public static double GlobalFontSize { get; set; } = 12;

        // 获取指定节点
        public static MenuItemNode GetNode(List<MenuItemNode> rootNodes, string path)
        {
            return NodeTree.FindNodeByPath(rootNodes, path);
        }

        // 设置节点文本（若rootNodes为null，后续操作将忽略，实际无意义）
        public static void SetNodeText(List<MenuItemNode> rootNodes, string path, string text)
        {
            var node = GetNode(rootNodes, path);
            if (node != null)
            {
                node.Text = text;
                if (node.UiButton != null)
                    node.UiButton.Content = text;
            }
        }

        // 读取节点文本
        public static string GetNodeText(List<MenuItemNode> rootNodes, string path)
        {
            var node = GetNode(rootNodes, path);
            return node?.Text;
        }

        // 若传入null，则以下方法为设置全局属性（已通过静态属性暴露，也可直接修改）
        // 为符合“传入空节点代表全局控制”，提供便捷方法：
        public static void SetGlobalBackground(Brush background)
        {
            GlobalBackground = background;
        }
        // 更多全局方法可根据需要添加
    }
    public static class CenterNodeConfig
    {
        public static bool ShowCenterNode { get; set; } = true;
        public static double ButtonSize { get; set; } = 60;
        public static Brush Background { get; set; } = new SolidColorBrush(Color.FromRgb(80, 80, 80));
        public static Brush Foreground { get; set; } = Brushes.White;
        public static string Text { get; set; } = "↩";
        public static double LongPressThresholdMs { get; set; } = 500;
        public static double DeleteRepeatIntervalMs { get; set; } = 100;
        public static string DeleteKey { get; set; } = "{BACKSPACE}";
    }
}