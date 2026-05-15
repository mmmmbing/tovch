using System;
using System.Threading;
using System.Windows;
using System.Runtime.InteropServices;

namespace full_AI_tovch
{
    public partial class App : Application
    {
        private static Mutex _mutex;
        private const string MutexName = "Global\\Full_AI_Tovch_SingleInstance_Mutex";

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // 已有实例，激活现有窗口
                ActivateExistingWindow();
                // 立即退出当前实例
                Environment.Exit(0);
                return;
            }

            // 第一次启动，创建主窗口
            var mainWindow = new MainWindow();
            mainWindow.Show();

            base.OnStartup(e);
        }

        private void ActivateExistingWindow()
        {
            // 通过窗口标题查找
            IntPtr hWnd = FindWindow(null, "full_AI_tovch");
            if (hWnd != IntPtr.Zero)
            {
                if (IsIconic(hWnd))
                    ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;
    }
}