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
        private const double dragThreshold = 8.0;
        private const double hoverTimeMs = 500;

        private static MenuItemNode dragSource;
        private static Button dragGhost;
        private static Point startMousePoint;
        private static bool isDragging;
        private static bool suspendUpdate;          // 层级切换后短暂抑制检测

        private static List<MenuItemNode> combinedNodes = new List<MenuItemNode>();
        private static HashSet<Button> highlightedButtons = new HashSet<Button>();

        private static DispatcherTimer hoverTimer;
        private static MenuItemNode hoveredNode;

        private static Button registeredCenterButton;
        private static bool isHoveringCenter;
        private static DispatcherTimer centerHoverTimer;

        public static Action<MenuItemNode> ExpandAction { get; set; }
        public static Action BackAction { get; set; }

        public static bool IsDragging => isDragging;
        public static MenuItemNode DragSource => dragSource;

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

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

        public static void RegisterCenterButton(Button btn) => registeredCenterButton = btn;

        public static void StartDrag(MenuItemNode node, Button button, Point mousePos)
        {
            // 强制清理旧状态（幽灵残留等）
            if (isDragging || dragSource != null)
            {
                if (button.Parent is Canvas c) CancelDrag(c);
                else Cleanup(null);
            }

            if (node == null || button == null) return;
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
        }

        public static void OnMouseMove(Point currentMousePos, Canvas canvas, List<MenuItemNode> currentLevelNodes)
        {
            if (dragSource == null) return;

            // 暂停更新期间直接返回
            if (suspendUpdate) return;

            // 如果幽灵意外残留但未拖动，予以清除
            if (!isDragging && dragGhost != null)
            {
                RemoveGhost();
                return;
            }

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

            Rect ghostRect = GetGhostRect();
            UpdateCombinedAndHover(currentLevelNodes, ghostRect);
            UpdateCenterHover(ghostRect);
        }

        public static void EndDrag(Point releasePos, Canvas canvas, List<MenuItemNode> currentLevelNodes)
        {
            if (dragSource == null) return;

            bool wasDragging = isDragging;
            if (wasDragging)
            {
                ExecuteComboInjection(new List<MenuItemNode>(combinedNodes));
            }

            Cleanup(canvas);
        }

        public static void CancelDrag(Canvas canvas) => Cleanup(canvas);

        public static void LevelChanged(Canvas newCanvas, List<MenuItemNode> newLevelNodes, Point currentMousePos)
        {
            if (!isDragging || dragSource == null) return;

            // 暂停检测，避免切换期间大量调用导致卡顿
            suspendUpdate = true;

            if (dragGhost != null)
            {
                if (dragGhost.Parent is Panel oldParent)
                    oldParent.Children.Remove(dragGhost);
                newCanvas.Children.Add(dragGhost);
                Canvas.SetLeft(dragGhost, currentMousePos.X - dragGhost.Width / 2);
                Canvas.SetTop(dragGhost, currentMousePos.Y - dragGhost.Height / 2);
            }

            ResetHoverTimer();
            isHoveringCenter = false;
            centerHoverTimer.Stop();
            registeredCenterButton = null;

            startMousePoint = currentMousePos;

            // 下一帧恢复检测
            newCanvas.Dispatcher.BeginInvoke(new Action(() => suspendUpdate = false),
                DispatcherPriority.Loaded);
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

            MenuItemNode foundNode = null;
            foreach (var node in currentLevelNodes)
            {
                if (node == dragSource) continue;
                Button btn = node.UiButton;
                if (btn == null) continue;
                double l = Canvas.GetLeft(btn), t = Canvas.GetTop(btn);
                if (double.IsNaN(l) || double.IsNaN(t)) continue;
                if (ghostRect.IntersectsWith(new Rect(l, t, btn.ActualWidth, btn.ActualHeight)))
                { foundNode = node; break; }
            }

            if (foundNode != null)
            {
                bool isLeaf = foundNode.Children.Count == 0;
                bool isMod = ModifierKeyConfig.IsModifierKey(foundNode.Path);
                bool isExpandableNonMod = !isLeaf && !isMod;

                if (!isExpandableNonMod)
                {
                    if (!combinedNodes.Contains(foundNode))
                        combinedNodes.Add(foundNode);
                    if (!highlightedButtons.Contains(foundNode.UiButton))
                    {
                        foundNode.UiButton.Background = Brushes.LightGreen;
                        highlightedButtons.Add(foundNode.UiButton);
                    }
                }

                if (isExpandableNonMod)
                {
                    if (hoveredNode != foundNode)
                    {
                        ResetHoverTimer();
                        hoveredNode = foundNode;
                        hoverTimer.Start();
                    }
                }
                else ResetHoverTimer();
            }
            else ResetHoverTimer();

            // 恢复不再相交的高亮节点外观
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
            if (registeredCenterButton == null || !registeredCenterButton.IsVisible || ghostRect.IsEmpty || suspendUpdate) return;
            double l = Canvas.GetLeft(registeredCenterButton), t = Canvas.GetTop(registeredCenterButton);
            if (double.IsNaN(l) || double.IsNaN(t)) return;
            if (ghostRect.IntersectsWith(new Rect(l, t, registeredCenterButton.ActualWidth, registeredCenterButton.ActualHeight)))
            {
                if (!isHoveringCenter) { isHoveringCenter = true; centerHoverTimer.Start(); }
            }
            else
            {
                if (isHoveringCenter) { isHoveringCenter = false; centerHoverTimer.Stop(); }
            }
        }

        private static void ResetHoverTimer() { hoverTimer.Stop(); hoveredNode = null; }

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
                NodeActionHandlers.UpdateModifierKeyAppearance?.Invoke(node);
            else
                btn.Background = Brushes.LightBlue;
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
                if (dragGhost.Parent is Panel p) p.Children.Remove(dragGhost);
                dragGhost = null;
            }
        }

        private static void Cleanup(Canvas canvas)
        {
            suspendUpdate = false;
            ResetHoverTimer();
            isHoveringCenter = false;
            centerHoverTimer.Stop();
            RemoveGhost();
            RestoreSource();
            foreach (var btn in highlightedButtons) UpdateNodeAppearance(btn);
            highlightedButtons.Clear();
            combinedNodes.Clear();
            isDragging = false;
        }
    }
}