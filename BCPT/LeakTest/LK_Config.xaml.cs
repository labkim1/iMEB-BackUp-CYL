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
using BCPT.Properties;
using System.Collections;

namespace BCPT.LeakTest
{
    /// <summary>
    /// LK_Config.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LK_Config : Window
    {
        private BCPT.LeakTestDefine _LeakTestDefine = null;
        public LK_Config()
        {
            InitializeComponent();
            LocalInit();
        }
        private bool LocalInit()
        {
            ArrayList selected = new ArrayList();
            _LeakTestDefine = BCPT.MainWindow.Bc.LeakTestConfig;
            object item = this.GetType().GetField("_LeakTestDefine",
                          System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this);
            selected.Add(item);
            this.PG_LeakTest.SelectedObjects = selected.ToArray();
            this.PG_LeakTest.HelpVisible = true;
            this.PG_LeakTest.RefreshPropertyList();
            return true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Close
            BCPT.MainWindow.Bc.LeakTestConfig = _LeakTestDefine;
            this.Close();
        }
    }
}
