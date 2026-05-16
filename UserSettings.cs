using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public NodeTreeConfigData NodeTreeConfig { get; set; } = GetDefaultNodeTreeConfig();

        public static NodeTreeConfigData GetDefaultNodeTreeConfig()
        {
            var defaultConfig = new NodeTreeConfigData
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 5,
                Labels = new List<string> {  "num", "chara", "spcl", "fun", "Sym" }, // 注意：这里 labels 用于根节点，子节点名称另行设置
                ExpandableConfigs = new Dictionary<int, NodeTreeConfigData>()
            };

            // 索引0: num 节点（数字 0-9）
            defaultConfig.ExpandableConfigs[0] = new NodeTreeConfigData
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 10,
                Labels = new List<string> { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" }
            };

            // 索引1: chara 节点（字母 a-z）
            defaultConfig.ExpandableConfigs[1] = new NodeTreeConfigData
            {
                TrackRadius = 210,
                ButtonSize = 40,
                VertexCount = 26,
                Labels = new List<string> { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z" }
            };

            // 索引2: spcl 节点（符号）
            defaultConfig.ExpandableConfigs[2] = new NodeTreeConfigData
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 10,
                Labels = new List<string> { "!", "@", "#", "$", "%", "^", "&", "*", "(", ")" }
            };

            // 索引3: fun 节点（功能键）
            defaultConfig.ExpandableConfigs[3] = new NodeTreeConfigData
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 11,
                Labels = new List<string> { "Tab", "Enter", "Space", "Delete", "Insert", "Home", "End", "up", "down", "left", "right" }
            };

            // 索引4: Sym 节点（修饰键）
            defaultConfig.ExpandableConfigs[4] = new NodeTreeConfigData
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 5,
                Labels = new List<string> { "Win", "Shift", "Ctrl", "CapsLk", "Alt" },
                ExpandableConfigs = new Dictionary<int, NodeTreeConfigData>()
            };
            defaultConfig.ExpandableConfigs[4].ExpandableConfigs[0] = new NodeTreeConfigData
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 2,
                Labels = new List<string> { "LeftWin", "RightWin" }
            };
            return defaultConfig;
        }

        public static NodeTreeConfig ToNodeTreeConfig(NodeTreeConfigData data)
        {
            var config = new NodeTreeConfig
            {
                TrackRadius = data.TrackRadius,
                ButtonSize = data.ButtonSize,
                VertexCount = data.VertexCount,
                Labels = data.Labels ?? new List<string>(),
                ExpandableConfigs = new Dictionary<int, NodeTreeConfig>()
            };
            if (data.ExpandableConfigs != null)
            {
                foreach (var kv in data.ExpandableConfigs)
                {
                    config.ExpandableConfigs[kv.Key] = ToNodeTreeConfig(kv.Value);
                }
            }
            return config;
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

    [Serializable]
    public class NodeTreeConfigData
    {
        public double TrackRadius { get; set; }
        public double ButtonSize { get; set; }
        public int VertexCount { get; set; }
        public List<string> Labels { get; set; }
        public Dictionary<int, NodeTreeConfigData> ExpandableConfigs { get; set; }
        public ExpandStyle ExpandStyle { get; set; } = ExpandStyle.Normal;   // 新增：扩展方式
    }
}