using full_AI_tovch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace tovch
{
    /// <summary>
    /// AddNodeDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AddNodeDialog : Window
    {
        public bool IsExpandable => chkExpandable.IsChecked == true;
        public ExpandStyle ExpandStyle => cmbExpandStyle.SelectedIndex == 0 ? ExpandStyle.Normal : ExpandStyle.Inline;

        public AddNodeDialog()
        {
            InitializeComponent();
            pnlExpandStyle.IsEnabled = false;
            chkExpandable.Checked += (s, e) => pnlExpandStyle.IsEnabled = true;
            chkExpandable.Unchecked += (s, e) => pnlExpandStyle.IsEnabled = false;
        }

        private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
