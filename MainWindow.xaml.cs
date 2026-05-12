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

            NodeController.PrepareChildrenLayout = (parentNode) =>
            {
                LayoutNodesAroundPoint(parentNode.Children, parentNode.CenterX, parentNode.CenterY);
            };


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
                this.Background = new SolidColorBrush(Colors.Red) { Opacity = 0.3 };

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
            this.ReleaseMouseCapture();
            // 1. 检查节点树
            if (rootNodes == null)
            {
                MessageBox.Show("rootNodes 为 null ！");
                return;
            }
            if (rootNodes.Count == 0)
            {
                MessageBox.Show("rootNodes 为空列表 ！");
                return;
            }


            // 2. 取得鼠标位置
            Point mousePos = MousePositionHelper.GetCursorPosition();
            //var mousePos = System.Windows.Forms.Cursor.Position;
            //Point mousePos = Mouse.GetPosition(this);
            double centerX = mousePos.X;
            double centerY = mousePos.Y;
            centerX = centerX- 237;
            centerY = centerY+ 27;



            //MessageBox.Show("鼠标坐标" + centerX.ToString() +","+centerX.ToString());
            // 3. 清除画布
            ClearCanvas();


            // 4. 布局节点并创建按钮
            LayoutNodesAroundPoint(rootNodes, centerX, centerY);

            for (int i = 0; i < rootNodes.Count; i++)
            {
                var node = rootNodes[i];

                // 创建按钮（使用极端醒目的方式）
                Button btn = new Button
                {

                    Width = node.ButtonSize,
                    Height = node.ButtonSize,
                    Content = node.DisplayText,
                    Focusable = false,
                    Cursor = Cursors.Hand,
                    Opacity = 1,                                     // 强行可见
                    RenderTransform = new ScaleTransform(1, 1),      // 正常比例
                    RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                    //Background = new SolidColorBrush(Colors.Orange), // 橙色背景，绝对可见
                    Foreground = new SolidColorBrush(Colors.Black),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Template = CreateCircleButtonTemplate(50)                              // 用默认按钮样式（方角）
                };


                btn.Tag = node;
                btn.Click += (s, e) =>
                {
                    MessageBox.Show("按钮被点击了！");
                };
                //MessageBox.Show(node.CenterX.ToString() + " "+ node.CenterY.ToString() );
                // 设置位置（已经考虑 ButtonSize 偏移）
                Canvas.SetLeft(btn, node.CenterX - node.ButtonSize / 2);
                Canvas.SetTop(btn, node.CenterY - node.ButtonSize / 2);
                //MessageBox.Show((node.CenterX - node.ButtonSize / 2).ToString() + " " + (node.CenterY - node.ButtonSize / 2).ToString());
                //调试——输出坐标
                double left = Canvas.GetLeft(btn);
                double top = Canvas.GetTop(btn);
                // 添加到画布
                MainCanvas.Children.Add(btn);
                //绑定节点
                //btn.PreviewMouseRightButtonDown += OnButtonRightButtonDown;
                //btn.PreviewMouseRightButtonUp += OnButtonRightButtonUp;
                //btn.MouseLeave += OnButtonMouseLeave;
                node.UiButton = btn;

                //临时不播放动画，直接可见
                node.PlayShowAnimation(i);
            }

            //生成中心节点
            currentCenterX = centerX; /*- 237+268 -60+4; */
            currentCenterY = centerY; /* + 27 -68 +20+3;*/
            CreateCenterButton(currentCenterX, currentCenterY);

            //NodeEventBinder.Bind(rootNodes);
            // 再弹一下画布上的按钮总数
            currentLevelNodes = rootNodes;

            //        MessageBox.Show($"Canvas 子元素总数: {MainCanvas.Children.Count}, " +
            //$"其中 Button 数量: {MainCanvas.Children.OfType<Button>().Count()}");
            //NodeActionHandlers.UpdateModifierStatusUI?.Invoke();
            //NodeController.ConfigureAll(rootNodes);
            NodeActionHandlers.UpdateModifierStatusUI?.Invoke();
            NodeController.ConfigureAll(rootNodes);
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
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 6,
                Labels = new List<string> { "A-E", "F-J", "K-O", "P-T","U-Y","Z" },
                ExpandableConfigs = new Dictionary<int, NodeTreeConfig>
                {
                     { 0, new NodeTreeConfig   
                        {
                            TrackRadius = 100,
                            ButtonSize = 50,
                            VertexCount = 5,
                            Labels = new List<string> { "A", "B","C","D","E" }
                        }
                    },
                    { 1, new NodeTreeConfig  
                        {
                            TrackRadius = 100,
                            ButtonSize = 50,
                            VertexCount = 5,
                            Labels = new List<string> { "F", "G","H","I","J" }
                        }
                    },
                    { 2, new NodeTreeConfig  
                        {
                            TrackRadius = 100,
                            ButtonSize = 50,
                            VertexCount = 5,
                            Labels = new List<string> { "K", "M","L","N","Q" }
                        }
                    },
                    { 3, new NodeTreeConfig  
                        {
                            TrackRadius = 100,
                            ButtonSize = 50,
                            VertexCount = 5,
                            Labels = new List<string> { "P", "Q","R","S","T" }
                        }
                    },
                    { 4, new NodeTreeConfig
                        {
                            TrackRadius = 100,
                            ButtonSize = 50,
                            VertexCount = 5,
                            Labels = new List<string> { "U", "V","W","X","Y" }
                        }
                    }



                }
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
                            TrackRadius = 40,
                            ButtonSize = 25,
                            VertexCount = 2,
                            Labels = new List<string> { "快跑", "慢跑" }
                        }
                    }
                }
            }
        }
    }
};
            rootNodes = NodeTree.BuildTree(config);

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

        // 后退功能（原有逻辑）
        private void GoBack()
        {
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
                    CreateButtonForNode(node);
                    node.PlayShowAnimation(0);   // 无交错，保证立即显示
                }
                NodeController.ConfigureAll(previousLevel);
                CreateCenterButton(currentCenterX, currentCenterY);
                // 刷新修饰键状态显示
                NodeActionHandlers.UpdateModifierStatusUI?.Invoke();
                // 通知拖拽控制器层级已变化
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
            DragController.CancelDrag(MainCanvas);
            this.ReleaseMouseCapture();
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
            NodeController.ConfigureAll(nodes);
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
                Opacity = 1,
                RenderTransform = new ScaleTransform(1, 1),
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                Background = Brushes.LightBlue,   // ★ 添加这行，确保按钮可命中
                Template = CreateCircleButtonTemplate(node.ButtonSize / 2)
            };
            btn.Tag = node;
            btn.Background = Brushes.Orange;   // 或其他颜色

            // 不设置 Background/Foreground，因为模板里写死了
            Canvas.SetLeft(btn, node.CenterX - node.ButtonSize / 2);
            Canvas.SetTop(btn, node.CenterY - node.ButtonSize / 2);
            MainCanvas.Children.Add(btn);

            // 输出实际尺寸以调试
            btn.Loaded += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"按钮加载后 ActualWidth={btn.ActualWidth}, ActualHeight={btn.ActualHeight}");
            };

            System.Diagnostics.Debug.WriteLine($"按钮已添加: {btn.Content}, 子元素总数: {MainCanvas.Children.Count}");

            //btn.PreviewMouseRightButtonDown += OnButtonRightButtonDown;
            //btn.PreviewMouseRightButtonUp += OnButtonRightButtonUp;
            //btn.MouseLeave += OnButtonMouseLeave;

            // 拖拽事件
            btn.Tag = node; 
            //btn.PreviewMouseLeftButtonDown += OnNodePreviewMouseLeftButtonDown;
            //btn.PreviewMouseMove += OnNodePreviewMouseMove;
            //btn.PreviewMouseLeftButtonUp += OnNodePreviewMouseLeftButtonUp;
            // 直接绑定点击事件（绕过 ConfigureAll 的不稳定）
            btn.Click += (s, e) =>
            {
                MessageBox.Show("clicked");
                // 优先处理修饰键
                if (ModifierKeyConfig.IsModifierKey(node.Path))
                {
                    NodeActionHandlers.HandleModifierClick(node);
                    return;
                }

                if (node.Children.Count > 0)
                {
                    // 可展开节点：先布局子节点，再导航
                    NodeController.PrepareChildrenLayout?.Invoke(node);
                    NodeController.NavigateToChildren?.Invoke(node.Children);
                }
                else
                {
                    // 叶子节点：注入文本
                    if (!string.IsNullOrEmpty(node.DisplayText))
                        TextInjection.Send(node.DisplayText);
                }
            };

            node.UiButton = btn;
            btn.Tag = node;


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
            border.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            border.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            // ★ 让边框不拦截鼠标
            border.SetValue(UIElement.IsHitTestVisibleProperty, false);

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
            // ★ 让内容不拦截鼠标
            content.SetValue(UIElement.IsHitTestVisibleProperty, false);

            border.AppendChild(content);
            template.VisualTree = border;
            return template;
        }

        private void CleanupDragCapture()
        {
            this.ReleaseMouseCapture();
            this.MouseMove -= Window_MouseMoveForDrag;
            this.PreviewMouseLeftButtonUp -= Window_MouseUpForDrag;
        }
        private void OnNodePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Button btn = sender as Button;
            MenuItemNode node = btn?.Tag as MenuItemNode;
            if (node == null || node.DisplayText == CenterNodeConfig.Text) return;

            // 记录拖拽起点（不启动拖拽，也不处理事件）
            Point mousePos = e.GetPosition(MainCanvas);
            DragController.StartDrag(node, btn, mousePos);
            // 注意：不设置 e.Handled = true，不捕获鼠标，让 Click 有机会触发
        }

        private void OnNodePreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (DragController.DragSource == null) return;

            Point mousePos = e.GetPosition(MainCanvas);
            // 如果还未开始拖拽，检测移动距离
            if (!DragController.IsDragging)
            {
                // 此时由 DragController 内部判断阈值，若超过阈值则 IsDragging 变为 true
                DragController.OnMouseMove(mousePos, MainCanvas, currentLevelNodes);
                if (DragController.IsDragging)
                {
                    // 开始真正的拖拽，启动窗口级事件捕获
                    this.MouseMove += Window_MouseMoveForDrag;
                    this.PreviewMouseLeftButtonUp += Window_MouseUpForDrag;
                    this.CaptureMouse();
                    e.Handled = true;
                }
            }
            else
            {
                // 已经拖拽中，继续更新（窗口级事件会处理，这里可留空）
            }
        }

        private void OnNodePreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 如果没有拖拽源，直接返回（让 Click 正常触发）
            if (DragController.DragSource == null) return;

            if (DragController.IsDragging)
            {
                // 拖拽结束
                Point mousePos = e.GetPosition(MainCanvas);
                DragController.EndDrag(mousePos, MainCanvas, currentLevelNodes);
                CleanupDragCapture();
                e.Handled = true; // 阻止 Click
            }
            else
            {
                // 未发生拖拽，重置状态，让 Click 触发
                DragController.CancelDrag(MainCanvas);
                // 不需要设置 e.Handled = false，默认会让 Click 执行
            }
        }
        //生成中心模板
        private void CreateCenterButton(double cx, double cy)
        {
            if (!CenterNodeConfig.ShowCenterNode) return;

            // 先移除旧的中心按钮（如果有）
            if (centerButton != null && MainCanvas.Children.Contains(centerButton))
            {
                MainCanvas.Children.Remove(centerButton);
            }
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

            // 长按删除逻辑
            DispatcherTimer longPressTimer = new DispatcherTimer();
            DispatcherTimer deleteRepeatTimer = new DispatcherTimer();
            bool isLongPressTriggered = false;
            //bool isMouseDown = false;

            longPressTimer.Interval = TimeSpan.FromMilliseconds(CenterNodeConfig.LongPressThresholdMs);
            longPressTimer.Tick += (s, e) =>
            {
                longPressTimer.Stop();
                isLongPressTriggered = true;
                deleteRepeatTimer.Interval = TimeSpan.FromMilliseconds(CenterNodeConfig.DeleteRepeatIntervalMs);
                deleteRepeatTimer.Start();
                // 立即发送一次
                System.Windows.Forms.SendKeys.SendWait(CenterNodeConfig.DeleteKey);
            };

            deleteRepeatTimer.Tick += (s, e) =>
            {
                System.Windows.Forms.SendKeys.SendWait(CenterNodeConfig.DeleteKey);
            };

            btn.Click += (s, e) =>
            {
                // 如果刚结束拖拽（300ms 内），忽略本次点击，防止异常注入
                if ((DateTime.Now - DragController.LastDragEndTime).TotalMilliseconds < 300)
                {
                    e.Handled = true;
                    return;
                }

                // 短按：如果未触发长按，则返回上一级
                if (!isLongPressTriggered)
                    GoBack();
                isLongPressTriggered = false;
            };

            btn.PreviewMouseLeftButtonDown += (s, e) =>
            {
                // 如果正在拖拽或刚结束拖拽，阻止中心按钮接收事件
                if (DragController.IsDragging || (DateTime.Now - DragController.LastDragEndTime).TotalMilliseconds < 200)
                    e.Handled = true;
                else
                {
                    isLongPressTriggered = false;
                    longPressTimer.Start();
                }
            };

            btn.PreviewMouseLeftButtonUp += (s, e) =>
            {
                longPressTimer.Stop();
                deleteRepeatTimer.Stop();
            };

            btn.MouseLeave += (s, e) =>
            {
                longPressTimer.Stop();
                deleteRepeatTimer.Stop();
            };

            centerButton = btn;
            DragController.RegisterCenterButton(centerButton);
        }

        private void Window_MouseMoveForDrag(object sender, MouseEventArgs e)
        {
            if (DragController.DragSource == null) return;
            Point mousePos = e.GetPosition(MainCanvas);   // 修正为画布坐标
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