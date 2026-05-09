using System.Collections.Generic;
using System.Windows.Input;

namespace full_AI_tovch
{
    public static class ModifierKeyConfig
    {
        // 修饰键节点路径 → 对应的键
        public static readonly Dictionary<string, Key> ModifierKeyMap = new Dictionary<string, Key>
        {
            // 假设你有一个“数字”节点展开，其子节点分别是 Ctrl, Shift, Alt, Win, CapsLock, Tab
            { "3/0", Key.LWin },
            { "3/1", Key.LeftShift },
            { "3/2", Key.LeftCtrl },
            { "3/3", Key.CapsLock },   
            { "3/4", Key.LeftAlt }            };

        // 是否开启修饰键叠加状态显示（在轨道外显示文字）
        public static bool ShowStatusIndicator { get; set; } = true;

        // 判断某个路径是否为修饰键节点
        public static bool IsModifierKey(string nodePath)
        {
            return ModifierKeyMap.ContainsKey(nodePath);
        }
    }
}