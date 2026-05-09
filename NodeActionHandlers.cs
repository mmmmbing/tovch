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

            // 处理功能键
            if (specialKeys.Contains(node.DisplayText))
            {
                if (Enum.TryParse<Key>(node.DisplayText, true, out Key key))
                    TextInjection.SendKey(key, ModifierKeyState.GetCurrentModifierKeys());
                else
                    TextInjection.Send(node.DisplayText);
                // 清除修饰键等...
                if (ModifierKeyState.AutoClearAfterSend) ModifierKeyState.ClearModifiers();
                UpdateModifierStatusUI?.Invoke();
                return;
            }

            ModifierKeys mods = ModifierKeyState.GetCurrentModifierKeys();
            bool caps = ModifierKeyState.CapsLockActive;

            // CapsLock 处理：单字母自动大写（直接发送字符的大写版本，绕过 SendKey）
            if (caps && node.DisplayText.Length == 1 && char.IsLetter(node.DisplayText[0]))
            {
                char upper = char.ToUpper(node.DisplayText[0]);
                TextInjection.Send(upper.ToString()); // 现在会通过 SendKeys 发送
            }
            else if (mods != ModifierKeys.None)
            {
                // 有修饰键，尝试组合键发送
                Key key = KeyInterop.KeyFromVirtualKey(0);
                if (node.DisplayText.Length == 1)
                {
                    short vkScan = NativeMethods.VkKeyScan(node.DisplayText[0]);
                    key = KeyInterop.KeyFromVirtualKey((int)(vkScan & 0xFF));
                }
                else
                {
                    Enum.TryParse(node.DisplayText, true, out key);
                }

                if (key != Key.None)
                    TextInjection.SendKey(key, mods);
                else
                    TextInjection.Send(node.DisplayText); // 回退
            }
            else
            {
                // 无修饰键，普通文本注入
                TextInjection.Send(node.DisplayText);
            }

            if (ModifierKeyState.AutoClearAfterSend)
                ModifierKeyState.ClearModifiers();
            UpdateModifierStatusUI?.Invoke();
        }

        /// <summary>展开节点的子层</summary>
        public static void ExpandChildren(MenuItemNode node)
        {
            if (node == null || node.Children.Count == 0) return;
            try
            {
                if (PrepareChildrenLayout == null)
                    MessageBox.Show("PrepareChildrenLayout 委托为 null！");
                if (NavigateToChildren == null)
                    MessageBox.Show("NavigateToChildren 委托为 null！");

                PrepareChildrenLayout?.Invoke(node);
                NavigateToChildren?.Invoke(node.Children);
            }
            catch (Exception ex)
            {
                MessageBox.Show("展开子节点异常: " + ex.Message);
            }
        }

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