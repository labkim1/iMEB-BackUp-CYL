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
using MahApps.Metro;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

using System.Collections;

using BCPT.Properties;
using System.Windows.Threading;
using System.Threading;

namespace BCPT.SystemConfig
{

    /// <summary>
    /// SystemConfig.xaml에 대한 상호 작용 논리
    /// </summary>   
    public partial class SystemConfig : MetroWindow
    {
        
        private BCPT.MainWindow.MESSPEC.Specification     _messpec      = null;
        private BCPT.MainWindow.SystemConfig1.SYSTEM_CONFIG _systemconfig = null;
        public SystemConfig(ref BCPT.MainWindow.MESSPEC.Specification messpec, ref BCPT.MainWindow.SystemConfig1.SYSTEM_CONFIG systemconfig)
        {
            InitializeComponent();
            _messpec      = messpec;
            _systemconfig = systemconfig;
            // 프로퍼티 그리드 초기 설정
            ArrayList selected = new ArrayList();
            object item = this.GetType().GetField("_messpec",
                          System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this);
            selected.Add(item);
            this.PG_Mes.SelectedObjects = selected.ToArray();
            this.PG_Mes.HelpVisible = true;
            this.PG_Mes.RefreshPropertyList();

            selected.Clear();
            object item1 = this.GetType().GetField("_systemconfig",
                          System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this);
            selected.Add(item1);
            this.PG_System.SelectedObjects = selected.ToArray();
            this.PG_System.HelpVisible = true;
            this.PG_System.RefreshPropertyList();
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            
        }
    }
}
