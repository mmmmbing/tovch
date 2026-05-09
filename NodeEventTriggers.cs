using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace full_AI_tovch
{
    // 定义触发动作枚举
    public enum TriggerAction
    {
        None,
        ClickInjectText,          // 左键点击 -> 文本注入
        ClickExpandChildren,      // 左键点击 -> 展开子节点
        ClickSwitchLabel,         // 左键点击 -> 切换标签
        ClickModifier,            // 左键点击 -> 修饰键处理
        MouseEnterExpandChildren, // 鼠标进入 -> 展开子节点
        MouseLeaveBack,           // 鼠标离开 -> 返回上一级（需配合计时器）
        MouseEnterChangeAppearance, // 鼠标进入 -> 改变外观
        MouseLeaveRestoreAppearance, // 鼠标离开 -> 恢复外观
        ClearModifiers,           // 清除所有修饰键
    }

    public static class NodeEventTriggers
    {
        // 全局默认触发器（叶子节点默认打字，可展开节点默认鼠标进入展开）
        public static readonly TriggerConfig DefaultTrigger = new TriggerConfig
        {
            OnClickForLeaf = TriggerAction.ClickInjectText,
            OnClickForExpandable = TriggerAction.ClickExpandChildren, // 可展开节点点击也可打字？但我们将点击留给特殊行为，所以可设为 None，由鼠标进入展开
            OnMouseEnterForExpandable = TriggerAction.MouseEnterExpandChildren,
            OnMouseLeaveForBack = TriggerAction.MouseLeaveBack,
            OnMouseEnterAppearance = TriggerAction.MouseEnterChangeAppearance,
            OnMouseLeaveAppearance = TriggerAction.MouseLeaveRestoreAppearance,
            AppearanceBackground = Colors.LightBlue.ToColor(), // 普通背景
            HoverBackground = Colors.Orange.ToColor(),
            HoverScale = 1.2,
        };

        // 单节点覆盖配置（路径 -> 触发器）
        public static readonly Dictionary<string, TriggerConfig> Overrides = new Dictionary<string, TriggerConfig>
        {
            // 示例：颜色节点不显示外观变化，鼠标进入仅展开
            //{ "1", new TriggerConfig
            //    {
            //        OnMouseEnterAppearance = TriggerAction.None,
            //        OnMouseLeaveAppearance = TriggerAction.None,
            //    }
            //},
            //// 修饰键节点：左键点击处理为修饰键
            //{ "3/0", new TriggerConfig { OnClickForLeaf = TriggerAction.ClickModifier } },
            //// 清除修饰键节点
            //{ "3/6", new TriggerConfig { OnClickForLeaf = TriggerAction.ClearModifiers } },
        };

        // 获取某个节点的有效触发器（覆盖优先）
        public static TriggerConfig GetTrigger(MenuItemNode node)
        {
            if (Overrides.TryGetValue(node.Path, out var config))
                return config;
            return DefaultTrigger;
        }
    }

    // 触发器配置数据类
    public class TriggerConfig
    {
        public TriggerAction OnClickForLeaf { get; set; }
        public TriggerAction OnClickForExpandable { get; set; }
        public TriggerAction OnMouseEnterForExpandable { get; set; }
        public TriggerAction OnMouseLeaveForBack { get; set; }
        public TriggerAction OnMouseEnterAppearance { get; set; }
        public TriggerAction OnMouseLeaveAppearance { get; set; }
        public Color? AppearanceBackground { get; set; }
        public Color? HoverBackground { get; set; }
        public double? HoverScale { get; set; } = 1.0;
    }
    // 简单颜色扩展
    public static class ColorExtensions
    {
        public static Color ToColor(this System.Windows.Media.Color color) => color;
    }
}