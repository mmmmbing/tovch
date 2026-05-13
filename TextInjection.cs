using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace full_AI_tovch
{

    public static class TextInjection
    {
        #region Win32 定义（标准且完整）
        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYDOWN = 0x0000;
        const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        #endregion

        // ---------- 文本发送（使用 SendKeys，稳定且已解决特殊字符）----------
        public static void Send(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // 对特殊字符进行转义（已有 EscapeForSendKeys 方法）
            string escaped = EscapeForSendKeys(text);
            System.Windows.Forms.SendKeys.SendWait(escaped);
        }

        // ---------- 组合键发送（使用修复后的 SendInput）----------
        public static void SendKey(Key key, ModifierKeys modifiers)
        {
            int vk = KeyInterop.VirtualKeyFromKey(key);
            var inputs = new List<INPUT>();

            // 按下修饰键
            if (modifiers.HasFlag(ModifierKeys.Control)) AddKey(inputs, 0x11, false);
            if (modifiers.HasFlag(ModifierKeys.Shift)) AddKey(inputs, 0x10, false);
            if (modifiers.HasFlag(ModifierKeys.Alt)) AddKey(inputs, 0x12, false);
            if (modifiers.HasFlag(ModifierKeys.Windows)) AddKey(inputs, 0x5B, false);

            // 按下并释放目标键
            AddKey(inputs, (ushort)vk, false);
            AddKey(inputs, (ushort)vk, true);

            // 释放修饰键（逆序）
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

        // ---------- 辅助方法 ----------
        private static void AddKey(List<INPUT> inputs, ushort vk, bool isUp)
        {
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = isUp ? KEYEVENTF_KEYUP : KEYEVENTF_KEYDOWN,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            inputs.Add(input);
        }

        private static string EscapeForSendKeys(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Length == 1)
            {
                char c = text[0];
                if (c == '+' || c == '^' || c == '%' || c == '~' ||
                    c == '(' || c == ')' || c == '{' || c == '}')
                {
                    return "{" + c + "}";
                }
            }
            return text;
        }
    }
}