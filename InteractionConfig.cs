using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace full_AI_tovch 
{
    public static class InteractionConfig
    {
        // 唤醒热键
        public static Key WakeUpKey { get; set; } = Key.F12;
        public static ModifierKeys WakeUpModifiers { get; set; } = ModifierKeys.Control |ModifierKeys.Shift;

        // 隐藏热键
        public static Key HideKey { get; set; } = Key.Escape;
        public static ModifierKeys HideModifiers { get; set; } = ModifierKeys.None;

        // 长按判定阈值 (毫秒)
        public static double LongPressThreshold { get; set; } = 500;

        // 长按后重复发送删除键的间隔 (毫秒)
        public static double DeleteRepeatInterval { get; set; } = 100;

        // 长按发送的删除键（符合 SendKeys 格式）
        public static string DeleteKey { get; set; } = "{BACKSPACE}";
    }
}
