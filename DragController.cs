using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace full_AI_tovch
{
    public static class DragController
    {
        // 原有字段
        private static MenuItemNode dragSource;
        private static Button dragGhost;
        private static Point startMousePoint;
        private static bool isDragging = false;
        private const double dragThreshold = 5.0;
        private const double hoverTimeMs = 500;

        private static List<MenuItemNode> combinedNodes = new List<MenuItemNode>();
        private static HashSet<Button> highlightedButtons = new HashSet<Button>();

        // 悬停相关
        private static DispatcherTimer hoverTimer;
        private static MenuItemNode hoveredNode;
        private static Point lastMousePos;

        // 中心按钮悬停
        private static Button registeredCenterButton;
        private static bool isHoveringCenter;
        private static DispatcherTimer centerHoverTimer;
        //public static void RegisterCenterButton(Button btn) => registeredCenterButton = btn;
        public static Action<MenuItemNode> ExpandAction { get; set; }
        public static Action BackAction { get; set; }

        public static bool IsDragging => isDragging;
        public static MenuItemNode DragSource => dragSource;

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        static DragController()
        {
            hoverTimer = new DispatcherTimer();
            hoverTimer.Interval = TimeSpan.FromMilliseconds(hoverTimeMs);
            hoverTimer.Tick += OnHoverTimerTick;

            centerHoverTimer = new DispatcherTimer();
            centerHoverTimer.Interval = TimeSpan.FromMilliseconds(hoverTimeMs);
            centerHoverTimer.Tick += (s, e) =>
            {
                centerHoverTimer.Stop();
                if (isHoveringCenter)
                    BackAction?.Invoke();
                isHoveringCenter = false;
            };
        }

        public static void RegisterCenterButton(Button btn)
        {
            registeredCenterButton = btn;
        }

        public static void StartDrag(MenuItemNode node, Button button, Point mousePos)
        {
            if (node == null || button == null) return;
            dragSource = node;
            startMousePoint = mousePos;
            isDragging = false;

            combinedNodes.Clear();
            combinedNodes.Add(node);
            highlightedButtons.Clear();

            button.Opacity = 0.5;
            dragGhost = null;
            hoveredNode = null;
            hoverTimer.Stop();
            isHoveringCenter = false;
            centerHoverTimer.Stop();
        }

        public static void OnMouseMove(Point currentMousePos, Canvas canvas, List<MenuItemNode> currentLevelNodes)
        {
            if (dragSource == null) return;
            lastMousePos = currentMousePos;

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

            // 计算幽灵矩形（供后续方法使用）
            Rect ghostRect = Rect.Empty;
            if (dragGhost != null)
            {
                double gl = Canvas.GetLeft(dragGhost);
                double gt = Canvas.GetTop(dragGhost);
                if (!double.IsNaN(gl) && !double.IsNaN(gt))
                    ghostRect = new Rect(gl, gt, dragGhost.ActualWidth, dragGhost.ActualHeight);
            }

            // 更新编组与悬停
            UpdateCombinedAndHover(currentLevelNodes, dragGhost, currentMousePos, ghostRect);

            // 中心节点悬停检测
            UpdateCenterHover(ghostRect);
        }

        public static void EndDrag(Point releasePos, Canvas canvas, List<MenuItemNode> currentLevelNodes)
        {
            if (dragSource == null) return;
            if (!isDragging)
            {
                RestoreSource();
                return;
            }

            ExecuteComboInjection(new List<MenuItemNode>(combinedNodes));
            Cleanup(canvas);
        }

        public static void CancelDrag(Canvas canvas)
        {
            if (dragSource != null)
                Cleanup(canvas);
        }

        // ---------- 编组与悬停 ----------
        private static void UpdateCombinedAndHover(List<MenuItemNode> currentLevelNodes, Button ghost, Point mousePos, Rect ghostRect)
        {
            if (ghost == null || ghostRect.IsEmpty) return;

            MenuItemNode foundNode = null;
            foreach (var node in currentLevelNodes)
            {
                if (node == dragSource) continue;
                Button btn = node.UiButton;
                if (btn == null) continue;
                double left = Canvas.GetLeft(btn);
                double top = Canvas.GetTop(btn);
                if (double.IsNaN(left) || double.IsNaN(top)) continue;
                Rect targetRect = new Rect(left, top, btn.ActualWidth, btn.ActualHeight);
                if (ghostRect.IntersectsWith(targetRect))
                {
                    foundNode = node;
                    break;
                }
            }

            // 处理叶子节点编组、悬停展开
            if (foundNode != null)
            {
                bool isLeaf = foundNode.Children.Count == 0;
                bool isMod = ModifierKeyConfig.IsModifierKey(foundNode.Path);
                bool isExpandableNonMod = !isLeaf && !isMod;

                if (!isExpandableNonMod)
                {
                    // 叶子或修饰键节点 → 立即编组
                    if (!combinedNodes.Contains(foundNode))
                        combinedNodes.Add(foundNode);
                    if (!highlightedButtons.Contains(foundNode.UiButton))
                    {
                        foundNode.UiButton.Background = Brushes.LightGreen;
                        highlightedButtons.Add(foundNode.UiButton);
                    }
                }

                // 悬停动作（中心节点另由 UpdateCenterHover 处理，这里忽略中心）
                if (isExpandableNonMod)
                {
                    if (hoveredNode != foundNode)
                    {
                        ResetHoverTimer();
                        hoveredNode = foundNode;
                        hoverTimer.Start();
                    }
                }
                else
                {
                    ResetHoverTimer();
                }
            }
            else
            {
                ResetHoverTimer();
            }

            // 离开的高亮节点恢复外观
            foreach (var btn in new List<Button>(highlightedButtons))
            {
                MenuItemNode node = btn.Tag as MenuItemNode;
                if (node != foundNode)
                {
                    UpdateNodeAppearance(btn);
                    highlightedButtons.Remove(btn);
                }
            }
        }

        private static void UpdateCenterHover(Rect ghostRect)
        {
            if (registeredCenterButton == null || !registeredCenterButton.IsVisible) return;
            double left = Canvas.GetLeft(registeredCenterButton);
            double top = Canvas.GetTop(registeredCenterButton);
            if (double.IsNaN(left) || double.IsNaN(top)) return;
            Rect centerRect = new Rect(left, top, registeredCenterButton.ActualWidth, registeredCenterButton.ActualHeight);
            bool overlapping = ghostRect.IntersectsWith(centerRect);
            if (overlapping)
            {
                if (!isHoveringCenter)
                {
                    isHoveringCenter = true;
                    centerHoverTimer.Start();
                }
            }
            else
            {
                if (isHoveringCenter)
                {
                    isHoveringCenter = false;
                    centerHoverTimer.Stop();
                }
            }
        }

        private static void ResetHoverTimer()
        {
            hoverTimer.Stop();
            hoveredNode = null;
        }

        private static void OnHoverTimerTick(object sender, EventArgs e)
        {
            hoverTimer.Stop();
            if (hoveredNode == null) return;
            if (hoveredNode.DisplayText != CenterNodeConfig.Text)
                ExpandAction?.Invoke(hoveredNode);
            hoveredNode = null;
        }

        // ---------- 外观恢复 ----------
        private static void UpdateNodeAppearance(Button btn)
        {
            if (btn?.Tag is MenuItemNode node)
                NodeActionHandlers.UpdateModifierKeyAppearance?.Invoke(node);
            else
                btn.Background = Brushes.LightBlue;
        }

        // ---------- 幽灵创建 ----------
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

        // ---------- 组合注入 ----------
        /// <summary>
        /// 当菜单层级变化（展开或返回）后调用，将拖拽状态迁移到新的画布。
        /// </summary>
        public static void LevelChanged(Canvas newCanvas, List<MenuItemNode> newLevelNodes, Point currentMousePos)
        {
            if (dragSource == null || !isDragging) return;

            // 如果幽灵存在于旧画布，先移除
            if (dragGhost != null)
            {
                if (dragGhost.Parent is Panel oldParent)
                    oldParent.Children.Remove(dragGhost);
                // 添加到新画布
                newCanvas.Children.Add(dragGhost);
                // 更新位置为当前鼠标 (可以在下次 OnMouseMove 时更新，但这里先置)
                Canvas.SetLeft(dragGhost, currentMousePos.X - dragGhost.Width / 2);
                Canvas.SetTop(dragGhost, currentMousePos.Y - dragGhost.Height / 2);
            }

            // 重置悬停状态（新环境下重新检测）
            ResetHoverTimer();
            isHoveringCenter = false;
            centerHoverTimer.Stop();

            // 中心按钮引用无效，等待新中心按钮注册；由 MainWindow CreateCenterButton 调用 RegisterCenterButton
            registeredCenterButton = null;
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
            ResetHoverTimer();
            isHoveringCenter = false;
            centerHoverTimer.Stop();
            if (dragGhost != null)
            {
                if (dragGhost.Parent is Panel parent)
                    parent.Children.Remove(dragGhost);
                dragGhost = null;
            }
            RestoreSource();
            foreach (var btn in highlightedButtons)
                UpdateNodeAppearance(btn);
            highlightedButtons.Clear();
            combinedNodes.Clear();
            isDragging = false;
        }
    }
}