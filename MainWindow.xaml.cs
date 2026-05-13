using full_AI_tovch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
//using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

//using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml.Linq;

namespace full_AI_tovch
{
    public class MousePositionHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public static Point GetCursorPosition()
        {
            POINT point;
            if (GetCursorPos(out point))
            {
                return point;
            }
            throw new InvalidOperationException("无法获取鼠标位置");
        }
    }
    public partial class MainWindow : Window
    {
        // 整个菜单树的根节点列表（永驻）
        private List<MenuItemNode> rootNodes;
        // 当前显示的节点列表
        private List<MenuItemNode> currentLevelNodes;
        // 历史层级，用于后退（每层保存其节点列表）
        private readonly Stack<List<MenuItemNode>> history = new Stack<List<MenuItemNode>>();
        //private DispatcherTimer autoBackTimer;

        private Point pendingCenterPoint;

        //右键离开后返回上一级菜单
        //private bool enableAutoBack = true;   // 可由 InteractionConfig 控制
        //private int autoBackDelayMs = 50000;


        //是否是修饰节点上右键
        private bool isModifierRightClick = false;

        // 右键长按相关
        private bool isRightButtonPressed = false;
        private System.Windows.Threading.DispatcherTimer longPressTimer;
        private System.Windows.Threading.DispatcherTimer deleteRepeatTimer;
        private DateTime rightButtonDownTime;

        private Button centerButton;
        private double currentCenterX, currentCenterY;
        private Stack<Tuple<double, double>> centerHistory = new Stack<Tuple<double, double>>();

        private Point dragStartPoint;
        private bool isDragActive = false;   
        private bool isPotentialDrag = false;
        private bool dragCaptureStarted = false;

        // 内联展开栈：记录当前内联展开的父节点（可能有嵌套，但子节点不允许再展开，栈深度通常为1）
        private Stack<MenuItemNode> inlineExpandStack = new Stack<MenuItemNode>();
        // 缓存被隐藏的兄弟节点列表（原父节点所在层级的除父节点外的其他节点）
        private List<MenuItemNode> hiddenSiblings = new List<MenuItemNode>();
        // 当前内联展开生成的子节点列表
        private List<MenuItemNode> inlineChildren = new List<MenuItemNode>();

        private static MainWindow _instance;
        private TextBlock statusIndicator;
        public MainWindow()
        {

            InitializeComponent();
            _instance = this;



            this.ShowInTaskbar = false;
            //this.FormBorderStyle = FormBorderStyle.None;
            // 防止窗口被激活抢焦点

            // 构造函数或 SourceInitialized 中
            MenuActivation.ToggleLabelsRequested += OnToggleLabels;


            //初始化计时器
            //autoBackTimer = new DispatcherTimer();
            //autoBackTimer.Interval = TimeSpan.FromMilliseconds(autoBackDelayMs);
            //autoBackTimer.Tick += (s, ev) =>
            //{
            //    autoBackTimer.Stop();
            //    if (enableAutoBack)
            //        GoBack();
            //};

            //this.PreviewMouseRightButtonUp += (s, e) =>
            //{
            //    // 简单判断如果右键按下的时间很短就返回
            //    if ((DateTime.Now - rightButtonDownTime).TotalMilliseconds < InteractionConfig.LongPressThreshold)
            //    {
            //        GoBack();
            //    }
            //};
            this.PreviewMouseRightButtonDown += (s, e) =>
            {
                rightButtonDownTime = DateTime.Now;
            };




            DragController.ExpandAction = (node) => NodeActionHandlers.ExpandChildren(node);
            DragController.BackAction = () => GoBack();
            DragController.RightClickInlineExpandAction = PerformInlineExpand;
            this.PreviewMouseRightButtonDown += OnGlobalRightButtonDownForInlineExpand;

            NodeActionHandlers.UpdateModifierKeyAppearance = (node) =>
            {
                if (node?.UiButton == null) return;
                bool active = false;
                if (ModifierKeyConfig.ModifierKeyMap.TryGetValue(node.Path, out var key))
                {
                    if (key == Key.CapsLock)
                        active = ModifierKeyState.CapsLockActive;
                    else
                        active = ModifierKeyState.GetCurrentModifierKeys().HasFlag(NodeActionHandlers.KeyToModifier(key));
                }
                node.UiButton.Background = active ? Brushes.Gold : Brushes.LightBlue;
            };

            NodeActionHandlers.UpdateModifierStatusUI = () =>
            {
                if (!ModifierKeyConfig.ShowStatusIndicator) return;
                string txt = ModifierKeyState.CapsLockActive ? "CapsLock" : "";
                var mods = ModifierKeyState.GetCurrentModifierKeys();
                if (mods.HasFlag(ModifierKeys.Control)) txt += " Ctrl";
                if (mods.HasFlag(ModifierKeys.Shift)) txt += " Shift";
                if (mods.HasFlag(ModifierKeys.Alt)) txt += " Alt";
                if (mods.HasFlag(ModifierKeys.Windows)) txt += " Win";
                UpdateModifierStatusText(txt.Trim());
            };


            NodeActionHandlers.NavigateToChildren = NavigateToLevel;
            NodeActionHandlers.NavigateBack = GoBack;
            NodeActionHandlers.PrepareChildrenLayout = (parentNode) =>
            {
                LayoutNodesAroundPoint(parentNode.Children, parentNode.CenterX, parentNode.CenterY);
            };

            NodeController.NavigateToChildren = NavigateToLevel;
            NodeController.NavigateBack = GoBack;


            NodeController.PrepareChildrenLayout = (parentNode) =>
            {
                LayoutNodesAroundPoint(parentNode.Children, parentNode.CenterX, parentNode.CenterY);
            };
            NodeController.MouseEnterNode = (node) =>
            {
                if (node.UiButton != null)
                {
                    node.UiButton.Background = Brushes.Orange;
                    node.UiButton.RenderTransform = new ScaleTransform(1.2, 1.2);
                    node.UiButton.RenderTransformOrigin = new Point(0.5, 0.5);
                }
            };

            NodeController.MouseLeaveNode = (node) =>
            {
                if (node.UiButton != null)
                {
                    node.UiButton.Background = Brushes.LightBlue;
                    node.UiButton.RenderTransform = new ScaleTransform(1.0, 1.0);
                }
            };

            NodeActionHandlers.InlineExpandAction = PerformInlineExpand;

            Loaded += Window_Loaded;
            SourceInitialized += (s, e) =>
            {
                var helper = new WindowInteropHelper(this);
                IntPtr handle = helper.Handle;
                MenuActivation.Initialize(handle);
                // 不激活窗口

                int exStyle = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
                exStyle |= NativeMethods.WS_EX_NOACTIVATE;
                NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, exStyle);

                //this.PreviewMouseLeftButtonUp += OnWindowPreviewMouseLeftButtonUp;

                // 初始化全局热键管理器
                MenuActivation.Initialize(handle);
                MenuActivation.ShowRequested += ShowMenu;
                MenuActivation.HideRequested += HideMenu;

                // 注册唤出热键（始终有效）
                MenuActivation.RegisterWakeUpHotkey();
            };



            this.Visibility = Visibility.Collapsed;
            //this.KeyDown += MainWindow_KeyDown;

            // 设置窗口覆盖全工作区（非最大化）
            Loaded += (s, e) =>
            {
                Left = 0;
                Top = 0;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
            };

            //节点控制区

            if (NodeController.PrepareChildrenLayout == null) MessageBox.Show("PrepareChildrenLayout 未注入！");
            if (NodeController.NavigateToChildren == null) MessageBox.Show("NavigateToChildren 未注入！");

            Closing += (s, e) => MenuActivation.Cleanup();

            InitializeRightButtonHandling();

        }

        //private void OnWindowPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        //{
        //    // 如果没有在拖拽，不处理
        //    if (!DragController.IsDragging) return;

        //    Point mousePos = e.GetPosition(MainCanvas);
        //    DragController.EndDrag(mousePos, MainCanvas, currentLevelNodes);
        //    e.Handled = true; // 阻止任何后续 Click 事件，避免误触发
        //}

        public void RefreshCenterButton()
        {
            if (centerButton != null && MainCanvas.Children.Contains(centerButton))
                MainCanvas.Children.Remove(centerButton);
            CreateCenterButton(currentCenterX, currentCenterY);
        }

        private void UpdateModifierStatusText(string text)
        {
            if (statusIndicator == null)
            {
                statusIndicator = new TextBlock
                {
                    FontSize = 14,
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                    Padding = new Thickness(4),
                    FontWeight = FontWeights.Bold
                };
                MainCanvas.Children.Add(statusIndicator);
            }
            statusIndicator.Text = text;
            statusIndicator.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

            // 放置在当前层级的轨道外部
            if (currentLevelNodes != null && currentLevelNodes.Count > 0)
            {
                // 取第一个节点的中心点（所有节点共享同一中心）
                double cx = currentLevelNodes[0].CenterX;
                double cy = currentLevelNodes[0].CenterY;
                double radius = currentLevelNodes[0].SelfTrackRadius + 20;
                // 放在右上方
                Canvas.SetLeft(statusIndicator, cx + radius);
                Canvas.SetTop(statusIndicator, cy - radius);
            }
        }

        private MenuItemNode GetNodeAtPosition(Point pos)
        {
            foreach (var node in currentLevelNodes)
            {
                if (node.UiButton == null || !node.UiButton.IsVisible) continue;
                var (center, radius) = DragController.GetCircle(node.UiButton);
                if (double.IsNaN(center.X)) continue;
                double dx = pos.X - center.X;
                double dy = pos.Y - center.Y;
                if (Math.Sqrt(dx * dx + dy * dy) <= radius)
                    return node;
            }
            return null;
        }
        private void OnGlobalRightButtonDownForInlineExpand(object sender, MouseButtonEventArgs e)
        {
            if (!DragController.IsDragging) return;

            // 方法1：优先使用 DragController 记录的节点
            bool handled = false;
            DragController.OnRightClickWhileDragging(ref handled);
            if (handled)
            {
                e.Handled = true;
                return;
            }

            // 方法2：备用：直接检测鼠标下的节点
            Point mousePos = e.GetPosition(MainCanvas);
            var node = GetNodeAtPosition(mousePos);
            if (node != null && node.ExpandStyle == ExpandStyle.Inline && node.InlineOnRightClick)
            {
                ForceInlineExpandForDrag(node);
                e.Handled = true;
            }
        }

        private void ForceInlineExpandForDrag(MenuItemNode parent)
        {
            if (parent == null || parent.Children.Count == 0) return;
            // 如果已展开，不做任何操作（防止收起）
            if (inlineExpandStack.Count > 0 && inlineExpandStack.Peek() == parent)
                return;
            PerformInlineExpand(parent);
        }
        //自动回到上一级速度
        //public static void TriggerDelayedBack()
        //{
        //    if (_instance != null)
        //    {
        //        _instance.autoBackTimer.Stop();
        //        _instance.autoBackTimer.Start();
        //    }
        //}

        //public static void StopAutoBackTimer()
        //{
        //    _instance?.autoBackTimer.Stop();
        //}

        //切换隐藏标签的代码
        private void OnToggleLabels()
        {
            if (currentLevelNodes == null) return;
            foreach (var node in currentLevelNodes)
                node.ToggleLabel();
        }
        private void ShowMenu()
        {
            try
            {
                //this.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 255, 0, 0));
                MenuActivation.RegisterToggleLabelsHotkey();
                Visibility = Visibility.Visible;
                
                //Topmost = true;
                
                MenuActivation.RegisterHideHotkey();
                
                SnapToMouse();
                
            }
            catch (Exception ex)
            {
                MessageBox.Show("ShowMenu 异常:\n" + ex.ToString());
            }
        }


        private void HideMenu()
        {

            if (Visibility != Visibility.Visible) return;

            Visibility = Visibility.Collapsed;

            //注销切换热键
            MenuActivation.UnregisterToggleLabelsHotkey();

            // 注销隐藏热键
            MenuActivation.UnregisterHideHotkey();
        }

        private void SnapToMouse()
        {
            DragController.CancelDrag(MainCanvas);

            // 1. 检查节点树
            if (rootNodes == null || rootNodes.Count == 0) return;


            // 2. 取得鼠标位置
            Point mousePos = MousePositionHelper.GetCursorPosition();
            double centerX = mousePos.X;
            double centerY = mousePos.Y;
            centerX = centerX - 237;
            centerY = centerY + 27;
            ResetNavigationState();
            ClearCanvas();
            LayoutNodesAroundPoint(rootNodes, centerX, centerY);

            for (int i = 0; i < rootNodes.Count; i++)
            {
                var node = rootNodes[i];
                var btn = CreateButtonForNode(node);  // 使用统一创建方法，自带背景、模板、事件
                //node.UiButton = btn;
                node.PlayShowAnimation(i);
            }

            //生成中心节点
            currentCenterX = centerX; /*- 237+268 -60+4; */
            currentCenterY = centerY; /* + 27 -68 +20+3;*/
            CreateCenterButton(currentCenterX, currentCenterY);

            //NodeEventBinder.Bind(rootNodes);
            // 再弹一下画布上的按钮总数
            currentLevelNodes = rootNodes;
            NodeActionHandlers.UpdateModifierStatusUI?.Invoke();
            //NodeController.ConfigureAll(rootNodes);


        }



        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 示例配置构建树
            var config = new NodeTreeConfig
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 4,
                Labels = new List<string> { "num", "charaters", "special", "Sym" },
                ExpandableConfigs = new Dictionary<int, NodeTreeConfig>
    {
        { 0, new NodeTreeConfig    // 颜色节点展开出 3 个子项
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 10,
                Labels = new List<string> { "0", "1", "2","3","4","5","6","7","8","9" }
                // 颜色子项不再展开 → 叶子节点
            }
        },
        { 1, new NodeTreeConfig    // 动物节点展开出 4 个子项
            {
                TrackRadius = 210,
                ButtonSize = 40,
                VertexCount = 26,
                Labels = new List<string> { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "N", "o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z" },
                //ExpandableConfigs = new Dictionary<int, NodeTreeConfig>
                //{
                //     { 0, new NodeTreeConfig   
                //        {
                //            TrackRadius = 100,
                //            ButtonSize = 50,
                //            VertexCount = 5,
                //            Labels = new List<string> { "A", "B","C","D","E" }
                //        }
                //    },
                //    { 1, new NodeTreeConfig  
                //        {
                //            TrackRadius = 100,
                //            ButtonSize = 50,
                //            VertexCount = 5,
                //            Labels = new List<string> { "F", "G","H","I","J" }
                //        }
                //    },
                //    { 2, new NodeTreeConfig  
                //        {
                //            TrackRadius = 100,
                //            ButtonSize = 50,
                //            VertexCount = 5,
                //            Labels = new List<string> { "K", "M","L","N","Q" }
                //        }
                //    },
                //    { 3, new NodeTreeConfig  
                //        {
                //            TrackRadius = 100,
                //            ButtonSize = 50,
                //            VertexCount = 5,
                //            Labels = new List<string> { "P", "Q","R","S","T" }
                //        }
                //    },
                //    { 4, new NodeTreeConfig
                //        {
                //            TrackRadius = 100,
                //            ButtonSize = 50,
                //            VertexCount = 5,
                //            Labels = new List<string> { "U", "V","W","X","Y" }
                //        }
                //    }



                //}
            }
        },
        { 2, new NodeTreeConfig    // 动作节点展开出 2 个子项，且子项还能继续展开
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 11,
                Labels = new List<string> { "!", "@","#","$","%","^","&","*","(",")","Tab" },
                //ExpandableConfigs = new Dictionary<int, NodeTreeConfig>
                //{
                //    { 0, new NodeTreeConfig   // “跑”展开
                //        {
                //            TrackRadius = 40,
                //            ButtonSize = 25,
                //            VertexCount = 2,
                //            Labels = new List<string> { "快跑", "慢跑" }
                //        }
                //    }
                //}
            }
        },
        { 3, new NodeTreeConfig    // 动作节点展开出 2 个子项，且子项还能继续展开
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 5,
                Labels = new List<string> { "Win", "Shift","Ctrl","CapsLk","Alt" },
                ExpandableConfigs = new Dictionary<int, NodeTreeConfig>
                {
                    { 0, new NodeTreeConfig   // “跑”展开
                        {
                            TrackRadius = 80,
                            ButtonSize = 25,
                            VertexCount = 7,
                            Labels = new List<string> { "1", "2", "D", "G", "S", "E", "M" }
                        }
                    },

                    { 2, new NodeTreeConfig   // “跑”展开
                        {
                            TrackRadius = 80,
                            ButtonSize = 25,
                            VertexCount = 7,
                            Labels = new List<string> { "V", "C", "X", "Z", "B", "N", "M" }
                        }
                    }
                }
            }
        }
    }
};
            rootNodes = NodeTree.BuildTree(config);

            var targetNode0 = NodeTree.FindNodeByPath(rootNodes, "3/0");
            if (targetNode0 != null)
            {
                targetNode0.ExpandStyle = ExpandStyle.Inline;
                targetNode0.InlineOnRightClick = true;
            }

            var targetNode1 = NodeTree.FindNodeByPath(rootNodes, "3/2");
            if (targetNode1 != null)
            {
                targetNode1.ExpandStyle = ExpandStyle.Inline;
                targetNode1.InlineOnRightClick = true;
            }
            Debug.WriteLine($"节点 {targetNode0.DisplayText} - ExpandStyle={targetNode0.ExpandStyle}, InlineOnRightClick={targetNode0.InlineOnRightClick}");
            LabelConfig.Apply(rootNodes);
            //NodeController.ConfigureAll(rootNodes);

            
            // 还可立即进行个性定制，例如：
            //NodeController.SetNodeText(rootNodes, "0", "开始");
            NodeController.SetNodeAnimationOverrides(rootNodes, "1/0", showDuration: 0.5);


        }

        // 切换到指定层级（播放出现动画，隐藏其他）
        private void NavigateToLevel(List<MenuItemNode> newLevelNodes)
        {
            // ⚠️ 先保存当前层级的中心（用于返回）
            centerHistory.Push(new Tuple<double, double>(currentCenterX, currentCenterY));

            // 再更新为新层级的中心（如果有 pending 的中心点）
            if (!double.IsNaN(pendingCenterPoint.X) && !double.IsNaN(pendingCenterPoint.Y))
            {
                currentCenterX = pendingCenterPoint.X;
                currentCenterY = pendingCenterPoint.Y;
                pendingCenterPoint = new Point(double.NaN, double.NaN);
            }

            // 原有的隐藏与显示逻辑
            if (currentLevelNodes != null)
            {
                PlayHideLevel(currentLevelNodes, () =>
                {
                    ClearCanvas();
                    CreateAndShowLevel(newLevelNodes);
                    DragController.LevelChanged(MainCanvas, newLevelNodes, MousePositionHelper.GetCursorPosition());
                });
                // 注意：history 压入的是当前显示的层级，我们已在上面保存了中心，这里照常
                history.Push(currentLevelNodes);
            }
            else
            {
                ClearCanvas();
                CreateAndShowLevel(newLevelNodes);
            }
            currentLevelNodes = newLevelNodes;
        }


        private void InitializeRightButtonHandling()
        {
            // 为整个窗口添加 Preview 事件以捕获所有 UI 元素上的右键
            //this.PreviewMouseRightButtonDown += OnGlobalRightButtonDown;
            //this.PreviewMouseRightButtonUp += OnGlobalRightButtonUp;

            // 准备定时器
            longPressTimer = new System.Windows.Threading.DispatcherTimer();
            longPressTimer.Interval = TimeSpan.FromMilliseconds(InteractionConfig.LongPressThreshold);
            longPressTimer.Tick += OnLongPressTriggered;

            deleteRepeatTimer = new System.Windows.Threading.DispatcherTimer();
            deleteRepeatTimer.Interval = TimeSpan.FromMilliseconds(InteractionConfig.DeleteRepeatInterval);
            deleteRepeatTimer.Tick += OnDeleteRepeatTick;
        }

        /*private void OnGlobalRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton != MouseButtonState.Pressed) return;

            isRightButtonPressed = true;
            rightButtonDownTime = DateTime.Now;

            // 启动长按计时器
            longPressTimer.Interval = TimeSpan.FromMilliseconds(InteractionConfig.LongPressThreshold);
            longPressTimer.Start();

            // 阻止后续的事件冒泡及默认上下文菜单
            e.Handled = true;
        }

        private void OnGlobalRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isRightButtonPressed) return;
            isRightButtonPressed = false;

            longPressTimer.Stop();
            deleteRepeatTimer.Stop();

            // 如果在长按阈值内释放，判定为短按 → 返回上一级
            if ((DateTime.Now - rightButtonDownTime).TotalMilliseconds < InteractionConfig.LongPressThreshold)
            {
                GoBack();
            }
            // 如果已触发长按，则已开始删除，释放时停止（已在上面停止定时器）
            e.Handled = true;
        }*/
        private void OnLongPressTriggered(object sender, EventArgs e)
        {
            longPressTimer.Stop();
            // 开始发送删除键
            deleteRepeatTimer.Start();
            // 立即发送第一个删除
            SendDelete();
        }

        private void OnDeleteRepeatTick(object sender, EventArgs e)
        {
            SendDelete();
        }

        private void SendDelete()
        {
            // 模拟按键，直接发送到当前焦点窗口（我们窗口不抢焦点，所以是目标窗口）
            if (!string.IsNullOrEmpty(InteractionConfig.DeleteKey))
            {
                System.Windows.Forms.SendKeys.SendWait(InteractionConfig.DeleteKey);
            }
        }

        private void ResetNavigationState()
        {
            // 清除历史记录
            history.Clear();
            centerHistory.Clear();
            // 清除内联展开状态
            RestoreInlineExpand();  // 会清空栈和还原隐藏节点
            inlineExpandStack.Clear();
            hiddenSiblings.Clear();
            inlineChildren.Clear();
        }


        // 后退功能（原有逻辑）
        private void GoBack()
        {
            if (inlineExpandStack.Count > 0)
            {
                RestoreInlineExpand();
                return;
            }

            if (history.Count == 0) return;
            var previousLevel = history.Pop();
            if (centerHistory.Count > 0)
            {
                var prevCenter = centerHistory.Pop();
                currentCenterX = prevCenter.Item1;
                currentCenterY = prevCenter.Item2;
            }
            PlayHideLevel(currentLevelNodes, () =>
            {
                ClearCanvas();

                // ★ 关键修复：用当前中心点重新计算上一级节点的环形位置
                LayoutNodesAroundPoint(previousLevel, currentCenterX, currentCenterY);

                for (int i = 0; i < previousLevel.Count; i++)
                {
                    var node = previousLevel[i];
                    CreateButtonForNode(node);
                    node.PlayShowAnimation(i);
                }
                //NodeController.ConfigureAll(previousLevel);
                CreateCenterButton(currentCenterX, currentCenterY);
                DragController.LevelChanged(MainCanvas, previousLevel, MousePositionHelper.GetCursorPosition());
            });
            currentLevelNodes = previousLevel;
        }

        // 播放层级消失动画（所有节点）
        private void PlayHideLevel(List<MenuItemNode> nodes, Action onAllCompleted)
        {
            if (nodes == null || nodes.Count == 0)
            {
                onAllCompleted?.Invoke();
                return;
            }

            int remaining = nodes.Count;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                node.PlayHideAnimation(() =>
                {
                    if (--remaining == 0)
                        onAllCompleted?.Invoke();
                }, i);   // 传入索引实现交错消失
            }
        }

        // 清除画布上所有按钮
        private void ClearCanvas()
        {
            // 移除所有 Button
            var buttons = MainCanvas.Children.OfType<Button>().ToList();
            foreach (var btn in buttons)
                MainCanvas.Children.Remove(btn);
        }

        // 创建并显示一层节点（立即出现动画）
        private void CreateAndShowLevel(List<MenuItemNode> nodes)
        {
            if (nodes == null) return;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                CreateButtonForNode(node);
                node.PlayShowAnimation(i);
            }
            //NodeEventBinder.Bind(nodes);
            CreateCenterButton(currentCenterX, currentCenterY);
            NodeActionHandlers.UpdateModifierStatusUI?.Invoke();
            //NodeController.ConfigureAll(nodes);
        }

        // 在指定中心点周围均匀排布节点，并存储坐标
        private void LayoutNodesAroundPoint(List<MenuItemNode> nodes, double cx, double cy, double startAngle = 0)
        {
            int count = nodes.Count;
            for (int i = 0; i < count; i++)
            {
                double angle = startAngle + (2 * Math.PI * i / count) - Math.PI / 2;
                nodes[i].CenterX = cx + nodes[i].SelfTrackRadius * Math.Cos(angle);  // 用 SelfTrackRadius
                nodes[i].CenterY = cy + nodes[i].SelfTrackRadius * Math.Sin(angle);
            }
        }

        // 动态创建节点按钮，绑定点击事件
        private Button CreateButtonForNode(MenuItemNode node)
        {
            var btn = new Button
            {
                Width = node.ButtonSize,
                Height = node.ButtonSize,
                Content = node.DisplayText,
                Focusable = false,
                Cursor = Cursors.Hand,
                Background = Brushes.LightBlue,          // 确保命中测试
                Foreground = Brushes.Black,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Opacity = 1,
                RenderTransform = new ScaleTransform(1, 1),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Template = CreateCircleButtonTemplate(node.ButtonSize / 2)   // 已修复内部穿透
            };
            btn.Tag = node;
            Canvas.SetLeft(btn, node.CenterX - node.ButtonSize / 2);
            Canvas.SetTop(btn, node.CenterY - node.ButtonSize / 2);
            MainCanvas.Children.Add(btn);

            // 拖拽事件（解决点击冲突的新版）
            //btn.MouseLeftButtonDown += OnNodeMouseLeftButtonDown;
            //btn.MouseMove += OnNodeMouseMove;
            //btn.MouseLeftButtonUp += OnNodeMouseLeftButtonUp;
            btn.PreviewMouseLeftButtonDown += OnNodePreviewMouseLeftButtonDown;
            btn.PreviewMouseMove += OnNodePreviewMouseMove;
            btn.PreviewMouseLeftButtonUp += OnNodePreviewMouseLeftButtonUp;
            // 核心点击逻辑
            if (node.ExpandStyle == ExpandStyle.Inline && node.InlineOnRightClick)
            {
                btn.PreviewMouseRightButtonDown += (s, e) =>
                {
                    PerformInlineExpand(node);
                    e.Handled = true;   // 阻止冒泡，避免触发全局右键返回
                };
            }

            btn.Click += (s, e) =>
            {
                if (ModifierKeyConfig.IsModifierKey(node.Path))
                {
                    NodeActionHandlers.HandleModifierClick(node);
                    return;
                }
                if (node.Children.Count > 0)
                {
                    // 新增：根据展开风格选择不同行为
                    if (node.ExpandStyle == ExpandStyle.Inline)
                    {
                        PerformInlineExpand(node);   // 内联展开
                    }
                    else
                    {
                        NodeController.PrepareChildrenLayout?.Invoke(node);
                        NodeController.NavigateToChildren?.Invoke(node.Children);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(node.DisplayText))
                        TextInjection.Send(node.DisplayText);
                }
            };

            // 悬停效果
            btn.MouseEnter += (s, e) => NodeController.MouseEnterNode?.Invoke(node);
            btn.MouseLeave += (s, e) => NodeController.MouseLeaveNode?.Invoke(node);



            node.UiButton = btn;
            return btn;
        }
        // 生成圆形按钮模板
        private static ControlTemplate CreateCircleButtonTemplate(double cornerRadius)
        {
            var template = new ControlTemplate(typeof(Button));

            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            border.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            // 注意：不要设置 IsHitTestVisible = false，让 Border 作为按钮的命中区域

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
            contentPresenter.SetValue(UIElement.IsHitTestVisibleProperty, false);   // 只让文字不拦截

            border.AppendChild(contentPresenter);
            template.VisualTree = border;
            return template;
        }



        //private void OnNodePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        //{
        //    Button btn = sender as Button;
        //    MenuItemNode node = btn?.Tag as MenuItemNode;
        //    if (node == null || node.DisplayText == CenterNodeConfig.Text) return;

        //    dragStartPoint = e.GetPosition(MainCanvas);
        //    dragCaptureStarted = false;
        //    // 不设置 e.Handled，让 Click 事件能够正常触发
        //}

        //private void OnNodePreviewMouseMove(object sender, MouseEventArgs e)
        //{
        //    if (dragCaptureStarted || DragController.DragSource != null) return;

        //    Point currentPos = e.GetPosition(MainCanvas);
        //    if (Math.Abs(currentPos.X - dragStartPoint.X) > DragController.DragThreshold ||
        //        Math.Abs(currentPos.Y - dragStartPoint.Y) > DragController.DragThreshold)
        //    {
        //        Button btn = sender as Button;
        //        MenuItemNode node = btn?.Tag as MenuItemNode;
        //        if (node != null)
        //        {
        //            DragController.StartDrag(node, btn, dragStartPoint);
        //            dragCaptureStarted = true;
        //            // 启动窗口级事件捕获
        //            this.MouseMove += Window_MouseMoveForDrag;
        //            this.PreviewMouseLeftButtonUp += Window_MouseUpForDrag;
        //            this.CaptureMouse();
        //            e.Handled = true;
        //        }
        //    }
        //}

        //private void OnNodePreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        //{
        //    // 如果没有拖拽发生，则直接返回，让 Click 正常触发
        //    if (!dragCaptureStarted && DragController.DragSource == null) return;

        //    Point mousePos = e.GetPosition(MainCanvas);
        //    DragController.EndDrag(mousePos, MainCanvas, currentLevelNodes);
        //    CleanupDragCapture();
        //    e.Handled = true;
        //}

        private void OnNodeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Button btn = sender as Button;
            MenuItemNode node = btn?.Tag as MenuItemNode;
            if (node == null || node.DisplayText == CenterNodeConfig.Text) return;

            dragStartPoint = e.GetPosition(MainCanvas);
            isPotentialDrag = true;
            isDragActive = false;

            btn.CaptureMouse();    // 关键：捕获鼠标，确保后续 Move/Up 都能收到
            //e.Handled = true;      // 防止事件继续冒泡干扰
        }
        private void OnNodePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Button btn = sender as Button;
            MenuItemNode node = btn?.Tag as MenuItemNode;
            if (node == null || node.DisplayText == CenterNodeConfig.Text) return;

            dragStartPoint = e.GetPosition(MainCanvas);
            dragCaptureStarted = false;
            // 不设置 e.Handled，让 Click 可以触发
        }
        private void RestoreInlineExpand()
        {
            if (inlineExpandStack.Count == 0) return;

            var parent = inlineExpandStack.Pop();
            parent.IsInlineExpanded = false;

            // 移除子节点按钮并从当前节点列表移除
            foreach (var child in inlineChildren)
            {
                if (child.UiButton != null && MainCanvas.Children.Contains(child.UiButton))
                    MainCanvas.Children.Remove(child.UiButton);
                child.UiButton = null;
                currentLevelNodes.Remove(child);   // 关键：从拖拽检测列表移除
            }
            inlineChildren.Clear();

            // 恢复兄弟节点（带动画）
            foreach (var node in hiddenSiblings)
            {
                if (node.UiButton != null)
                    FadeInAndShow(node.UiButton);
            }
            hiddenSiblings.Clear();

            // 恢复中心按钮
            if (centerButton != null)
                FadeInAndShow(centerButton);
            
            // 通知 DragController 层级变化
            DragController.LevelChanged(MainCanvas, currentLevelNodes, MousePositionHelper.GetCursorPosition());
        }

        // 淡出并隐藏（完成后设置 Visibility.Collapsed）
        private void FadeOutAndHide(UIElement element, Action onComplete = null)
        {
            if (element == null) return;
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, _) =>
            {
                element.Visibility = Visibility.Collapsed;
                onComplete?.Invoke();
            };
            element.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        // 显示并淡入（先设置可见，再动画）
        private void FadeInAndShow(UIElement element)
        {
            if (element == null) return;
            element.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150));
            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void FadeIn(UIElement element)
        {
            if (element == null) return;
            element.Visibility = Visibility.Visible;
            element.Opacity = 0;
            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150));
            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }
        private void PerformInlineExpand(MenuItemNode parent)
        {
            Debug.WriteLine($"[Main] PerformInlineExpand 被调用，节点: {parent?.DisplayText}, 当前内联栈: {inlineExpandStack.Count}");
            if (parent == null || parent.Children.Count == 0) return;

            // 如果已经处于内联展开状态，先恢复
            if (inlineExpandStack.Count > 0 && inlineExpandStack.Peek() == parent)
            {
                RestoreInlineExpand();
                // 恢复后也需要通知 DragController 层级变化
                DragController.LevelChanged(MainCanvas, currentLevelNodes, MousePositionHelper.GetCursorPosition());
                return;
            }

            // 保存当前层级的所有节点（即 currentLevelNodes）
            var siblings = currentLevelNodes;
            if (siblings == null) return;

            // 隐藏除父节点外的所有兄弟节点
            // 隐藏除父节点外的所有兄弟节点（带动画）
            inlineChildren.Clear();
            foreach (var node in siblings)
            {
                if (node == parent) continue;
                if (node.UiButton != null && node.UiButton.IsVisible)
                {
                    FadeOutAndHide(node.UiButton);
                    hiddenSiblings.Add(node);
                }
            }

            // 隐藏中心按钮（如果存在）
            if (centerButton != null && centerButton.IsVisible)
            {
                FadeOutAndHide(centerButton);
            }

            // 生成父节点的子节点按钮，布局在父节点周围
            inlineChildren.Clear();
            double centerX = parent.CenterX;
            double centerY = parent.CenterY;
            double radius = parent.SelfTrackRadius;
            int count = parent.Children.Count;
            for (int i = 0; i < count; i++)
            {
                var child = parent.Children[i];
                double angle = (2 * Math.PI * i / count) - Math.PI / 2;
                child.CenterX = centerX + radius * Math.Cos(angle);
                child.CenterY = centerY + radius * Math.Sin(angle);

                Button btn = CreateButtonForNode(child);
                FadeIn(btn);  // 使用已实现的动画方法

                inlineChildren.Add(child);
                currentLevelNodes.Add(child);   // 关键：加入拖拽检测列表
            }

            inlineExpandStack.Push(parent);
            parent.IsInlineExpanded = true;
            DragController.LevelChanged(MainCanvas, currentLevelNodes, MousePositionHelper.GetCursorPosition());
        }


        private void OnNodePreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (dragCaptureStarted || DragController.DragSource != null) return;

            Point currentPos = e.GetPosition(MainCanvas);
            if (Math.Abs(currentPos.X - dragStartPoint.X) > DragController.DragThreshold ||
                Math.Abs(currentPos.Y - dragStartPoint.Y) > DragController.DragThreshold)
            {
                Button btn = sender as Button;
                MenuItemNode node = btn?.Tag as MenuItemNode;
                if (node != null)
                {
                    // 启动拖拽前确保节点可拖拽（可选：允许所有节点）
                    DragController.StartDrag(node, btn, dragStartPoint);
                    dragCaptureStarted = true;
                    // 启动窗口级事件捕获
                    this.MouseMove += Window_MouseMoveForDrag;
                    this.PreviewMouseLeftButtonUp += Window_MouseUpForDrag;
                    this.CaptureMouse();
                    e.Handled = true;
                }
            }
        }

        private void OnNodePreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!dragCaptureStarted && DragController.DragSource == null) return;

            Point mousePos = e.GetPosition(MainCanvas);
            DragController.EndDrag(mousePos, MainCanvas, currentLevelNodes);
            CleanupDragCapture();
            e.Handled = true;
        }
        private void OnNodeMouseMove(object sender, MouseEventArgs e)
        {
            if (!isPotentialDrag) return;
            Button btn = sender as Button;
            MenuItemNode node = btn?.Tag as MenuItemNode;
            if (node == null) return;

            Point currentPos = e.GetPosition(MainCanvas);
            double dx = Math.Abs(currentPos.X - dragStartPoint.X);
            double dy = Math.Abs(currentPos.Y - dragStartPoint.Y);

            if (dx > DragController.DragThreshold || dy > DragController.DragThreshold)
            {
                // 启动正式拖拽
                isDragActive = true;
                isPotentialDrag = false;
                DragController.StartDrag(node, btn, dragStartPoint);
                // 拖拽过程中仍然保持鼠标捕获，以便后续的 Move 和 Up
                // 不需要再添加窗口级事件，因为按钮已经捕获了鼠标
            }
        }

        private void OnNodeMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Button btn = sender as Button;
            btn.ReleaseMouseCapture();

            if (isDragActive)
            {
                // 拖拽结束
                Point mousePos = e.GetPosition(MainCanvas);
                DragController.EndDrag(mousePos, MainCanvas, currentLevelNodes);
                isDragActive = false;
                e.Handled = true;   // 阻止 Click 等后续事件
            }
            else if (isPotentialDrag)
            {
                // 未发生拖拽，视为普通点击，让 Click 事件自然触发
                isPotentialDrag = false;
                // 不设置 e.Handled，Click 会随后执行
            }
        }
        private void CleanupDragCapture()
        {
            this.ReleaseMouseCapture();
            this.MouseMove -= Window_MouseMoveForDrag;
            this.PreviewMouseLeftButtonUp -= Window_MouseUpForDrag;
            dragCaptureStarted = false;
        }
        //生成中心模板
        private void CreateCenterButton(double cx, double cy)
        {
            if (!CenterNodeConfig.ShowCenterNode) return;

            // 自动校准中心点：以当前层级节点的平均坐标为准
            if (currentLevelNodes != null && currentLevelNodes.Count > 0)
            {
                double sumX = 0, sumY = 0;
                int validCount = 0;
                foreach (var n in currentLevelNodes)
                {
                    if (!double.IsNaN(n.CenterX) && !double.IsNaN(n.CenterY))
                    {
                        sumX += n.CenterX;
                        sumY += n.CenterY;
                        validCount++;
                    }
                }
                if (validCount > 0)
                {
                    cx = sumX / validCount;
                    cy = sumY / validCount;
                }
            }

            // 移除旧中心按钮
            if (centerButton != null && MainCanvas.Children.Contains(centerButton))
                MainCanvas.Children.Remove(centerButton);

            var btn = new Button
            {
                Width = CenterNodeConfig.ButtonSize,
                Height = CenterNodeConfig.ButtonSize,
                Content = CenterNodeConfig.Text,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = CenterNodeConfig.Foreground,
                Background = CenterNodeConfig.Background,
                Focusable = false,
                Cursor = Cursors.Hand,
                Template = CreateCircleButtonTemplate(CenterNodeConfig.ButtonSize / 2)
            };

            Canvas.SetLeft(btn, cx - CenterNodeConfig.ButtonSize / 2);
            Canvas.SetTop(btn, cy - CenterNodeConfig.ButtonSize / 2);
            MainCanvas.Children.Add(btn);

            // 长按删除逻辑（完全保留）
            DispatcherTimer longPressTimer = new DispatcherTimer();
            DispatcherTimer deleteRepeatTimer = new DispatcherTimer();
            bool isLongPressTriggered = false;

            longPressTimer.Interval = TimeSpan.FromMilliseconds(CenterNodeConfig.LongPressThresholdMs);
            longPressTimer.Tick += (s, e) =>
            {
                longPressTimer.Stop();
                isLongPressTriggered = true;
                deleteRepeatTimer.Interval = TimeSpan.FromMilliseconds(CenterNodeConfig.DeleteRepeatIntervalMs);
                deleteRepeatTimer.Start();
                System.Windows.Forms.SendKeys.SendWait(CenterNodeConfig.DeleteKey);
            };

            deleteRepeatTimer.Tick += (s, e) =>
            {
                System.Windows.Forms.SendKeys.SendWait(CenterNodeConfig.DeleteKey);
            };

            btn.Click += (s, e) =>
            {
                if ((DateTime.Now - DragController.LastDragEndTime).TotalMilliseconds < 300)
                {
                    e.Handled = true;
                    return;
                }

                if (!isLongPressTriggered)
                    GoBack();
                isLongPressTriggered = false;
            };

            DispatcherTimer rightLongPressTimer = new DispatcherTimer();
            DispatcherTimer rightDeleteRepeatTimer = new DispatcherTimer();
            bool isRightLongPressTriggered = false;

            rightLongPressTimer.Interval = TimeSpan.FromMilliseconds(InteractionConfig.LongPressThreshold);
            rightLongPressTimer.Tick += (s, e) =>
            {
                rightLongPressTimer.Stop();
                isRightLongPressTriggered = true;
                rightDeleteRepeatTimer.Interval = TimeSpan.FromMilliseconds(InteractionConfig.DeleteRepeatInterval);
                rightDeleteRepeatTimer.Start();
                // 立即发送第一个删除
                SendDelete();
            };

            rightDeleteRepeatTimer.Tick += (s, e) =>
            {
                SendDelete();
            };

            // 右键按下：启动长按计时器
            btn.PreviewMouseRightButtonDown += (s, e) =>
            {
                isRightLongPressTriggered = false;
                rightLongPressTimer.Start();
                e.Handled = true; // 阻止冒泡，避免触发窗口右键菜单
            };

            // 右键释放：停止所有定时器，如果长按未触发则发送单次删除
            btn.PreviewMouseRightButtonUp += (s, e) =>
            {
                rightLongPressTimer.Stop();
                rightDeleteRepeatTimer.Stop();

                if (!isRightLongPressTriggered)
                {
                    SendDelete(); // 短按 -> 单次删除
                }
                e.Handled = true;
            };

            // 鼠标离开按钮时，停止所有定时器（防止悬停时仍触发）
            btn.MouseLeave += (s, e) =>
            {
                rightLongPressTimer.Stop();
                rightDeleteRepeatTimer.Stop();
            };

            centerButton = btn;
            centerButton.Opacity = 1;
            DragController.RegisterCenterButton(centerButton);
        }

        private void Window_MouseMoveForDrag(object sender, MouseEventArgs e)
        {
            if (DragController.DragSource == null) return;
            Point mousePos = e.GetPosition(MainCanvas);
            DragController.OnMouseMove(mousePos, MainCanvas, currentLevelNodes);
        }

        private void Window_MouseUpForDrag(object sender, MouseButtonEventArgs e)
        {
            if (DragController.DragSource == null) return;
            bool wasDragging = DragController.IsDragging;
            Point mousePos = e.GetPosition(MainCanvas);
            DragController.EndDrag(mousePos, MainCanvas, currentLevelNodes);
            if (wasDragging) e.Handled = true;
            CleanupDragCapture();
        }


        // 点击空白区域后退
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == MainCanvas || e.OriginalSource == this)
            {
                GoBack();
            }
        }


        //private void OnButtonRightButtonDown(object sender, MouseButtonEventArgs e)
        //{
        //    if (e.RightButton != MouseButtonState.Pressed) return;

        //    // 获取当前按钮对应的节点
        //    Button btn = sender as Button;
        //    MenuItemNode node = btn?.Tag as MenuItemNode; // 需要确保按钮的 Tag 指向节点
        //                                                  // 如果 Tag 未存储节点，可改为从其他地方获取，这里假设你在创建按钮时已设置 Tag

        //    // 如果节点未存 Tag，可在此指定：在 CreateButtonForNode 中添加 btn.Tag = node;
        //    // 若尚未设置，临时从 UiButton 反向查找？建议确保 Tag 已设置。
        //    // 检查是否为修饰键节点且有子节点
        //    if (node != null && ModifierKeyConfig.IsModifierKey(node.Path) && node.Children.Count > 0)
        //    {
        //        isModifierRightClick = true;
        //        isRightButtonPressed = true;  // 避免释放时检查失败
        //        rightButtonDownTime = DateTime.Now;
        //        e.Handled = true;
        //        return; // 直接返回，不启动长按计时器
        //    }

        //    // 普通节点原有逻辑
        //    isRightButtonPressed = true;
        //    rightButtonDownTime = DateTime.Now;
        //    longPressTimer.Interval = TimeSpan.FromMilliseconds(InteractionConfig.LongPressThreshold);
        //    longPressTimer.Start();
        //    e.Handled = true;
        //}
        public static MainWindow Instance => _instance;
        // 按钮上右键释放：短按返回，长按结束删除
        //private void OnButtonRightButtonUp(object sender, MouseButtonEventArgs e)
        //{
        //    if (!isRightButtonPressed) return;

        //    // 处理修饰键右键
        //    if (isModifierRightClick)
        //    {
        //        isModifierRightClick = false;
        //        isRightButtonPressed = false;
        //        // 短按判定（防止长时间按住被当成其他操作）
        //        if ((DateTime.Now - rightButtonDownTime).TotalMilliseconds < InteractionConfig.LongPressThreshold)
        //        {
        //            Button btn = sender as Button;
        //            MenuItemNode node = btn?.Tag as MenuItemNode;
        //            if (node != null && node.Children.Count > 0)
        //            {
        //                // 展开子节点
        //                NodeActionHandlers.ExpandChildren(node);
        //            }
        //        }
        //        e.Handled = true;
        //        return;
        //    }

        //    // 普通节点原有逻辑
        //    isRightButtonPressed = false;
        //    longPressTimer.Stop();
        //    bool wasDeleting = deleteRepeatTimer.IsEnabled;
        //    deleteRepeatTimer.Stop();

        //    if (!wasDeleting && (DateTime.Now - rightButtonDownTime).TotalMilliseconds < InteractionConfig.LongPressThreshold)
        //    {
        //        GoBack();
        //    }
        //    e.Handled = true;
        //}

        public void SetPendingCenter(double x, double y)
        {
            pendingCenterPoint = new Point(x, y);
        }
        // 鼠标离开按钮：如果正在长按删除，则停止
        //private void OnButtonMouseLeave(object sender, MouseEventArgs e)
        //{
        //    if (deleteRepeatTimer.IsEnabled)
        //    {
        //        longPressTimer.Stop();
        //        deleteRepeatTimer.Stop();
        //        isRightButtonPressed = false;
        //    }
        //}

    }

    // 原生窗口样式调用
    internal static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // 添加这一行：
        [DllImport("user32.dll")]
        public static extern short VkKeyScan(char ch);
    }
}