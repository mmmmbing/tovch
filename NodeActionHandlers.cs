using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace full_AI_tovch
{
    public static class NodeActionHandlers
    {
        // ---------- 由 MainWindow 注入的委托 ----------
        public static Action<List<MenuItemNode>> NavigateToChildren { get; set; }
        public static Action NavigateBack { get; set; }
        public static Action<MenuItemNode> PrepareChildrenLayout { get; set; }
        public static Action<MenuItemNode> UpdateModifierKeyAppearance { get; set; }
        public static Action UpdateModifierStatusUI { get; set; }

        // ---------- 核心动作方法 ----------

        /// <summary>注入当前节点显示文本（考虑修饰键状态）</summary>

        private static string EscapeForSendKeys(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // 仅对单字符的特殊键进行处理，多字符直接返回（像"Tab"之类已在别处处理）
            if (text.Length == 1)
            {
                char c = text[0];
                // 这些字符在 SendKeys 中有特殊含义，需要加大括号
                if (c == '+' || c == '^' || c == '%' || c == '~' || c == '(' || c == ')' || c == '{' || c == '}')
                    return "{" + c + "}";
            }
            return text;
        }

        private static readonly HashSet<string> specialKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Tab", "Enter", "Backspace", "Delete", "Escape", "Space", "Up", "Down", "Left", "Right"
};

        public static void InjectText(MenuItemNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.DisplayText)) return;

            // 定义所有功能键（不区分大小写）
            var functionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Tab", "Enter", "Space", "Backspace", "Delete", "Insert",
        "Home", "End", "Up", "Down", "Left", "Right", "Escape"
    };

            string text = node.DisplayText;
            bool isFunctionKey = functionKeys.Contains(text);

            // 获取当前修饰键状态
            ModifierKeys currentMods = ModifierKeyState.GetCurrentModifierKeys();
            bool capsActive = ModifierKeyState.CapsLockActive;

            // 功能键处理
            if (isFunctionKey)
            {
                // 将文本转换为 Key 枚举
                Key key = Key.None;
                switch (text.ToLowerInvariant())
                {
                    case "tab": key = Key.Tab; break;
                    case "enter": key = Key.Enter; break;
                    case "space": key = Key.Space; break;
                    case "backspace": key = Key.Back; break;
                    case "delete": key = Key.Delete; break;
                    case "insert": key = Key.Insert; break;
                    case "home": key = Key.Home; break;
                    case "end": key = Key.End; break;
                    case "up": key = Key.Up; break;
                    case "down": key = Key.Down; break;
                    case "left": key = Key.Left; break;
                    case "right": key = Key.Right; break;
                    case "escape": key = Key.Escape; break;
                    default: key = Key.None; break;
                }

                if (key != Key.None)
                {
                    // 发送按键（若当前有修饰键则组合，否则单独发送）
                    TextInjection.SendKey(key, currentMods);
                }
                else
                {
                    // 降级：使用 SendKeys
                    string sendKey = "{" + text + "}";
                    System.Windows.Forms.SendKeys.SendWait(sendKey);
                }

                // 发送后清除修饰键（如果需要）
                if (ModifierKeyState.AutoClearAfterSend)
                    ModifierKeyState.ClearModifiers();
                UpdateModifierStatusUI?.Invoke();
                return;
            }

            // 普通文本处理（保留原有逻辑）
            if (capsActive && text.Length == 1 && char.IsLetter(text[0]))
            {
                char upper = char.ToUpper(text[0]);
                TextInjection.Send(upper.ToString());
            }
            else if (currentMods != ModifierKeys.None)
            {
                // 尝试作为单字符键发送组合键
                Key key = Key.None;
                if (text.Length == 1)
                {
                    short vkScan = NativeMethods.VkKeyScan(text[0]);
                    key = KeyInterop.KeyFromVirtualKey((int)(vkScan & 0xFF));
                }
                else
                {
                    Enum.TryParse(text, true, out key);
                }

                if (key != Key.None)
                    TextInjection.SendKey(key, currentMods);
                else
                    TextInjection.Send(text);
            }
            else
            {
                TextInjection.Send(text);
            }

            if (ModifierKeyState.AutoClearAfterSend)
                ModifierKeyState.ClearModifiers();
            UpdateModifierStatusUI?.Invoke();
        }

        /// <summary>展开节点的子层</summary>
        public static void ExpandChildren(MenuItemNode node)
        {
            if (node == null || node.Children.Count == 0) return;

            if (node.ExpandStyle == ExpandStyle.Normal)
            {
                // 原有下钻逻辑
                PrepareChildrenLayout?.Invoke(node);
                MainWindow.Instance?.SetPendingCenter(node.CenterX, node.CenterY);
                NavigateToChildren?.Invoke(node.Children);
            }
            else if (node.ExpandStyle == ExpandStyle.Inline)
            {
                // 新增内联展开逻辑
                InlineExpandAction?.Invoke(node);
            }
        }
        public static Action<MenuItemNode> InlineExpandAction { get; set; }

        /// <summary>切换节点标签</summary>
        public static void SwitchLabel(MenuItemNode node)
        {
            node?.ToggleLabel();
        }

        /// <summary>处理修饰键点击（Ctrl、Shift、Alt、Win、CapsLock）</summary>
        public static void HandleModifierClick(MenuItemNode node)
        {
            if (node == null || !ModifierKeyConfig.ModifierKeyMap.TryGetValue(node.Path, out var key))
                return;

            if (key == Key.CapsLock)
            {
                ModifierKeyState.ToggleCapsLock();
            }
            else
            {
                // 已激活则取消，未激活则添加
                if (ModifierKeyState.GetCurrentModifierKeys().HasFlag(KeyToModifier(key)))
                    ModifierKeyState.RemoveModifier(key);
                else
                    ModifierKeyState.AddModifier(key);
            }

            // 调用外观更新回调（由 MainWindow 注入）
            UpdateModifierKeyAppearance?.Invoke(node);
            UpdateModifierStatusUI?.Invoke();
        }

        /// <summary>清除所有普通修饰键（不含 CapsLock）</summary>
        public static void ClearAllModifiers()
        {
            ModifierKeyState.ClearModifiers();
            UpdateModifierStatusUI?.Invoke();
            // 可能需要刷新所有修饰键按钮的外观，这里简单起见，由 MainWindow 实现的回调处理
            // 但 MainWindow 可能需要知道执行刷新，可以额外注入一个 RefreshAllModifierButtons 委托，若无则跳过。
        }

        /// <summary>Key 转 ModifierKeys 枚举（公开，供外观回调使用）</summary>
        public static ModifierKeys KeyToModifier(Key key)
        {
            switch (key)
            {
                case Key.LeftCtrl: case Key.RightCtrl: return ModifierKeys.Control;
                case Key.LeftShift: case Key.RightShift: return ModifierKeys.Shift;
                case Key.LeftAlt: case Key.RightAlt: return ModifierKeys.Alt;
                case Key.LWin: case Key.RWin: return ModifierKeys.Windows;
                default: return ModifierKeys.None;
            }
        }

    }
}