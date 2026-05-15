using full_AI_tovch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
//using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

//using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml.Linq;

namespace full_AI_tovch
{
    public partial class SettingsWindow : Window
    {
        private UserSettings _settings;
        private bool _isSettingHotkey = false;
        private string _currentSettingTarget = null;

        public SettingsWindow(UserSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            LoadSettings();
        }

        private void LoadSettings()
        {
            chkAutoStart.IsChecked = _settings.AutoStart;
            chkShowCenter.IsChecked = _settings.ShowCenterNode;
            txtCenterText.Text = _settings.CenterButtonText;
            txtLongPressThreshold.Text = _settings.LongPressThreshold.ToString();
            UpdateButtonText(btnWakeUp, _settings.WakeUp);
            UpdateButtonText(btnHide, _settings.Hide);
            UpdateButtonText(btnToggleLabels, _settings.ToggleLabels);
            UpdateButtonText(btnExit, _settings.Exit);
        }

        private void UpdateButtonText(Button btn, HotkeyConfig config) => btn.Content = config.ToString();

        private void StartSetHotkey(string target, Button btn)
        {
            if (_isSettingHotkey) return;
            _isSettingHotkey = true;
            _currentSettingTarget = target;
            btn.Content = "按下组合键...";
            PreviewKeyDown += SettingsWindow_PreviewKeyDown;
        }

        private void SettingsWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isSettingHotkey) return;

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            ModifierKeys mods = Keyboard.Modifiers;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            var newConfig = new HotkeyConfig { Key = key, Modifiers = mods };

            switch (_currentSettingTarget)
            {
                case "WakeUp": _settings.WakeUp = newConfig; UpdateButtonText(btnWakeUp, newConfig); break;
                case "Hide": _settings.Hide = newConfig; UpdateButtonText(btnHide, newConfig); break;
                case "ToggleLabels": _settings.ToggleLabels = newConfig; UpdateButtonText(btnToggleLabels, newConfig); break;
                case "Exit": _settings.Exit = newConfig; UpdateButtonText(btnExit, newConfig); break;
            }

            PreviewKeyDown -= SettingsWindow_PreviewKeyDown;
            _isSettingHotkey = false;
            _currentSettingTarget = null;
            e.Handled = true;
        }

        private void BtnWakeUp_Click(object sender, RoutedEventArgs e) => StartSetHotkey("WakeUp", btnWakeUp);
        private void BtnHide_Click(object sender, RoutedEventArgs e) => StartSetHotkey("Hide", btnHide);
        private void BtnToggleLabels_Click(object sender, RoutedEventArgs e) => StartSetHotkey("ToggleLabels", btnToggleLabels);
        private void BtnExit_Click(object sender, RoutedEventArgs e) => StartSetHotkey("Exit", btnExit);

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _settings.AutoStart = chkAutoStart.IsChecked ?? false;
            _settings.ShowCenterNode = chkShowCenter.IsChecked ?? true;
            _settings.CenterButtonText = txtCenterText.Text;
            if (double.TryParse(txtLongPressThreshold.Text, out double threshold))
                _settings.LongPressThreshold = threshold;

            SetAutoStart(_settings.AutoStart);
            string configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UserSettings.xml");
            _settings.Save(configPath);

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SetAutoStart(bool enable)
        {
            string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            const string runKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKeyPath, true))
                {
                    if (enable)
                        key.SetValue("full_AI_tovch", appPath);
                    else
                        key.DeleteValue("full_AI_tovch", false);
                }
            }
            catch { }
        }
    }
}