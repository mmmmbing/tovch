using System;
using System.IO;
using System.Windows.Input;
using System.Xml.Serialization;

namespace full_AI_tovch
{
    [Serializable]
    public class UserSettings
    {
        // 开机自启动
        public bool AutoStart { get; set; } = false;

        // 快捷键配置
        public HotkeyConfig WakeUp { get; set; } = new HotkeyConfig { Key = Key.F12, Modifiers = ModifierKeys.Control | ModifierKeys.Shift };
        public HotkeyConfig Hide { get; set; } = new HotkeyConfig { Key = Key.Escape, Modifiers = ModifierKeys.None };
        public HotkeyConfig ToggleLabels { get; set; } = new HotkeyConfig { Key = Key.T, Modifiers = ModifierKeys.Control };
        public HotkeyConfig Exit { get; set; } = new HotkeyConfig { Key = Key.Escape, Modifiers = ModifierKeys.Control };

        // 交互参数
        public double LongPressThreshold { get; set; } = 500;
        public double DeleteRepeatInterval { get; set; } = 100;
        public string DeleteKey { get; set; } = "{BACKSPACE}";

        // 中心按钮配置
        public bool ShowCenterNode { get; set; } = true;
        public double CenterButtonSize { get; set; } = 60;
        public string CenterButtonText { get; set; } = "↩";
        public double CenterLongPressThresholdMs { get; set; } = 500;
        public double CenterDeleteRepeatIntervalMs { get; set; } = 100;

        public void Save(string filePath)
        {
            var serializer = new XmlSerializer(typeof(UserSettings));
            using (var writer = new StreamWriter(filePath))
            {
                serializer.Serialize(writer, this);
            }
        }

        public static UserSettings Load(string filePath)
        {
            if (!File.Exists(filePath)) return new UserSettings();
            try
            {
                var serializer = new XmlSerializer(typeof(UserSettings));
                using (var reader = new StreamReader(filePath))
                {
                    return (UserSettings)serializer.Deserialize(reader);
                }
            }
            catch
            {
                return new UserSettings();
            }
        }
    }

    [Serializable]
    public class HotkeyConfig
    {
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }

        public override string ToString()
        {
            string mod = "";
            if (Modifiers.HasFlag(ModifierKeys.Control)) mod += "Ctrl+";
            if (Modifiers.HasFlag(ModifierKeys.Shift)) mod += "Shift+";
            if (Modifiers.HasFlag(ModifierKeys.Alt)) mod += "Alt+";
            if (Modifiers.HasFlag(ModifierKeys.Windows)) mod += "Win+";
            return mod + Key.ToString();
        }
    }
}