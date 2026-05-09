# Tovch

一款结合kando设计理念的鼠标输入软件

本项目使用C sharp作为编程语言，使用WPF开发桌面应用架构。

项目结构如图
	

```test
/NodeTreeConfig.cs    //项目节点树的配置文件
/MenuItemNode.cs      //项目节点的Class类文件
/NodeTree.cs		  //节点树的Class文件
/AnimationConfig.cs   //动画配置文件
/NodeConfig.cs		  //节点配置文件
/TextInjection.cs     //文本注入实现文件
/MainWindow.xaml      //渲染进程文件
/MainWindow.xaml.cs   //主进程文件
/NodeController.cs    //节点方法配置文件
/LabelConfig.cs       //隐藏标签配置文件
/InteractionConfig.cs //交互配置文件<快捷键配置文件>
/MenuActivation.cs    //快捷键注册文件
```

项目结构清晰，主要的节点树的构成在Mainwindow.cs下的Window_Load方法下。

## 默认配置快捷键

项目启动后的默认唤醒快捷键是：ctrl + shift + F12   测试的时候防止快捷键冲突设置的冷门快捷键。
项目隐藏快捷键为:ESC

切换标签快捷键是：ctrl + T
