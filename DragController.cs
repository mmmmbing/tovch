using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace full_AI_tovch
{
    public static class DragController
    {
        public const double DragThreshold = 2.0;
        private const double hoverTimeMs = 500;

        private static MenuItemNode dragSource;
        private static Button dragGhost;
        private static Point startMousePoint;
        private static bool isDragging;
        private static bool suspendUpdate;          // 层级切换后短暂抑制检测

        private static HashSet<MenuItemNode> intersectingNodes = new HashSet<MenuItemNode>();

        private static List<MenuItemNode> combinedNodes = new List<MenuItemNode>();
        private static HashSet<Button> highlightedButtons = new HashSet<Button>();

        private static DispatcherTimer hoverTimer;
        private static MenuItemNode hoveredNode;

        private static Button registeredCenterButton;
        private static bool isHoveringCenter;
        private static DispatcherTimer centerHoverTimer;


        private static MenuItemNode rightClickInlineHoverNode;   // 当前悬停的右键内联节点

        public static Action<MenuItemNode> ExpandAction { get; set; }
        public static Action BackAction { get; set; }

        //private static Dictionary<MenuItemNode, DateTime> lastAddTime = new Dictionary<MenuItemNode, DateTime>();
        //private const double RepeatIntervalMs = 150;

        public static bool IsDragging => isDragging;
        public static MenuItemNode DragSource => dragSource;

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        private static DateTime lastDragEndTime;
        public static DateTime LastDragEndTime => lastDragEndTime;


        static DragController()
        {
            hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(hoverTimeMs) };
            hoverTimer.Tick += OnHoverTimerTick;
            centerHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(hoverTimeMs) };
            centerHoverTimer.Tick += (s, e) =>
            {
                centerHoverTimer.Stop();
                if (isHoveringCenter) BackAction?.Invoke();
                isHoveringCenter = false;
            };
        }


        

        // 新增委托，由 MainWindow 注册为 PerformInlineExpand
        public static Action<MenuItemNode> RightClickInlineExpandAction { get; set; }

        public static void OnRightClickWhileDragging(ref bool handled)
{
    if (!isDragging) return;
    if (rightClickInlineHoverNode != null)
    {
        RightClickInlineExpandAction?.Invoke(rightClickInlineHoverNode);
        handled = true;
    }
}

        public static void RegisterCenterButton(Button btn) => registeredCenterButton = btn;

        public static bool StartDrag(MenuItemNode node, Button button, Point mousePos)
        {
            // 禁止拖拽有子节点且非修饰键的节点
            if (node.Children.Count > 0 && !ModifierKeyConfig.IsModifierKey(node.Path))
                return false;

            // 强制清理旧的一次，避免残留
            if (isDragging || dragSource != null)
                CancelDrag(button.Parent as Canvas);

            if (node == null || button == null) return false;

            dragSource = node;
            startMousePoint = mousePos;
            isDragging = false;
            suspendUpdate = false;

            combinedNodes.Clear();
            combinedNodes.Add(node);
            highlightedButtons.Clear();

            button.Opacity = 0.5;
            dragGhost = null;
            hoveredNode = null;
            hoverTimer.Stop();
            isHoveringCenter = false;
            centerHoverTimer.Stop();

            intersectingNodes.Clear();
            //lastAddTime.Clear();
            return true;
        }

        public static void OnMouseMove(Point currentMousePos, Canvas canvas, List<MenuItemNode> currentLevelNodes)
        {
            if (dragSource == null) return;

            if (!isDragging)
            {
                if (Math.Abs(currentMousePos.X - startMousePoint.X) > DragController.DragThreshold ||
                    Math.Abs(currentMousePos.Y - startMousePoint.Y) > DragController.DragThreshold)
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

            // **无论是否暂停，幽灵都要跟随鼠标**（视觉流畅）
            if (dragGhost != null)
            {
                Canvas.SetLeft(dragGhost, currentMousePos.X - dragGhost.Width / 2);
                Canvas.SetTop(dragGhost, currentMousePos.Y - dragGhost.Height / 2);
            }

            // 暂停检测时跳过碰撞，避免卡顿与错误组队
            if (suspendUpdate) return;

            Rect ghostRect = GetGhostRect();
            UpdateCombinedAndHover(currentLevelNodes, ghostRect);
            UpdateCenterHover(ghostRect);
        }

        public static void EndDrag(Point releasePos, Canvas canvas, List<MenuItemNode> currentLevelNodes)
        {
            if (dragSource == null) return;
            if (isDragging)
            {
                ExecuteComboInjection(new List<MenuItemNode>(combinedNodes));
            }
            Cleanup(canvas, currentLevelNodes);
            lastDragEndTime = DateTime.MinValue;               // 记录时间
            //MainWindow.Instance?.RefreshCenterButton();        // 强制重建
        }

        public static void CancelDrag(Canvas canvas)
        {
            Cleanup(canvas);
        }

        public static void LevelChanged(Canvas newCanvas, List<MenuItemNode> newLevelNodes, Point currentMousePos)
        {
            if (!isDragging || dragSource == null) return;

            suspendUpdate = true;                        // 暂停检测，防止切换中大量计算

            if (dragGhost != null)
            {
                if (dragGhost.Parent is Panel oldParent)
                    oldParent.Children.Remove(dragGhost);
                newCanvas.Children.Add(dragGhost);
                // 立即更新位置到当前鼠标
                Canvas.SetLeft(dragGhost, currentMousePos.X - dragGhost.Width / 2);
                Canvas.SetTop(dragGhost, currentMousePos.Y - dragGhost.Height / 2);
            }

            // 重置所有悬停状态
            ResetHoverTimer();
            isHoveringCenter = false;
            centerHoverTimer.Stop();
            registeredCenterButton = null;

            startMousePoint = currentMousePos;

            // 延迟到界面完全空闲后再恢复检测，给新层级布局充足时间
            newCanvas.Dispatcher.BeginInvoke(new Action(() =>
            {
                suspendUpdate = false;
            }), DispatcherPriority.ContextIdle);
        }

        // ---------- 内部方法 ----------
        private static Rect GetGhostRect()
        {
            if (dragGhost == null) return Rect.Empty;
            double l = Canvas.GetLeft(dragGhost);
            double t = Canvas.GetTop(dragGhost);
            if (double.IsNaN(l) || double.IsNaN(t)) return Rect.Empty;
            return new Rect(l, t, dragGhost.ActualWidth, dragGhost.ActualHeight);
        }

        private static void UpdateCombinedAndHover(List<MenuItemNode> currentLevelNodes, Rect ghostRect)
        {
            if (ghostRect.IsEmpty) return;

            var (ghostCenter, ghostRadius) = GetGhostCircle();
            if (double.IsNaN(ghostCenter.X)) return;

            MenuItemNode foundNode = null;
            foreach (var node in currentLevelNodes)
            {
                // 不再跳过 dragSource，允许自身相交
                Button btn = node.UiButton;
                if (btn == null || !btn.IsVisible) continue;

                var (btnCenter, btnRadius) = GetCircle(btn);
                if (double.IsNaN(btnCenter.X)) continue;

                double dx = ghostCenter.X - btnCenter.X;
                double dy = ghostCenter.Y - btnCenter.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance <= ghostRadius + btnRadius)
                {
                    foundNode = node;
                    break;
                }
            }

            if (foundNode != null)
            {
                bool isLeaf = foundNode.Children.Count == 0;
                bool isMod = ModifierKeyConfig.IsModifierKey(foundNode.Path);
                bool isExpandableNonMod = !isLeaf && !isMod;

                if (!isExpandableNonMod)
                {
                    // 仅当这次相交是新进入（之前未相交）时添加
                    if (!intersectingNodes.Contains(foundNode))
                    {
                        intersectingNodes.Add(foundNode);
                        combinedNodes.Add(foundNode);
                        if (!highlightedButtons.Contains(foundNode.UiButton))
                        {
                            foundNode.UiButton.Background = Brushes.LightGreen;
                            highlightedButtons.Add(foundNode.UiButton);
                        }
                    }
                }
                else
                {
                    // 可展开节点悬停逻辑
                    if (hoveredNode != foundNode)
                    {
                        ResetHoverTimer();
                        hoveredNode = foundNode;
                        hoverTimer.Start();
                    }
                }
            }
            else
            {
                ResetHoverTimer();
                intersectingNodes.Clear();   // 无相交，清空集合
            }

            // 恢复不再相交的高亮节点（原有逻辑保留，但要确保从 intersectingNodes 中移除相应节点）
            foreach (var btn in new List<Button>(highlightedButtons))
            {
                MenuItemNode node = btn.Tag as MenuItemNode;
                if (node != foundNode)
                {
                    UpdateNodeAppearance(btn);
                    highlightedButtons.Remove(btn);
                    intersectingNodes.Remove(node);   // 同时从相交集合移除
                }
            }
        }



        private static void UpdateCenterHover(Rect ghostRect)
        {
            if (registeredCenterButton == null || !registeredCenterButton.IsVisible || ghostRect.IsEmpty || suspendUpdate) return;

            var (ghostCenter, ghostRadius) = GetGhostCircle();
            if (double.IsNaN(ghostCenter.X)) return;

            var (btnCenter, btnRadius) = GetCircle(registeredCenterButton);
            if (double.IsNaN(btnCenter.X)) return;

            double dx = ghostCenter.X - btnCenter.X;
            double dy = ghostCenter.Y - btnCenter.Y;
            bool intersecting = Math.Sqrt(dx * dx + dy * dy) <= ghostRadius + btnRadius;

            if (intersecting)
            {
                if (!isHoveringCenter) { isHoveringCenter = true; centerHoverTimer.Start(); }
            }
            else
            {
                if (isHoveringCenter) { isHoveringCenter = false; centerHoverTimer.Stop(); }
            }
        }

        private static void ResetHoverTimer()
        {
            hoverTimer.Stop();
            hoveredNode = null;
            intersectingNodes.Clear();   // 添加这行
            //ResetHoverTimer();
        }

        private static void OnHoverTimerTick(object sender, EventArgs e)
        {
            hoverTimer.Stop();
            if (hoveredNode != null) ExpandAction?.Invoke(hoveredNode);
            hoveredNode = null;
        }

        private static Button CreateGhost(MenuItemNode node)
        {
            Color c = Colors.Gray;
            if (node.UiButton?.Background is SolidColorBrush b) c = b.Color;
            Button ghost = new Button
            {
                Width = node.ButtonSize,
                Height = node.ButtonSize,
                Content = node.DisplayText,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(180, c.R, c.G, c.B)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                IsHitTestVisible = false,
                Focusable = false,
                Opacity = 0.8
            };
            ghost.Opacity = 0;
            var fadeIn = new DoubleAnimation(1, TimeSpan.FromSeconds(0.15));
            ghost.BeginAnimation(UIElement.OpacityProperty, fadeIn);
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

        // 核心注入逻辑（保持原有）
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

            // 只有修饰键节点：激活它们
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

            // 构建最终修饰键集合
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

            // 发送第一个普通节点（可带修饰键），然后依次发送剩余节点文本
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

            // 发送后的修饰键清理（根据配置）
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
        private static void UpdateNodeAppearance(Button btn)
        {
            if (btn?.Tag is MenuItemNode node)
            {
                NodeActionHandlers.UpdateModifierKeyAppearance?.Invoke(node);
            }
            else
            {
                btn.Background = Brushes.LightBlue;
            }
        }

        private static void RestoreSource()
        {
            if (dragSource?.UiButton != null) dragSource.UiButton.Opacity = 1.0;
            dragSource = null;
        }

        private static void RemoveGhost()
        {
            if (dragGhost != null)
            {
                var fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.1));
                fadeOut.Completed += (s, e) =>
                {
                    if (dragGhost != null && dragGhost.Parent is Panel p)
                        p.Children.Remove(dragGhost);
                    dragGhost = null;
                };
                dragGhost.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }

        private static void Cleanup(Canvas canvas, List<MenuItemNode> currentLevelNodes = null)
        {
            ResetHoverTimer();
            isHoveringCenter = false;
            centerHoverTimer.Stop();
            RemoveGhost();
            RestoreSource();

            // 恢复高亮集合中的按钮
            foreach (var btn in highlightedButtons)
                UpdateNodeAppearance(btn);
            highlightedButtons.Clear();
            rightClickInlineHoverNode = null;

            // 额外遍历当前层级所有节点，强制重置外观（防止漏网之鱼）
            if (currentLevelNodes != null)
            {
                foreach (var node in currentLevelNodes)
                {
                    if (node.UiButton != null)
                    {
                        node.UiButton.Opacity = 1.0;          // 强制恢复透明度
                        UpdateNodeAppearance(node.UiButton);   // 恢复背景色
                    }
                    // 递归子节点（如果有显示的话，但当前层级通常不包含隐藏子节点）
                }
            }

            combinedNodes.Clear();
            isDragging = false;
            intersectingNodes.Clear();
            //lastAddTime.Clear();
        }

        public static (Point center, double radius) GetCircle(Button btn)
        {
            if (btn == null) return (new Point(double.NaN, double.NaN), 0);
            double left = Canvas.GetLeft(btn);
            double top = Canvas.GetTop(btn);
            if (double.IsNaN(left) || double.IsNaN(top)) return (new Point(double.NaN, double.NaN), 0);
            double radius = btn.ActualWidth / 2;
            Point center = new Point(left + radius, top + radius);
            return (center, radius);
        }

        private static (Point center, double radius) GetGhostCircle()
        {
            if (dragGhost == null) return (new Point(double.NaN, double.NaN), 0);
            double left = Canvas.GetLeft(dragGhost);
            double top = Canvas.GetTop(dragGhost);
            if (double.IsNaN(left) || double.IsNaN(top)) return (new Point(double.NaN, double.NaN), 0);
            double radius = dragGhost.ActualWidth > 0 ? dragGhost.ActualWidth / 2 : dragSource.ButtonSize / 2;
            Point center = new Point(left + radius, top + radius);
            return (center, radius);
        }

    }
}