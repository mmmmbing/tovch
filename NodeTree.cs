using full_AI_tovch;
using System.Collections.Generic;
using System.Linq;

namespace full_AI_tovch
{
    public static class NodeTree
    {
        // 根据配置递归构建完整节点树
        public static List<MenuItemNode> BuildTree(NodeTreeConfig config, double inheritedTrackRadius = 0)
        {
            if (config == null) return new List<MenuItemNode>();

            var nodes = new List<MenuItemNode>();
            // 本层排列使用的半径：优先使用 config.TrackRadius，否则用从父层继承来的半径
            double selfRadius = config.TrackRadius > 0 ? config.TrackRadius : inheritedTrackRadius;

            for (int i = 0; i < config.VertexCount; i++)
            {
                string label = (config.Labels != null && i < config.Labels.Count)
                                ? config.Labels[i]
                                : i.ToString();

                var node = new MenuItemNode
                {
                    Text = label,
                    ButtonSize = config.ButtonSize,
                    SelfTrackRadius = selfRadius,                      // 本层排列半径
                    TrackRadius = config.ChildTree?.TrackRadius ?? 0   // 下一层子节点的排列半径
                };

                if (config.ExpandableIndices.Contains(i) && config.ChildTree != null)
                {
                    // 递归时把下一层的 SelfTrackRadius 传给子节点
                    node.Children = BuildTree(config.ChildTree, node.TrackRadius);
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
