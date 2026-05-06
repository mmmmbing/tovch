using System;
using System.Collections.Generic;

namespace full_AI_tovch
{
    /// <summary>
    /// 节点标签的外部集中配置。用路径映射标签列表和自定义切换行为。
    /// 调用 Apply(rootNodes) 完成注入。
    /// </summary>
    public static class LabelConfig
    {
        // 路径 → 标签列表（第一个为主标签，其余为隐藏标签）
        private static readonly Dictionary<string, List<string>> LabelMap
            = new Dictionary<string, List<string>>
            {
                
                { "1/0/0", new List<string> { "a", "A" } },
                { "1/0/1", new List<string> { "b", "B" } },
                { "1/0/2", new List<string> { "c", "C" } },
                { "1/0/3", new List<string> { "d", "D" } },
                { "1/0/4", new List<string> { "e", "E" } },

                { "1/1/0", new List<string> { "f", "F" } },
                { "1/1/1", new List<string> { "g", "G" } },
                { "1/1/2", new List<string> { "h", "H" } },
                { "1/1/3", new List<string> { "j", "J" } },
                { "1/1/4", new List<string> { "i", "I" } },

                { "1/2/0", new List<string> { "k", "K" } },
                { "1/2/1", new List<string> { "m", "M" } },
                { "1/2/2", new List<string> { "l", "L" } },
                { "1/2/3", new List<string> { "n", "N" } },
                { "1/2/4", new List<string> { "q", "O" } },

                { "1/3/0", new List<string> { "p", "P" } },
                { "1/3/1", new List<string> { "q", "Q" } },
                { "1/3/2", new List<string> { "r", "R" } },
                { "1/3/3", new List<string> { "s", "S" } },
                { "1/3/4", new List<string> { "t", "T" } },

                { "1/4/0", new List<string> { "u", "U" } },
                { "1/4/1", new List<string> { "v", "V" } },
                { "1/4/2", new List<string> { "w", "W" } },
                { "1/4/3", new List<string> { "x", "X" } },
                { "1/4/4", new List<string> { "y", "Y" } },

                { "1/5", new List<string> { "z", "Z" } },
            };

        // 存放特殊切换行为：路径 → 自定义 Action
        private static readonly Dictionary<string, Action<MenuItemNode>> CustomActions
            = new Dictionary<string, Action<MenuItemNode>>
            {
                // 对于 1/1 节点，仅在 CapsLock 按下时切换
                { "1/1", (node) =>
                    {
                        if (System.Windows.Input.Keyboard.IsKeyToggled(System.Windows.Input.Key.CapsLock))
                            node.SwitchToNextLabel();
                    }
                }
            };

        /// <summary>
        /// 将标签和切换行为注入到整棵节点树。
        /// 应在 Window_Loaded 构建树之后调用。
        /// </summary>
        public static void Apply(List<MenuItemNode> rootNodes)
        {
            if (rootNodes == null) return;

            foreach (var kvp in LabelMap)
            {
                var node = NodeTree.FindNodeByPath(rootNodes, kvp.Key);
                if (node != null)
                {
                    node.Labels = kvp.Value;
                }
            }

            // 应用特殊切换行为
            foreach (var kvp in CustomActions)
            {
                var node = NodeTree.FindNodeByPath(rootNodes, kvp.Key);
                if (node != null)
                {
                    node.CustomToggleAction = () => kvp.Value(node);
                }
            }
        }
    }
}