using full_AI_tovch;
using System;
using System.Windows.Input;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace full_AI_tovch
{
    /// <summary>
    /// 集中控制节点属性、方法绑定、细粒度配置。
    /// 在 MainWindow.Window_Loaded 构建节点树后调用 ConfigureAll(rootNodes)。
    /// </summary>
public static class NodeController
    {
        // ---------- 导航委托（由 MainWindow 注入）----------
        /// <summary>点击可展开节点时，导航到子层。参数为要展开的节点。</summary>
        public static Action<List<MenuItemNode>> NavigateToChildren { get; set; }


        // 新增委托：用于在展开子节点前，为子节点计算布局
        public static Action<MenuItemNode> PrepareChildrenLayout { get; set; }




        /// <summary>右键短按返回上一级（或其它后退逻辑）。</summary>
        public static Action NavigateBack { get; set; }

        // 新增鼠标事件委托（参数为对应的节点）
        public static Action<MenuItemNode> MouseEnterNode { get; set; }
        public static Action<MenuItemNode> MouseLeaveNode { get; set; }


        // 存储待绑定的处理器：节点路径 → 事件处理器 用来延迟给节点绑定方法
        private static Dictionary<string, List<RoutedEventHandler>> pendingHandlers
            = new Dictionary<string, List<RoutedEventHandler>>();



        // ---------- 全局配置入口 ----------
        /// <summary>
        /// 批量配置所有节点：绑定点击事件、设置默认行为等。
        /// 在 MainWindow.Window_Loaded 末尾调用。
        /// </summary>
        public static void ConfigureAll(List<MenuItemNode> rootNodes)
        {
            if (rootNodes == null) return;
            ConfigureNodeEvents(rootNodes);
        }

        // ---------- 核心事件绑定 ---------- 将事件绑定到btn上面
        private static void ConfigureNodeEvents(List<MenuItemNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.UiButton != null)
                {
                    // 先移除旧事件防止重复
                    node.UiButton.MouseEnter -= OnNodeMouseEnter;
                    node.UiButton.MouseLeave -= OnNodeMouseLeave;
                    node.UiButton.Click -= OnNodeClick;

                    // 绑定
                    node.UiButton.MouseEnter += OnNodeMouseEnter;
                    node.UiButton.MouseLeave += OnNodeMouseLeave;

                    //下面是将点击事件开始监听
                    node.UiButton.Click += OnNodeClick;

                    node.UiButton.Tag = node; // 确保 Tag 可用
                }
                if (node.Children.Count > 0)
                    ConfigureNodeEvents(node.Children);
            }
        }

        private static void OnNodeMouseEnter(object sender, MouseEventArgs e)
        {
            Button btn = sender as Button;
            MenuItemNode node = btn?.Tag as MenuItemNode;
            if (node != null) MouseEnterNode?.Invoke(node);
        }

        private static void OnNodeMouseLeave(object sender, MouseEventArgs e)
        {
            Button btn = sender as Button;
            MenuItemNode node = btn?.Tag as MenuItemNode;
            if (node != null) MouseLeaveNode?.Invoke(node);
        }

        /// <summary>
        /// 统一按钮点击处理  这里的点击是文本注入事件
        /// </summary> 
        private static void OnNodeClick(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            MenuItemNode node = btn?.Tag as MenuItemNode;
            if (node == null) return;

            if (node.Children.Count > 0)
            {
                // *** 先布局子节点（以父节点为中心） ***
                PrepareChildrenLayout?.Invoke(node);
                // 然后导航到子层
                NavigateToChildren?.Invoke(node.Children);
            }
            else
            {
                if (!string.IsNullOrEmpty(node.DisplayText))
                    TextInjection.Send(node.DisplayText);
            }
        }
        // ---------- 对外暴露的细粒度控制方法 ----------

        /// <summary>通过路径查找节点</summary>
        public static MenuItemNode GetNode(List<MenuItemNode> rootNodes, string path)
        {
            return NodeTree.FindNodeByPath(rootNodes, path);
        }

        /// <summary>设置节点文本（立即更新 UI 按钮）</summary>
        public static void SetNodeText(List<MenuItemNode> rootNodes, string path, string text)
        {
            var node = GetNode(rootNodes, path);
            if (node != null)
            {
                node.Text = text;
                if (node.UiButton != null)
                    node.UiButton.Content = text;
            }
        }




        /// <summary>设置节点的动画时长覆盖</summary>
        public static void SetNodeAnimationOverrides(List<MenuItemNode> rootNodes, string path,
            double? showDuration = null, double? hideDuration = null)
        {
            var node = GetNode(rootNodes, path);
            if (node != null)
            {
                node.ShowDurationOverride = showDuration;
                node.HideDurationOverride = hideDuration;
            }
        }

        /// <summary>动态启用/禁用节点的展开能力（清除或恢复子节点）</summary>
        public static void SetNodeExpandable(List<MenuItemNode> rootNodes, string path, bool expandable)
        {
            var node = GetNode(rootNodes, path);
            if (node != null)
            {
                if (!expandable)
                {
                    // 简单处理：清空子节点（如需可恢复，需额外存储原始子节点）
                    node.Children.Clear();
                }
                // 若要恢复展开，需从原始配置重建（这里可扩展，暂时忽略）
            }
        }

        /// <summary>为指定节点的按钮添加额外的 Click 事件处理器</summary>
        public static void AddClickHandler(List<MenuItemNode> rootNodes, string path, RoutedEventHandler handler)
        {
            var node = GetNode(rootNodes, path);
            if (node?.UiButton != null)
            {
                node.UiButton.Click += handler;
            }
        }



        //隐藏标签绑定事件
        /// <summary>通过路径切换节点标签（轮转）</summary>
        public static void SwitchNodeLabel(List<MenuItemNode> rootNodes, string path)
        {
            var node = GetNode(rootNodes, path);
            node?.SwitchToNextLabel();
        }

        /// <summary>通过路径执行节点的 ToggleLabel（含自定义动作）</summary>
        public static void ToggleNodeLabel(List<MenuItemNode> rootNodes, string path)
        {
            var node = GetNode(rootNodes, path);
            node?.ToggleLabel();
        }

        //延迟绑定方法
        /// <summary>安全地添加点击处理器，即使按钮尚未创建也会自动延迟绑定</summary>
        public static void AddClickHandlerSafe(List<MenuItemNode> rootNodes, string path, RoutedEventHandler handler)
        {
            var node = GetNode(rootNodes, path);
            if (node?.UiButton != null)
            {
                // 按钮已存在，直接绑定
                node.UiButton.Click += handler;
            }
            else
            {
                // 按钮还未创建，放入待处理列表
                if (!pendingHandlers.ContainsKey(path))
                    pendingHandlers[path] = new List<RoutedEventHandler>();
                pendingHandlers[path].Add(handler);
            }
        }
    }
}