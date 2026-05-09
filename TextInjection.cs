using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace full_AI_tovch
{
    public static class TextInjection
    {
        #region Win32 definitions
        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct INPUT
        {
            [FieldOffset(0)] public uint type;
            [FieldOffset(4)] public KEYBDINPUT ki;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYDOWN = 0x0000;
        const uint KEYEVENTF_KEYUP = 0x0002;
        #endregion

        private static Key CharToKey(char ch)
        {
            // 字母和数字
            if (ch >= 'a' && ch <= 'z') return Key.A + (ch - 'a');
            if (ch >= 'A' && ch <= 'Z') return Key.A + (ch - 'A');
            if (ch >= '0' && ch <= '9') return Key.D0 + (ch - '0');

            // 常用符号映射（部分示例，可根据需要扩充）
            switch (ch)
            {
                case ' ': return Key.Space;
                case '.': return Key.OemPeriod;
                case ',': return Key.OemComma;
                case '-': return Key.OemMinus;
                case '=': return Key.OemPlus;
                case ';': return Key.OemSemicolon;
                case '\'': return Key.OemQuotes;
                case '/': return Key.OemQuestion;   // 通常与 ? 同键，需要 Shift 来区分，这里只映射基础键
                case '\\': return Key.OemBackslash;
                case '[': return Key.OemOpenBrackets;
                case ']': return Key.OemCloseBrackets;
                // 对于需要 Shift 才能产生的字符，返回基础键，修饰符由 GetModifiersFromChar 给出
                case '!': return Key.D1;
                case '@': return Key.D2;
                case '#': return Key.D3;
                case '$': return Key.D4;
                case '%': return Key.D5;
                case '^': return Key.D6;
                case '&': return Key.D7;
                case '*': return Key.D8;
                case '(': return Key.D9;
                case ')': return Key.D0;
                case '_': return Key.OemMinus;
                case '+': return Key.OemPlus;
                case '{': return Key.OemOpenBrackets;
                case '}': return Key.OemCloseBrackets;
                case ':': return Key.OemSemicolon;
                case '"': return Key.OemQuotes;
                case '<': return Key.OemComma;
                case '>': return Key.OemPeriod;
                case '?': return Key.OemQuestion;
                case '~': return Key.OemTilde;
                default: return Key.None;
            }
        }

        private static ModifierKeys GetModifiersFromChar(char ch)
        {
            ModifierKeys mods = ModifierKeys.None;
            short vkScan = NativeMethods.VkKeyScan(ch);
            if (vkScan != -1)
            {
                if ((vkScan & 0x100) != 0) mods |= ModifierKeys.Shift;
                if ((vkScan & 0x200) != 0) mods |= ModifierKeys.Control;
                if ((vkScan & 0x400) != 0) mods |= ModifierKeys.Alt;
            }
            else
            {
                // 如果无法获取，手动补全常用符号的 Shift 需求
                switch (ch)
                {
                    case '!':
                    case '@':
                    case '#':
                    case '$':
                    case '%':
                    case '^':
                    case '&':
                    case '*':
                    case '(':
                    case ')':
                    case '_':
                    case '+':
                    case '{':
                    case '}':
                    case ':':
                    case '"':
                    case '<':
                    case '>':
                    case '?':
                    case '~':
                        mods = ModifierKeys.Shift;
                        break;
                }
            }
            return mods;
        }
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


        // 发送文本（逐字符，支持所有可打印字符及常见控制符）
        public static void Send(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // 使用已有的转义函数处理特殊字符
            string escaped = EscapeForSendKeys(text);
            System.Windows.Forms.SendKeys.SendWait(escaped);
        }
        /// <summary>使用 Alt + Unicode 码点发送任意字符 (相当于按住 Alt 并在小键盘上输入数字)</summary>
        // 发送组合键（如 Ctrl+A）
        public static void SendKey(Key key, ModifierKeys modifiers)
        {
            int vk = KeyInterop.VirtualKeyFromKey(key);
            var inputs = new List<INPUT>();

            if (modifiers.HasFlag(ModifierKeys.Control)) AddKey(inputs, 0x11, false);
            if (modifiers.HasFlag(ModifierKeys.Shift)) AddKey(inputs, 0x10, false);
            if (modifiers.HasFlag(ModifierKeys.Alt)) AddKey(inputs, 0x12, false);
            if (modifiers.HasFlag(ModifierKeys.Windows)) AddKey(inputs, 0x5B, false);

            AddKey(inputs, (ushort)vk, false);
            AddKey(inputs, (ushort)vk, true);

            if (modifiers.HasFlag(ModifierKeys.Windows)) AddKey(inputs, 0x5B, true);
            if (modifiers.HasFlag(ModifierKeys.Alt)) AddKey(inputs, 0x12, true);
            if (modifiers.HasFlag(ModifierKeys.Shift)) AddKey(inputs, 0x10, true);
            if (modifiers.HasFlag(ModifierKeys.Control)) AddKey(inputs, 0x11, true);

            uint result = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            if (result == 0)
            {
                int err = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"SendInput 失败，错误码: {err}");
            }
        }

        private static void AddKey(List<INPUT> inputs, ushort vk, bool isUp)
        {
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = isUp ? KEYEVENTF_KEYUP : KEYEVENTF_KEYDOWN
                }
            });
        }
    }
}