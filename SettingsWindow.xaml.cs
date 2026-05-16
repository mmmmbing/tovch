using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace full_AI_tovch
{
    public partial class SettingsWindow : Window
    {
        private UserSettings _settings;
        private TreeNodeViewModel _currentNode;

        public SettingsWindow(UserSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            LoadSettings();
            LoadNodeConfig();
        }

        // ========== 基本设置（快捷键、开机自启动等）==========
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

        private bool _isSettingHotkey = false;
        private string _currentSettingTarget = null;

        private void StartSetHotkey(string target, Button btn)
        {
            if (_isSettingHotkey) return;
            _isSettingHotkey = true;
            _currentSettingTarget = target;
            btn.Content = "按下组合键...";
            this.PreviewKeyDown += SettingsWindow_PreviewKeyDown;
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
            this.PreviewKeyDown -= SettingsWindow_PreviewKeyDown;
            _isSettingHotkey = false;
            _currentSettingTarget = null;
            e.Handled = true;
        }

        private void BtnWakeUp_Click(object sender, RoutedEventArgs e) => StartSetHotkey("WakeUp", btnWakeUp);
        private void BtnHide_Click(object sender, RoutedEventArgs e) => StartSetHotkey("Hide", btnHide);
        private void BtnToggleLabels_Click(object sender, RoutedEventArgs e) => StartSetHotkey("ToggleLabels", btnToggleLabels);
        private void BtnExit_Click(object sender, RoutedEventArgs e) => StartSetHotkey("Exit", btnExit);

        // ========== 节点配置 ==========
        private void LoadNodeConfig()
        {
            if (_settings.NodeTreeConfig == null)
                _settings.NodeTreeConfig = UserSettings.GetDefaultNodeTreeConfig();

            var rootVm = TreeNodeViewModel.FromConfig(_settings.NodeTreeConfig);

            // 为根节点的直接子节点设置友好的显示名称（因为配置里的 Labels 存储的是它们的子节点标签，而不是节点本身的名称）
            if (rootVm.Children.Count >= 5)
            {
                rootVm.Children[0].Label = "num";
                rootVm.Children[1].Label = "chara";
                rootVm.Children[2].Label = "spcl";
                rootVm.Children[3].Label = "fun";
                rootVm.Children[4].Label = "Sym";
            }

            tvNodeConfig.ItemsSource = new ObservableCollection<TreeNodeViewModel> { rootVm };
        }

        private void SaveNodeConfig()
        {
            if (tvNodeConfig.ItemsSource is ObservableCollection<TreeNodeViewModel> list && list.Count > 0)
                _settings.NodeTreeConfig = list[0].ToConfig();
        }

        private void tvNodeConfig_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _currentNode = e.NewValue as TreeNodeViewModel;
            if (_currentNode != null)
            {
                txtLabel.Text = _currentNode.Label;
                txtTrackRadius.Text = _currentNode.TrackRadius.ToString();
                txtButtonSize.Text = _currentNode.ButtonSize.ToString();
                txtLabels.Text = _currentNode.LabelsText;
                // 绑定扩展方式
                //var binding = new System.Windows.Data.Binding("ExpandStyle") { Source = _currentNode, Mode = BindingMode.TwoWay };
                //cmbExpandStyle.SetBinding(ComboBox.SelectedItemProperty, binding);
            }
            else
            {
                txtLabel.Text = txtTrackRadius.Text = txtButtonSize.Text = txtLabels.Text = "";
                //cmbExpandStyle.SelectedItem = null;
            }
        }

        private void NodeProperty_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_currentNode == null) return;
            _currentNode.Label = txtLabel.Text;
            if (double.TryParse(txtTrackRadius.Text, out double tr)) _currentNode.TrackRadius = tr;
            if (double.TryParse(txtButtonSize.Text, out double bs)) _currentNode.ButtonSize = bs;
            _currentNode.LabelsText = txtLabels.Text;
        }

        private void AddRootNode_Click(object sender, RoutedEventArgs e)
        {
            var newRoot = new TreeNodeViewModel
            {
                Label = "新根节点",
                TrackRadius = 100,
                ButtonSize = 50,
                Labels = new System.Collections.Generic.List<string> { "新标签" },
                ExpandStyle = ExpandStyle.Normal
            };
            var collection = tvNodeConfig.ItemsSource as ObservableCollection<TreeNodeViewModel>;
            collection?.Add(newRoot);
        }

        private void AddChildNode_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNode == null) { MessageBox.Show("请先选中一个节点"); return; }
            var dialog = new AddNodeDialog();
            dialog.Owner = this;   // 关键：避免被主窗口遮挡
            if (dialog.ShowDialog() == true)
            {
                var child = new TreeNodeViewModel
                {
                    Label = dialog.NodeName,
                    TrackRadius = 80,
                    ButtonSize = 40,
                    Labels = new System.Collections.Generic.List<string> { dialog.NodeName },
                    ExpandStyle = ExpandStyle.Normal
                };
                _currentNode.Children.Add(child);
                // 展开父节点
                var container = tvNodeConfig.ItemContainerGenerator.ContainerFromItem(_currentNode) as System.Windows.Controls.TreeViewItem;
                if (container != null) container.IsExpanded = true;
            }
        }

        private void DeleteNode_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNode == null)
            {
                MessageBox.Show("请先选中一个节点", "提示");
                return;
            }
            if (MessageBox.Show($"确定删除节点“{_currentNode.Label}”及其所有子节点吗？", "确认删除", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                var parent = FindParent(_currentNode);
                if (parent != null)
                    parent.Children.Remove(_currentNode);
                else
                {
                    var collection = tvNodeConfig.ItemsSource as ObservableCollection<TreeNodeViewModel>;
                    collection?.Remove(_currentNode);
                }
                _currentNode = null;
            }
        }

        private TreeNodeViewModel FindParent(TreeNodeViewModel target)
        {
            var rootList = tvNodeConfig.ItemsSource as ObservableCollection<TreeNodeViewModel>;
            if (rootList == null) return null;
            foreach (var root in rootList)
            {
                var found = FindParentRecursive(root, target);
                if (found != null) return found;
            }
            return null;
        }

        private TreeNodeViewModel FindParentRecursive(TreeNodeViewModel current, TreeNodeViewModel target)
        {
            if (current.Children.Contains(target)) return current;
            foreach (var child in current.Children)
            {
                var found = FindParentRecursive(child, target);
                if (found != null) return found;
            }
            return null;
        }

        private void ResetNodeConfig_Click(object sender, RoutedEventArgs e)
        {
            _settings.NodeTreeConfig = UserSettings.GetDefaultNodeTreeConfig();
            LoadNodeConfig();
            _currentNode = null;
        }

        // ========== 保存/取消 ==========
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 保存基本设置
            _settings.AutoStart = chkAutoStart.IsChecked ?? false;
            _settings.ShowCenterNode = chkShowCenter.IsChecked ?? true;
            _settings.CenterButtonText = txtCenterText.Text;
            if (double.TryParse(txtLongPressThreshold.Text, out double threshold))
                _settings.LongPressThreshold = threshold;
            SetAutoStart(_settings.AutoStart);
            // 保存节点配置
            SaveNodeConfig();
            // 保存到文件
            string configPath = GetConfigPath();
            _settings.Save(configPath);
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

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

        private string GetConfigPath()
        {
            string appDataPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "full_AI_tovch");
            if (!System.IO.Directory.Exists(appDataPath))
                System.IO.Directory.CreateDirectory(appDataPath);
            return System.IO.Path.Combine(appDataPath, "UserSettings.xml");
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => this.DragMove();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();
        private void Window_Loaded(object sender, RoutedEventArgs e) { }
    }

    // 转换器
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is int count && count > 0) || (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !((value is int count && count > 0) || (value is bool b && b));
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // 添加节点对话框
    public class AddNodeDialog : Window
    {
        public string NodeName { get; private set; } = "新节点";
        public ExpandStyle ExpandStyle { get; private set; } = ExpandStyle.Normal;

        public AddNodeDialog()
        {
            Width = 300; Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Title = "添加子节点";
            var stack = new StackPanel { Margin = new Thickness(10) };
            stack.Children.Add(new TextBlock { Text = "节点名称：" });
            var txtName = new TextBox { Text = NodeName, Margin = new Thickness(0, 2, 0, 10) };
            txtName.TextChanged += (s, e) => NodeName = txtName.Text;
            stack.Children.Add(txtName);
            stack.Children.Add(new TextBlock { Text = "扩展方式：" });
            var cmb = new ComboBox { Margin = new Thickness(0, 2, 0, 10) };
            cmb.ItemsSource = new[] { ExpandStyle.Normal, ExpandStyle.Inline };
            cmb.SelectedItem = ExpandStyle.Normal;
            cmb.SelectionChanged += (s, e) => ExpandStyle = (ExpandStyle)cmb.SelectedItem;
            stack.Children.Add(cmb);
            var btnOk = new Button { Content = "确定", Width = 80, Margin = new Thickness(0, 10, 0, 0) };
            btnOk.Click += (s, e) => DialogResult = true;
            stack.Children.Add(btnOk);
            Content = stack;
        }
    }
}