using Fiddler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Gizeta.KanColleModifier.KCV
{
    /// <summary>
    /// ModifierView.xaml 的交互逻辑
    /// </summary>
    public partial class ModifierView : UserControl
    {
        public ModifierView()
        {
            InitializeComponent();

            this.DataContext = ModifierViewModel.Instance;
        }
    }
}
