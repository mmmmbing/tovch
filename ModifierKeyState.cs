using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace full_AI_tovch
{
    public static class ModifierKeyState
    {
        // 记录当前激活的修饰键（Ctrl/Shift/Alt/Win），不含 CapsLock
        private static readonly List<Key> activeModifiers = new List<Key>();

        // CapsLock 独立状态
        public static bool CapsLockActive { get; private set; } = false;

        public static void AddModifier(Key key)
        {
            if (!activeModifiers.Contains(key))
                activeModifiers.Add(key);
        }

        // 移除单个修饰键（再次点击同一修饰键时取消）
        public static void RemoveModifier(Key key)
        {
            activeModifiers.Remove(key);
        }

        // 清空所有普通修饰键
        public static void ClearModifiers()
        {
            activeModifiers.Clear();
        }

        public static void ToggleCapsLock()
        {
            CapsLockActive = !CapsLockActive;
        }

        // 当前修饰键集合（转为 ModifierKeys 枚举）
        public static ModifierKeys GetCurrentModifierKeys()
        {
            ModifierKeys mod = ModifierKeys.None;
            foreach (var key in activeModifiers)
            {
                switch (key)
                {
                    case Key.LeftCtrl: case Key.RightCtrl: mod |= ModifierKeys.Control; break;
                    case Key.LeftShift: case Key.RightShift: mod |= ModifierKeys.Shift; break;
                    case Key.LeftAlt: case Key.RightAlt: mod |= ModifierKeys.Alt; break;
                    case Key.LWin: case Key.RWin: mod |= ModifierKeys.Windows; break;
                }
            }
            return mod;
        }

        // 发送组合键后是否自动清空修饰键（可由配置控制，默认清空）
        public static bool AutoClearAfterSend { get; set; } = true;
    }
}
