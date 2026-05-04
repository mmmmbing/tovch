using full_AI_tovch;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace full_AI_tovch
{
    internal static class ModifierKeysConverter
    {
    
      
        public static uint ToWin32(ModifierKeys modifiers)
        {
            uint mod = 0;
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) mod |= 0x0001;
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control) mod |= 0x0002;
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) mod |= 0x0004;
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows) mod |= 0x0008;
            return mod;
        }
    }
    public static class MenuActivation
    {
        private static HwndSource hwndSource;
        private static IntPtr handle;  // 保存窗口句柄
        private static int wakeUpHotkeyId = 9001;
        private static int hideHotkeyId = 9002;

        public static event Action ShowRequested;
        public static event Action HideRequested;


        public static void Initialize(IntPtr hWnd)
        {
            handle = hWnd;                        // 保存句柄
            hwndSource = HwndSource.FromHwnd(hWnd);
            hwndSource.AddHook(WndProc);
        }

        public static void RegisterWakeUpHotkey()
        {
            if (handle == IntPtr.Zero)
            {
                MessageBox.Show("窗口句柄为 Zero！");
                return;
            }
            bool success = RegisterHotKey(handle, wakeUpHotkeyId,
                ModifierKeysConverter.ToWin32(InteractionConfig.WakeUpModifiers),
                (uint)KeyInterop.VirtualKeyFromKey(InteractionConfig.WakeUpKey));
            if (!success)
            {
                int err = Marshal.GetLastWin32Error();
                MessageBox.Show($"注册失败，错误码: {err}");
            }
            else
            {
                //MessageBox.Show("注册成功！");
            }
        }
        public static void RegisterHideHotkey()
        {
            if (hwndSource == null) return;
            UnregisterHotKey(handle, hideHotkeyId);
            RegisterHotKey(handle, hideHotkeyId,
                ModifierKeysConverter.ToWin32(InteractionConfig.HideModifiers),
                (uint)KeyInterop.VirtualKeyFromKey(InteractionConfig.HideKey));
        }

        public static void UnregisterHideHotkey()
        {
            UnregisterHotKey(handle, hideHotkeyId);
        }

        public static void Cleanup()
        {
            UnregisterHotKey(handle, wakeUpHotkeyId);
            UnregisterHotKey(handle, hideHotkeyId);
            if (hwndSource != null)
                hwndSource.RemoveHook(WndProc);
        }

        // Win32 API 正确声明
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                System.Windows.MessageBox.Show($"WM_HOTKEY 收到，ID: {hotkeyId}");

                if (hotkeyId == wakeUpHotkeyId)
                {
                    if (ShowRequested != null)
                        ShowRequested.Invoke();
                    else
                        System.Windows.MessageBox.Show("ShowRequested 事件无订阅！");
                    handled = true;
                }
                else if (hotkeyId == hideHotkeyId)
                {
                    HideRequested?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }
        }
}