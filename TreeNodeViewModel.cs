using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace full_AI_tovch
{
    public class TreeNodeViewModel : INotifyPropertyChanged
    {
        private string _label;
        private double _trackRadius;
        private double _buttonSize;
        private List<string> _labels;
        private ExpandStyle _expandStyle;

        public string Label { get => _label; set { _label = value; OnPropertyChanged(); } }
        public double TrackRadius { get => _trackRadius; set { _trackRadius = value; OnPropertyChanged(); } }
        public double ButtonSize { get => _buttonSize; set { _buttonSize = value; OnPropertyChanged(); } }
        public List<string> Labels { get => _labels; set { _labels = value; OnPropertyChanged(); OnPropertyChanged(nameof(LabelsText)); } }
        public string LabelsText
        {
            get => _labels == null ? "" : string.Join(",", _labels);
            set
            {
                _labels = value?.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Labels));
            }
        }
        public ExpandStyle ExpandStyle { get => _expandStyle; set { _expandStyle = value; OnPropertyChanged(); } }
        public ObservableCollection<TreeNodeViewModel> Children { get; set; } = new ObservableCollection<TreeNodeViewModel>();

        public static TreeNodeViewModel FromConfig(NodeTreeConfigData config, string defaultLabel = null)
        {
            var vm = new TreeNodeViewModel
            {
                Label = defaultLabel ?? (config.Labels?.FirstOrDefault() ?? "节点"),
                TrackRadius = config.TrackRadius,
                ButtonSize = config.ButtonSize,
                Labels = config.Labels ?? new List<string>()
            };
            // 根据 Labels 列表创建子节点
            if (config.Labels != null)
            {
                for (int i = 0; i < config.Labels.Count; i++)
                {
                    string childLabel = config.Labels[i];
                    NodeTreeConfigData childConfig = null;
                    // 如果该子节点有扩展配置（即可以继续展开），则使用对应配置
                    if (config.ExpandableConfigs != null && config.ExpandableConfigs.ContainsKey(i))
                        childConfig = config.ExpandableConfigs[i];
                    else
                        childConfig = new NodeTreeConfigData(); // 叶子节点，无进一步子节点
                    var childVm = FromConfig(childConfig, childLabel);
                    vm.Children.Add(childVm);
                }
            }
            return vm;
        }

        public NodeTreeConfigData ToConfig()
        {
            var config = new NodeTreeConfigData
            {
                TrackRadius = TrackRadius,
                ButtonSize = ButtonSize,
                VertexCount = Children.Count,
                Labels = Labels,
                ExpandStyle = ExpandStyle,
                ExpandableConfigs = new Dictionary<int, NodeTreeConfigData>()
            };
            int idx = 0;
            foreach (var child in Children)
                config.ExpandableConfigs[idx++] = child.ToConfig();
            return config;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}