using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.Threading;
namespace BCPT
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        Mutex _mutex = null;
        protected override void OnStartup(StartupEventArgs e)
        {
            string mutextName = "BCPT";
            bool isCreatedNew = false;
            try
            {
                _mutex = new Mutex(true, mutextName, out isCreatedNew);

                if (isCreatedNew)
                {
                    base.OnStartup(e);
                }
                else
                {
                    MessageBox.Show("Application already started.", "Error", MessageBoxButton.OK, MessageBoxImage.Information); 
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n" + ex.StackTrace + "\n\n" + "Application Existing...", "Exception");
                Application.Current.Shutdown();
            }
           
        }

    }
}
