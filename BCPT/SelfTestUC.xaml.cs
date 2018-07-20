using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;

namespace BCPT
{
    /// <summary>
    /// SelfTestUC.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SelfTestUC : UserControl
    {
        private DispatcherTimer LeakTestControlTimer;

        //private bool _IsTestDone = false; //
        private int _LastPlcData0 = 0x0000;
        private int _LastPlcData1 = 0x0000;

        private bool _OnceRun = false;

        private LeakTestStep _LT_Step;
        private Stopwatch _CheckTimer = new Stopwatch();
        private LeakTestDefine _LeakTestSpec;
        private double _FirstPriValue;
        private double _FirstFloValue;
        private double _LastPriValue;
        private double _LastFloValue;
        public List<LeakTestGrid> _LeakTestGrid = new List<LeakTestGrid>();
        public SelfTestUC()
        {
            InitializeComponent();
            _LeakTestSpec = MainWindow.Bc.LeakTestConfig;          
            this.LeakTestGridDisplay.ItemsSource = _LeakTestGrid;
            this.LeakTestGridDisplay.DataContext = _LeakTestGrid;
            _LastPlcData0 = 0x0000;
            _LastPlcData1 = 0x0000;
            //
            LeakTestControlTimer            = new DispatcherTimer();
            LeakTestControlTimer.Interval   = TimeSpan.FromMilliseconds(100); // 0.1Sec
            LeakTestControlTimer.Tick      += new EventHandler(LeakTestControlTimer_Elapsed);
            LeakTestControlTimer.Start();
            Delay(50);
            _OnceRun = false;
            MainWindow.Bc.PLC_BusySetting();
            Delay(300);
            _LT_Step = LeakTestStep.LOWAIR_STEP_PCBUSYSET;
        }
        private static void DoEvents()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                                                  new Action(delegate { }));
        }
        private static DateTime Delay(int MS)
        {
            DateTime ThisMoment = DateTime.Now;
            TimeSpan duration = new TimeSpan(0, 0, 0, 0, MS);
            DateTime AfterWards = ThisMoment.Add(duration);
            while (AfterWards >= ThisMoment)
            {
                DoEvents();
                ThisMoment = DateTime.Now;
            }
            return DateTime.Now;
        }
        public enum LeakTestStep : int
        {
            IDLE                      = 0,
            LOWAIR_STEP_PCBUSYSET     = 1,
            LOWAIR_STEP_SOLSET        = 2,
            LOWAIR_STEP_COSMOSET      = 3,
            LOWAIR_STEP_TIMECHECK1    = 4,
            LOWAIR_STEP_HOLDSET       = 5,
            LOWAIR_STEP_LEAKCHECK1    = 6,
            LOWAIR_STEP_JUDGE         = 7,

            LOWVAC_STEP_SOLSET        = 10,
            LOWVAC_STEP_COSMOSET      = 11,
            LOWVAC_STEP_TIMECHECK1    = 12,
            LOWVAC_STEP_HOLDSET       = 13,
            LOWVAC_STEP_LEAKCHECK1    = 14,
            LOWVAC_STEP_JUDGE         = 15,

            HIGHAIR_STEP_SOL_SET      = 20,
            HIGHAIR_STEP_TIMECHECK1   = 21,
            HIGHAIR_STEP_HOLDSET      = 22,
            HIGHAIR_STEP_LEAKCHECK1   = 23,
            HIGHAIR_STEP_JUDGE        = 24,

            AIRSENSOR_STEP_SOL_SET    = 30,
            AIRSENSOR_STEP_COSMOSET   = 31,
            AIRSENSOR_STEP_TIMECHECK1 = 32,
            AIRSENSOR_STEP_HOLDSET    = 34,
            AIRSENSOR_STEP_LEAKCHECK1 = 35,
            AIRSENSOR_STEP_JUDGE      = 36,

            VACSENSOR_STEP_SOL_SET    = 40,
            VACSENSOR_STEP_COSMOSET   = 41,
            VACSENSOR_STEP_TIMECHECK1 = 42,
            VACSENSOR_STEP_HOLDSET    = 43,
            VACSENSOR_STEP_LEAKCHECK1 = 44,
            VACSENSOR_STEP_JUDGE      = 45,

            END_TEST                  = 50,

            ERROR                     = 100

        }
        private void LeakTestControlTimer_Elapsed(object sender,EventArgs e)
        {
            double cdt = _CheckTimer.ElapsedMilliseconds/1000.0;
            DurationDisplay.Text = string.Format("{0:F3}", cdt);
            this.StepMessage.Text = _LT_Step.ToString();
            MainWindow.Bc.PLC_CurrentState();            
            switch (_LT_Step)
            {
                case LeakTestStep.IDLE :
                    if (_OnceRun)
                    {
                        if ((MainWindow.Bc.CurPLCState.Loading) && (MainWindow.Bc.CurPLCState.AutomManual))
                        {
                            MainWindow.Bc.PLC_BusySetting();
                            _LT_Step = LeakTestStep.LOWAIR_STEP_PCBUSYSET;
                            _OnceRun = false;
                            break;
                        }
                    }
                    break;
                case LeakTestStep.LOWAIR_STEP_PCBUSYSET:
            _LeakTestGrid.Clear();
            _LeakTestGrid.Add(new LeakTestGrid("저압 PRI", "코스모 장치에 설정된 압력으로 리크테스트" + _LeakTestSpec.LowAir_CheckTime.ToString() + "초후 측정)",
                                               _LeakTestSpec.LowAirMaster,
                                               0.0,
                                               0.0,
                                               _LeakTestSpec.LowAir_DiffBar,
                                               false
                                              ));
            _LeakTestGrid.Add(new LeakTestGrid("저압 FLO", "코스모 장치에 설정된 압력으로 리크테스트(" + _LeakTestSpec.LowAir_CheckTime.ToString() + "초후 측정)",
                                               _LeakTestSpec.LowAirMaster,
                                               0.0,
                                               0.0,
                                               _LeakTestSpec.LowAir_DiffBar,
                                               false
                                              ));
            _LeakTestGrid.Add(new LeakTestGrid("저압 PRI 센서단", "코스모 장치에 설정된 압력으로 리크테스트" + _LeakTestSpec.LowAir_CheckTime.ToString() + "초후 측정)",
                                               _LeakTestSpec.LowAirMaster,
                                               0.0,
                                               0.0,
                                               _LeakTestSpec.LowAir_DiffBar,
                                               false
                                              ));
            _LeakTestGrid.Add(new LeakTestGrid("저압 FLO 센서단", "코스모 장치에 설정된 압력으로 리크테스트" + _LeakTestSpec.LowAir_CheckTime.ToString() + "초후 측정)",
                                               _LeakTestSpec.LowAirMaster,
                                               0.0,
                                               0.0,
                                               _LeakTestSpec.LowAir_DiffBar,
                                               false
                                              ));

            _LeakTestGrid.Add(new LeakTestGrid("진공 PRI", "코스모 장치에 설정된 진공으로 리크테스트" + _LeakTestSpec.Vacuum_CheckTime.ToString() + "초후 측정)",
                                               _LeakTestSpec.VacuumAirMaster,
                                               0.0,
                                               0.0,
                                               _LeakTestSpec.Vacuum_DiffBar,
                                               false
                                              ));
            _LeakTestGrid.Add(new LeakTestGrid("진공 FLO", "코스모 장치에 설정된 진공으로 리크테스트" + _LeakTestSpec.Vacuum_CheckTime.ToString() + "초후 측정)",
                                               _LeakTestSpec.VacuumAirMaster,
                                               0.0,
                                               0.0,
                                               _LeakTestSpec.Vacuum_DiffBar,
                                               false
                                              ));
            _LeakTestGrid.Add(new LeakTestGrid("진공 PRI 센서단", "코스모 장치에 설정된 진공으로 리크테스트" + _LeakTestSpec.Vacuum_CheckTime.ToString() + "초후 측정)",
                                               _LeakTestSpec.VacuumAirMaster,
                                               0.0,
                                               0.0,
                                               _LeakTestSpec.Vacuum_DiffBar,
                                               false
                                              ));
            _LeakTestGrid.Add(new LeakTestGrid("진공 FLO 센서단", "코스모 장치에 설정된 진공으로 리크테스트" + _LeakTestSpec.Vacuum_CheckTime.ToString() + "초후 측정)",
                                               _LeakTestSpec.VacuumAirMaster,
                                               0.0,
                                               0.0,
                                               _LeakTestSpec.Vacuum_DiffBar,
                                               false
                                              ));


            _LeakTestGrid.Add(new LeakTestGrid("고압 PRI", "고압 레귤레이터 압력으로 리크테스트" + _LeakTestSpec.HighAir_CheckTime.ToString() + "초후 측정)",
                                               _LeakTestSpec.HighAirMaster,
                                               0.0,
                                               0.0,
                                               _LeakTestSpec.HighAir_DiffBar,
                                               false
                                              ));
            _LeakTestGrid.Add(new LeakTestGrid("고압 FLO", "고압 레귤레이터 압력으로 리크테스트" + _LeakTestSpec.HighAir_CheckTime.ToString() + "초후 측정)",
                                               _LeakTestSpec.HighAirMaster,
                                               0.0,
                                               0.0,
                                               _LeakTestSpec.HighAir_DiffBar,
                                               false
                                              ));

            LeakTestGridDisplay.ItemsSource = _LeakTestGrid;
            LeakTestGridDisplay.Items.Refresh();                          
      
                    Delay(50);
                    _LastPlcData0 = 0x0000;
                    _LastPlcData1 = 0x0000;
                    MainWindow.Bc.PLC_BusySetting();
                    _LT_Step = LeakTestStep.LOWAIR_STEP_SOLSET;
                    break;
                #region 저압 전체 라인 테스트
                case LeakTestStep.LOWAIR_STEP_SOLSET:
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE));
                    // Air Sensor ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE));
                    // High Air OFF
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);

                    _LT_Step = LeakTestStep.LOWAIR_STEP_COSMOSET;
                    break;
                case LeakTestStep.LOWAIR_STEP_COSMOSET:
                    // CH=3 is 5.0Bar Charge Mode
                    
                    int PriCh = _LeakTestSpec.LowAirCosmoCh;
                    int FloCh = _LeakTestSpec.LowAirCosmoCh;

                    MainWindow.Bc.PLC_CosmoChSetting(PriCh, FloCh, 2.0);
                    MainWindow.BCControl.CMDRET ret = MainWindow.Bc.PLC_CosmoChargeRun(3.0);
                        //  ret = PLC_CosmoChSetting(2, 2, 3.0);
                    if (ret == MainWindow.BCControl.CMDRET.DONE) _LT_Step = LeakTestStep.LOWAIR_STEP_TIMECHECK1;
                    else                                         _LT_Step = LeakTestStep.ERROR;
                    _CheckTimer.Restart();
                    break;
                case LeakTestStep.LOWAIR_STEP_TIMECHECK1:
                    double DurationTime = _LeakTestSpec.LowAir_PressTime;
                    double Dt = _CheckTimer.ElapsedMilliseconds / 1000.0;
                    if (Dt>DurationTime)
                    {
                        _CheckTimer.Stop();
                        _LT_Step = LeakTestStep.LOWAIR_STEP_HOLDSET;               
                        break;
                    }
                    break;
                case LeakTestStep.LOWAIR_STEP_HOLDSET:                    
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN));
                    // Air Sensor ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE));
                    // High Air OFF
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);

                    // COSMO STOP
                    MainWindow.BCControl.CMDRET ret1 = MainWindow.Bc.PLC_CosmoStopSetting(3.0);
                    
                    _FirstPriValue = MainWindow.Bc.CurDAQ.PriAir;
                    _FirstFloValue = MainWindow.Bc.CurDAQ.FloAir;

                    _CheckTimer.Restart();

                    _LT_Step = LeakTestStep.LOWAIR_STEP_LEAKCHECK1;
                    break;
                case LeakTestStep.LOWAIR_STEP_LEAKCHECK1:
                    //double MasterValue
                    double ChkFloAir = MainWindow.Bc.CurDAQ.FloAir;
                    double ChkPriAir = MainWindow.Bc.CurDAQ.PriAir;
                    Delay(10);
                    Dt           = _CheckTimer.ElapsedMilliseconds/1000.0;
                    DurationTime = _LeakTestSpec.LowAir_CheckTime;
                    if (Dt>DurationTime)
                    {
                        _LastFloValue = ChkFloAir;
                        _LastPriValue = ChkPriAir;
                        _CheckTimer.Stop();
                        _LT_Step = LeakTestStep.LOWAIR_STEP_JUDGE;
                    }
                    break;
                case LeakTestStep.LOWAIR_STEP_JUDGE:
                    double DiffPriValue = _FirstPriValue - _LastPriValue;
                    double DiffFloValue = _FirstFloValue - _LastFloValue;
                    
                    _LeakTestGrid[0].MeasurementValue = _FirstPriValue;
                    _LeakTestGrid[1].MeasurementValue = _FirstFloValue;

                    _LeakTestGrid[0].LeakValue = DiffPriValue;
                    _LeakTestGrid[1].LeakValue = DiffFloValue;

                    if (DiffPriValue > _LeakTestSpec.LowAir_DiffBar)   _LeakTestGrid[0].Result = false;
                    else                                               _LeakTestGrid[0].Result = true;
                    if (DiffFloValue > _LeakTestSpec.LowAir_DiffBar)   _LeakTestGrid[1].Result = false;
                    else                                               _LeakTestGrid[1].Result = true;
                    
                    //if (_LeakTestGrid[0].Result) LeakTestGridDisplay.row
                    LeakTestGridDisplay.Items.Refresh();
                    _CheckTimer.Stop();
                    _LT_Step = LeakTestStep.AIRSENSOR_STEP_SOL_SET;
                    break;
                #endregion
                #region 저압 센서단 테스트
                case LeakTestStep.AIRSENSOR_STEP_SOL_SET:
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE));
                    // Air Sensor ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE));
                    // High Air OFF
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);

                    _LT_Step = LeakTestStep.AIRSENSOR_STEP_COSMOSET;                    
                    break;
                case LeakTestStep.AIRSENSOR_STEP_COSMOSET:
                    // CH=3 is 5.0Bar Charge Mode

                    PriCh = _LeakTestSpec.LowAirCosmoCh;
                    FloCh = _LeakTestSpec.LowAirCosmoCh;

                    MainWindow.Bc.PLC_CosmoChSetting(PriCh, FloCh, 2.0);
                    ret = MainWindow.Bc.PLC_CosmoChargeRun(3.0);
                    //  ret = PLC_CosmoChSetting(2, 2, 3.0);
                    if (ret == MainWindow.BCControl.CMDRET.DONE) _LT_Step = LeakTestStep.AIRSENSOR_STEP_TIMECHECK1;
                    else _LT_Step = LeakTestStep.ERROR;
                    _CheckTimer.Restart();
                    break;
                case LeakTestStep.AIRSENSOR_STEP_TIMECHECK1:
                    DurationTime = _LeakTestSpec.AirSensor_PressTime;
                    Dt = _CheckTimer.ElapsedMilliseconds / 1000.0;
                    if (Dt > DurationTime)
                    {
                        _CheckTimer.Stop();
                        _LT_Step = LeakTestStep.AIRSENSOR_STEP_HOLDSET;
                        break;
                    }
                    break;
                case LeakTestStep.AIRSENSOR_STEP_HOLDSET:
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN));
                    // Air Sensor ON - 센서단 입구만 막고...
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN));
                    // High Air OFF
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);

                    // COSMO STOP
                    ret1 = MainWindow.Bc.PLC_CosmoStopSetting(3.0);

                    _FirstPriValue = MainWindow.Bc.CurDAQ.PriAir;
                    _FirstFloValue = MainWindow.Bc.CurDAQ.FloAir;

                    _CheckTimer.Restart();

                    _LT_Step = LeakTestStep.AIRSENSOR_STEP_LEAKCHECK1;
                    break;
                case LeakTestStep.AIRSENSOR_STEP_LEAKCHECK1:
                    //double MasterValue
                    ChkFloAir = MainWindow.Bc.CurDAQ.FloAir;
                    ChkPriAir = MainWindow.Bc.CurDAQ.PriAir;
                    Delay(10);
                    Dt = _CheckTimer.ElapsedMilliseconds / 1000.0;
                    DurationTime = _LeakTestSpec.AirSensor_CheckTime;
                    if (Dt > DurationTime)
                    {
                        _LastFloValue = ChkFloAir;
                        _LastPriValue = ChkPriAir;
                        _CheckTimer.Stop();
                        _LT_Step = LeakTestStep.AIRSENSOR_STEP_JUDGE;
                    }
                    break;
                case LeakTestStep.AIRSENSOR_STEP_JUDGE:
                    DiffPriValue = _FirstPriValue - _LastPriValue;
                    DiffFloValue = _FirstFloValue - _LastFloValue;

                    _LeakTestGrid[2].MeasurementValue = _FirstPriValue;
                    _LeakTestGrid[3].MeasurementValue = _FirstFloValue;

                    _LeakTestGrid[2].LeakValue = DiffPriValue;
                    _LeakTestGrid[3].LeakValue = DiffFloValue;

                    if (DiffPriValue > _LeakTestSpec.AirSensor_DiffBar) _LeakTestGrid[2].Result = false;
                    else _LeakTestGrid[2].Result = true;
                    if (DiffFloValue > _LeakTestSpec.AirSensor_DiffBar) _LeakTestGrid[3].Result = false;
                    else _LeakTestGrid[3].Result = true;

                    LeakTestGridDisplay.Items.Refresh();
                    _CheckTimer.Stop();
                    _LT_Step = LeakTestStep.LOWVAC_STEP_SOLSET;
                    break;
                #endregion
                #region 진공 전체 라인 테스트
                case LeakTestStep.LOWVAC_STEP_SOLSET:
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE));
                    // Air Sensor ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN));
                    // High Air OFF
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);

                    _LT_Step = LeakTestStep.LOWVAC_STEP_COSMOSET;
                    break;
                case LeakTestStep.LOWVAC_STEP_COSMOSET:
                    // CH=3 is 5.0Bar Charge Mode

                    PriCh = _LeakTestSpec.VacuumAirCosmoCh;
                    FloCh = _LeakTestSpec.VacuumAirCosmoCh;

                    MainWindow.Bc.PLC_CosmoChSetting(PriCh, FloCh, 2.0);
                    ret = MainWindow.Bc.PLC_CosmoChargeRun(3.0);
                    //  ret = PLC_CosmoChSetting(2, 2, 3.0);
                    if (ret == MainWindow.BCControl.CMDRET.DONE) _LT_Step = LeakTestStep.LOWVAC_STEP_TIMECHECK1;
                    else _LT_Step = LeakTestStep.ERROR;
                    _CheckTimer.Restart();
                    break;
                case LeakTestStep.LOWVAC_STEP_TIMECHECK1:
                    DurationTime = _LeakTestSpec.Vacuum_PressTime;
                    Dt = _CheckTimer.ElapsedMilliseconds / 1000.0;
                    if (Dt > DurationTime)
                    {
                        _CheckTimer.Stop();
                        _LT_Step = LeakTestStep.LOWVAC_STEP_HOLDSET;
                        break;
                    }
                    break;
                case LeakTestStep.LOWVAC_STEP_HOLDSET:
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN));
                    // Air Sensor ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN));
                    // High Air OFF
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);

                    // COSMO STOP
                    ret1 = MainWindow.Bc.PLC_CosmoStopSetting(3.0);

                    _FirstPriValue = MainWindow.Bc.CurDAQ.PriVacuum;
                    _FirstFloValue = MainWindow.Bc.CurDAQ.FloVacuum;

                    _CheckTimer.Restart();

                    _LT_Step = LeakTestStep.LOWVAC_STEP_LEAKCHECK1;
                    break;
                case LeakTestStep.LOWVAC_STEP_LEAKCHECK1:
                    //double MasterValue
                    double ChkFloVac = MainWindow.Bc.CurDAQ.FloVacuum;
                    double ChkPriVac = MainWindow.Bc.CurDAQ.PriVacuum;
                    Delay(10);
                    Dt = _CheckTimer.ElapsedMilliseconds / 1000.0;
                    DurationTime = _LeakTestSpec.Vacuum_CheckTime;
                    if (Dt > DurationTime)
                    {
                        _LastFloValue = ChkFloVac;
                        _LastPriValue = ChkPriVac;
                        _CheckTimer.Stop();
                        _LT_Step = LeakTestStep.LOWVAC_STEP_JUDGE;
                    }
                    break;
                case LeakTestStep.LOWVAC_STEP_JUDGE:
                    DiffPriValue = _FirstPriValue - _LastPriValue;
                    DiffFloValue = _FirstFloValue - _LastFloValue;

                    _LeakTestGrid[4].MeasurementValue = _FirstPriValue;
                    _LeakTestGrid[5].MeasurementValue = _FirstFloValue;

                    _LeakTestGrid[4].LeakValue = DiffPriValue;
                    _LeakTestGrid[5].LeakValue = DiffFloValue;

                    if (DiffPriValue > _LeakTestSpec.Vacuum_DiffBar) _LeakTestGrid[4].Result = false;
                    else _LeakTestGrid[4].Result = true;
                    if (DiffFloValue > _LeakTestSpec.Vacuum_DiffBar) _LeakTestGrid[5].Result = false;
                    else _LeakTestGrid[5].Result = true;

                    LeakTestGridDisplay.Items.Refresh();
                    _CheckTimer.Stop();
                    _LT_Step = LeakTestStep.VACSENSOR_STEP_SOL_SET;
                    break;
                #endregion
                #region 진공 센서단 테스트
                case LeakTestStep.VACSENSOR_STEP_SOL_SET:
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE));
                    // Air Sensor ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN));
                    // High Air OFF
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);

                    _LT_Step = LeakTestStep.VACSENSOR_STEP_COSMOSET;
                    break;
                case LeakTestStep.VACSENSOR_STEP_COSMOSET:
                    // CH=4 is 500mhg Charge Mode

                    PriCh = _LeakTestSpec.VacuumAirCosmoCh;
                    FloCh = _LeakTestSpec.VacuumAirCosmoCh;

                    MainWindow.Bc.PLC_CosmoChSetting(PriCh, FloCh, 2.0);
                    ret = MainWindow.Bc.PLC_CosmoChargeRun(3.0);
                    if (ret == MainWindow.BCControl.CMDRET.DONE) _LT_Step = LeakTestStep.VACSENSOR_STEP_TIMECHECK1;
                    else _LT_Step = LeakTestStep.ERROR;
                    _CheckTimer.Restart();
                    break;
                case LeakTestStep.VACSENSOR_STEP_TIMECHECK1:
                    DurationTime = _LeakTestSpec.VacuumSensor_PressTime;
                    Dt = _CheckTimer.ElapsedMilliseconds / 1000.0;
                    if (Dt > DurationTime)
                    {
                        _CheckTimer.Stop();
                        _LT_Step = LeakTestStep.VACSENSOR_STEP_HOLDSET;
                        break;
                    }
                    break;
                case LeakTestStep.VACSENSOR_STEP_HOLDSET:
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE));
                    // Air Sensor ON - 센서단 입구만 막고...
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN));
                    // High Air OFF
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);

                    // COSMO STOP
                    ret1 = MainWindow.Bc.PLC_CosmoStopSetting(3.0);

                    _FirstPriValue = MainWindow.Bc.CurDAQ.PriVacuum;
                    _FirstFloValue = MainWindow.Bc.CurDAQ.FloVacuum;
                    Delay(100);
                    _CheckTimer.Restart();

                    _LT_Step = LeakTestStep.VACSENSOR_STEP_LEAKCHECK1;
                    break;
                case LeakTestStep.VACSENSOR_STEP_LEAKCHECK1:
                    //double MasterValue
                    ChkFloVac = MainWindow.Bc.CurDAQ.FloVacuum;
                    ChkPriVac = MainWindow.Bc.CurDAQ.PriVacuum;
                    Delay(10);
                    Dt = _CheckTimer.ElapsedMilliseconds / 1000.0;
                    DurationTime = _LeakTestSpec.VacuumSensor_CheckTime;
                    if (Dt > DurationTime)
                    {
                        _LastFloValue = ChkFloVac;
                        _LastPriValue = ChkPriVac;
                        _CheckTimer.Stop();
                        _LT_Step = LeakTestStep.VACSENSOR_STEP_JUDGE;
                    }
                    break;
                case LeakTestStep.VACSENSOR_STEP_JUDGE:
                    DiffPriValue = _FirstPriValue - _LastPriValue;
                    DiffFloValue = _FirstFloValue - _LastFloValue;

                    _LeakTestGrid[6].MeasurementValue = _FirstPriValue;
                    _LeakTestGrid[7].MeasurementValue = _FirstFloValue;

                    _LeakTestGrid[6].LeakValue = DiffPriValue;
                    _LeakTestGrid[7].LeakValue = DiffFloValue;

                    if (DiffPriValue > _LeakTestSpec.VacuumSensor_DiffBar) _LeakTestGrid[6].Result = false;
                    else                                                   _LeakTestGrid[6].Result = true;
                    if (DiffFloValue > _LeakTestSpec.VacuumSensor_DiffBar) _LeakTestGrid[7].Result = false;
                    else                                                   _LeakTestGrid[7].Result = true;

                    LeakTestGridDisplay.Items.Refresh();
                    _CheckTimer.Stop();
                    _LT_Step = LeakTestStep.HIGHAIR_STEP_SOL_SET;
                    break;
                #endregion
                #region 고압 전체 라인 테스트
                case LeakTestStep.HIGHAIR_STEP_SOL_SET:
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN));
                    // Air Sensor ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE));
                    // High Air ON
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);

                    _CheckTimer.Reset();
                    _CheckTimer.Start();
                    _LT_Step = LeakTestStep.HIGHAIR_STEP_TIMECHECK1;
                    break;
                case LeakTestStep.HIGHAIR_STEP_TIMECHECK1:
                    DurationTime = _LeakTestSpec.HighAir_PressTime;
                    Dt = _CheckTimer.ElapsedMilliseconds / 1000.0;
                    if (Dt > DurationTime)
                    {
                        _CheckTimer.Stop();
                        _LT_Step = LeakTestStep.HIGHAIR_STEP_HOLDSET;
                        break;
                    }
                    break;
                case LeakTestStep.HIGHAIR_STEP_HOLDSET:
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN));
                    // Air Sensor ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE));
                    // High Air OFF
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);

                    // COSMO STOP
                    ret1 = MainWindow.Bc.PLC_CosmoStopSetting(3.0);

                    _FirstPriValue = MainWindow.Bc.CurDAQ.PriAir;
                    _FirstFloValue = MainWindow.Bc.CurDAQ.FloAir;

                    _CheckTimer.Restart();

                    _LT_Step = LeakTestStep.HIGHAIR_STEP_LEAKCHECK1;
                    break;
                case LeakTestStep.HIGHAIR_STEP_LEAKCHECK1:
                    //double MasterValue
                    ChkFloAir = MainWindow.Bc.CurDAQ.FloAir;
                    ChkPriAir = MainWindow.Bc.CurDAQ.PriAir;
                    Delay(10);
                    Dt = _CheckTimer.ElapsedMilliseconds / 1000.0;
                    DurationTime = _LeakTestSpec.HighAir_CheckTime;
                    if (Dt > DurationTime)
                    {
                        _LastFloValue = ChkFloAir;
                        _LastPriValue = ChkPriAir;
                        _CheckTimer.Stop();
                        _LT_Step = LeakTestStep.HIGHAIR_STEP_JUDGE;
                    }
                    break;
                case LeakTestStep.HIGHAIR_STEP_JUDGE:
                    DiffPriValue = _FirstPriValue - _LastPriValue;
                    DiffFloValue = _FirstFloValue - _LastFloValue;

                    _LeakTestGrid[8].MeasurementValue = _FirstPriValue;
                    _LeakTestGrid[9].MeasurementValue = _FirstFloValue;

                    _LeakTestGrid[8].LeakValue = DiffPriValue;
                    _LeakTestGrid[9].LeakValue = DiffFloValue;

                    if (DiffPriValue > _LeakTestSpec.HighAir_DiffBar) _LeakTestGrid[8].Result = false;
                    else _LeakTestGrid[8].Result = true;
                    if (DiffFloValue > _LeakTestSpec.HighAir_DiffBar) _LeakTestGrid[9].Result = false;
                    else _LeakTestGrid[9].Result = true;

                    LeakTestGridDisplay.Items.Refresh();
                    _CheckTimer.Stop();
                    _LT_Step = LeakTestStep.END_TEST;
                    break;
                #endregion


                case LeakTestStep.END_TEST:
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE));
                    // Air Sensor ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE));
                    // High Air OFF
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);
                    Delay(50);
                    MainWindow.Bc.PLC_TestEndSet((int)3);
                    Delay(50);
                    MainWindow.Bc.PLC_ClearWorkArea(3.0);
                    Delay(50);
                    // Low Air ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE));
                    // Vacuum Sensor OFF 
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                    // Protect ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE));
                    // Air Sensor ON
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                    _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE));
                    _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE));
                    // High Air OFF
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
                    _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;

                    MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);

                    _OnceRun = true;
                    _LT_Step = LeakTestStep.IDLE;
                    break;

            }

        }



        private void CloseTest_Click(object sender, RoutedEventArgs e)
        {
           
        }

        private void SelfTestSignalSet_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Bc.PLC_BusySetting();
        }

        private void SelfTestSignalEnd_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Bc.PLC_TestEndSet((int)3);
            MainWindow.Bc.PLC_ClearWorkArea(3.0);
        }

        private void OutletSolOn_Click(object sender, RoutedEventArgs e)
        {
            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN;
            _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN;
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE));
            _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE));
            MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);
        }

        private void OutletSolOff_Click(object sender, RoutedEventArgs e)
        {
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_OPEN));
            _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_OPEN));
            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_LOWAIR_CLOSE;
            _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_LOWAIR_CLOSE;
            MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);
        }

        private void VacuumSolOn_Click(object sender, RoutedEventArgs e)
        {
            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
            _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE));
            _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE));
            MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);
        }

        private void VacuumSolOff_Click(object sender, RoutedEventArgs e)
        {
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN));
            _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN));
            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
            _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
            MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);
        }

        private void ProtectSolOn_Click(object sender, RoutedEventArgs e)
        {
            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN;
            _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN;
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE));
            _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE));
            MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);
        }

        private void ProtectSolOff_Click(object sender, RoutedEventArgs e)
        {
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_PROTECT_OPEN));
            _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_PROTECT_OPEN));
            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_PROTECT_CLOSE;
            _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_PROTECT_CLOSE;
            MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);
        }

        private void AirSensorSolOn_Click(object sender, RoutedEventArgs e)
        {
            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
            _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE));
            _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE));
            MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);
        }

        private void AirSensorSolOff_Click(object sender, RoutedEventArgs e)
        {
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_OPEN));
            _LastPlcData1 = _LastPlcData1 & (~((int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_OPEN));
            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
            _LastPlcData1 = _LastPlcData1 | (int)MainWindow.BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
            MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);
        }

        private void HighSolOn_Click(object sender, RoutedEventArgs e)
        {

            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN;
            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN;
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE));
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE));
            MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);
        }

        private void HighSolOff_Click(object sender, RoutedEventArgs e)
        {
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_OPEN));
            _LastPlcData0 = _LastPlcData0 & (~((int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_OPEN));
            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOPRI_HIGHAIR_CLOSE;
            _LastPlcData0 = _LastPlcData0 | (int)MainWindow.BCControl.SOL.DOFLO_HIGHAIR_CLOSE;
            MainWindow.Bc.PLC_SOLSET(_LastPlcData0, _LastPlcData1, 3.0);
        }

        private void Test1_Click(object sender, RoutedEventArgs e)
        {
            // Test 1 버튼 
            _LeakTestSpec = MainWindow.Bc.LeakTestConfig;
            _LT_Step = LeakTestStep.LOWAIR_STEP_PCBUSYSET;
        }


    }
}
