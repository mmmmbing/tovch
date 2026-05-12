using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace full_AI_tovch
{
    //public static class NodeEventBinder
    //{
    //    public static void Bind(List<MenuItemNode> nodes, bool includeChildren = true)
    //    {
    //        if (nodes == null) return;
    //        foreach (var node in nodes)
    //        {
    //            if (node.UiButton == null) continue;

    //            var trigger = NodeEventTriggers.GetTrigger(node);
    //            bool isLeaf = node.Children.Count == 0;
    //            bool isModifier = ModifierKeyConfig.IsModifierKey(node.Path);

    //            //if (node.DisplayText == "Ctrl" || node.DisplayText == "Shift" || node.DisplayText == "Win" || node.DisplayText == "Alt" || node.DisplayText == "CapsLk" || node.DisplayText == "Tab")
    //            //{
    //            //    MessageBox.Show($"节点 {node.DisplayText}, Path={node.Path}, isModifier={isModifier}");
    //            //}

    //            // 先清空可能已绑定的事件（如果担心重复绑定）
    //            node.UiButton.Click -= OnButtonClick;
    //            node.UiButton.MouseEnter -= OnButtonMouseEnter;
    //            node.UiButton.MouseLeave -= OnButtonMouseLeave;


    //            bool isModifierTemp = node.DisplayText == "Ctrl" || node.DisplayText == "Shift" || node.DisplayText == "Alt" || node.DisplayText == "Win"
    //|| node.DisplayText == "CapsLk" || node.DisplayText == "Tab";
    //            // 绑定 Click
    //            if (isModifierTemp)
    //            {
    //                node.UiButton.Click += (s, e) => NodeActionHandlers.HandleModifierClick(node);
    //            }
    //            else if (!isLeaf && trigger.OnClickForExpandable == TriggerAction.ClickExpandChildren)
    //            {
    //                node.UiButton.Click += (s, e) =>
    //                {
    //                    // 确保设置 pending center（有些路径可能没有调用 ExpandChildren 内的设置，但为了双重保险）
    //                    MainWindow.Instance?.SetPendingCenter(node.CenterX, node.CenterY);
    //                    NodeActionHandlers.ExpandChildren(node);
    //                };
    //            }
    //            else
    //            {
    //                node.UiButton.Click += (s, e) => NodeActionHandlers.InjectText(node);
    //            }

    //            // 绑定 MouseEnter（外观 + 展开 + 停止计时器）
    //            // 先绑定停止自动返回计时器（无论何种触发器）
    //            //node.UiButton.MouseEnter += (s, e) => MainWindow.StopAutoBackTimer();

    //            //if (!isLeaf && !isModifier && trigger.OnMouseEnterForExpandable == TriggerAction.MouseEnterExpandChildren)
    //            //{
    //            //    node.UiButton.MouseEnter += (s, e) => NodeActionHandlers.ExpandChildren(node);
    //            //}

    //            //if (trigger.OnMouseEnterAppearance == TriggerAction.MouseEnterChangeAppearance)
    //            //{
    //            //    node.UiButton.MouseEnter += (s, e) =>
    //            //    {
    //            //        if (trigger.HoverBackground.HasValue)
    //            //            node.UiButton.Background = new SolidColorBrush(trigger.HoverBackground.Value);
    //            //        if (trigger.HoverScale.HasValue)
    //            //        {
    //            //            node.UiButton.RenderTransform = new ScaleTransform(trigger.HoverScale.Value, trigger.HoverScale.Value);
    //            //            node.UiButton.RenderTransformOrigin = new Point(0.5, 0.5);
    //            //        }
    //            //    };
    //            //}

    //            // 绑定 MouseLeave（外观恢复 + 启动返回计时器）
    //            //if (trigger.OnMouseLeaveAppearance == TriggerAction.MouseLeaveRestoreAppearance)
    //            //{
    //            //    node.UiButton.MouseLeave += (s, e) =>
    //            //    {
    //            //        if (trigger.AppearanceBackground.HasValue)
    //            //            node.UiButton.Background = new SolidColorBrush(trigger.AppearanceBackground.Value);
    //            //        node.UiButton.RenderTransform = new ScaleTransform(1.0, 1.0);
    //            //    };
    //            //}

    //            //if (trigger.OnMouseLeaveForBack == TriggerAction.MouseLeaveBack)
    //            //{
    //            //    node.UiButton.MouseLeave += (s, e) => MainWindow.TriggerDelayedBack();
    //            //}

    //            // 递归子节点
    //            if (includeChildren && node.Children.Count > 0)
    //                Bind(node.Children);
    //        }
    //    }

    //    // 这些只是为了 -= 时编译通过，不会实际调用
    //    private static void OnButtonClick(object sender, RoutedEventArgs e) { }
    //    private static void OnButtonMouseEnter(object sender, System.Windows.Input.MouseEventArgs e) { }
    //    private static void OnButtonMouseLeave(object sender, System.Windows.Input.MouseEventArgs e) { }
    //}
}