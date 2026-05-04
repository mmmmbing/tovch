using full_AI_tovch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace full_AI_tovch
{
    public partial class MainWindow : Window
    {
        // 整个菜单树的根节点列表（永驻）
        private List<MenuItemNode> rootNodes;
        // 当前显示的节点列表
        private List<MenuItemNode> currentLevelNodes;
        // 历史层级，用于后退（每层保存其节点列表）
        private readonly Stack<List<MenuItemNode>> history = new Stack<List<MenuItemNode>>();



        // 右键长按相关
        private bool isRightButtonPressed = false;
        private System.Windows.Threading.DispatcherTimer longPressTimer;
        private System.Windows.Threading.DispatcherTimer deleteRepeatTimer;
        private DateTime rightButtonDownTime;

        public MainWindow()
        {
            InitializeComponent();

            // 防止窗口被激活抢焦点
            SourceInitialized += (s, e) =>
            {
                var helper = new WindowInteropHelper(this);
                IntPtr handle = helper.Handle;
                MenuActivation.Initialize(handle);
                // 不激活窗口
                int exStyle = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
                exStyle |= NativeMethods.WS_EX_NOACTIVATE;
                NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, exStyle);


                // 初始化全局热键管理器
                MenuActivation.Initialize(handle);
                MenuActivation.ShowRequested += ShowMenu;
                MenuActivation.HideRequested += HideMenu;
                System.Windows.MessageBox.Show("事件绑定完成");

                // 注册唤出热键（始终有效）
                MenuActivation.RegisterWakeUpHotkey();
            };

            this.Visibility = Visibility.Collapsed;
            //this.KeyDown += MainWindow_KeyDown;

            // 设置窗口覆盖全工作区（非最大化）
            Loaded += (s, e) =>
            {
                Left = SystemParameters.WorkArea.Left;
                //Top = SystemParameters.WorkArea.Top;
                //Width = SystemParameters.WorkArea.Width;
                //Height = SystemParameters.WorkArea.Height;
            };


            Closing += (s, e) => MenuActivation.Cleanup();

            InitializeRightButtonHandling();

        }

        private void ShowMenu()
        {
            try
            {
                MessageBox.Show("ShowMenu 开始执行");
                this.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 255, 0, 0));

                if (Visibility == Visibility.Visible)
                {
                    //MessageBox.Show("窗口已处于可见状态，直接返回");
                    //return;
                }

                Visibility = Visibility.Visible;
                Topmost = true;
                MessageBox.Show("窗口已设为 Visible，准备注册隐藏热键");
                MenuActivation.RegisterHideHotkey();
                MessageBox.Show("隐藏热键注册完毕，准备调用 SnapToMouse");
                SnapToMouse();
                MessageBox.Show("SnapToMouse 调用完成");
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
            // 注销隐藏热键
            MenuActivation.UnregisterHideHotkey();
        }

        private void SnapToMouse()
        {
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
            MessageBox.Show($"rootNodes 共 {rootNodes.Count} 个节点");

            // 2. 取得鼠标位置
            var mousePos = System.Windows.Forms.Cursor.Position;
            double centerX = mousePos.X;
            double centerY = mousePos.Y;

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
                    Content = node.Text,
                    Focusable = false,
                    Cursor = Cursors.Hand,
                    Opacity = 1,                                     // 强行可见
                    RenderTransform = new ScaleTransform(1, 1),      // 正常比例
                    RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                    Background = new SolidColorBrush(Colors.Orange), // 橙色背景，绝对可见
                    Foreground = new SolidColorBrush(Colors.Black),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Template = null                                  // 用默认按钮样式（方角）
                };

                // 设置位置（已经考虑 ButtonSize 偏移）
                Canvas.SetLeft(btn, node.CenterX - node.ButtonSize / 2);
                Canvas.SetTop(btn, node.CenterY - node.ButtonSize / 2);

                // 调试输出坐标
                System.Diagnostics.Debug.WriteLine($"按钮{i}: 文字={node.Text}, 中心=({node.CenterX},{node.CenterY}), 左上=({node.CenterX - node.ButtonSize / 2},{node.CenterY - node.ButtonSize / 2})");

                // 添加到画布
                MainCanvas.Children.Add(btn);

                // 绑定节点
                node.UiButton = btn;

                // 临时不播放动画，直接可见
                // node.PlayShowAnimation();
            }

            // 再弹一下画布上的按钮总数
            MessageBox.Show($"Canvas 上 Button 数量: {MainCanvas.Children.OfType<Button>().Count()}");

            currentLevelNodes = rootNodes;
            history.Clear();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 示例配置构建树
            var config = new NodeTreeConfig
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 6,
                Labels = new List<string> { "你好", "苹果", "香蕉", "橙子", "葡萄", "结束" },
                ExpandableIndices = new List<int> { 1, 2 },
                ChildTree = new NodeTreeConfig
                {
                    TrackRadius = 60,
                    ButtonSize = 35,
                    VertexCount = 4,
                    Labels = new List<string> { "红", "绿", "酸", "甜" },
                    ExpandableIndices = new List<int> { 0 },
                    ChildTree = new NodeTreeConfig
                    {
                        TrackRadius = 40,
                        ButtonSize = 25,
                        VertexCount = 3,
                        Labels = new List<string> { "深红", "浅红", "粉" }
                    }
                }
            };
            rootNodes = NodeTree.BuildTree(config);
        }

        // 切换到指定层级（播放出现动画，隐藏其他）
        private void NavigateToLevel(List<MenuItemNode> newLevelNodes)
        {
            // 隐藏当前层级
            if (currentLevelNodes != null)
            {
                PlayHideLevel(currentLevelNodes, () =>
                {
                    // 移除按钮后显示新层级
                    ClearCanvas();
                    CreateAndShowLevel(newLevelNodes);
                });
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
            this.PreviewMouseRightButtonDown += OnGlobalRightButtonDown;
            this.PreviewMouseRightButtonUp += OnGlobalRightButtonUp;

            // 准备定时器
            longPressTimer = new System.Windows.Threading.DispatcherTimer();
            longPressTimer.Interval = TimeSpan.FromMilliseconds(InteractionConfig.LongPressThreshold);
            longPressTimer.Tick += OnLongPressTriggered;

            deleteRepeatTimer = new System.Windows.Threading.DispatcherTimer();
            deleteRepeatTimer.Interval = TimeSpan.FromMilliseconds(InteractionConfig.DeleteRepeatInterval);
            deleteRepeatTimer.Tick += OnDeleteRepeatTick;
        }

        private void OnGlobalRightButtonDown(object sender, MouseButtonEventArgs e)
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
        }

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

            // 隐藏当前节点，然后显示上一级
            PlayHideLevel(currentLevelNodes, () =>
            {
                ClearCanvas();
                foreach (var node in previousLevel)
                {
                    CreateButtonForNode(node);
                    node.PlayShowAnimation();
                }
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
            foreach (var node in nodes)
            {
                node.PlayHideAnimation(() =>
                {
                    if (--remaining == 0)
                        onAllCompleted?.Invoke();
                });
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
            // 计算布局中心点（屏幕中心）
            double centerX = SystemParameters.PrimaryScreenWidth / 2;
            double centerY = SystemParameters.PrimaryScreenHeight / 2;

            // 对于根层级，围绕屏幕中心；子层级则围绕父节点位置计算。
            // 这里通过节点的 CenterX/CenterY 已存储，若为根节点则需临时计算。
            if (history.Count == 0) // 顶层
            {
                LayoutNodesAroundPoint(nodes, centerX, centerY, 0);
            }
            // 否则 nodes 来自父节点的 Children，它们的 CenterX/CenterY 已经在展开时计算（由展开调用者设置）

            foreach (var node in nodes)
            {
                CreateButtonForNode(node);
                node.PlayShowAnimation();
            }
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
                Content = node.Text,
                Focusable = false,
                Cursor = Cursors.Hand
            };

            // 外观（圆角 = 高度一半 → 正圆）
            var bg = node.BackgroundOverride ?? NodeConfig.GlobalBackground;
            var fg = node.ForegroundOverride ?? NodeConfig.GlobalForeground;
            btn.Background = bg;
            btn.Foreground = fg;
            btn.FontFamily = NodeConfig.GlobalFontFamily;
            btn.FontSize = NodeConfig.GlobalFontSize;
            btn.BorderThickness = new Thickness(0);
            btn.Template = CreateCircleButtonTemplate(node.ButtonSize / 2);

            // 放置到 Canvas
            Canvas.SetLeft(btn, node.CenterX - node.ButtonSize / 2);
            Canvas.SetTop(btn, node.CenterY - node.ButtonSize / 2);
            MainCanvas.Children.Add(btn);

            // 点击事件
            btn.Click += (s, e) =>
            {
                if (node.Children.Count > 0) // 可展开节点：进入子层
                {
                    // 布置子节点：以当前节点中心为圆心，子节点的轨道半径为由 node.Children[0].TrackRadius 提供
                    double childRadius = node.Children[0].TrackRadius; // 假设同层统一
                    LayoutNodesAroundPoint(node.Children, node.CenterX, node.CenterY);
                    NavigateToLevel(node.Children);
                }
                else // 叶节点：打字
                {
                    TextInjection.Send(node.Text);
                    // 可选：窗口退出或保留
                }
            };

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

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(contentPresenter);

            template.VisualTree = border;
            return template;
        }

        // 点击空白区域后退
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == MainCanvas || e.OriginalSource == this)
            {
                GoBack();
            }
        }
    }

    // 原生窗口样式调用
    internal static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}