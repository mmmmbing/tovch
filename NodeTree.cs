using full_AI_tovch;
using System.Collections.Generic;
using System.Linq;

//节点树的构建以及节点树的遍历

namespace full_AI_tovch
{
    public static class NodeTree
    {
        // 根据配置递归构建完整节点树
        public static List<MenuItemNode> BuildTree(NodeTreeConfig config, double selfTrackRadius = 0)
        {
            if (config == null) return new List<MenuItemNode>();

            // 本层节点排列使用的半径（优先用 config.TrackRadius，否则用父层传入的）
            double selfRadius = config.TrackRadius > 0 ? config.TrackRadius : selfTrackRadius;

            var nodes = new List<MenuItemNode>();
            for (int i = 0; i < config.VertexCount; i++)
            {
                string label = (config.Labels != null && i < config.Labels.Count)
                                ? config.Labels[i]
                                : i.ToString();

                var node = new MenuItemNode
                {
                    Text = label,
                    ButtonSize = config.ButtonSize,
                    SelfTrackRadius = selfRadius      // 本层排列半径
                };

                // 检查当前索引是否配置了子层
                if (config.ExpandableConfigs.TryGetValue(i, out var childConfig))
                {
                    node.Children = BuildTree(childConfig, config.TrackRadius);
                }

                nodes.Add(node);
            }
            return nodes;
        }

        // 通过路径（如 "0/2/1"）查找节点
        public static MenuItemNode FindNodeByPath(List<MenuItemNode> rootNodes, string path)
        {
            if (rootNodes == null || string.IsNullOrWhiteSpace(path))
                return null;

            var indices = path.Split('/').Select(s => int.Parse(s)).ToArray();
            IEnumerable<MenuItemNode> current = rootNodes;

            for (int i = 0; i < indices.Length; i++)
            {
                var node = current.ElementAtOrDefault(indices[i]);
                if (node == null) return null;
                if (i == indices.Length - 1) return node;
                current = node.Children;
            }
            return null;
        }
    }
}
