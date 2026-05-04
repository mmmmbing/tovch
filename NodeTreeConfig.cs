using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace full_AI_tovch
{

        public class NodeTreeConfig
        {
            public double TrackRadius { get; set; } = 80;       // 当前层子节点的轨道半径
            public double ButtonSize { get; set; } = 40;        // 当前层节点按钮大小
            public int VertexCount { get; set; } = 8;           // 当前层节点数量
            public List<int> ExpandableIndices { get; set; } = new List<int>(); // 可展开的索引
            public NodeTreeConfig ChildTree { get; set; }       // 子层统一配置（当某个节点展开时使用）

            // 每个节点的初始文本，数量不足时用索引填充
            public List<string> Labels { get; set; } = new List<string>();
        }
   }
