using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


//这个文件用来写一个节点树类，里面包含了一下节点树的默认属性

namespace full_AI_tovch
{

        public class NodeTreeConfig
        {
            public double TrackRadius { get; set; } = 80;       // 当前层子节点的轨道半径
            public double ButtonSize { get; set; } = 40;        // 当前层节点按钮大小
            public int VertexCount { get; set; } = 8;           // 当前层节点数量

            public List<string> Labels { get; set; } = new List<string>();// 每个节点的初始文本，数量不足时用索引填充
        public Dictionary<int, NodeTreeConfig> ExpandableConfigs { get; set; }
        = new Dictionary<int, NodeTreeConfig>();


    }
   }
