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



        // 右键长按相关
        private bool isRightButtonPressed = false;
        private System.Windows.Threading.DispatcherTimer longPressTimer;
        private System.Windows.Threading.DispatcherTimer deleteRepeatTimer;
        private DateTime rightButtonDownTime;

        public MainWindow()
        {

            InitializeComponent();

            this.ShowInTaskbar = false;
            //this.FormBorderStyle = FormBorderStyle.None;
            // 防止窗口被激活抢焦点

            // 构造函数或 SourceInitialized 中
            MenuActivation.ToggleLabelsRequested += OnToggleLabels;



            NodeController.NavigateToChildren = NavigateToLevel;
            NodeController.NavigateBack = GoBack;

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
            centerX = centerX - 237;
            centerY = centerY + 27;



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
                    Background = new SolidColorBrush(Colors.Orange), // 橙色背景，绝对可见
                    Foreground = new SolidColorBrush(Colors.Black),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Template = CreateCircleButtonTemplate(50)                              // 用默认按钮样式（方角）
                };

               

                //MessageBox.Show(node.CenterX.ToString() + " "+ node.CenterY.ToString() );
                // 设置位置（已经考虑 ButtonSize 偏移）
                Canvas.SetLeft(btn,node.CenterX -node.ButtonSize);
                Canvas.SetTop(btn, node.CenterY -node.ButtonSize);
                //MessageBox.Show((node.CenterX - node.ButtonSize / 2).ToString() + " " + (node.CenterY - node.ButtonSize / 2).ToString());
                //调试——输出坐标
                double left = Canvas.GetLeft(btn);
                double top = Canvas.GetTop(btn);
                // 添加到画布
                MainCanvas.Children.Add(btn);
                //绑定节点
                btn.PreviewMouseRightButtonDown += OnButtonRightButtonDown;
                btn.PreviewMouseRightButtonUp += OnButtonRightButtonUp;
                btn.MouseLeave += OnButtonMouseLeave;
                node.UiButton = btn;

                //临时不播放动画，直接可见
                node.PlayShowAnimation();
            }

            NodeController.ConfigureAll(rootNodes);
            // 再弹一下画布上的按钮总数
            currentLevelNodes = rootNodes;

            //        MessageBox.Show($"Canvas 子元素总数: {MainCanvas.Children.Count}, " +
            //$"其中 Button 数量: {MainCanvas.Children.OfType<Button>().Count()}");


        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 示例配置构建树
            var config = new NodeTreeConfig
            {
                TrackRadius = 100,
                ButtonSize = 50,
                VertexCount = 4,
                Labels = new List<string> { "num", "charaters", "Sym", "special" },
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
                    { 2, new NodeTreeConfig  
                        {
                            TrackRadius = 100,
                            ButtonSize = 50,
                            VertexCount = 5,
                            Labels = new List<string> { "F", "G","H","I","J" }
                        }
                    },
                    { 3, new NodeTreeConfig  
                        {
                            TrackRadius = 100,
                            ButtonSize = 50,
                            VertexCount = 5,
                            Labels = new List<string> { "K", "M","L","N","Q" }
                        }
                    },
                    { 4, new NodeTreeConfig  
                        {
                            TrackRadius = 100,
                            ButtonSize = 50,
                            VertexCount = 5,
                            Labels = new List<string> { "P", "Q","R","S","T" }
                        }
                    },
                    { 5, new NodeTreeConfig
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
                VertexCount = 10,
                Labels = new List<string> { "!", "@","#","$","%","^","&","*","(",")" },
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
                TrackRadius = 60,
                ButtonSize = 35,
                VertexCount = 2,
                Labels = new List<string> { "跑", "跳" },
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
            NodeController.ConfigureAll(rootNodes);

            
            // 还可立即进行个性定制，例如：
            //NodeController.SetNodeText(rootNodes, "0", "开始");
            NodeController.SetNodeAnimationOverrides(rootNodes, "1/0", showDuration: 0.5);


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

            // 隐藏当前节点，然后显示上一级
            PlayHideLevel(currentLevelNodes, () =>
            {
                ClearCanvas();
                foreach (var node in previousLevel)
                {
                    CreateButtonForNode(node);
                    node.PlayShowAnimation();
                }

                NodeController.ConfigureAll(previousLevel);
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
    foreach (var node in nodes)
    {
        CreateButtonForNode(node);
        node.PlayShowAnimation();
    }
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
                Template = CreateCircleButtonTemplate(node.ButtonSize / 2)   // 直接设置
            };

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

            btn.PreviewMouseRightButtonDown += OnButtonRightButtonDown;
            btn.PreviewMouseRightButtonUp += OnButtonRightButtonUp;
            btn.MouseLeave += OnButtonMouseLeave;

            node.UiButton = btn;

            return btn;
        }
        // 生成圆形按钮模板
        private static ControlTemplate CreateCircleButtonTemplate(double cornerRadius)
        {
            var template = new ControlTemplate(typeof(Button));

            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
            // ▼ 关键修改：背景色不写死，而是绑定到按钮的 Background 属性
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            border.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Stretch);

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));

            border.AppendChild(content);
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


        private void OnButtonRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton != MouseButtonState.Pressed) return;
            isRightButtonPressed = true;
            rightButtonDownTime = DateTime.Now;

            longPressTimer.Interval = TimeSpan.FromMilliseconds(InteractionConfig.LongPressThreshold);
            longPressTimer.Start();

            e.Handled = true; // 阻止冒泡，防止触发窗口的全局右键事件
        }

        // 按钮上右键释放：短按返回，长按结束删除
        private void OnButtonRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isRightButtonPressed) return;
            isRightButtonPressed = false;

            longPressTimer.Stop();
            bool wasDeleting = deleteRepeatTimer.IsEnabled;
            deleteRepeatTimer.Stop();

            // 如果没触发过长按（不是删除状态），且右键持续时间很短 → 返回上一级
            if (!wasDeleting && (DateTime.Now - rightButtonDownTime).TotalMilliseconds < InteractionConfig.LongPressThreshold)
            {
                GoBack();
            }

            e.Handled = true;
        }

        // 鼠标离开按钮：如果正在长按删除，则停止
        private void OnButtonMouseLeave(object sender, MouseEventArgs e)
        {
            if (deleteRepeatTimer.IsEnabled)
            {
                longPressTimer.Stop();
                deleteRepeatTimer.Stop();
                isRightButtonPressed = false;
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