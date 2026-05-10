using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace full_AI_tovch
{
    public static class DragController
    {
        private static MenuItemNode dragSource;
        private static Button dragGhost;
        private static Point startMousePoint;
        private static bool isDragging = false;
        private const double dragThreshold = 5.0;

        // 当前拖拽过程中扫过的所有节点（包含源节点），用于最终组合注入
        private static List<MenuItemNode> combinedNodes = new List<MenuItemNode>();

        // 当前高亮的节点（绿色）集合，方便恢复外观
        private static HashSet<Button> highlightedButtons = new HashSet<Button>();

        public static bool IsDragging => isDragging;

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        public static MenuItemNode DragSource => dragSource;

        public static void StartDrag(MenuItemNode node, Button button, Point mousePos)
        {
            if (node == null || button == null) return;
            dragSource = node;
            startMousePoint = mousePos;
            isDragging = false;

            combinedNodes.Clear();
            combinedNodes.Add(node);   // 源节点始终在第一位
            highlightedButtons.Clear();

            button.Opacity = 0.5;
            dragGhost = null;
        }

        public static void OnMouseMove(Point currentMousePos, Canvas canvas, List<MenuItemNode> currentLevelNodes)
        {
            if (dragSource == null) return;

            if (!isDragging)
            {
                if (Math.Abs(currentMousePos.X - startMousePoint.X) > dragThreshold ||
                    Math.Abs(currentMousePos.Y - startMousePoint.Y) > dragThreshold)
                {
                    isDragging = true;
                    if (dragGhost == null)
                    {
                        dragGhost = CreateGhost(dragSource);
                        canvas.Children.Add(dragGhost);
                    }
                }
                else return;
            }

            if (dragGhost != null)
            {
                Canvas.SetLeft(dragGhost, currentMousePos.X - dragGhost.Width / 2);
                Canvas.SetTop(dragGhost, currentMousePos.Y - dragGhost.Height / 2);
            }

            // 更新重叠节点集合
            UpdateCombinedNodes(currentLevelNodes, dragGhost);
        }

        public static void EndDrag(Point releasePos, Canvas canvas, List<MenuItemNode> currentLevelNodes)
        {
            if (dragSource == null) return;
            if (!isDragging)
            {
                RestoreSource();
                return;
            }

            // 使用最终编组集合执行注入
            ExecuteComboInjection(new List<MenuItemNode>(combinedNodes));
            Cleanup(canvas);
        }

        public static void CancelDrag(Canvas canvas)
        {
            if (dragSource != null)
            {
                RestoreSource();
                Cleanup(canvas);
            }
        }

        /// <summary>
        /// 根据幽灵位置动态维护编组集合：新重叠的加入，不再重叠的移除（源节点除外）
        /// </summary>
        private static void UpdateCombinedNodes(List<MenuItemNode> currentLevelNodes, Button ghost)
        {
            if (ghost == null) return;
            double ghostLeft = Canvas.GetLeft(ghost);
            double ghostTop = Canvas.GetTop(ghost);
            if (double.IsNaN(ghostLeft) || double.IsNaN(ghostTop)) return;
            Rect ghostRect = new Rect(ghostLeft, ghostTop, ghost.ActualWidth, ghost.ActualHeight);

            foreach (var node in currentLevelNodes)
            {
                if (node == dragSource) continue;
                Button btn = node.UiButton;
                if (btn == null || node.DisplayText == CenterNodeConfig.Text) continue;

                double left = Canvas.GetLeft(btn);
                double top = Canvas.GetTop(btn);
                if (double.IsNaN(left) || double.IsNaN(top)) continue;
                Rect targetRect = new Rect(left, top, btn.ActualWidth, btn.ActualHeight);

                bool overlapping = ghostRect.IntersectsWith(targetRect);

                if (overlapping)
                {
                    // 加入编组（若尚未加入）
                    if (!combinedNodes.Contains(node))
                        combinedNodes.Add(node);

                    // 高亮该按钮（如果还没高亮）
                    if (!highlightedButtons.Contains(btn))
                    {
                        btn.Background = Brushes.LightGreen;
                        highlightedButtons.Add(btn);
                    }
                }
                else
                {
                    // 离开该节点：恢复外观，但不从 combinedNodes 移除
                    if (highlightedButtons.Contains(btn))
                    {
                        UpdateNodeAppearance(btn);
                        highlightedButtons.Remove(btn);
                    }
                }
            }
        }

        /// <summary>恢复单个按钮的外观（保留修饰键激活状态等）</summary>
        private static void UpdateNodeAppearance(Button btn)
        {
            if (btn?.Tag is MenuItemNode node)
                NodeActionHandlers.UpdateModifierKeyAppearance?.Invoke(node);
            else
                btn.Background = Brushes.LightBlue;
        }

        private static Button CreateGhost(MenuItemNode node)
        {
            Color sourceColor = Colors.Gray;
            if (node.UiButton?.Background is SolidColorBrush brush)
                sourceColor = brush.Color;

            Button ghost = new Button
            {
                Width = node.ButtonSize,
                Height = node.ButtonSize,
                Content = node.DisplayText,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(180, sourceColor.R, sourceColor.G, sourceColor.B)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                IsHitTestVisible = false,
                Focusable = false,
                Opacity = 0.8
            };

            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(node.ButtonSize / 2));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;
            ghost.Template = template;

            return ghost;
        }

        private static void ExecuteComboInjection(List<MenuItemNode> nodes)
        {
            if (nodes.Count == 0) return;

            List<MenuItemNode> modifierNodes = new List<MenuItemNode>();
            List<MenuItemNode> normalNodes = new List<MenuItemNode>();

            foreach (var node in nodes)
            {
                if (ModifierKeyConfig.IsModifierKey(node.Path))
                    modifierNodes.Add(node);
                else
                    normalNodes.Add(node);
            }

            if (normalNodes.Count == 0)
            {
                ModifierKeyState.ClearModifiers();
                foreach (var modNode in modifierNodes)
                {
                    if (ModifierKeyConfig.ModifierKeyMap.TryGetValue(modNode.Path, out Key key))
                    {
                        if (key == Key.CapsLock)
                            ModifierKeyState.ToggleCapsLock();
                        else
                            ModifierKeyState.AddModifier(key);
                    }
                }
                NodeActionHandlers.UpdateModifierStatusUI?.Invoke();
                return;
            }

            ModifierKeys mods = ModifierKeyState.GetCurrentModifierKeys();
            foreach (var modNode in modifierNodes)
            {
                if (ModifierKeyConfig.ModifierKeyMap.TryGetValue(modNode.Path, out Key key))
                {
                    if (key != Key.CapsLock)
                        mods |= NodeActionHandlers.KeyToModifier(key);
                    else
                        ModifierKeyState.ToggleCapsLock();
                }
            }

            if (mods != ModifierKeys.None)
            {
                MenuItemNode first = normalNodes[0];
                Key firstKey = GetKeyFromNode(first);
                if (firstKey != Key.None)
                    TextInjection.SendKey(firstKey, mods);
                else
                    TextInjection.Send(first.DisplayText);

                for (int i = 1; i < normalNodes.Count; i++)
                    TextInjection.Send(normalNodes[i].DisplayText);
            }
            else
            {
                foreach (var node in normalNodes)
                    TextInjection.Send(node.DisplayText);
            }

            if (ModifierKeyState.AutoClearAfterSend)
                ModifierKeyState.ClearModifiers();
            NodeActionHandlers.UpdateModifierStatusUI?.Invoke();
        }

        private static Key GetKeyFromNode(MenuItemNode node)
        {
            if (node.DisplayText.Length == 1)
            {
                short vkScan = VkKeyScan(node.DisplayText[0]);
                return KeyInterop.KeyFromVirtualKey((int)(vkScan & 0xFF));
            }
            else if (Enum.TryParse<Key>(node.DisplayText, true, out Key key))
                return key;
            return Key.None;
        }

        private static void RestoreSource()
        {
            if (dragSource?.UiButton != null)
                dragSource.UiButton.Opacity = 1.0;
            dragSource = null;
        }

        private static void Cleanup(Canvas canvas)
        {
            // 移除幽灵
            if (dragGhost != null)
            {
                if (dragGhost.Parent is Panel parent)
                    parent.Children.Remove(dragGhost);
                dragGhost = null;
            }

            RestoreSource();

            // 恢复所有被高亮过的按钮（可能已部分恢复，但无妨）
            foreach (var btn in highlightedButtons)
                UpdateNodeAppearance(btn);
            highlightedButtons.Clear();
            combinedNodes.Clear();

            isDragging = false;
        }
    }
}