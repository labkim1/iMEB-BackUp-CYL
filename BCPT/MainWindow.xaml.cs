using NationalInstruments.Analysis.Dsp.Filters;
using NationalInstruments.DAQmx;
using NationalInstruments.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.IO.Ports;
using System.Diagnostics;
using System.Windows.Threading;
using System.IO;
using System.Xml.Serialization;

using NLog;

using MahApps.Metro.Controls;

using TLog;
using TLog.Properties;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using System.Net;
using System.Net.Sockets;

using Microsoft.Win32;


namespace BCPT
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>      
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {
        #region 전역 변수 및 상수 : 전체어플리케이션
        // 전체 자동/수동 제어관련 타이머
        DispatcherTimer MainControlTimer;
        DispatcherTimer MainUITimer;
        public static BCControl    Bc       = new BCControl();
        private Thread      AutomaticThread = null;
        // System Config file(BCPTConfig.xml) read
        public Config.ConfigData SysConfig;
        PerformanceCounter       cpuCounter;
        PerformanceCounter       ramCounter;
        // Chart Collection
        private ChartCollection<Point>  PMChartCollection_PM     = new ChartCollection<Point>(500000);   // Piston Moving 전체 데이터 갯수 5000로 표시할수 있도록, 전체 속도문제확인후 증가.......2500/2500 전진/후진
        private ChartCollection<Point>  PMChartCollection_LTP    = new ChartCollection<Point>(2000);   // Lost Travel Pri
        private ChartCollection<Point>  PMChartCollection_LTF    = new ChartCollection<Point>(2000);   // Lost Travel Flo
        private ChartCollection<Point>  PMChartCollection_LTP2   = new ChartCollection<Point>(2000);   // Lost Travel Pri 2
        private ChartCollection<Point>  PMChartCollection_LTF2   = new ChartCollection<Point>(2000);   // Lost Travel Flo 2

        private ChartCollection<double, double>[] PMChartCollection_LT15P = new[] { // 최대 60초 데이터 분량=600=60*10Hz
                                                                             new ChartCollection<double,double>(2000),
                                                                             new ChartCollection<double,double>(2000)
                                                                            };
        private ChartCollection<double, double>[] PMChartCollection_LT15F = new[] {
                                                                             new ChartCollection<double,double>(2000),
                                                                             new ChartCollection<double,double>(2000)
                                                                            };

        private ChartCollection<double, double>[] PMChartCollection_LT50P = new[] { // 최대 60초 데이터 분량=600=60*10Hz
                                                                             new ChartCollection<double,double>(2000),
                                                                             new ChartCollection<double,double>(2000)
                                                                            };
        private ChartCollection<double, double>[] PMChartCollection_LT50F = new[] {
                                                                             new ChartCollection<double,double>(2000),
                                                                             new ChartCollection<double,double>(2000)
                                                                            };



        private ChartCollection<Point>  PMChartCollection_PSP    = new ChartCollection<Point>(2000);   // Piston Stroke Pri
        private ChartCollection<Point>  PMChartCollection_PSF    = new ChartCollection<Point>(2000);   // Piston Stroke Flo
        private ChartCollection<Point>  PMChartCollection_PSP500 = new ChartCollection<Point>(2000);   // Piston Stroke Pri 500mmHg
        private ChartCollection<Point>  PMChartCollection_PSF500 = new ChartCollection<Point>(2000);   // Piston Stroke Flo 500mmHg

        private ChartCollection<double, double>[] GraphData_Vacuum1 = new[] { // 최대 20초 데이터 분량=20000=20*1000Hz
                                                                             new ChartCollection<double,double>(20000),
                                                                             new ChartCollection<double,double>(20000)
                                                                            };
        private ChartCollection<double, double>[] GraphData_Vacuum2 = new[] {
                                                                             new ChartCollection<double,double>(20000),
                                                                             new ChartCollection<double,double>(20000)
                                                                            };

        // 로그 표시관련
        private readonly IEnumerable<EncodingInfo> _encodings = Encoding.GetEncodings().OrderBy(e => e.DisplayName);
        private readonly ObservableCollection<FileMonitorViewModel> _fileMonitors = new ObservableCollection<FileMonitorViewModel>();
        private System.Threading.Timer _refreshTimer;
        private string _font;
        private DateTime? _lastUpdateDateTime;
        private string _lastUpdated;
        private FileMonitorViewModel _lastUpdatedViewModel;
        private FileMonitorViewModel _selectedItem;


        public enum FuncError : int 
        {
            OK=0,
            ERROR_Undefined_internal_function_call    = -9999,
            ERROR_DAQ_Highspeed_mode_Initialize_fails = -100,
            ERROR_PLC_Handle_open_fails               = -101,
            ERROR_Time_over                           = -1,
            ERROR_Communication_fails                 = -2
        }
        #endregion
        #region 델리게이트 - 화면 업데이트용        
        public delegate void RealTimeDAQConversionDelegate(BCControl.DAQPM curdaq);  // 고속데이터 수집모드에서 실시간으로 현재값을 반영
        public delegate void UIClearDelegate();                                      // 화면 클리어(Graph,Text...Grid....)
        public delegate void LastErrorDelegate(string msg, string step);             // DoWork 진행중 마지막 에러메세지 표시용
        public delegate void TestProgressDelegate(string testname, string step);     // 해당시험 세부진행 표시용
        public delegate void CosmoGraph1Delegate();                                  // 코스모 사용하여 리크테스트 진행시 그래프 표시용(1.5bar)
        public delegate void CosmoGraph2Delegate();                                  // 코스모 사용하여 리크테스트 진행시 그래프 표시용(5.0bar)
        public delegate void PistonMovingFWDDelegate();                              // 피스톤 무빙 테스트 진행시 그래프 표시용 -FWD
        public delegate void PistonMovingBWDDelegate();                              // 피스톤 무빙 테스트 진행시 그래프 표시용 -BWD
        public delegate void LostTravel1Delegate();                                  // 로스트 트레블 테스트 진행시 그래프 표시용 1
        public delegate void LostTravel2Delegate();                                  // 로스트 트레블 테스트 진행시 그래프 표시용 2
        public delegate void PistonStrokeDelegate();                                 // 피스톤 스트로크 테스트 진행시 그래프 표시용 
        public delegate void PistonStrokeVACDelegate();                              // 피스톤 스트로크 테스트 진행시 그래프 표시용 
        public delegate void VacuumDelegate();                                       // 진공시험 테스트 진행시 그래프 표시용 
        public delegate void Vacuum2Delegate();                                      // 진공시험 테스트 진행시 그래프 표시용 
        public delegate void rtLampDelegate(bool cosmo1, bool cosmo2, bool plc);     // 실시간 통신 상태 표시용        
        public delegate void LDS_StartDelegate();
        public delegate void LDS_StopDelegate(bool result, int resultcode);
        public delegate void ANDON_Delegate(string msg);

        public delegate void NGMessage(string msg);                                  // NG 발생시 팝업화면 표시용
        public delegate void NGMessageHide();                                        // NG 팝업화면 숨김
        public delegate void OKMessage(string msg);                                  // OK 발생시 팝업화면 표시용
        public delegate void OKMessageHide();                                        // OK 팝업화면 숨김
        private void StripStatusUpdate(BCControl.PLCSTATE plc)
        {
            //Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            //{
            //    try
            //    {
                    //Update UI here
                    if (plc.AutomManual)   StripMode.Content  = "AUTO";
                    else                   StripMode.Content  = "MANUAL";
                    if (plc.Reset)         StripReset.Content = "RESET";
                    else                   StripReset.Content = "";
                    if (plc.Error)         StripError.Content = "ERROR";
                    else                   StripError.Content = "";
                    if (Bc.curDAQDataSave) StripDAQSave.Value = true;
                    else                   StripDAQSave.Value = false;
            //    }
            //    catch (Exception e1) { }
            //}));
        }
        private void StripLamp(bool cosmo1, bool cosmo2, bool plc)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                //Update UI here
                if (StripCosmo1.Value != cosmo1) StripCosmo1.Value = cosmo1;
                if (StripCosmo2.Value != cosmo2) StripCosmo2.Value = cosmo2;
                if (StripPLCAlive.Value != plc) StripPLCAlive.Value = plc;
            }));
        }
        #endregion
        BesselLowpassFilter _NoiseFilter_BBF;
        private MetroWindow ThisSubForm;    
        private Window      SubWIndow;

        private string      CurLogFilePath = "";                                     // 현재 표시되고 있는 로그 파일의 경로 정보를 저장
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this; // 20170910 추가, WPF XAML Local DataSource Mapping
            Bc.CurMesSpec.SPEC.ImsiBarCode = "12345678901";
            #region 델리게이트
            //Bc.realtimeDAQconversionCallback += new RealTimeDAQConversionDelegate(otherobj_realtimecurdaq);
            Bc.UIClearCallback               += new UIClearDelegate(otherobj_uiclear);
            //Bc.LastErrorCallback             += new LastErrorDelegate(otehrobj_LastError);
            Bc.TestProgressCallBack          += new TestProgressDelegate(otherobj_TestProgress);
            Bc.CosmoGraph1CallBack           += new CosmoGraph1Delegate(otherobj_CosmoGraph1);
            Bc.CosmoGraph2CallBack           += new CosmoGraph1Delegate(otherobj_CosmoGraph2);
            Bc.PistonMovingFWDCallBack       += new PistonMovingFWDDelegate(otherobj_PistonMovingFWD);
            Bc.PistonMovingBWDCallBack       += new PistonMovingBWDDelegate(otherobj_PistonMovingBWD);
            Bc.LostTravel1CallBack           += new LostTravel1Delegate(otherobj_LostTravel1);
            Bc.LostTravel2CallBack           += new LostTravel2Delegate(otherobj_LostTravel2);
            Bc.PistonStrokeCallBack          += new PistonStrokeDelegate(otherobj_PistonStroke);
            Bc.PistonStrokeVACCallBack       += new PistonStrokeVACDelegate(otherobj_PistonStrokeVAC);
            Bc.VacuumCallBack                += new VacuumDelegate(otherobj_Vacuum);
            Bc.Vacuum2CallBack               += new Vacuum2Delegate(otherobj_Vacuum2);
            Bc.rtLampCallBack                += new rtLampDelegate(StripLamp);
            Bc.LDS_StartCallBack             += new LDS_StartDelegate(dele_LDSStart);
            Bc.LDS_StopCallBack              += new LDS_StopDelegate(dele_LDSStop);
            Bc.M_MesLds.AndonCallBack        += new ANDON_Delegate(dele_Andon);
            Bc.NGMsg_CallBack                += new NGMessage(NGShow);
            Bc.NGMsgHide_CallBack            += new NGMessageHide(NGHide);
            Bc.OKMsg_CallBack                += new OKMessage(OKShow);
            Bc.OKMsgHide_CallBack            += new OKMessageHide(OKHide);
            #endregion
            #region Screen Fix
            //this.Width = 1280;
            //this.MaxWidth = 1280;
            //this.Height = 1024;
            //this.MaxHeight = 1024;
            #endregion
            #region System Initialize
            // Log
            try
            {
                Bc.Log = LogManager.GetLogger("Log");
            }
            catch (Exception){}



            NGShow("처음 테스트 화면, 최초 시작시 자동 모드일때만 현 화면 표시됨.\r\n 리셋 버튼을 누르십시오.");




            _refreshTimer = CreateRefreshTimer();
            CurLogFilePath = System.Environment.CurrentDirectory + "\\Logs\\" + System.DateTime.Now.ToShortDateString() + ".log";
            var monitorViewModel = new FileMonitorViewModel(CurLogFilePath, GetFileNameForPath(CurLogFilePath), "UTF-8", false);
            monitorViewModel.Renamed += MonitorViewModelOnRenamed;
            monitorViewModel.Updated += MonitorViewModelOnUpdated;
            FileMonitors.Add(monitorViewModel);
            SelectedItem = monitorViewModel;

            this.TestResultGrid.Loaded += SetMinWidths;
            this.PMTable.Loaded += SetMinWidths_PMTable;

            BCSystemInitialize();
            #endregion
            #region 내부 사용 차트 데이터소스 맵핑
            // 챠트관련
            Graph_PistonMoving.DataSource       = PMChartCollection_PM;
            Graph_LostTravelP.DataSource        = PMChartCollection_LTP;
            Graph_LostTravelF.DataSource        = PMChartCollection_LTF;
            Graph_LostTravelP2.DataSource       = PMChartCollection_LTP2;
            Graph_LostTravelF2.DataSource       = PMChartCollection_LTF2;
            Graph_PistonStrokeF.DataSource      = PMChartCollection_PSF;
            Graph_PistonStrokeP500.DataSource   = PMChartCollection_PSP500;
            Graph_PistonStrokeF500.DataSource   = PMChartCollection_PSF500;
            Graph_15Pri.DataSource              = PMChartCollection_LT15P;
            Graph_15Flo.DataSource              = PMChartCollection_LT15F;
            Graph_50Pri.DataSource              = PMChartCollection_LT50P;
            Graph_50Flo.DataSource              = PMChartCollection_LT50F;        
            Graph_Vacuum.DataSource             = GraphData_Vacuum1;
            Graph_Vacuum2.DataSource            = GraphData_Vacuum2;
            #endregion

        }
        public void SetMinWidths_PMTable(object source, EventArgs e)
        {
            foreach (var column in PMTable.Columns)
            {
                column.MinWidth = column.ActualWidth;
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            }
        }
        public void SetMinWidths(object source, EventArgs e)
        {
            foreach (var column in TestResultGrid.Columns)
            {
                column.MinWidth = column.ActualWidth;
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            }
        }
        #region 자동 로그 표시용 내부처리 함수들...
        public IEnumerable<EncodingInfo> Encodings
        {
            get { return _encodings; }
        }

        public string LastUpdated
        {
            get { return _lastUpdated; }
            set
            {
                if (value == _lastUpdated) return;
                _lastUpdated = value;
                OnPropertyChanged();
            }
        }

        public FileMonitorViewModel SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                if (Equals(value, _selectedItem)) return;
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FileMonitorViewModel> FileMonitors
        {
            get { return _fileMonitors; }
        }

        public string Font
        {
            get { return _font; }
            set
            {
                if (value == _font) return;
                _font = value;
                OnPropertyChanged();
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        private System.Threading.Timer CreateRefreshTimer()
        {
            var timer = new System.Threading.Timer(state => RefreshLastUpdatedText());
            timer.Change((DateTime.Now.Date.AddDays(1) - DateTime.Now), TimeSpan.FromDays(1));
            this.Closing += DisposeTimer;
            return timer;
        }

        private void DisposeTimer(object s, CancelEventArgs e)
        {
            try
            {
                _refreshTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _refreshTimer.Dispose();
            }
            catch (Exception e1)
            {
                if (Bc.LogEnable)
                {
                    Bc.Log.Error("[DisposeTimer] Exception "+e1.Message);
                }
            }
        }

        private void AddFileMonitor(string filepath)
        {
            var existingMonitor = FileMonitors.FirstOrDefault(m => string.Equals(m.FilePath, filepath, StringComparison.CurrentCultureIgnoreCase));

            if (existingMonitor != null)
            {
                // Already being monitored
                SelectedItem = existingMonitor;
                return;
            }

            var monitorViewModel = new FileMonitorViewModel(filepath, GetFileNameForPath(filepath), "String", false);
            monitorViewModel.Renamed += MonitorViewModelOnRenamed;
            monitorViewModel.Updated += MonitorViewModelOnUpdated;

            FileMonitors.Add(monitorViewModel);
            SelectedItem = monitorViewModel;
        }

        private void MonitorViewModelOnUpdated(FileMonitorViewModel obj)
        {
            _lastUpdateDateTime = DateTime.Now;
            _lastUpdatedViewModel = obj;
            RefreshLastUpdatedText();
        }

        private void MonitorViewModelOnRenamed(FileMonitorViewModel renamedViewModel)
        {
            var filepath = renamedViewModel.FilePath;

            renamedViewModel.FileName = GetFileNameForPath(filepath);
        }

        private static string GetFileNameForPath(string filepath)
        {
            return System.IO.Path.GetFileName(filepath);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        private void AddButton_OnClick(object sender, RoutedEventArgs e)
        {
            PromptForFile();
        }

        private void PromptForFile()
        {
            var openFileDialog = new OpenFileDialog { CheckFileExists = false, Multiselect = true };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    foreach (var fileName in openFileDialog.FileNames)
                    {
                        AddFileMonitor(fileName);
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show("Error: " + exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void RefreshLastUpdatedText()
        {
            if (_lastUpdateDateTime != null)
            {
                var dateTime = _lastUpdateDateTime.Value;
                var datestring = dateTime.Date != DateTime.Now.Date ? " on " + dateTime : " at " + dateTime.ToLongTimeString();
                LastUpdated = _lastUpdatedViewModel.FilePath + datestring;
            }
        }
        #endregion

        private DateTime _Last_rtcd = DateTime.Now;
        private DateTime   _CurTime = DateTime.Now;
        private double   _DoingTIme = 0.0;
        public class TestResultTable // 시험결과 그리드테이블 표시용
        {
            public string No { get; set; }
            public string Name { get; set; }
            public string Low { get; set; }
            public string Result { get; set; }
            public string High { get; set; }
            public string Unit { get; set; }
            public string ResultMsg { get; set; }
            public string Note { get; set; }
        }
        private List<TestResultTable> _TRT = new List<TestResultTable> { };
        private void TestSpecGridMapping() // 시험사양을 그리드 테이블로 변환
        {
            _TRT.Clear();
            _TRT.Add(new TestResultTable()
            {
                No        = Bc.AirLeakTestSpec15Pri.TestNo,
                Name      = Bc.AirLeakTestSpec15Pri.TestName,
                Low       = string.Format("{0:F1}", Bc.AirLeakTestSpec15Pri.OKLowLimit),
                Result    = string.Format("{0:F2}", Bc.AirLeakTestSpec15Pri.MeasurementValue),
                High      = string.Format("{0:F1}", Bc.AirLeakTestSpec15Pri.OKHighLimit),
                Unit      = Bc.AirLeakTestSpec15Pri.TestUnit.ToString(),
                ResultMsg = Bc.AirLeakTestSpec15Pri.Result.ToString(),
                Note      = Bc.AirLeakTestSpec15Pri.Note });
            _TRT.Add(new TestResultTable()
            {
                No        = Bc.AirLeakTestSpec15Flo.TestNo,
                Name      = Bc.AirLeakTestSpec15Flo.TestName,
                Low       = string.Format("{0:F1}", Bc.AirLeakTestSpec15Flo.OKLowLimit),
                Result = string.Format("{0:F2}", Bc.AirLeakTestSpec15Flo.MeasurementValue),
                High      = string.Format("{0:F1}", Bc.AirLeakTestSpec15Flo.OKHighLimit),
                Unit      = Bc.AirLeakTestSpec15Flo.TestUnit.ToString(),
                ResultMsg = Bc.AirLeakTestSpec15Flo.Result.ToString(),
                Note      = Bc.AirLeakTestSpec15Flo.Note
            });
            _TRT.Add(new TestResultTable()
            {
                No        = Bc.AirLeakTestSpec50Pri.TestNo,
                Name      = Bc.AirLeakTestSpec50Pri.TestName,
                Low       = string.Format("{0:F1}", Bc.AirLeakTestSpec50Pri.OKLowLimit),
                Result = string.Format("{0:F2}", Bc.AirLeakTestSpec50Pri.MeasurementValue),
                High      = string.Format("{0:F1}", Bc.AirLeakTestSpec50Pri.OKHighLimit),
                Unit      = Bc.AirLeakTestSpec50Pri.TestUnit.ToString(),
                ResultMsg = Bc.AirLeakTestSpec50Pri.Result.ToString(),
                Note      = Bc.AirLeakTestSpec50Pri.Note
            });
            _TRT.Add(new TestResultTable()
            {
                No        = Bc.AirLeakTestSpec50Flo.TestNo,
                Name      = Bc.AirLeakTestSpec50Flo.TestName,
                Low       = string.Format("{0:F1}", Bc.AirLeakTestSpec50Flo.OKLowLimit),
                Result = string.Format("{0:F2}", Bc.AirLeakTestSpec50Flo.MeasurementValue),
                High      = string.Format("{0:F1}", Bc.AirLeakTestSpec50Flo.OKHighLimit),
                Unit      = Bc.AirLeakTestSpec50Flo.TestUnit.ToString(),
                ResultMsg = Bc.AirLeakTestSpec50Flo.Result.ToString(),
                Note      = Bc.AirLeakTestSpec50Flo.Note
            });
            _TRT.Add(new TestResultTable()
            {
                No = Bc.PistonMovingFWDTest.TestNo,
                Name = Bc.PistonMovingFWDTest.TestName,
                Low = string.Format("{0:F1}", Bc.PistonMovingFWDTest.OKLowLimit),
                Result = string.Format("{0:F2}", Bc.PistonMovingFWDTest.MeasurementValue),
                High = string.Format("{0:F1}", Bc.PistonMovingFWDTest.OKHighLimit),
                Unit = Bc.PistonMovingFWDTest.TestUnit.ToString(),
                ResultMsg = Bc.PistonMovingFWDTest.Result.ToString(),
                Note = Bc.PistonMovingFWDTest.Note
            });
            _TRT.Add(new TestResultTable()
            {
                No = Bc.PistonMovingBWDTest.TestNo,
                Name = Bc.PistonMovingBWDTest.TestName,
                Low = string.Format("{0:F1}", Bc.PistonMovingBWDTest.OKLowLimit),
                Result = string.Format("{0:F2}", Bc.PistonMovingBWDTest.MeasurementValue),
                High = string.Format("{0:F1}", Bc.PistonMovingBWDTest.OKHighLimit),
                Unit = Bc.PistonMovingBWDTest.TestUnit.ToString(),
                ResultMsg = Bc.PistonMovingBWDTest.Result.ToString(),
                Note = Bc.PistonMovingBWDTest.Note
            });
            _TRT.Add(new TestResultTable()
            {
                No        = Bc.LostTravelTest.TestNo,
                Name      = Bc.LostTravelTest.TestName,
                Low       = string.Format("{0:F1}", Bc.LostTravelTest.OKLowLimit),
                Result    = string.Format("{0:F2}", Bc.LostTravelTest.MeasurementValue),
                High      = string.Format("{0:F1}", Bc.LostTravelTest.OKHighLimit),
                Unit      = Bc.LostTravelTest.TestUnit.ToString(),
                ResultMsg = Bc.LostTravelTest.Result.ToString(),
                Note      = Bc.LostTravelTest.Note
            });
            _TRT.Add(new TestResultTable()
            {
                No = Bc.LostTravelTest1.TestNo,
                Name = Bc.LostTravelTest1.TestName,
                Low = string.Format("{0:F1}", Bc.LostTravelTest1.OKLowLimit),
                Result = string.Format("{0:F2}", Bc.LostTravelTest1.MeasurementValue),
                High = string.Format("{0:F1}", Bc.LostTravelTest1.OKHighLimit),
                Unit = Bc.LostTravelTest1.TestUnit.ToString(),
                ResultMsg = Bc.LostTravelTest1.Result.ToString(),
                Note = Bc.LostTravelTest1.Note
            });

            _TRT.Add(new TestResultTable()
            {
                No = Bc.PistonStrokeTest.TestNo,
                Name = Bc.PistonStrokeTest.TestName,
                Low = string.Format("{0:F1}", Bc.PistonStrokeTest.OKLowLimit),
                Result = string.Format("{0:F2}", Bc.PistonStrokeTest.MeasurementValue),
                High = string.Format("{0:F1}", Bc.PistonStrokeTest.OKHighLimit),
                Unit = Bc.PistonStrokeTest.TestUnit.ToString(),
                ResultMsg = Bc.PistonStrokeTest.Result.ToString(),
                Note = Bc.PistonStrokeTest.Note
            });
            _TRT.Add(new TestResultTable()
            {
                No = Bc.VacuumTestPri.TestNo,
                Name = Bc.VacuumTestPri.TestName,
                Low = string.Format("{0:F1}", Bc.VacuumTestPri.OKLowLimit),
                Result = string.Format("{0:F2}", Bc.VacuumTestPri.MeasurementValue),
                High = string.Format("{0:F1}", Bc.VacuumTestPri.OKHighLimit),
                Unit = Bc.VacuumTestPri.TestUnit.ToString(),
                ResultMsg = Bc.VacuumTestPri.Result.ToString(),
                Note = Bc.VacuumTestPri.Note
            });
            _TRT.Add(new TestResultTable()
            {
                No = Bc.VacuumTestFlo.TestNo,
                Name = Bc.VacuumTestFlo.TestName,
                Low = string.Format("{0:F1}", Bc.VacuumTestFlo.OKLowLimit),
                Result = string.Format("{0:F2}", Bc.VacuumTestFlo.MeasurementValue),
                High = string.Format("{0:F1}", Bc.VacuumTestFlo.OKHighLimit),
                Unit = Bc.VacuumTestFlo.TestUnit.ToString(),
                ResultMsg = Bc.VacuumTestFlo.Result.ToString(),
                Note = Bc.VacuumTestFlo.Note
            });  
        }

        public class PistonMovingTable
        {
            public string CheckPos { get; set; }
            public string Min { get; set; }
            public string CheckN { get; set; }
            public string Max { get; set; }
            public string Result { get; set; }
            public string HalfResult { get; set; }
        }
        private List<PistonMovingTable> _PMT = new List<PistonMovingTable> { };  // 피스톤 무빙 반력선도 테이블(결과표시용)
        private void PistonMovingResponseTable(double Pos,double Min,double N,double Max)
        {
            _PMT.Add(new PistonMovingTable()
            {
               CheckPos   = string.Format("{0:F1}",Pos),
               Min        = string.Format("{0:F1}",Min),
               CheckN     = string.Format("{0:F1}",N),
               Max        = string.Format("{0:F1}",Max),
               Result     = ((N >= Min) && (N <= Max)) ? "OK" : "NG",
               HalfResult = ((N >= Min) && (N <= (Min+Max)/2.0 )) ? "*" : ""
            });
        }
        // Delegate fuction
        #region Delegate => Function,otherobj
        private void GridResultReset() // 메인화면 그리드 결과창을 재정의
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                TestResultGrid.ItemsSource = _TRT;
                TestResultGrid.Items.Refresh();
            }));
        }
        private void GridPMTResultReset() // 피스톤 무빙 반력선도 테이블 표시용
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                PMTable.ItemsSource = _PMT;
                PMTable.Items.Refresh();
            }));
        }
        public void otherobj_uiclear()
        {

            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
                {
                try
                {

                    //Graph_LostTravelP.DataSource = null;
                    //Graph_LostTravelF.DataSource = null;
                    //Graph_LostTravelP2.DataSource = null;
                    //Graph_LostTravelF2.DataSource = null;

                    //Graph_15Pri.DataSource = null;
                    //Graph_15Flo.DataSource = null;
                    //Graph_50Pri.DataSource = null;
                    //Graph_50Flo.DataSource = null;

                    //Graph_PistonMoving.DataSource = null;

                    //Graph_PistonStrokeP500.DataSource = null;
                    //Graph_PistonStrokeF500.DataSource = null;

                   
                    Graph_Vacuum.DataSource = null;
                    Graph_Vacuum2.DataSource = null;

                    PMChartCollection_PM.Clear();
                    PMChartCollection_LTP.Clear();
                    PMChartCollection_LTF.Clear();
                    PMChartCollection_LTP2.Clear();
                    PMChartCollection_LTF2.Clear();
                    PMChartCollection_PSP.Clear();
                    PMChartCollection_PSF.Clear();
                    PMChartCollection_PSP500.Clear();
                    PMChartCollection_PSF500.Clear();

                    PMChartCollection_LT15P[0].Clear();
                    PMChartCollection_LT15P[1].Clear();
                    PMChartCollection_LT15F[0].Clear();
                    PMChartCollection_LT15F[1].Clear();

                    PMChartCollection_LT50P[0].Clear();
                    PMChartCollection_LT50P[1].Clear();
                    PMChartCollection_LT50F[0].Clear();
                    PMChartCollection_LT50F[1].Clear();

                    GC.Collect();
                }
                catch (Exception e1)
                { 
                    if (Bc.LogEnable)
                    {
                        Bc.Log.Error("[UI CLEAR] Exception " + e1.Message);
                    }
                }
                FINALLAMP_15.Background = Brushes.WhiteSmoke;
                FINALLAMP_50.Background = Brushes.WhiteSmoke;
                FINALLAMP_PM.Background = Brushes.WhiteSmoke;
                FINALLAMP_LT.Background = Brushes.WhiteSmoke;
                FINALLAMP_PS.Background = Brushes.WhiteSmoke;
                FINALLAMP_VA.Background = Brushes.WhiteSmoke;

                // Lost Travel - PRI
                LT_PRI_TestBar.Content        = "-";
                LT_PRI_TestBar.Foreground     = Brushes.Black;
                LT_PRI_CutOffHole.Content     = ".";
                LT_PRI_CutOffHole.Foreground  = Brushes.Black;
                LT_PRI_FLOTestBar.Content     = "-";
                LT_PRI_FLOTestBar.Foreground  = Brushes.Black;   
                LT_PRI_FLOResult.Content      = ".";
                LT_PRI_FLOResult.Foreground   = Brushes.Black;
                LT_PRI_TestTime.Content       = ".";
                LT_PRI_TestTime.Foreground    = Brushes.Black;
                LT_PRI_TotalResult.Content    = ".";
                LT_PRI_TotalResult.Foreground = Brushes.Black;
                // Lost Travel - FLO
                LT_FLO_PRITestBar.Content     = "-";
                LT_FLO_PRITestBar.Foreground  = Brushes.Black;
                LT_FLO_PRIResult.Content      = ".";
                LT_FLO_PRIResult.Foreground   = Brushes.Black;
                LT_FLO_FLOTestBar.Content     = ".";
                LT_FLO_FLOTestBar.Foreground  = Brushes.Black;
                LT_FLO_FLOResult.Content      = ".";
                LT_FLO_FLOResult.Foreground   = Brushes.Black;
                LT_FLO_TestTime.Content       = ".";
                LT_FLO_TestTime.Foreground    = Brushes.Black;
                LT_FLO_TotalResult.Content    = ".";
                LT_FLO_TotalResult.Foreground = Brushes.Black;
                // Leak Test 1.5bar
                LT15_TestBar.Content          = ".";
                LT15_TestBar.Foreground       = Brushes.Black;
                LT15_PRIResult.Content        = ".";
                LT15_PRIResult.Foreground     = Brushes.Black;
                LT15_FLOResult.Content        = ".";
                LT15_FLOResult.Foreground     = Brushes.Black;
                LT15_TestTime.Content         = ".";
                LT15_TestTime.Foreground      = Brushes.Black;
                LT15_TotalResult.Content      = ".";
                LT15_TotalResult.Foreground   = Brushes.Black;
                // Leak Test 5.0bar
                LT50_TestBar.Content          = ".";
                LT50_TestBar.Foreground       = Brushes.Black;
                LT50_PRIResult.Content        = ".";
                LT50_PRIResult.Foreground     = Brushes.Black;
                LT50_FLOResult.Content        = ".";
                LT50_FLOResult.Foreground     = Brushes.Black;
                LT50_TestTime.Content         = ".";
                LT50_TestTime.Foreground      = Brushes.Black;
                LT50_TotalResult.Content      = ".";
                LT50_TotalResult.Foreground   = Brushes.Black;
                // Piston Moving
                PM_FWDForce.Content           = ".";
                PM_FWDForce.Foreground        = Brushes.Black;
                PM_FWDResult.Content          = ".";
                PM_FWDResult.Foreground       = Brushes.Black;
                PM_BWDResult.Content          = ".";
                PM_BWDResult.Foreground       = Brushes.Black;
                PM_FullStroke.Content         = ".";
                PM_FullStroke.Foreground      = Brushes.Black;
                PM_Response.Content           = "-";
                PM_Response.Foreground        = Brushes.Black;
                PM_TestTime.Content           = ".";
                PM_TestTime.Foreground        = Brushes.Black;
                PM_TotalResult.Content        = "-";
                PM_TotalResult.Foreground     = Brushes.Black;
                // Piston Stroke - 5.0bar
                PS_FWDForce.Content           = ".";
                PS_FWDForce.Foreground        = Brushes.Black;
                PS_35Position.Content         = ".";
                PS_35Position.Foreground      = Brushes.Black;
                PS_PRIGAP.Content             = ".";
                PS_PRIGAP.Foreground          = Brushes.Black;
                PS_PRIStroke.Content          = ".";
                PS_PRIStroke.Foreground       = Brushes.Black;
                PS_SECGAP.Content             = ".";
                PS_SECGAP.Foreground          = Brushes.Black;
                PS_SECStroke.Content          = ".";
                PS_SECStroke.Foreground       = Brushes.Black;
                PS_FullStroke.Content         = ".";
                PS_FullStroke.Foreground      = Brushes.Black;
                PS_TestTime.Content           = ".";
                PS_TestTime.Foreground        = Brushes.Black;
                PS_TotalResult.Content        = "-";
                PS_TotalResult.Foreground     = Brushes.Black;
                // Piston Stroke - 500mmHg
                PS5_TestVacuum.Content        = ".";
                PS5_TestVacuum.Foreground     = Brushes.Black;
                PS5_PRIResult.Content         = "-";
                PS5_TestVacuum.Foreground     = Brushes.Black;
                PS5_SECResult.Content         = "-";
                PS5_SECResult.Foreground      = Brushes.Black;
                PS5_TestTime.Content          = ".";
                PS5_TestTime.Foreground       = Brushes.Black;
                PS5_TotalResult.Content       = "-";
                PS5_TotalResult.Foreground    = Brushes.Black;
                  
                PV5_PRIResult.Content = "-";
                PV5_PRIResult.Foreground = Brushes.Black;
                PV5_SECResult.Content = "-";
                PV5_SECResult.Foreground = Brushes.Black;

                // Vacuum Hold's - PRI
                //VH_PRITestVacuum.Content      = ".";
                //VH_PRITestVacuum.Foreground   = Brushes.Black;
                VH_PRIResult.Content          = ".";
                VH_PRIResult.Foreground       = Brushes.Black;
                VH_PRITestTime.Content        = ".";
                VH_PRITestTime.Foreground     = Brushes.Black;
                VH_PRITotalResult.Content     = "-";
                VH_PRITotalResult.Foreground  = Brushes.Black;
                // Vacuum Hold's - SEC
                VH_SECTestVacuum.Content      = ".";
                VH_SECTestVacuum.Foreground   = Brushes.Black;
                VH_SECTestBar.Content         = ".";
                VH_SECTestBar.Foreground      = Brushes.Black;
                VH_SECResult.Content          = ".";
                VH_SECResult.Foreground       = Brushes.Black;
                VH_SECTesTime.Content         = ".";
                VH_SECTesTime.Foreground      = Brushes.Black;
                VH_SECTotalResult.Content     = "-";
                VH_SECTotalResult.Foreground  = Brushes.Black;
                    

                Graph_15Pri.Foreground = Brushes.Black;
                Graph_15Flo.Foreground = Brushes.Black;
                Graph_50Pri.Foreground = Brushes.Black;
                Graph_50Flo.Foreground = Brushes.Black;

                _TRT.Clear();

                _PMT.Clear();  // 반력 선도 테이블 클리어
                
                PMTable.ItemsSource = _PMT;
                PMTable.Items.Refresh();

                PMF_FEOK.Visibility = System.Windows.Visibility.Hidden;
                GC.Collect();
                                                            
            }));
        }


        private StringBuilder UI_msg = new StringBuilder();
        public void otherobj_TestProgress(string testname,string step)
        {
            UI_msg.Clear();
            testname = testname.Replace("_", "");
            UI_msg.AppendFormat("TEST: {0} ({1})", testname, step);
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                if (string.Compare(TESTNAME.Text, UI_msg.ToString()) != 0) TESTNAME.Text = UI_msg.ToString();
            }));
        }
        /// <summary>
        /// 리크 테스트 1.5바 결과 확인 및 그래프 표시
        /// </summary>
        public void otherobj_CosmoGraph1()
        {
             Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
             {

            int LastFloDataCount     = Bc.LastFloDataCount;
            int LastPriDataCount     = Bc.LastPriDataCount;
            BCControl.GRIDLABEL _Pri = Bc.AirLeakTestSpec15Pri;
            BCControl.GRIDLABEL _Flo = Bc.AirLeakTestSpec15Flo;
            //    public string TT;             // Test Timer
            //    public double LV;             // Leak Value
            //    public double NRDP;           // Notthing Revision Differential Pessure
            //    public double CQ;             // Compensating Quantity
            //    public double DP;             // Delta Pressure
            //    public double TP;             // Test Pressure
            //    public string TN;             // Test Name
            //    public int RC;                // Run CH (CH#)
            #region PRI Part Check & View
            PMChartCollection_LT15P[0].Clear();
            PMChartCollection_LT15P[1].Clear();
            PMChartCollection_LT15F[0].Clear();
            PMChartCollection_LT15F[1].Clear();
            double TimeIndex      = 0.0;
            double TestValue      = 0.0;
            double LeakValue      = 0.0;        
            double SPEC_Leak_Low  = Bc.CurMesSpec.SPEC.LeakTest_15_Pri_Min;
            double SPEC_Leak_High = Bc.CurMesSpec.SPEC.LeakTest_15_Pri_Max;
            double Result_PRI     = 20.0;                                             // Leak Check NG범위값을 기본으로 하여 측정되면.....
            double Result_FLO     = 20.0;
            string FilePath = Bc.IMSI_DataFullPath + Bc.CurMesSpec.SPEC.ImsiBarCode + "_LeakTest15bar_PRI.txt";
            StreamWriter outputfile = new StreamWriter(FilePath);
            outputfile.WriteLine("(코스모 에어리크 테스터 장비에서 통신으로 읽음)데이터 샘플링 속도 : 10hz\n");
            outputfile.WriteLine("TestName,TestPressure(bar),Leak Value(mbar)\n");
            if (LastPriDataCount > 0)
            {               
                double[,] newData1 = new double[2, LastPriDataCount];
                for (int i=0; i<LastPriDataCount; i++)
                {
                    string sFmt = "";
                    sFmt = string.Format("{0},{1:F3},{2:F3}",
                                        Bc.priData[i].TN,
                                        Bc.priData[i].TP,
                                        Bc.priData[i].LV * 0.01
                                        );
                    outputfile.WriteLine(sFmt + "\n");

                    TestValue = Bc.priData[i].TP;
                    LeakValue = Bc.priData[i].LV * 0.01; // 스케일 변환
                    PMChartCollection_LT15P[0].Append(TimeIndex, TestValue);
                    PMChartCollection_LT15P[1].Append(TimeIndex, LeakValue);
                    TimeIndex = TimeIndex + 0.1;         // 통신 속도 10Hz 기준
                    if ((string.Compare(Bc.priData[i].TN, "END")) > 0)   Result_PRI = Bc.priData[i].LV * 0.01;                    
                }
                if ((Result_PRI >= SPEC_Leak_Low) && (Result_PRI <= SPEC_Leak_High))
                {
                    _Pri.MeasurementValue = Result_PRI;
                    _Pri.Result           = BCControl.TESTRESULT.OK;
                }
                else
                {
                    _Pri.MeasurementValue = Result_PRI;
                    _Pri.Result           = BCControl.TESTRESULT.NG;
                }
                if (Result_PRI>=9.99)
                {
                    _Pri.MeasurementValue = Result_PRI;
                    _Pri.Result = BCControl.TESTRESULT.NG;
                }
                outputfile.Close();
                Graph_15Pri.DataSource = PMChartCollection_LT15P;               
                Graph_15Pri.Refresh();                                
            }
            #endregion
            #region FLO Part Check & View
            TimeIndex = 0.0;
            if (LastFloDataCount > 0)
            {                
                double[,] newData2 = new double[2, LastFloDataCount];
                for (int i = 0; i < LastFloDataCount; i++)
                {
                    TestValue = Bc.floData[i].TP;
                    LeakValue = Bc.floData[i].LV * 0.01; // 스케일 변환
                    PMChartCollection_LT15F[0].Append(TimeIndex, TestValue);
                    PMChartCollection_LT15F[1].Append(TimeIndex, LeakValue);
                    TimeIndex = TimeIndex + 0.1;
                    if ((string.Compare(Bc.floData[i].TN, "END")) > 0)  Result_FLO = Bc.floData[i].LV * 0.01;                    
                }
                if ((Result_FLO > SPEC_Leak_Low) && (Result_FLO < SPEC_Leak_High))
                {
                    _Flo.MeasurementValue = Result_FLO;
                    _Flo.Result           = BCControl.TESTRESULT.OK;
                }
                else
                {
                    _Flo.MeasurementValue = Result_FLO;
                    _Flo.Result           = BCControl.TESTRESULT.NG;
                }               
                Graph_15Flo.DataSource = PMChartCollection_LT15F;
                Graph_15Flo.Refresh();               
            }
            #endregion
            #region 데이터 수신이 안된 경우
            if (LastFloDataCount <= 0) { _Flo.Result = BCControl.TESTRESULT.SYSTEM_ERROR; _Flo.Note = _Flo.Note + " 통신안됨"; _Flo.Result = BCControl.TESTRESULT.NG; }
            if (LastPriDataCount <= 0) { _Pri.Result = BCControl.TESTRESULT.SYSTEM_ERROR; _Pri.Note = _Pri.Note + " 통신안됨"; _Pri.Result = BCControl.TESTRESULT.NG;}    
            #endregion
            #region 코스모 장치 에러 확인
                 bool CosmoPRI = false;
                 bool CosmoFLO = false;
                 bool chk = Bc.PLC_CosmoErrorCheck(2.0, ref CosmoPRI, ref CosmoFLO);                 
                      chk = Bc.PLC_CosmoErrorCheck(2.0, ref CosmoPRI, ref CosmoFLO);
                 if (chk)
                 {
                     if (CosmoPRI)
                     {
                         Graph_15Pri.Foreground = Brushes.Red;
                         _Pri.Note = _Pri.Note + " 코스모 대리크 발생";
                         _Pri.Result = BCControl.TESTRESULT.NG;
                     }
                     if (CosmoFLO)
                     {
                         Graph_15Flo.Foreground = Brushes.Red;
                         _Flo.Note = _Flo.Note + " 코스모 대리크 발생";
                         _Flo.Result = BCControl.TESTRESULT.NG;
                     }
                 }
                 else
                 {
                     _Pri.Note = _Pri.Note + " 코스모 에러 체크 통신안됨";
                     _Pri.Result = BCControl.TESTRESULT.NG;
                     _Flo.Note = _Flo.Note + " 코스모 에러 체크 통신안됨";
                     _Flo.Result = BCControl.TESTRESULT.NG;
                 }
            #endregion
            if ((_Pri.Result == BCControl.TESTRESULT.OK) && (_Flo.Result == BCControl.TESTRESULT.OK))  FINALLAMP_15.Background = Brushes.Green;                
            else                                                                                       FINALLAMP_15.Background = Brushes.Red;
                
                _Pri.OKLowLimit  = SPEC_Leak_Low;
                _Pri.OKHighLimit = SPEC_Leak_High;
                R_15LT_Min.Content = string.Format("{0:F1}", SPEC_Leak_Low);
                R_15LT_Max.Content = string.Format("{0:F1}", SPEC_Leak_High);

                _Pri.TestName    = "1.5바 리크 시험(PRI)";
                _Pri.TestNo      = "3";
                _Pri.TestValue   = 1.5;
                _Pri.TestUnit    = BCControl.TESTUNIT.mbar;

                _Flo.OKLowLimit  = SPEC_Leak_Low;
                _Flo.OKHighLimit = SPEC_Leak_High;
                _Flo.TestName    = "1.5바 리크 시험(FLO)";
                _Flo.TestNo      = "3";
                _Flo.TestValue   = 1.5;
                _Flo.TestUnit    = BCControl.TESTUNIT.mbar;

                Bc.AirLeakTestSpec15Pri = _Pri;
                Bc.AirLeakTestSpec15Flo = _Flo;
                // 결과 출력
                LT15_TestBar.Content = "1.5";
                LT15_TestTime.Content = string.Format("{0:F2}", Bc._SubTestTimer.ResultTime(TestTime.TESTNAME.LEAKTEST_AIR_15));
                LT15_PRIResult.Content = string.Format("{0:F2}", _Pri.MeasurementValue);
                if (_Pri.Result == BCControl.TESTRESULT.OK) LT15_PRIResult.Foreground = Brushes.Blue;
                else                                        LT15_PRIResult.Foreground = Brushes.Red;
                LT15_FLOResult.Content = string.Format("{0:F2}", _Flo.MeasurementValue);
                if (_Flo.Result == BCControl.TESTRESULT.OK) LT15_FLOResult.Foreground = Brushes.Blue;
                else                                        LT15_FLOResult.Foreground = Brushes.Red;
                if ((_Pri.Result == BCControl.TESTRESULT.OK) && (_Flo.Result == BCControl.TESTRESULT.OK))
                {
                    LT15_TotalResult.Content = "OK";
                    LT15_TotalResult.Foreground = Brushes.Blue;
                }
                else
                {
                    LT15_TotalResult.Content = "NG";
                    LT15_TotalResult.Foreground = Brushes.Red;
                    Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                }
                if (Bc.LogEnable)
                {
                    Bc.Log.Info("[RESULT][1.5bar Leak Test] PRI " + _Pri.Result.ToString());
                    Bc.Log.Info("[RESULT][1.5bar Leak Test] FLO " + _Flo.Result.ToString());
                    Bc.Log.Info("[SPEC] mbar Low : " + string.Format("{0:F2}", _Pri.OKLowLimit));
                    Bc.Log.Info("[SPEC] mbar High : " + string.Format("{0:F2}", _Pri.OKHighLimit));
                    if (_Pri.Result == BCControl.TESTRESULT.OK) Bc.Log.Info("[측정값] PRI(mbar) : " + string.Format("{0:F2}", _Pri.MeasurementValue));
                    else                                        Bc.Log.Info("[측정값] PRI(mbar) : NG ");
                    if (_Flo.Result == BCControl.TESTRESULT.OK) Bc.Log.Info("[측정값] FLO(mbar) : " + string.Format("{0:F2}", _Flo.MeasurementValue));
                    else                                        Bc.Log.Info("[측정값] FLO(mbar) : NG ");
                }

                TestSpecGridMapping();
                GridResultReset();

                MainSubScreen.SelectedIndex = 2;

             }));
        }
        /// <summary>
        /// 리크 테스트 5.0바 결과 확인 및 그래프 표시
        /// </summary>
        public void otherobj_CosmoGraph2()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
            int LastFloDataCount     = Bc.LastFloDataCount;
            int LastPriDataCount     = Bc.LastPriDataCount;  
            BCControl.GRIDLABEL _Pri = Bc.AirLeakTestSpec50Pri;
            BCControl.GRIDLABEL _Flo = Bc.AirLeakTestSpec50Flo;
            //    public string TT;             // Test Timer
            //    public double LV;             // Leak Value
            //    public double NRDP;           // Notthing Revision Differential Pessure
            //    public double CQ;             // Compensating Quantity
            //    public double DP;             // Delta Pressure
            //    public double TP;             // Test Pressure
            //    public string TN;             // Test Name
            //    public int RC;                // Run CH (CH#)

            PMChartCollection_LT50P[0].Clear();
            PMChartCollection_LT50P[1].Clear();
            PMChartCollection_LT50F[0].Clear();
            PMChartCollection_LT50F[1].Clear();

            double TimeIndex      = 0.0;
            double TestValue      = 0.0;
            double LeakValue      = 0.0;
            double SPEC_Leak_Low  = Bc.CurMesSpec.SPEC.LeakTest_50_Pri_Min;
            double SPEC_Leak_High = Bc.CurMesSpec.SPEC.LeakTest_50_Pri_Max;
            double Result_PRI     = 20.0;
            double Result_FLO     = 20.0;

            #region PRI Part Check & View
            if (LastPriDataCount > 0)
            {
                double[,] newData1 = new double[2, LastPriDataCount];    
                for (int i = 0; i < LastPriDataCount; i++)
                {
                    TestValue = Bc.priData[i].TP;
                    LeakValue = Bc.priData[i].LV * 0.01; // 스케일 변환
                    PMChartCollection_LT50P[0].Append(TimeIndex, TestValue);
                    PMChartCollection_LT50P[1].Append(TimeIndex, LeakValue);
                    TimeIndex = TimeIndex + 0.1;
                    if ((string.Compare(Bc.priData[i].TN, "END")) > 0)          Result_PRI =  Bc.priData[i].LV * 0.01;                    
                }
                if ((Result_PRI >= SPEC_Leak_Low) && (Result_PRI <= SPEC_Leak_High))
                {
                    _Pri.MeasurementValue = Result_PRI;
                    _Pri.Result = BCControl.TESTRESULT.OK;
                }
                else
                {
                    _Pri.MeasurementValue = Result_PRI;
                    _Pri.Result = BCControl.TESTRESULT.NG;
                }
                Graph_50Pri.DataSource = PMChartCollection_LT50P;
                Graph_50Pri.Refresh();                        
            }
            #endregion
            #region FLO Part Check & View
            TimeIndex = 0.0;
            if (LastFloDataCount > 0)
            {               
                double[,] newData2 = new double[2, LastFloDataCount];
                for (int i = 0; i < LastFloDataCount; i++)
                {
                    TestValue = Bc.floData[i].TP;
                    LeakValue = Bc.floData[i].LV * 0.01; // 스케일 변환
                    PMChartCollection_LT50F[0].Append(TimeIndex, TestValue);
                    PMChartCollection_LT50F[1].Append(TimeIndex, LeakValue);
                    TimeIndex = TimeIndex + 0.1;
                    if ((string.Compare(Bc.floData[i].TN, "END")) > 0)      Result_FLO =  Bc.floData[i].LV * 0.01;                    
                }
                if ((Result_FLO > SPEC_Leak_Low) && (Result_FLO < SPEC_Leak_High))
                {
                    _Flo.MeasurementValue = Result_FLO;
                    _Flo.Result           = BCControl.TESTRESULT.OK;
                }
                else
                {
                    _Flo.MeasurementValue = Result_FLO;
                    _Flo.Result           = BCControl.TESTRESULT.NG;
                }
                Graph_50Flo.DataSource = PMChartCollection_LT50F;
                Graph_50Flo.Refresh();               
            }
            #endregion
            #region 데이터 수신이 안된 경우
            if (LastFloDataCount <= 0) { _Flo.Result = BCControl.TESTRESULT.SYSTEM_ERROR; _Flo.Note = _Flo.Note + " 통신안됨"; _Flo.Result = BCControl.TESTRESULT.NG; }
            if (LastPriDataCount <= 0) { _Pri.Result = BCControl.TESTRESULT.SYSTEM_ERROR; _Pri.Note = _Flo.Note + " 통신안됨"; _Pri.Result = BCControl.TESTRESULT.NG; }
            #endregion
            #region 코스모 장치 에러 확인
                 bool CosmoPRI = false;
                 bool CosmoFLO = false;
                 bool chk = Bc.PLC_CosmoErrorCheck(2.0, ref CosmoPRI, ref CosmoFLO);
                      chk = Bc.PLC_CosmoErrorCheck(2.0, ref CosmoPRI, ref CosmoFLO);
                 if (chk)
                 {
                     if (CosmoPRI)
                     {
                         Graph_50Pri.Foreground = Brushes.Red;
                         _Pri.Note = _Pri.Note + " 코스모 대리크 발생";
                         _Pri.Result = BCControl.TESTRESULT.NG;
                     }
                     if (CosmoFLO)
                     {
                         Graph_50Flo.Foreground = Brushes.Red;
                         _Flo.Note = _Flo.Note + " 코스모 대리크 발생";
                         _Flo.Result = BCControl.TESTRESULT.NG;
                     }
                 }
                 else
                 {
                     _Pri.Note = _Pri.Note + " 코스모 에러 체크 통신안됨";
                     _Pri.Result = BCControl.TESTRESULT.NG;
                     _Flo.Note = _Flo.Note + " 코스모 에러 체크 통신안됨";
                     _Flo.Result = BCControl.TESTRESULT.NG;
                 }
            #endregion
            _Pri.OKLowLimit  = SPEC_Leak_Low;
            _Pri.OKHighLimit = SPEC_Leak_High;
            R_50LT_Min.Content = string.Format("{0:F1}", SPEC_Leak_Low);
            R_50LT_Max.Content = string.Format("{0:F1}", SPEC_Leak_High);

            _Pri.TestName    = "5.0바 리크 시험(PRI)";
            _Pri.TestNo      = "4";
            _Pri.TestValue   = 5.0;
            _Pri.TestUnit    = BCControl.TESTUNIT.mbar;

            _Flo.OKLowLimit  = SPEC_Leak_Low;
            _Flo.OKHighLimit = SPEC_Leak_High;
            _Flo.TestName    = "5.0바 리크 시험(FLO)";
            _Flo.TestNo      = "4";
            _Flo.TestValue   = 5.0;
            _Flo.TestUnit    = BCControl.TESTUNIT.mbar;

            Bc.AirLeakTestSpec50Pri = _Pri;
            Bc.AirLeakTestSpec50Flo = _Flo;

            LT50_TestBar.Content = "5.0";
            LT50_TestTime.Content = string.Format("{0:F2}", Bc._SubTestTimer.ResultTime(TestTime.TESTNAME.LEAKTEST_AIR_50));
            LT50_PRIResult.Content = string.Format("{0:F2}", _Pri.MeasurementValue);
            if (_Pri.Result == BCControl.TESTRESULT.OK) LT50_PRIResult.Foreground = Brushes.Blue;
            else                                        LT50_PRIResult.Foreground = Brushes.Red;
            LT50_FLOResult.Content = string.Format("{0:F2}", _Flo.MeasurementValue);
            if (_Flo.Result == BCControl.TESTRESULT.OK) LT50_FLOResult.Foreground = Brushes.Blue;
            else                                        LT50_FLOResult.Foreground = Brushes.Red;
            if ((_Pri.Result == BCControl.TESTRESULT.OK) && (_Flo.Result == BCControl.TESTRESULT.OK))
            {
                LT50_TotalResult.Content = "OK";
                LT50_TotalResult.Foreground = Brushes.Blue;
            }
            else
            {
                LT50_TotalResult.Content = "NG";
                LT50_TotalResult.Foreground = Brushes.Red;
                Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
            }

            if (Bc.LogEnable)
            {
                Bc.Log.Info("[RESULT][5.0bar Leak Test] PRI " + _Pri.Result.ToString());
                Bc.Log.Info("[RESULT][5.0bar Leak Test] FLO " + _Flo.Result.ToString());
                Bc.Log.Info("[SPEC] mbar Low : " + string.Format("{0:F2}", _Pri.OKLowLimit));
                Bc.Log.Info("[SPEC] mbar High : " + string.Format("{0:F2}", _Pri.OKHighLimit));
                if (_Pri.Result == BCControl.TESTRESULT.OK) Bc.Log.Info("[측정값] PRI(mbar) : " + string.Format("{0:F2}", _Pri.MeasurementValue));
                else Bc.Log.Info("[측정값] PRI(mbar) : NG ");
                if (_Flo.Result == BCControl.TESTRESULT.OK) Bc.Log.Info("[측정값] FLO(mbar) : " + string.Format("{0:F2}", _Flo.MeasurementValue));
                else Bc.Log.Info("[측정값] FLO(mbar) : NG ");
            }

                if ((_Pri.Result == BCControl.TESTRESULT.OK) && (_Flo.Result == BCControl.TESTRESULT.OK)) FINALLAMP_50.Background = Brushes.Green;
                else FINALLAMP_50.Background = Brushes.Red;

                TestSpecGridMapping();
                GridResultReset();

                MainSubScreen.SelectedIndex = 3;
  
              }));
        }



        private double FWDOffsetDisplacement   = 0.0;         // 피스톤 부빙 시험시 거리 보정용
        private double PistonMovingStartOffset = 0.0;         // 피스톤 무빙 시험시 초기 전진시 발생하는 옵셋을 처리하기 위함.
        public double  Imsi_FullStroke         = 0.0;         // 임시 활용 풀스트로크
        public class MinNomMax
        {
            public double Stroke;
            public double Min;
            public double Nom;
            public double Max;
            public MinNomMax(double stroke,double min,double nom,double max)
            {
                this.Stroke = stroke;
                this.Min = min;
                this.Nom = nom;
                this.Max = max;
            }
            public MinNomMax(double stroke,double min,double max)
            {
                this.Stroke = stroke;
                this.Min = min;
                this.Nom = (max+min)/2.0;
                this.Max = max;
            }
        }
        private MinNomMax DefaultPistonMovingSpec(int CheckCount,ref int ReadCheckCount)
        {

            /*
             * PMT_3
               PMT_5
               PMT_10
               PMT_15
               PMT_20
               PMT_24
               PMT_25
               PMT_26
               PMT_27
               PMT_28
               PMT_29
               PMT_30
               PMT_31
               PMT_32
               PMT_32_5
             */
            bool TextFileReadOK = false;
            int lineCount = 0;
            MinNomMax[] _Default = new MinNomMax[35]; // 최대 35mm기준으로 1mm
            try
            {
                string[] lines = System.IO.File.ReadAllLines(@"PedalForceSpec.txt");
                if (lines.Length > 0)
                {
                    double Checkmm = 0.0;
                    double Min     = 0.0;
                    double Max     = 0.0;
                    string[] ValueString = new string[5];
                    foreach (string line in lines)
                    {
                        try
                        {
                            string lineSpaceKill        = line.Replace(" ", "");
                            ValueString = lineSpaceKill.Split(',');
                            Checkmm     = Convert.ToDouble(ValueString[0]);
                            Min         = Convert.ToDouble(ValueString[1]);
                            Max         = Convert.ToDouble(ValueString[2]);
                        }
                        catch (Exception) { }
                        _Default[lineCount] = new MinNomMax(Checkmm, Min, Max);
                        lineCount++;
                    }
                    TextFileReadOK = true;
                    ReadCheckCount = lineCount;
                }
                else
                {
                    TextFileReadOK = false;
                }
            }
            catch ( Exception e1)
            {
                string emsg = e1.Message;
                TextFileReadOK = false;
            }

            if (!TextFileReadOK)
            {
                _Default[0]  = new MinNomMax(3.0, 35.2, 56.1);
                _Default[1]  = new MinNomMax(5.0, 44.7, 65.3);
                _Default[2]  = new MinNomMax(10.0, 62.7, 84.7);
                _Default[3]  = new MinNomMax(15.0, 82.7, 106.1);
                _Default[4]  = new MinNomMax(20.0, 102.6, 127.5);
                _Default[5]  = new MinNomMax(24.0, 122.6, 147.9);
                _Default[6]  = new MinNomMax(25.0, 125.4, 163.2);
                _Default[7]  = new MinNomMax(26.0, 133.0, 218.3);
                _Default[8]  = new MinNomMax(27.0, 180.4, 283.3);
                _Default[9]  = new MinNomMax(28.0, 230.6, 387.6);
                _Default[10] = new MinNomMax(29.0, 317.1, 510.0);
                _Default[11] = new MinNomMax(30.0, 409.2, 651.8);
                _Default[12] = new MinNomMax(31.0, 518.0, 826.2);
                _Default[13] = new MinNomMax(32.0, 653.8, 1066.9);
                _Default[14] = new MinNomMax(32.5, 725.4, 1341.3);
                ReadCheckCount = 15;
                if (CheckCount < 15) return _Default[CheckCount];
                else return _Default[0];
            }
            else
            {
                if (CheckCount < lineCount) return _Default[CheckCount];
                else return _Default[0];
            }
        }

        public void otherobj_PistonMovingFWD()
        {

            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {

                Bc.TestLastResult = false;
                double[] FWD_Table            = new double[35];  // 반력 선도 확인용
                bool[]   FWD_Table_Result     = new bool[35];                
                double[] Nor_FWD_Table        = new double[35];  // 반력 선도 확인용(노미널 이하 확인용)
                bool[]   Nor_FWD_Table_Result = new bool[35];   
                // 챠트 데이터 클리어
                PMChartCollection_PM.Clear();
                // 변수 선언
                //_NoiseFilter_BBF              = new BesselLowpassFilter(5, 1000.0, 60.0);
                int LastCountIndex            = Bc.LastpmData1IndexDataCount;
                double[] force                = new double[5000];
                double[] displacement         = new double[5000];
                double[] orgForce             = new double[LastCountIndex];
                double[] orgDisplacement      = new double[LastCountIndex];
                //double[] filteredForce        = new double[LastCountIndex];
                //double[] filteredDisplacement = new double[LastCountIndex];
                double OffsetDisplacement     = Bc.pmData1[0].Displacement;

                double FindMaxForce      = 0.0;
                double FindMaxStroke     = 0.0;
                double CheckForce        = 0.0;
                double CheckStroke       = 0.0;

                FWDOffsetDisplacement    = OffsetDisplacement;
                double TargetForce = 1900;// OSEV 1600.0;

                if ((Bc.CurSysConfig.CurSysConfig.PistonMoving_FullStrokeFindForce >= 1000.0)&&(Bc.CurSysConfig.CurSysConfig.PistonMoving_FullStrokeFindForce <= 11000.0))
                {
                    TargetForce = Bc.CurSysConfig.CurSysConfig.PistonMoving_FullStrokeFindForce;
                }
                double TargetPlusOffset  = 25.0;
                double TargetMinusOffset = 25.0;

                double TargetCheckLow    = TargetForce - TargetMinusOffset;
                double TargetCheckHigh   = TargetForce + TargetPlusOffset;
                bool Find1600NStroke     = false;
                double Result_1600Stroke = 0.0;

                double SPEC_FWD_Pos      = 0.3; // 0.3mm일때의 포스 확인
                if ((Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDFindPosition>=0.3)&&(Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDFindPosition<=10.0))
                {
                    SPEC_FWD_Pos = Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDFindPosition;
                }
                bool   Check_FWD_OK      = false;
                Check_FWD_OK = false;
                double Check_Sum         = 0.0;
                int    Check_Index       = 0;
                double FWD_ResultValue   = 0.0;
                // 반력 선도 확인전 변수 클리어
                for (int cl = 0; cl < 35; cl++)
                {
                    FWD_Table_Result[cl]     = false;
                    FWD_Table[cl]            = 0.0;
                    Nor_FWD_Table_Result[cl] = false;
                    Nor_FWD_Table[cl]        = 0.0;
                }
                // 센서 교체전까지 사용할 옵셋 처리
                double FirstDataValue = Bc.pmData1[0].Force;
                if (FirstDataValue > 0.0)
                {
                    PistonMovingStartOffset = 0.0 - FirstDataValue;
                }
                else
                {
                    PistonMovingStartOffset = Math.Abs(FirstDataValue);
                }
                MinNomMax[] ReadPistonMovingSpec = new MinNomMax[50];
                int ReadPistonMovingSpecCount = 0;
                DefaultPistonMovingSpec(0, ref ReadPistonMovingSpecCount);
                for (int i = 0; i < ReadPistonMovingSpecCount; i++ )
                {
                    int tmpcount = 0;
                    ReadPistonMovingSpec[i] = DefaultPistonMovingSpec(i, ref tmpcount);
                }
                for (int i = 0; i < LastCountIndex; i++)
                {
                    orgForce[i]        = Bc.pmData1[i].Force + PistonMovingStartOffset;
                    orgDisplacement[i] = Bc.pmData1[i].Displacement - OffsetDisplacement;
                    CheckForce         = orgForce[i];
                    CheckStroke        = orgDisplacement[i];
                    // 최대 압력 및 스트로크값 계산
                    if (CheckForce > FindMaxForce) FindMaxForce = CheckForce;
                    if (CheckStroke>FindMaxStroke) FindMaxStroke = CheckStroke;
                    // 1600N 일때 스트로크 확인
                    if ((CheckForce>TargetCheckLow)&&(CheckForce<TargetCheckHigh)&&(!Find1600NStroke))
                    {
                        Result_1600Stroke = orgDisplacement[i];
                        Find1600NStroke    = true;
                    }

                    // 초기 전진시 압력 확인
                    if ((CheckStroke > (SPEC_FWD_Pos - 0.05)) && (CheckStroke < (SPEC_FWD_Pos + 0.05)))
                    {
                        Check_Sum = Check_Sum + CheckForce;
                        Check_Index++;
                    }
                    // 반력 선도 확인용
                    for (int i2 =0; i2<ReadPistonMovingSpecCount; i2++)
                    {
                        MinNomMax _tmp = new MinNomMax(0.0,0.0,0.0);
                        double pos     = 0.0;
                        double min     = 0.0;
                        double nor     = 0.0;
                        double max     = 0.0;
                        double Offset  = 0.1;
                        _tmp = ReadPistonMovingSpec[i2];
                        pos  = _tmp.Stroke;
                        min  = _tmp.Min;
                        nor  = _tmp.Nom;
                        max  = _tmp.Max;
                        if ( (CheckStroke>(pos-Offset))&&(CheckStroke<(pos+Offset)) )
                        {
                            FWD_Table[i2]        = CheckForce;
                            FWD_Table_Result[i2] = true;
                        }
                    }
                }                             

                // 초기 전진위치 및 압력 확인
                if (Check_Index>0)
                {
                    Check_FWD_OK = true;
                    FWD_ResultValue = Check_Sum / Check_Index;
                }
                
                #region 데이터 저장
                string barCodeName = "";
                if (Bc.CurMesSpec.SPEC.ImsiBarCode.Length == 11)
                {
                    barCodeName = Bc.CurMesSpec.SPEC.ImsiBarCode;
                }
                string FilePath =  Bc.IMSI_DataFullPath + barCodeName + "_PistoMoving_fwd.txt";
                StreamWriter outputfile = new StreamWriter(FilePath);
                outputfile.WriteLine("Displacement(mm), Force(N)\n");
                for (int i = 0; i < LastCountIndex; i++)
                {
                    outputfile.WriteLine(string.Format("{0:F3},{1:F3}", orgDisplacement[i], orgForce[i]) + "\n");
                }
                outputfile.Close();
                #endregion

                //filteredForce        = _NoiseFilter_BBF.FilterData(orgForce);
                //filteredDisplacement = _NoiseFilter_BBF.FilterData(orgDisplacement);

                int intervaln = LastCountIndex / 5000;
                if (LastCountIndex < 5000) intervaln = 1;  // 데이터가 없을 경우
                int stepn = 0;
                //for (int i = 0; i < LastCountIndex; i++)
                //{
                //    stepn             = stepn + intervaln;
                //    if (stepn >= orgDisplacement.Length) stepn = orgDisplacement.Length -1;
                //    displacement[i]   = orgDisplacement[stepn];
                //    force[i]          = orgForce[stepn];
                //}
                Point[] FD = new Point[LastCountIndex];
                for (int i = 0; i < LastCountIndex; i++)
                {
                    FD[i] = new Point(orgDisplacement[i], orgForce[i]);                                               
                }

                FD.OrderByDescending(p => p.X).ToList();

                BCControl.PISTONMOVING_SPEC gtmp  = Bc.PistonMovingSpec;
                BCControl.GRIDLABEL         gtmp2 = Bc.PistonMovingFWDTest;
                BCControl.GRIDLABEL         gtmp3 = Bc.PistonMovingFWDTest_1;

                gtmp2.Result      = BCControl.TESTRESULT.OK;
                double Spec_Low   = Bc.CurMesSpec.SPEC.PistonMoving_FWD_N_Min; // MES SPEC Download
                double Spec_High  = Bc.CurMesSpec.SPEC.PistonMoving_FWD_N_Max; 
                R_PMF_Min.Content = string.Format("{0:F1}", Spec_Low);
                R_PMF_Max.Content = string.Format("{0:F1}", Spec_High);

                if ((FWD_ResultValue >= Spec_Low) && (FWD_ResultValue <= Spec_High))
                {
                    gtmp2.MeasurementValue  = FWD_ResultValue;
                    gtmp2.Result            = BCControl.TESTRESULT.OK;
                    FINALLAMP_PM.Background = Brushes.Green; 
                }
                else
                {
                    gtmp2.MeasurementValue  = FWD_ResultValue;
                    gtmp2.Result            = BCControl.TESTRESULT.NG;
                    FINALLAMP_PM.Background = Brushes.Red; 
                }
                // 1600일때의 풀 스트로크 확인
                double SPEC_1600_FullStroke_Low  = Bc.CurMesSpec.SPEC.PistonMoving_FullStroke_Min;
                double SPEC_1600_FullStroke_High = Bc.CurMesSpec.SPEC.PistonMoving_FullStroke_Max;
                R_PMFS_Min.Content               = string.Format("{0:F1}", SPEC_1600_FullStroke_Low);
                R_PMFS_Max.Content               = string.Format("{0:F1}", SPEC_1600_FullStroke_High);

                // 반력 선도 결과 확인
                bool FWD_LineResult = true;
                bool Nor_FWD_LineResult = true;
                for (int ck = 0; ck < ReadPistonMovingSpecCount; ck++)
                {
                    MinNomMax _tmp = new MinNomMax(0.0, 0.0, 0.0);
                    double min = 0.0;
                    double max = 0.0;
                    double nor = 0.0;
                    double CheckValue = 0.0;
                    bool[] _result = new bool[35];
                    bool[] _result_nor = new bool[35];

                    _tmp = ReadPistonMovingSpec[ck];

                    min = _tmp.Min;
                    nor = _tmp.Nom;
                    max = _tmp.Max;
                    double chkstroke = _tmp.Stroke;
                    CheckValue = FWD_Table[ck];

                    if (FWD_Table_Result[ck])
                    {
                        if ((CheckValue>min)&&(CheckValue<max))
                        {
                            _result[ck] = true;
                        }
                        else
                        {
                            _result[ck] = false;
                        }
                        FWD_LineResult = FWD_LineResult & _result[ck];
                       if ((CheckValue>=min)&&(CheckValue<=nor) )
                        {
                            _result_nor[ck] = true;
                        }
                        else
                        {
                            if (chkstroke <= 32.0) _result_nor[ck] = false;
                            else
                            {
                                if ((CheckValue >= min) && (CheckValue <= max)) _result_nor[ck] = true;
                                else _result_nor[ck] = false;
                            }
                        }
                        Nor_FWD_LineResult = Nor_FWD_LineResult & _result_nor[ck];
                        // PMTable 출력용
                        PistonMovingResponseTable(chkstroke, min, CheckValue, max);
                    }
                }
                GridPMTResultReset();
                // 결과값 표시용
                if (FWD_LineResult) gtmp2.Result = BCControl.TESTRESULT.OK;
                else
                {
                    gtmp2.Result = BCControl.TESTRESULT.NG;
                    Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                }

                if (Nor_FWD_LineResult)
                {
                     PMF_FEOK.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                     PMF_FEOK.Visibility = System.Windows.Visibility.Hidden;
                }

                if (Bc.LogEnable)
                {
                    Bc.Log.Info("[RESULT][Piston Moving Test FWD] " + gtmp2.Result.ToString());
                    Bc.Log.Info("[SPEC] 0.3mm Min Force(N) : " + string.Format("{0:F2}", gtmp2.OKLowLimit));
                    Bc.Log.Info("[SPEC] 0.3mm Max Force(N) : " + string.Format("{0:F2}", gtmp2.OKHighLimit));
                    Bc.Log.Info("[측정값] FWD(N) : " + string.Format("{0:F2}", gtmp2.MeasurementValue));                    
                    Bc.Log.Info("[SPEC] 1900N Min Pos(mm) : " + string.Format("{0:F2}", SPEC_1600_FullStroke_Low));
                    Bc.Log.Info("[SPEC] 1900N Max Pos(mm)  : " + string.Format("{0:F2}", SPEC_1600_FullStroke_High));
                    Bc.Log.Info("[측정값] MaxStroke(mm) : " + string.Format("{0:F2}", Result_1600Stroke));
                    if (FWD_LineResult) Bc.Log.Info("[측정값] 반력 확인 FWD : OK" );
                    else Bc.Log.Info("[측정값] 반력 확인 FWD : NG");
                    for (int ind=0; ind<ReadPistonMovingSpecCount; ind++)
                    {
                        string rmsg = "";
                        rmsg = string.Format("위치({0:N}) - 측정값={1:F1}  최소({2:F1})-중간({3:F1})-최대({4:F1})",
                               ReadPistonMovingSpec[ind].Stroke, FWD_Table[ind], ReadPistonMovingSpec[ind].Min,
                               (ReadPistonMovingSpec[ind].Min + ReadPistonMovingSpec[ind].Max)/2.0, ReadPistonMovingSpec[ind].Max);
                        Bc.Log.Info("[측정값] "+rmsg );
                    }
                }

                // 화면 출력                
                double SPEC_SET_Force_Low  = 2500.0; //9000
                double SPEC_SET_Force_High = 3500.0; //11000
                if ((Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce >= 3000.0) && (Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce <= 11000.0))
                {
                    // 10% 임계값 설정
                    SPEC_SET_Force_Low  = Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce - (Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce * 0.1);
                    SPEC_SET_Force_High = Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce + (Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce * 0.1);
                }
                // 최대 전진압
                PM_FWDForce.Content = string.Format("{0:F1}", FindMaxForce);
                if ((FindMaxForce >= SPEC_SET_Force_Low) && (FindMaxForce <= SPEC_SET_Force_High))
                {
                    PM_FWDForce.Foreground = Brushes.Blue;
                }
                else
                {
                    PM_FWDForce.Foreground = Brushes.Red;
                    gtmp2.Result = BCControl.TESTRESULT.NG;
                    Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                }
                // 풀 스트로크
                PM_FullStroke.Content = string.Format("{0:F2}", Result_1600Stroke);
                if ((Result_1600Stroke >= SPEC_1600_FullStroke_Low) && (Result_1600Stroke <= SPEC_1600_FullStroke_High))
                {
                    PM_FullStroke.Foreground = Brushes.Blue;
                }
                else
                {
                    PM_FullStroke.Foreground = Brushes.Red;
                    gtmp2.Result = BCControl.TESTRESULT.NG;
                    Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                }
                // 전진압
                PM_FWDResult.Content = string.Format("{0:F1}", FWD_ResultValue);
                if ((FWD_ResultValue >= Spec_Low) && (FWD_ResultValue <= Spec_High))
                {
                    PM_FWDResult.Foreground = Brushes.Blue;
                }
                else
                {
                    PM_FWDResult.Foreground = Brushes.Red;
                    gtmp2.Result = BCControl.TESTRESULT.NG;
                    Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                }
                // 반력선도
                if (FWD_LineResult) 
                {
                    PM_Response.Content = "OK";
                    PM_Response.Foreground = Brushes.Blue;
                }
                else
                {
                    PM_Response.Content = "NG";
                    PM_Response.Foreground = Brushes.Red;
                    gtmp2.Result = BCControl.TESTRESULT.NG;
                    Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                }


                gtmp2.OKLowLimit  = Spec_Low;
                gtmp2.OKHighLimit = Spec_High;
                gtmp2.TestName    = "피스톤 무빙 테스트(전진)";
                gtmp2.TestNo      = "4";
                gtmp2.TestValue   = 3000;
                if ((Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce >= 3000.0) && (Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce <= 11000.0))
                {
                    // 10% 임계값 설정
                    gtmp2.TestValue = Bc.CurSysConfig.CurSysConfig.PistonMoving_FullStrokeFindForce;
                    
                }                
                gtmp2.TestUnit    = BCControl.TESTUNIT.N;

                gtmp3.MeasurementValue = Result_1600Stroke;

                Bc.PistonMovingFWDTest   = gtmp2;
                Bc.PistonMovingFWDTest_1 = gtmp3;

                PM_TestTime.Content    = ".";
                PM_TotalResult.Content = "?";

                Imsi_FullStroke = Result_1600Stroke;
                Bc.FullStroke   = Imsi_FullStroke;
                TestSpecGridMapping();                
                GridResultReset();
                PMChartCollection_PM.Append(FD);
                Graph_PistonMoving.Refresh();

                MainSubScreen.SelectedIndex = 4;

            }));                
            // 스펙 검사 루틴 추가.
        }
        public void otherobj_PistonMovingBWD()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
            // 챠트 데이터 클리어
            //PMChartCollection.Clear();
            // 변수 선언
            //_NoiseFilter_BBF              = new BesselLowpassFilter(5, 1000.0, 60.0);

                FINALLAMP_PS.Background = Brushes.Red;

                Bc.TestLastResult = false;
            int LastCountIndex            = Bc.LastpmData1IndexDataCount;
            double[] force                = new double[5000];
            double[] displacement         = new double[5000];
            double[] orgForce             = new double[LastCountIndex];
            double[] orgDisplacement      = new double[LastCountIndex];

            double SPEC_BWD_Pos    = 0.5; // 0.5mm일때의 포스 확인  
            if ( (Bc.CurSysConfig.CurSysConfig.PistonMoving_BWDFindPosition>=0.5)&&(Bc.CurSysConfig.CurSysConfig.PistonMoving_BWDFindPosition<=10.0) )
            {
                SPEC_BWD_Pos = Bc.CurSysConfig.CurSysConfig.PistonMoving_BWDFindPosition;
            }
            bool   Check_BWD_OK    = false;
            double Check_Sum       = 0.0;
            int    Check_Index     = 0;
            double BWD_ResultValue = 0.0;
            // NoisFiltering
            for (int i = 0; i < LastCountIndex; i++)
            {
                double CheckForce  = 0.0;
                double CheckStroke = 0.0;
                orgForce[i]        = Bc.pmData1[i].Force + PistonMovingStartOffset;
                orgDisplacement[i] = Bc.pmData1[i].Displacement - FWDOffsetDisplacement;

                CheckForce  = orgForce[i];
                CheckStroke = orgDisplacement[i];
                // 후진시 압력 확인
                if ((CheckStroke > (SPEC_BWD_Pos - 0.05)) && (CheckStroke < (SPEC_BWD_Pos + 0.05)))
                {
                    Check_Sum = Check_Sum + CheckForce;
                    Check_Index++;
                }
            }
            // 후진위치 및 압력 확인
            if (Check_Index > 0)
            {
                Check_BWD_OK = true;
                BWD_ResultValue = Check_Sum / Check_Index;
            }
            #region 데이터 저장
            // all data save
            string barCodeName = "";            
            if (Bc.CurMesSpec.SPEC.ImsiBarCode.Length == 11)
            {
                barCodeName = Bc.CurMesSpec.SPEC.ImsiBarCode;
            }
            string FilePath = Bc.IMSI_DataFullPath + barCodeName +"_PistoMoving_bwd.txt";
            StreamWriter outputfile = new StreamWriter(FilePath);
            outputfile.WriteLine("Displacement(mm), Force(N)\n");
            for (int i = 0; i < LastCountIndex; i++)
            {
                outputfile.WriteLine(string.Format("{0:F3},{1:F3}", orgDisplacement[i], orgForce[i]) + "\n");
            }
            outputfile.Close();
            #endregion

            //filteredForce        = _NoiseFilter_BBF.FilterData(orgForce);
            //filteredDisplacement = _NoiseFilter_BBF.FilterData(orgDisplacement);

            int intervaln = LastCountIndex / 5000;
            if (LastCountIndex < 5000) intervaln = 1;  // 데이터가 없을 경우
            int stepn = 0;
            //for (int i = 0; i < LastCountIndex; i++)
            //{
            //    stepn           = stepn + intervaln;
            //    if (stepn >= orgDisplacement.Length) stepn = orgDisplacement.Length - 1;
            //    displacement[i]   = orgDisplacement[stepn];
            //    force[i]          = orgForce[stepn];
            //}
            Point[] FD = new Point[LastCountIndex];


            for (int i = 0; i < LastCountIndex; i++)
            {
                FD[i] = new Point(orgDisplacement[i], orgForce[i]);
            }

            FD.OrderByDescending(p => p.X).ToList();

            BCControl.PISTONMOVING_SPEC gtmp = Bc.PistonMovingSpec;
            BCControl.GRIDLABEL        gtmp2 = Bc.PistonMovingBWDTest;


            double SPEC_Force_Low  = Bc.CurMesSpec.SPEC.PistonMoving_BWD_N_Min;// 10.0; // OSEV
            double SPEC_Force_High = Bc.CurMesSpec.SPEC.PistonMoving_BWD_N_Max;// 10.0; // OSEV
            R_PMB_Min.Content      = string.Format("{0:F1}", SPEC_Force_Low);

            gtmp2.OKLowLimit  = SPEC_Force_Low;
            gtmp2.OKHighLimit = SPEC_Force_High;

            gtmp2.Result = BCControl.TESTRESULT.NG;
            gtmp2.MeasurementValue = BWD_ResultValue;

            PM_BWDResult.Content = string.Format("{0:F1}", BWD_ResultValue);
            PM_BWDResult.Foreground = Brushes.Red;
            if ((BWD_ResultValue >= SPEC_Force_Low) && (BWD_ResultValue <= SPEC_Force_High))
            {
                gtmp2.Result            = BCControl.TESTRESULT.OK;
                PM_BWDResult.Foreground = Brushes.Blue;
            }
            if (Check_Index <= 0)
            {
                gtmp2.Note        = gtmp2.Note + " 검출안됨";
                gtmp2.Result      = BCControl.TESTRESULT.NG;
                Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
            }

            PM_TestTime.Content       = string.Format("{0:F2}",Bc._SubTestTimer.ResultTime(TestTime.TESTNAME.PISTON_MOVING));
            PM_TotalResult.Content    = "NG";
            PM_TotalResult.Foreground = Brushes.Red;
            FINALLAMP_PM.Background   = Brushes.Red; 
            if ((Bc.PistonMovingFWDTest.Result== BCControl.TESTRESULT.OK)&&(gtmp2.Result == BCControl.TESTRESULT.OK))
            {
                PM_TotalResult.Content    = "OK";
                PM_TotalResult.Foreground = Brushes.Blue;
                FINALLAMP_PM.Background   = Brushes.Green;
                Bc.TestLastResult = true;
            }
            else
            {
                Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
            }
         
            Bc.PistonMovingBWDTest = gtmp2;

            if (Bc.LogEnable)
            {
                Bc.Log.Info("[RESULT][Piston Moving Test BWD] " + gtmp2.Result.ToString());
                Bc.Log.Info("[SPEC] Force  " + string.Format("{0:F2}", gtmp2.OKLowLimit) + " ~ " + string.Format("{0:F2}", gtmp2.OKHighLimit));
                Bc.Log.Info("[측정값] BWD(N) : " + string.Format("{0:F2}", gtmp2.MeasurementValue));
                if (gtmp2.Result == BCControl.TESTRESULT.OK) Bc.Log.Info("[측정값] BWD(N) : OK");
                else                                         Bc.Log.Info("[측정값] BWD(N) : NG, Index Count =  " + string.Format("{0:D}",Check_Index));
            }

            TestSpecGridMapping();
            GridResultReset();
            PMChartCollection_PM.Append(FD);             
            Graph_PistonMoving.Refresh();

            MainSubScreen.SelectedIndex = 4;            
            }));
            // 스펙 검사 루틴 추가.
        }
        public void otherobj_LostTravel1()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                // 변수 선언
                _NoiseFilter_BBF = new BesselLowpassFilter(5, 1000.0, 60.0);
                int LastCountIndex = Bc.LastpmData1IndexDataCount;
                double[] priair               = new double[2000];
                double[] floair               = new double[2000];
                double[] displacement         = new double[2000];
                double[] orgPriair            = new double[LastCountIndex];
                double[] orgFloair            = new double[LastCountIndex];
                double[] orgDisplacement      = new double[LastCountIndex];

                if (LastCountIndex < 5000)
                {
                    return;
                }
                // NoisFiltering
                double OffsetDisplacement = Bc.pmData1[0].Displacement;
                for (int i = 0; i < LastCountIndex; i++)
                {
                    orgPriair[i]       = Bc.pmData1[i].PriAir;
                    orgFloair[i]       = Bc.pmData1[i].FloAir;
                    orgDisplacement[i] = Bc.pmData1[i].Displacement - OffsetDisplacement;
                }

                int intervaln = LastCountIndex / 2000;
                if (LastCountIndex < 2000) intervaln = 1;  // 데이터가 없을 경우
                int stepn = 0;
                Point[] DP = new Point[2000];  // Displacement,PriAir
                Point[] DF = new Point[2000];  // Displacement,FloAir
                for (int i = 0; i < 2000; i++)
                {
                    stepn = stepn + intervaln;
                    DP[i] = new Point(orgDisplacement[stepn], orgPriair[stepn]);
                    DF[i] = new Point(orgDisplacement[stepn], orgFloair[stepn]);
                }
             

                // 다시 정리 요망!!!!!
                BCControl.LOSTTRAVEL_SPEC gtmp = Bc.LostTravelSpec;
                BCControl.GRIDLABEL      gtmp2 = Bc.LostTravelTest;
                BCControl.GRIDLABEL      gtmp3 = Bc.LostTravelTest_1;
                // Find Max Value
                
                int MaxPriIndex;
                double BaseLow;

                BaseLow = 1.0*0.95;  // TestBar 1Bar
                MaxPriIndex = 0;
                for (int i = 0; i < 2000; i++)
                {
                    if ((DP[i].Y > BaseLow) && (MaxPriIndex == 0)) { MaxPriIndex = i; }
                }
                gtmp2.MeasurementValue  = 0.0;
                gtmp2.Result            = BCControl.TESTRESULT.NG;
                FINALLAMP_LT.Background = Brushes.Red;

                double SPEC_CutOffHole_Low  = Bc.CurMesSpec.SPEC.LostTravel_PRI_CutOffHole_Min; //1.0;                
                double SPEC_CutOffHole_High = Bc.CurMesSpec.SPEC.LostTravel_PRI_CutOffHole_Max; //2.0;
                R_CutOffHole_Min.Content = string.Format("{0:F1}", SPEC_CutOffHole_Low);
                R_CutOffHole_Max.Content = string.Format("{0:F1}", SPEC_CutOffHole_High);

                LT_PRI_TestBar.Content    = "1.0";
                LT_PRI_FLOTestBar.Content = "3.0";

                double SPEC_FLOBar_Low  = Bc.CurMesSpec.SPEC.LostTravel_PRI_Sec_Min; // 2.60;   // 컷 오프홀 위치에서 FLO측 압력 측정 사양
                double SPEC_FLOBar_Hgih = Bc.CurMesSpec.SPEC.LostTravel_PRI_Sec_Max; // 3.3;
                double Check_FLO_Bar    = 0.0;

                bool Check_FLO_Result = false;

                if (MaxPriIndex > 0)
                {
                    Check_FLO_Bar = DF[MaxPriIndex].Y;  // CutOffHole 위치일때의 FLO측 압력 확인
                    if ((Check_FLO_Bar >= SPEC_FLOBar_Low) && (Check_FLO_Bar <= SPEC_FLOBar_Hgih))
                    {
                        Check_FLO_Result = true;
                        gtmp3.MeasurementValue = Check_FLO_Bar;
                    }
                    else
                    {
                        Check_FLO_Result = false;
                        gtmp3.MeasurementValue = 0.0;
                    }

                    Bc.CutOffHolePositionPri = DP[MaxPriIndex].X;   
                    gtmp2.MeasurementValue   = Bc.CutOffHolePositionPri;
                    if ((Bc.CutOffHolePositionPri >= SPEC_CutOffHole_Low) && (Bc.CutOffHolePositionPri <= SPEC_CutOffHole_High))
                    {
                        FINALLAMP_LT.Background = Brushes.Green;
                        gtmp2.Result = BCControl.TESTRESULT.OK;
                    }
                    else
                    {
                        FINALLAMP_LT.Background = Brushes.Red;
                        gtmp2.Result = BCControl.TESTRESULT.NG;
                        Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                    }
                    //FINALLAMP_LT.Background = Brushes.Green;  // 후반부 로스트 트레블 2에서 최종 결정
                }



                #region 시험 로우 
                string barCodeName = "";
                if (Bc.CurMesSpec.SPEC.ImsiBarCode.Length == 11)
                {
                    barCodeName = Bc.CurMesSpec.SPEC.ImsiBarCode;
                }
                string FilePath = Bc.IMSI_DataFullPath + barCodeName+ "_LostTravel_PRI.txt";
                StreamWriter outputfile = new StreamWriter(FilePath);
                outputfile.WriteLine("CutOffHole(mm) :" + string.Format("{0:F2}", Bc.CutOffHolePositionPri) + "\n");
                outputfile.WriteLine("Displacement(mm), Pri Air(bar), Flo Air(bar)\n");
                for (int i = 0; i < 2000; i++)
                {
                    outputfile.WriteLine(string.Format("{0:F3},{1:F3},{2:F3}", DP[i].X, DP[i].Y, DF[i].Y) + "\n");
                }
                outputfile.Close();
                #endregion

                // 그래프쪽 텍스트 디스플레이
                gtmp2.Result = BCControl.TESTRESULT.OK;

                LT_PRI_CutOffHole.Content = string.Format("{0:F2}", Bc.CutOffHolePositionPri);
                if ((Bc.CutOffHolePositionPri >= SPEC_CutOffHole_Low) && (Bc.CutOffHolePositionPri <= SPEC_CutOffHole_High))
                {
                    LT_PRI_CutOffHole.Foreground = Brushes.Blue;
                }
                else
                {
                    gtmp2.Result                 = BCControl.TESTRESULT.NG;
                    LT_PRI_CutOffHole.Foreground = Brushes.Red;
                }
                LT_PRI_FLOResult.Content = string.Format("{0:F2}", Check_FLO_Bar);
                if ((Check_FLO_Bar > SPEC_FLOBar_Low) && (Check_FLO_Bar < SPEC_FLOBar_Hgih))
                {
                    LT_PRI_FLOResult.Foreground = Brushes.Blue;
                }
                else
                {
                    gtmp2.Result = BCControl.TESTRESULT.NG;
                    LT_PRI_FLOResult.Foreground = Brushes.Red;
                }
                // 결과 그리드에 적용할 값 변환
                gtmp2.Note             = "컷 오프홀 위치 검색";
                gtmp2.OKLowLimit       = SPEC_CutOffHole_Low;
                gtmp2.OKHighLimit      = SPEC_CutOffHole_High;
                gtmp2.MeasurementValue = Bc.CutOffHolePositionPri;
                gtmp2.TestName          = "로스트 트레블 PRI 시험";
                gtmp2.TestNo            = "1";
                gtmp2.TestUnit          = BCControl.TESTUNIT.mm;
                gtmp2.TestValue         = 1.60;

                LT_PRI_TestTime.Content = string.Format("{0:F2}",Bc._SubTestTimer.ResultTime(TestTime.TESTNAME.LOSTTRAVEL_PRI));
                if (gtmp2.Result == BCControl.TESTRESULT.OK)
                {
                    LT_PRI_TotalResult.Content = "OK";
                    LT_PRI_TotalResult.Foreground = Brushes.Blue;
                }
                else
                {
                    LT_PRI_TotalResult.Content = "NG";
                    LT_PRI_TotalResult.Foreground = Brushes.Red;
                }
                
                //gtmp2.Result = BCControl.TESTRESULT.OK;  상위 조건에 설정됨.
                Bc.LostTravelTest   = gtmp2;
                Bc.LostTravelTest_1 = gtmp3;

                if (Bc.LogEnable)
                {
                    Bc.Log.Info("[RESULT][Lost Tarvel PRI]  " + gtmp2.Result.ToString());
                    Bc.Log.Info("[SPEC] CutOffHole Low : " + string.Format("{0:F2}",gtmp2.OKLowLimit));
                    Bc.Log.Info("[SPEC] CutOffHole High : " + string.Format("{0:F2}", gtmp2.OKHighLimit));
                    Bc.Log.Info("[측정값] Cutoffhole Position(mm) : " + string.Format("{0:F2}", gtmp2.MeasurementValue));
                }
                // UI Refresh
                TestSpecGridMapping();
                GridResultReset();
                PMChartCollection_LTP.Append(DP);
                PMChartCollection_LTF.Append(DF);

                //LT15_PRI_Cursor.InteractionMode = CursorInteractionModes.ReadOnly;
                LT_PRI_1.AxisValue = gtmp2.MeasurementValue;
                Graph_LostTravelP.DataSource = PMChartCollection_LTP;
                Graph_LostTravelP.Refresh();

                Graph_LostTravelF.DataSource = PMChartCollection_LTF;
                Graph_LostTravelF.Refresh();

                MainSubScreen.SelectedIndex = 0;
            }));

            // 스펙 검사 루틴 추가.
        }
        public void otherobj_LostTravel2()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                // 로스트 트레블 챠트 데이터 클리어
                //PMChartCollection_LTP.Clear();
                //PMChartCollection_LTF.Clear();
                // 변수 선언
                _NoiseFilter_BBF = new BesselLowpassFilter(5, 1000.0, 60.0);
                int LastCountIndex            = Bc.LastpmData1IndexDataCount;
                double[] priair               = new double[2000];
                double[] floair               = new double[2000];
                double[] displacement         = new double[2000];
                double[] orgPriair            = new double[LastCountIndex];
                double[] orgFloair            = new double[LastCountIndex];
                double[] orgDisplacement      = new double[LastCountIndex];   

                if (LastCountIndex < 2000)
                {
                    return;
                }
                // NoisFiltering
                for (int i = 0; i < LastCountIndex; i++)
                {
                    orgPriair[i] = Bc.pmData1[i].PriAir;
                    orgFloair[i] = Bc.pmData1[i].FloAir;
                }

                int intervaln = LastCountIndex / 2000;
                if (LastCountIndex < 2000) intervaln = 1;  // 데이터가 없을 경우

                int stepn = 0;
                Point[] DP = new Point[2000];  // Time,PriAir
                Point[] DF = new Point[2000];  // Time,FloAir

                double _ConversionTime = 0.0;
                for (int i = 0; i < 2000; i++)
                {
                    stepn = stepn + intervaln;
                    _ConversionTime = stepn * 0.001;
                    if (stepn >= LastCountIndex) stepn = 2000 - 1;
                    DP[i] = new Point(_ConversionTime, orgPriair[stepn]);
                    DF[i] = new Point(_ConversionTime, orgFloair[stepn]);
                }
                


                // First Max point Search (fitted data)

                BCControl.LOSTTRAVEL_SPEC gtmp = Bc.LostTravelSpec;
                BCControl.GRIDLABEL      gtmp2 = Bc.LostTravelTest1;
                BCControl.GRIDLABEL      gtmp3 = Bc.LostTravelTest1_1;

                // Find Max Value
                bool TimeSearch;
                int SearchIndex;
                double SerarchTime;
                double ResultValue_PRI;
                double ResultValue_FLO;

                TimeSearch      = false;
                SearchIndex     = 0;
                SerarchTime     = 2.00;
                ResultValue_PRI = 0.0;
                ResultValue_FLO = 0.0;

                for (int i = 0; i < 2000; i++)
                {
                    if ((DF[i].X > SerarchTime) && (!TimeSearch))
                    { 
                        SearchIndex = i;
                        TimeSearch = true;
                        ResultValue_PRI = DP[SearchIndex].Y;
                        ResultValue_FLO = DF[SearchIndex].Y;
                    }
                }

                double Spec_PRI_Low, Spec_PRI_High;
                double Spec_FLO_Low, Spec_FLO_High;

                Spec_PRI_Low          = Bc.CurMesSpec.SPEC.LostTravel_SEC_Pri_Min;
                Spec_PRI_High         = Bc.CurMesSpec.SPEC.LostTravel_SEC_Pri_Max;
                Spec_FLO_Low          = Bc.CurMesSpec.SPEC.LostTravel_SEC_Sec_Min;  //0.8;
                Spec_FLO_High         = Bc.CurMesSpec.SPEC.LostTravel_SEC_Sec_Max;  //1.1;
                R_FLO2Sec_Min.Content = string.Format("{0:F1}", Spec_FLO_Low);
                R_FLO2Sec_Max.Content = string.Format("{0:F1}", Spec_FLO_High);
                bool TestCheck = false;

                gtmp2.Result            = BCControl.TESTRESULT.NG;

                LT_FLO_TotalResult.Content    = "NG";
                LT_FLO_TotalResult.Foreground = Brushes.Red;
                LT_FLO_TestTime.Content       = string.Format("{0:F2}",Bc._SubTestTimer.ResultTime(TestTime.TESTNAME.LOSTTRAVEL_SEC));
                if ((TimeSearch) & (ResultValue_FLO >= Spec_FLO_Low) & (ResultValue_FLO <= Spec_FLO_High))
                {
                    if ((ResultValue_PRI >= Spec_PRI_Low) && (ResultValue_PRI<=Spec_PRI_High))
                    {
                        TestCheck                     = true;
                        gtmp2.Result                  = BCControl.TESTRESULT.OK;
                        LT_FLO_TotalResult.Content    = "OK";
                        LT_FLO_TotalResult.Foreground = Brushes.Blue;
                        Bc.LostTravelTest1 = gtmp2; // 마지막 결과를 업데이트 하기 위함.
                    }
                }
                // 로스트 트레블 1/2단계 최종 확인

                if ((Bc.LostTravelTest.Result == BCControl.TESTRESULT.OK)&&(Bc.LostTravelTest1.Result== BCControl.TESTRESULT.OK))
                {
                    FINALLAMP_LT.Background = Brushes.Green;
                }
                else
                {
                    FINALLAMP_LT.Background = Brushes.Red;
                }
                #region 데이터 저장
                string barCodeName = "";
                if (Bc.CurMesSpec.SPEC.ImsiBarCode.Length == 11)
                {
                    barCodeName = Bc.CurMesSpec.SPEC.ImsiBarCode;
                }
                string FilePath = Bc.IMSI_DataFullPath + barCodeName+ "_LostTravel_FLO.txt";
                StreamWriter outputfile = new StreamWriter(FilePath);
                outputfile.WriteLine("Time(sec), Pri Air(bar), Flo Air(bar)\n");
                for (int i = 0; i < 2000; i++)
                {
                    outputfile.WriteLine(string.Format("{0:F3},{1:F3},{2:F3}", DP[i].X, DP[i].Y, DF[i].Y) + "\n");
                }
                outputfile.Close();
                #endregion
                //

                // 그래프쪽 텍스트 디스플레이
                LT_FLO_PRITestBar.Content = "3.0";
                LT_FLO_FLOTestBar.Content = "1.0";

                LT_FLO_FLOResult.Content = string.Format("{0:F2}", ResultValue_FLO);
                LT_FLO_PRIResult.Content = string.Format("{0:F2}", ResultValue_PRI);
                if ((ResultValue_FLO >= Spec_FLO_Low) & (ResultValue_FLO <= Spec_FLO_High))
                {
                    LT_FLO_FLOResult.Foreground = Brushes.Blue;
                }
                else
                {
                    LT_FLO_FLOResult.Foreground = Brushes.Red;
                    Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                }
                if ((ResultValue_PRI > Spec_PRI_Low) & (ResultValue_PRI < Spec_PRI_High))
                {
                    LT_FLO_PRIResult.Foreground = Brushes.Blue;
                }
                else
                {
                    LT_FLO_PRIResult.Foreground = Brushes.Red;
                    Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                }




                // 결과 그리드에 적용할 값 변환
                gtmp2.Note             = "컷 오프홀 테스트(FLO 압력 형성유무확인)";
                gtmp2.OKLowLimit       = Spec_FLO_Low;
                gtmp2.OKHighLimit      = Spec_FLO_High;
                gtmp2.MeasurementValue = ResultValue_FLO;
                gtmp2.TestName         = "로스트 트레블 FLO 시험";
                gtmp2.TestNo           = "2";
                gtmp2.TestUnit         = BCControl.TESTUNIT.bar;
                gtmp2.TestValue        = 1.00;

                gtmp3.MeasurementValue = ResultValue_PRI;

                Bc.LostTravelTest1     = gtmp2;
                Bc.LostTravelTest1_1   = gtmp3;

                if (Bc.LogEnable)
                {
                    Bc.Log.Info("[RESULT][Lost Tarvel FLO]  " + gtmp2.Result.ToString());
                    Bc.Log.Info("[SPEC] FLO Side Low : " + string.Format("{0:F2}", gtmp2.OKLowLimit));
                    Bc.Log.Info("[SPEC] FLO Side High : " + string.Format("{0:F2}", gtmp2.OKHighLimit));
                    Bc.Log.Info("[측정값] FLO Pressure(bar) : " + string.Format("{0:F2}", gtmp2.MeasurementValue));
                }

                TestSpecGridMapping();
                GridResultReset();
                PMChartCollection_LTP2.Append(DP);
                PMChartCollection_LTF2.Append(DF);
                Graph_LostTravelP2.Refresh();
                Graph_LostTravelF2.Refresh();

                MainSubScreen.SelectedIndex = 1;
            }));

            // 스펙 검사 루틴 추가.
        }
        public double PistonStroke_FullStroke = 0.0;
        public void otherobj_PistonStroke()   // 피스톤 스트로크 5.0바 테스트 결과 확인.
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                FINALLAMP_PS.Background = Brushes.Red;
                Bc.TestLastResult = false;
                // 피스톤 스트로크 챠트 데이터 클리어
                //PMChartCollection_PSP.Clear();
                //PMChartCollection_PSF.Clear();
                // 변수 선언
                _NoiseFilter_BBF  = new BesselLowpassFilter(5, 1000.0, 60.0);
                int LastCountIndex = Bc.LastpmData1IndexDataCount;
                double[] priair               = new double[2000];
                double[] floair               = new double[2000];
                double[] displacement         = new double[2000];
                double[] orgPriair            = new double[LastCountIndex];
                double[] orgFloair            = new double[LastCountIndex];
                double[] orgDisplacement      = new double[LastCountIndex];                
                double offsetDisplacement     = Bc.pmData1[LastCountIndex-1].Displacement;

                bool localOKCheck = true;

                if (LastCountIndex < 5000)
                {
                    FINALLAMP_PS.Background = Brushes.Red;
                    // 측정 데이터가 적을 경우 
                    return;
                }

                #region 데이터 저장
                // Low Data File
                string barCodeName = "";
                if (Bc.CurMesSpec.SPEC.ImsiBarCode.Length == 11)
                {
                    barCodeName = Bc.CurMesSpec.SPEC.ImsiBarCode;
                }
                string FilePath = Bc.IMSI_DataFullPath + barCodeName+ "_PistoStroke_AIR.txt";
                StreamWriter outputfile = new StreamWriter(FilePath);
                outputfile.WriteLine("Displacement(mm), Flo Air, Force(N)\n");
                for (int i = 0; i < LastCountIndex; i++)
                {
                    outputfile.WriteLine(string.Format("{0:F3},{1:F3},{2:F3}", Bc.pmData1[i].Displacement - offsetDisplacement, Bc.pmData1[i].FloAir, Bc.pmData1[i].Force) + "\n");
                }
                outputfile.Close();
                #endregion

                double Check_BaseBAR     = 3.5; // 3.5바로 떨어지는 지점의 위치를 확인하여 PRI Piston Stroke,FLO측 압력 변화로 확인
                if ((Bc.CurSysConfig.CurSysConfig.PistonStroke_FullStrokeFindBar>=3.0)&&(Bc.CurSysConfig.CurSysConfig.PistonStroke_FullStrokeFindBar<=4.0))
                {
                    Check_BaseBAR = Bc.CurSysConfig.CurSysConfig.PistonStroke_FullStrokeFindBar;
                }
                
                this.PS_FSCheckBar.Content = string.Format("{0:F1}", Check_BaseBAR) + "bar 위치";
                
                bool   FindCheckBar      = false;
                double FindCheckPosition = 0.0;

                double FindMaxStroke = 0.0; // 최대로 전진의 위치 확인용
                double FindMaxForce  = 0.0; // 전진의 가압후 최대 가압확인용

                double CheckValue = 0.0;
                double CheckForce = 0.0;
                double CheckBar   = 0.0;
                // NoiseFiltering                
                for (int i = 0; i < LastCountIndex; i++)
                {
                    orgPriair[i]       = Bc.pmData1[i].PriAir;  // 프라이머리 데이터 사용 안함.(2017/09)
                    orgFloair[i]       = Bc.pmData1[i].FloAir;
                    orgDisplacement[i] = Bc.pmData1[i].Displacement - offsetDisplacement;
                    
                    CheckValue = orgDisplacement[i];
                    CheckForce = Bc.pmData1[i].Force;
                    CheckBar   = Bc.pmData1[i].FloAir;
                    if (CheckValue > FindMaxStroke) FindMaxStroke = CheckValue;
                    if (CheckForce > FindMaxForce) FindMaxForce = CheckForce;
                    
                    if ((CheckBar<Check_BaseBAR)&&(!FindCheckBar))
                    {
                        FindCheckPosition = CheckValue;
                        FindCheckBar = true;
                    }
                }


                int intervaln = LastCountIndex / 2000;
                if (LastCountIndex < 2000) intervaln = 1;  // 데이터 갯수가 적을 경우
                int stepn = 0;

                Point[] DP = new Point[2000];  // Displacement,PriAir
                Point[] DF = new Point[2000];  // Displacement,FloAir
                for (int i = 0; i < 2000; i++)
                {
                    stepn = stepn + intervaln;
                    if (stepn >= orgDisplacement.Length) stepn = orgDisplacement.Length - 1;
                    DP[i] = new Point(orgDisplacement[stepn], orgPriair[stepn]);
                    DF[i] = new Point(orgDisplacement[stepn], orgFloair[stepn]);
                }

                DP.OrderByDescending(p => p.X).ToList();  // 화면 거리순으로 순차정렬용
                DF.OrderByDescending(p => p.X).ToList();

                BCControl.PISTONSTROKE_SPEC gtmp  = Bc.PistonStrokeSpec;
                BCControl.GRIDLABEL         gtmp2 = Bc.PistonStrokeTest;
                BCControl.GRIDLABEL         gtmp3 = Bc.PistonStrokeTest_1;


                gtmp2.MeasurementValue = FindCheckPosition;
                gtmp2.OKLowLimit       = 0.0;
                gtmp2.OKHighLimit      = 35.0;
                gtmp2.TestUnit         = BCControl.TESTUNIT.mm;

                if (FindCheckBar)
                {
                    gtmp2.Result            = BCControl.TESTRESULT.OK;
                    FINALLAMP_PS.Background = Brushes.Green;
                }
                else
                {
                    gtmp2.Result            = BCControl.TESTRESULT.NG;
                    FINALLAMP_PS.Background = Brushes.Red;
                }
                gtmp2.Note = gtmp2.Note + string.Format("{0:F1}", Check_BaseBAR)+ "바 일때의 위치";




                // TEXT Result Display
                

                double SPEC_Max_Force_Low  = 2700;   // MES 스펙 없음, 고정 상수
                double SPEC_Max_Force_High = 3400;   // 2018-02-28 변경
                PS_FWDForce.Content = string.Format("{0:F1}", FindMaxForce);
                if ((FindMaxForce > SPEC_Max_Force_Low) && (FindMaxForce < SPEC_Max_Force_High))
                {
                    PS_FWDForce.Foreground = Brushes.Blue;
                }
                else
                {
                    localOKCheck = false;
                    PS_FWDForce.Foreground = Brushes.Red;
                    gtmp2.Result = BCControl.TESTRESULT.NG;
                }

                PS_35Position.Content = string.Format("{0:F1}", FindCheckPosition);
                double SPEC_35_Low;
                double SPEC_35_High;
                SPEC_35_Low  = 0.0;
                SPEC_35_High = 35.0;
                if ((FindCheckPosition>=SPEC_35_Low)&&(FindCheckPosition<=SPEC_35_High))
                {
                    PS_35Position.Foreground = Brushes.Blue;
                }
                else
                {
                    localOKCheck = false;
                    PS_35Position.Foreground = Brushes.Red;
                    gtmp2.Result = BCControl.TESTRESULT.NG;
                }
                double SecondaryStroke    = 0.0;
                double SecondaryGapStroke = 0.0;
                double PrimaryStroke      = 0.0;
                double PrimaryGapStroke   = 0.0;
                double FullStroke         = 0.0;

                FullStroke         = Imsi_FullStroke;
                SecondaryStroke    = FullStroke - FindCheckPosition;
                SecondaryGapStroke = 1.5; // 상수적용
                PrimaryGapStroke   = Bc.LostTravelTest.MeasurementValue;
                PrimaryStroke      = FullStroke - SecondaryStroke - SecondaryGapStroke - PrimaryGapStroke;

                PS_FullStroke.Content     = string.Format("{0:F2}", FullStroke);
                PS_SECGAP.Content         = string.Format("{0:F2}", SecondaryGapStroke);
                PS_SECStroke.Content      = string.Format("{0:F2}", SecondaryStroke);
                PS_PRIGAP.Content         = string.Format("{0:F2}", PrimaryGapStroke);
                PS_PRIStroke.Content      = string.Format("{0:F2}", PrimaryStroke);

                double SPEC_PrimaryPistonMin = Bc.CurMesSpec.SPEC.PistonStroke_PriLength_Min;////19.0;
                double SPEC_PrimaryPistonMax = Bc.CurMesSpec.SPEC.PistonStroke_PriLength_Max; 

                PS_PRI_Min.Content = string.Format("{0:F1}", SPEC_PrimaryPistonMin);
                PS_PRI_Max.Content = string.Format("{0:F1}", SPEC_PrimaryPistonMax);
                if ((PrimaryStroke >= SPEC_PrimaryPistonMin) && (PrimaryStroke <= SPEC_PrimaryPistonMax))
                {
                    PS_PRIStroke.Foreground = Brushes.Blue;
                }
                else
                {
                    localOKCheck = false;
                    PS_PRIStroke.Foreground = Brushes.Red;
                    gtmp2.Result = BCControl.TESTRESULT.NG;
                }
                double SPEC_SecondaryPistonMin = Bc.CurMesSpec.SPEC.PistonStroke_SecLength_Min;
                double SPEC_SecondaryPistonMax = Bc.CurMesSpec.SPEC.PistonStroke_SecLength_Max;
                PS_SEC_Min.Content = string.Format("{0:F1}", SPEC_SecondaryPistonMin);
                PS_SEC_Max.Content = string.Format("{0:F1}", SPEC_SecondaryPistonMax);
                if (SecondaryStroke >= SPEC_SecondaryPistonMin)
                {
                    PS_SECStroke.Foreground = Brushes.Blue;
                }
                else
                {
                    localOKCheck = false;
                    PS_SECStroke.Foreground = Brushes.Red;
                    gtmp2.Result = BCControl.TESTRESULT.NG;
                }


                if (Bc.LogEnable)
                {
                    Bc.Log.Info("[RESULT][Piston Stroke 5bar Test] " + gtmp2.Result.ToString());
                    Bc.Log.Info("[SPEC] " + string.Format("{0:F1}", Check_BaseBAR) + "bar Position Low : " + string.Format("{0:F2}", gtmp2.OKLowLimit));
                    Bc.Log.Info("[SPEC] " + string.Format("{0:F1}", Check_BaseBAR) + "bar Position High : " + string.Format("{0:F2}", gtmp2.OKHighLimit));
                    if (gtmp2.Result == BCControl.TESTRESULT.OK) Bc.Log.Info("[측정값] Position(mm) : " + string.Format("{0:F2}", gtmp2.MeasurementValue));
                    else                                         Bc.Log.Info("[측정값] Position(mm) : NG ");
                    Bc.Log.Info("[CALC] Full Stroke : " + string.Format("{0:F2}", FullStroke));
                    Bc.Log.Info("[CALC] Secondary GAP : " + string.Format("{0:F2}", SecondaryGapStroke));
                    Bc.Log.Info("[CALC] Secondary : " + string.Format("{0:F2}", SecondaryStroke));
                    Bc.Log.Info("[CALC] Primary GAP : " + string.Format("{0:F2}", PrimaryGapStroke));
                    Bc.Log.Info("[CALC] Primary : " + string.Format("{0:F2}", PrimaryStroke));
                }

                if (gtmp2.Result == BCControl.TESTRESULT.OK)
                {
                    PS_TotalResult.Content    = "OK";
                    PS_TotalResult.Foreground = Brushes.Blue;
                    FINALLAMP_PS.Background   = Brushes.Green;
                    Bc.TestLastResult = true;
                }
                else
                {
                    PS_TotalResult.Content    = "NG";
                    PS_TotalResult.Foreground = Brushes.Red;
                    FINALLAMP_PS.Background   = Brushes.Red;
                    Bc.TestLastResult         = false; // 시험 NG시 메인쓰레드에게 알림.
                }

                gtmp2.MeasurementValue = PrimaryStroke;  // 기존 3.5바의 위치를 표시하는 부분을 프라이머리 유효 길이로 대체
                gtmp3.MeasurementValue = SecondaryStroke;

                Bc.PistonStrokeTest_1 = gtmp3;
                Bc.PistonStrokeTest   = gtmp2;

                PS_TestTime.Content = string.Format("{0:F2}", 0.0);// 2018-02-28 이전시험과 병행하므로 Bc._SubTestTimer.ResultTime(TestTime.TESTNAME.PISTON_MOVING));
                
                TestSpecGridMapping();
                GridResultReset();
                PMChartCollection_PSP.Append(DP);
                PMChartCollection_PSF.Append(DF);

                Graph_PistonStrokeF.DataSource = PMChartCollection_PSF;
                Graph_PistonStrokeF.Refresh();
                
                MainSubScreen.SelectedIndex = 5;

            }));  

            // 스펙 검사 루틴 추가.
        }
        public void otherobj_PistonStrokeVAC()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                // 2018-03-05
                // 진공 누설향 검사항목 병합
                // 변수 선언
                Bc.TestLastResult = false;
                FINALLAMP_PS.Background = Brushes.Red;

                _NoiseFilter_BBF         = new BesselLowpassFilter(5, 1000.0, 60.0);
                int LastCountIndex       = Bc.LastpmData1IndexDataCount;
                double[] priair          = new double[2000];
                double[] floair          = new double[2000];
                double[] displacement    = new double[2000];
                double[] orgPriair       = new double[LastCountIndex];
                double[] orgFloair       = new double[LastCountIndex];
                double[] orgDisplacement = new double[LastCountIndex];

                double offsetDisplacement = Bc.pmData1[LastCountIndex-1].Displacement;
                if (LastCountIndex < 5000)
                {
                    return;
                }
                #region 데이터 저장
                // Low Data File
                string barCodeName = "";
                if (Bc.CurMesSpec.SPEC.ImsiBarCode.Length == 11)
                {
                    barCodeName = Bc.CurMesSpec.SPEC.ImsiBarCode;
                }
                string FilePath = Bc.IMSI_DataFullPath + barCodeName+ "_PistoStroke_Vacuum.txt";
                StreamWriter outputfile = new StreamWriter(FilePath);
                outputfile.WriteLine("Displacement(mm), Pri Vacuum(mmHg), Flo Vacuum(mmHg), Force(N)\n");
                for (int i = 0; i < LastCountIndex; i++)
                {
                    outputfile.WriteLine(string.Format("{0:F3},{1:F3},{2:F3},{3:F3}", Bc.pmData1[i].Displacement - offsetDisplacement, Bc.pmData1[i].PriVacuum, Bc.pmData1[i].FloVacuum, Bc.pmData1[i].Force) + "\n");
                }
                outputfile.Close();
                #endregion

                double SPEC_PRI_Pos     = 8.0;  // 7mm 이상  -> 10(09/13)
                if ((Bc.CurSysConfig.CurSysConfig.PistonStroke_VacPriPosition>=5.0)&&(Bc.CurSysConfig.CurSysConfig.PistonStroke_VacPriPosition>=20.0))
                {
                    SPEC_PRI_Pos = Bc.CurSysConfig.CurSysConfig.PistonStroke_VacPriPosition;
                }
                double SPEC_FLO_Pos     = 30.0; // 29mm 이상  -> 30(09/13)
                if ((Bc.CurSysConfig.CurSysConfig.PistonStroke_VacSecPosition >= 5.0) && (Bc.CurSysConfig.CurSysConfig.PistonStroke_VacSecPosition >= 35.0))
                {
                    SPEC_FLO_Pos = Bc.CurSysConfig.CurSysConfig.PistonStroke_VacSecPosition;
                }

                R_PS500_Pri.Content = string.Format("{0:F1}", SPEC_PRI_Pos);
                R_PS500_Sec.Content = string.Format("{0:F1}", SPEC_FLO_Pos);

                double SPEC_PRI_OverVac = 420.0;
                SPEC_PRI_OverVac        = Bc.CurMesSpec.SPEC.PistonStroke_PriVac_Min;
                R_PS500V_Up.Content     = string.Format("{0:F0}", SPEC_PRI_OverVac);
                double SPEC_FLO_OverVac = 420.0;
                SPEC_FLO_OverVac        = Bc.CurMesSpec.SPEC.PistonStroke_SecVac_Min;
                R_PS500V_SUp.Content    = string.Format("{0:F0}", SPEC_FLO_OverVac);


                bool Result_PRI = true;
                bool Result_FLO = true;

                double LastResult_PRI = 0.0;
                double LastResult_FLO = 0.0;
                // 2018-03-05
                double Check5mmPriVac  = 0.0; // PRI 5mm 위치일때의 진공량
                bool   Check5mmPriFind = false;
                double PrimmPos = 5.0;
                double Check27mmSecVac = 0.0; // SEC 27mm 위치일때의 진공량
                bool Check27mmSecFind = false;
                double SecmmPos = 27.0;

                bool Result_Vac_PRI = false;
                bool Result_Vac_FLO = false;
                // NoisFiltering                
                for (int i = 0; i < LastCountIndex; i++)
                {
                    double Check_PRI = 0.0;
                    double Check_FLO = 0.0;
                    double Check_Position = 0.0;

                    orgPriair[i] = Bc.pmData1[i].PriVacuum;
                    orgFloair[i] = Bc.pmData1[i].FloVacuum;
                    orgDisplacement[i] = Bc.pmData1[i].Displacement - offsetDisplacement;

                    Check_Position = orgDisplacement[i];
                    Check_PRI = orgPriair[i];
                    Check_FLO = orgFloair[i];

                    if (Check_Position >= SPEC_PRI_Pos)
                    {
                        if (Check_PRI <= SPEC_PRI_OverVac) Result_PRI = false;
                        LastResult_PRI = Check_PRI;
                    }
                    if (Check_Position >= SPEC_FLO_Pos)
                    {
                        if (Check_FLO <= SPEC_FLO_OverVac) Result_FLO = false;
                        LastResult_FLO = Check_FLO;
                    }
                    //2018-03-04
                    Bc.VacuumTestPri = new BCControl.GRIDLABEL("Vacuum", "PistonStroke VAC PRI", 500.0, Bc.CurMesSpec.SPEC.VacuumHold_Pri_Min, Bc.CurMesSpec.SPEC.VacuumHold_Pri_Max, Check5mmPriVac, BCControl.TESTRESULT.OK, BCControl.TESTUNIT.mmHg, "Pass");
                    if ((Check_Position>=PrimmPos-0.05)&&(Check_Position<=PrimmPos+0.05)&&(Check5mmPriFind==false))
                    {
                        Check5mmPriVac = Check_PRI;
                        Check5mmPriFind = true;
                    }
                    Bc.VacuumTestFlo = new BCControl.GRIDLABEL("Vacuum", "PistonStroke VAC FLO", 500.0, Bc.CurMesSpec.SPEC.VacuumHold_Sec_Min, Bc.CurMesSpec.SPEC.VacuumHold_Sec_Max, Check27mmSecVac, BCControl.TESTRESULT.NG, BCControl.TESTUNIT.mmHg, "Pass");
                    if ((Check_Position >= SecmmPos - 0.05) && (Check_Position <= SecmmPos + 0.05) && (Check27mmSecFind == false))
                    {
                        Check27mmSecVac  = Check_FLO;
                        Check27mmSecFind = true;                       
                    }
                }
                Bc.VacuumTestPri = new BCControl.GRIDLABEL("Vacuum", "PistonStroke VAC", 500.0, 38.0, 380.0, Check5mmPriVac, BCControl.TESTRESULT.OK, BCControl.TESTUNIT.mmHg, "Pass");
                //Bc.VacuumTestFlo.MeasurementValue = Check27mmSecVac;
                //filteredPriair       = _NoiseFilter_BBF.FilterData(orgPriair);
                //filteredFloair       = _NoiseFilter_BBF.FilterData(orgFloair);
                //filteredDisplacement = _NoiseFilter_BBF.FilterData(orgDisplacement);

                int intervaln = LastCountIndex / 2000;
                if (LastCountIndex < 2000) intervaln = 1;  // 데이터가 없을 경우
                int stepn = 0;
                Point[] DP = new Point[2000];  // Displacement,PriAir
                Point[] DF = new Point[2000];  // Displacement,FloAir
                for (int i = 0; i < 2000; i++)
                {
                    stepn = stepn + intervaln;
                    if (stepn >= orgDisplacement.Length) stepn = orgDisplacement.Length - 1;
                    DP[i] = new Point(orgDisplacement[stepn], orgPriair[stepn]);
                    DF[i] = new Point(orgDisplacement[stepn], orgFloair[stepn]);
                }

                DP.OrderByDescending(p => p.X).ToList();
                DF.OrderByDescending(p => p.X).ToList();


                //2018-03-5

                double Spec_Pri_Low = Bc.CurMesSpec.SPEC.VacuumHold_Pri_Min;//38.0;
                double Spec_Pri_High = Bc.CurMesSpec.SPEC.VacuumHold_Pri_Max;//380.0;
                R_PV500V_Min.Content = string.Format("{0:F1}", Spec_Pri_Low);
                R_PV500V_Max.Content = string.Format("{0:F1}", Spec_Pri_High);
                double Spec_Sec_Low = Bc.CurMesSpec.SPEC.VacuumHold_Sec_Min;//38.0;
                double Spec_Sec_High = Bc.CurMesSpec.SPEC.VacuumHold_Sec_Max;//380.0;
                R_PV500S_Min.Content = string.Format("{0:F1}", Spec_Sec_Low);
                R_PV500S_Max.Content = string.Format("{0:F1}", Spec_Sec_High);


                PV5_PRIResult.Content    = "NG";
                PV5_PRIResult.Foreground = Brushes.Red;
                if (Check5mmPriFind)
                {
                    if ((Check5mmPriVac >= Spec_Pri_Low) && (Check5mmPriVac <= Spec_Pri_High))
                    {
                        PV5_PRIResult.Content = "OK";
                        PV5_PRIResult.Foreground = Brushes.Blue;
                        Result_Vac_PRI = true;
                        Bc.VacuumTestPri = new BCControl.GRIDLABEL("Vacuum", "PistonStroke VAC PRI", 500.0, Bc.CurMesSpec.SPEC.VacuumHold_Pri_Min, Bc.CurMesSpec.SPEC.VacuumHold_Pri_Max, Check5mmPriVac, BCControl.TESTRESULT.OK, BCControl.TESTUNIT.mmHg, "Pass");
                    }
                }



                PV5_SECResult.Content = "NG";
                PV5_SECResult.Foreground = Brushes.Red;
                if (Check27mmSecFind)
                {
                    if ((Check27mmSecVac >= Spec_Sec_Low) && (Check27mmSecVac <= Spec_Sec_High))
                    {
                        PV5_SECResult.Content     = "OK";
                        PV5_SECResult.Foreground = Brushes.Blue;
                        Result_Vac_FLO = true;
                        Bc.VacuumTestFlo = new BCControl.GRIDLABEL("Vacuum", "PistonStroke VAC FLO", 500.0, Bc.CurMesSpec.SPEC.VacuumHold_Sec_Min, Bc.CurMesSpec.SPEC.VacuumHold_Sec_Max, Check27mmSecVac, BCControl.TESTRESULT.OK, BCControl.TESTUNIT.mmHg, "Pass");
                    }
                }
                







                BCControl.GRIDLABEL gtmp2 = Bc.PistonStrokeVacTest;
                BCControl.GRIDLABEL gtmp3 = Bc.PistonStrokeVacTest_1;

                gtmp2.MeasurementValue = LastResult_PRI;
                gtmp3.MeasurementValue = LastResult_FLO;
                // 화면 표시
                gtmp2.Result = BCControl.TESTRESULT.OK;
                PS5_TestVacuum.Content = "500.0";
                
                if (Result_PRI)
                {
                    PS5_PRIResult.Content = "OK";
                    PS5_PRIResult.Foreground = Brushes.Blue;
                }
                else
                {
                    PS5_PRIResult.Content = "NG";
                    PS5_PRIResult.Foreground = Brushes.Red;                    
                }



                if (Result_FLO)
                {
                    PS5_SECResult.Content = "OK";
                    PS5_SECResult.Foreground = Brushes.Blue;
                }
                else
                {
                    PS5_SECResult.Content = "NG";
                    PS5_SECResult.Foreground = Brushes.Red;
                }

                PS5_TestTime.Content = string.Format("{0:F2}",Bc._SubTestTimer.ResultTime(TestTime.TESTNAME.PISTON_STROKE_500mmHg));

                if ((Result_PRI)&&(Result_FLO))
                {
                    PS5_TotalResult.Content = "OK";
                    PS5_TotalResult.Foreground = Brushes.Blue;
                }
                else
                {
                    gtmp2.Result               = BCControl.TESTRESULT.NG;
                    PS5_TotalResult.Content    = "NG";
                    PS5_TotalResult.Foreground = Brushes.Red;
                }
                Bc.PistonStrokeVacTest = gtmp2;
                if (Bc.LogEnable)
                {
                    if ((Result_FLO)&&(Result_PRI))  Bc.Log.Info("[RESULT][Piston Stroke 500mmHg Test] OK" );
                    else                             Bc.Log.Info("[RESULT][Piston Stroke 500mmHg Test] NG");
                

                    if (!Result_PRI) Bc.Log.Info("[측정] PRI Side NG");
                    if (!Result_FLO) Bc.Log.Info("[측정] FLO Side NG");  
                }
                Bc.TestLastResult = true;
                if (Bc.PistonStrokeTest.Result== BCControl.TESTRESULT.OK)
                {
                    if ((Result_FLO)&&(Result_PRI)&&(Result_Vac_PRI)&&(Result_Vac_FLO))
                    {
                        FINALLAMP_PS.Background = Brushes.Green;
                    }
                    else
                    {
                        FINALLAMP_PS.Background = Brushes.Red;
                        Bc.TestLastResult       = false;           // 시험 NG시 메인쓰레드에게 알림.
                    }
                }
                else
                {
                    FINALLAMP_PS.Background = Brushes.Red;
                    Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                }
                PMChartCollection_PSP500.Append(DP);
                PMChartCollection_PSF500.Append(DF);
                Graph_PistonStrokeP500.DataSource = PMChartCollection_PSP500;
                Graph_PistonStrokeF500.DataSource = PMChartCollection_PSF500;

                Graph_PistonStrokeP500.Refresh();
                Graph_PistonStrokeF500.Refresh();

                Bc.PistonStrokeVacTest   = gtmp2;
                Bc.PistonStrokeVacTest_1 = gtmp3;
                MainSubScreen.SelectedIndex = 6;

            }));

            // 스펙 검사 루틴 추가.
        }
        public void otherobj_Vacuum()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                // 로스트 트레블 챠트 데이터 클리어
                //PMChartCollection_PS.Clear();
                // 변수 선언
                _NoiseFilter_BBF        = new BesselLowpassFilter(5, 1000.0, 60.0);
                int LastCountIndex      = Bc.LastpmData1IndexDataCount;
                double[] privac         = new double[2501];
                double[] flovac         = new double[2501];
                double[] orgPrivac      = new double[LastCountIndex];
                double[] orgFlovac      = new double[LastCountIndex];
                double[] filteredPrivac = new double[LastCountIndex];
                double[] filteredFlovac = new double[LastCountIndex];
                double[,] ttdata        = new double[2, 2000];

                GraphData_Vacuum1[0].Clear();
                GraphData_Vacuum1[1].Clear();

                #region 데이터 저장
                // Low Data Save & Temp storage
                string barCodeName = "";
                if (Bc.CurMesSpec.SPEC.ImsiBarCode.Length == 11)
                {
                    barCodeName = Bc.CurMesSpec.SPEC.ImsiBarCode;
                }
                string FilePath = Bc.IMSI_DataFullPath + barCodeName+ "_VacuumHoldsTest_PRI.txt";
                StreamWriter outputfile = new StreamWriter(FilePath);
                outputfile.WriteLine("Time(sec), PRI vacuum(mmHg), SEC(Flo) vacuum(mmHg)\n");
                double _TimeIndex = 0.0;
                for (int i = 0; i < LastCountIndex; i++)
                {
                    _TimeIndex   = i * 0.001;                // 0.001 is 1Khz DAQ SampleRate
                    orgPrivac[i] = Bc.pmData1[i].PriVacuum;
                    orgFlovac[i] = Bc.pmData1[i].FloVacuum;
                    outputfile.WriteLine(string.Format("{0:F3}, {1:F3}, {2:F3}",_TimeIndex, Bc.pmData1[i].PriVacuum, Bc.pmData1[i].FloVacuum) + "\n");
                }
                outputfile.Close();
                #endregion
                // NoisFiltering
                filteredPrivac = _NoiseFilter_BBF.FilterData(orgPrivac);
                filteredFlovac = _NoiseFilter_BBF.FilterData(orgFlovac);

                int intervaln = LastCountIndex / 2000;     // Screen Fix
                if (LastCountIndex < 2000) intervaln = 1;  // 데이터가 없을 경우
                int stepn = 0;
                double _ConversionTime = 0.0;

                double CheckTime_Start = 4.8;  // 시작시간에서 종료까지 평균값을 측정 값으로
                if ((Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckStart>=0.5)&&(Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckStart<=7.0))
                {
                    CheckTime_Start = Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckStart;
                }
                double CheckTime_End   = 5.2;
                if ((Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckEnd >= 0.5) && (Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckEnd <= 8.0))
                {
                    CheckTime_End = Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckEnd;
                }
                VH_TestCheckTime.Content = string.Format("{0:F0}",(CheckTime_Start+CheckTime_End)/2.0)+"초후 측정기준";

                double avgPRI          = 0.0;
                int avgDataCount       = 0;
                for (int i = 0; i < 2000; i++)
                {
                    stepn = stepn + intervaln;
                    privac[i] = filteredPrivac[stepn];
                    flovac[i] = filteredFlovac[stepn];
                    // Graph Data Append
                    _ConversionTime = stepn * 0.001;
                    GraphData_Vacuum1[0].Append(_ConversionTime, privac[i]);
                    GraphData_Vacuum1[1].Append(_ConversionTime, flovac[i]);

                    // 데이터 확인
                    if ((_ConversionTime > CheckTime_Start) && (_ConversionTime < CheckTime_End))
                    {
                        avgPRI = avgPRI + privac[i];
                        avgDataCount++;
                    }
                }


                // 시험 스펙기준으로 진공값 구하기
                double ResultPRI_Vacuum = 0.0;
                if (avgDataCount>0)
                {
                    ResultPRI_Vacuum = avgPRI / avgDataCount;
                }
                else
                {
                    ResultPRI_Vacuum = 0.0;
                }

                double Spec_Low  = Bc.CurMesSpec.SPEC.VacuumHold_Pri_Min;//38.0;
                double Spec_High = Bc.CurMesSpec.SPEC.VacuumHold_Pri_Max;//380.0;
                R_VH_PRIMin.Content = string.Format("{0:F1}", Spec_Low);
                R_VH_PRIMax.Content = string.Format("{0:F1}", Spec_High);
                BCControl.VACUUM_SPEC vas = Bc.VacuumSpec;
                BCControl.GRIDLABEL   vap = Bc.VacuumTestPri;
                //BCControl.GRIDLABEL vaf = Bc.VacuumTestFlo;
                bool chk1 = false;
                if ((ResultPRI_Vacuum >= Spec_Low) && (ResultPRI_Vacuum <= Spec_High))
                {
                        vap.MeasurementValue = ResultPRI_Vacuum;
                        vap.Result = BCControl.TESTRESULT.OK;
                        VH_PRITotalResult.Content = "OK";
                        VH_PRIResult.Foreground = Brushes.Blue;
                        VH_PRITotalResult.Foreground = Brushes.Blue;
                        chk1 = true;
                }
                else
                {
                    vap.MeasurementValue = ResultPRI_Vacuum;
                    VH_PRITotalResult.Content = "NG";
                    VH_PRITotalResult.Foreground = Brushes.Red;
                    VH_PRIResult.Foreground = Brushes.Red;
                    vap.Result = BCControl.TESTRESULT.NG;
                }

                VH_TestVacuum.Content  = "500.0";
                VH_PRIResult.Content   = string.Format("{0:F2}", ResultPRI_Vacuum);
                VH_PRITestTime.Content = string.Format("{0:F2}", Bc._SubTestTimer.ResultTime(TestTime.TESTNAME.VACUUMHOLD_PRI));

                if (chk1) FINALLAMP_VA.Background = Brushes.Green;
                else
                {
                    FINALLAMP_VA.Background = Brushes.Red;
                    Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                }

                // 결과 그리드에 적용할 값 변환
                vap.Note             = "PRI/FLO 500mmHg";
                vap.OKLowLimit       = Spec_Low;
                vap.OKHighLimit      = Spec_High;
                vap.MeasurementValue = ResultPRI_Vacuum;
                vap.TestName         = "진공시험(PRI)";
                vap.TestNo           = "6";
                vap.TestUnit         = BCControl.TESTUNIT.mmHg;
                vap.TestValue        = 500.00;


                Bc.VacuumTestPri = vap;

                if (Bc.LogEnable)
                {
                    Bc.Log.Info("[RESULT][Vacuum Test PRI] " + vap.Result.ToString());
                    Bc.Log.Info("[SPEC] Low : " + string.Format("{0:F2}", vap.OKLowLimit));
                    Bc.Log.Info("[SPEC] High : " + string.Format("{0:F2}", vap.OKHighLimit));
                    if (vap.Result == BCControl.TESTRESULT.OK) Bc.Log.Info("[측정값] Vacuum(mmHg) : " + string.Format("{0:F2}", vap.MeasurementValue));
                    else Bc.Log.Info("[측정값] Vacuum(mmHg) : NG ");
                }

                TestSpecGridMapping();
                GridResultReset();

                // Graph Refresh
                Graph_Vacuum.DataSource = GraphData_Vacuum1;
                Graph_Vacuum.Refresh();

                MainSubScreen.SelectedIndex = 7;

            }));  
            // 스펙 검사 루틴 추가.
        }
        public void otherobj_Vacuum2()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                // 로스트 트레블 챠트 데이터 클리어
                //PMChartCollection_PS.Clear();
                // 변수 선언
                _NoiseFilter_BBF        = new BesselLowpassFilter(5, 1000.0, 60.0);
                int LastCountIndex      = Bc.LastpmData1IndexDataCount;
                double[] priair         = new double[2501];
                double[] flovac         = new double[2501];
                double[] orgPriAir      = new double[LastCountIndex];
                double[] orgFlovac      = new double[LastCountIndex];
                double[] filteredPriair = new double[LastCountIndex];
                double[] filteredFlovac = new double[LastCountIndex];
                double[,] ttdata        = new double[2, 2000];


                GraphData_Vacuum2[0].Clear();
                GraphData_Vacuum2[1].Clear();
                // Low Data Save & Temp storage
                string FilePath = Bc.IMSI_DataFullPath + "VacuumHoldsTest_FLO.txt";
                StreamWriter outputfile = new StreamWriter(FilePath);
                outputfile.WriteLine("Time(sec), PRI Air(bar), SEC(Flo) vacuum(mmHg)\n");
                double _TimeIndex = 0.0;
                for (int i = 0; i < LastCountIndex; i++)
                {
                    _TimeIndex = i * 0.001;                // 0.001 is 1Khz DAQ SampleRate
                    orgPriAir[i] = Bc.pmData1[i].PriAir;
                    //orgPrivac[i] = Bc.pmData1[i].PriVacuum;
                    orgFlovac[i] = Bc.pmData1[i].FloVacuum;
                    outputfile.WriteLine(string.Format("{0:F3}, {1:F3}, {2:F3}", _TimeIndex, Bc.pmData1[i].PriAir, Bc.pmData1[i].FloVacuum) + "\n");
                }
                outputfile.Close();

                // NoisFiltering
                filteredPriair = _NoiseFilter_BBF.FilterData(orgPriAir);
                filteredFlovac = _NoiseFilter_BBF.FilterData(orgFlovac);

                int intervaln = LastCountIndex / 2000;     // Screen Fix
                if (LastCountIndex < 2000) intervaln = 1;  // 데이터가 없을 경우
                int stepn = 0;
                double _ConversionTime = 0.0;
                double CheckTime_Start = 4.8;  // 시작시간에서 종료까지 평균값을 측정 값으로
                if ((Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckStart >= 0.5) && (Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckStart <= 7.0))
                {
                    CheckTime_Start = Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckStart;
                }
                double CheckTime_End = 5.2;
                if ((Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckEnd >= 0.5) && (Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckEnd <= 8.0))
                {
                    CheckTime_End = Bc.CurSysConfig.CurSysConfig.VacuumHold_CheckEnd;
                }
                VH_TestCheckTimeSec.Content = string.Format("{0:F0}", (CheckTime_Start + CheckTime_End) / 2.0) + "초후 측정기준";

                double avgFLO = 0.0;
                int avgDataCount = 0;
                for (int i = 0; i < 2000; i++)
                {
                    stepn = stepn + intervaln;
                    priair[i] = filteredPriair[stepn];
                    flovac[i] = filteredFlovac[stepn];
                    // Graph Data Append
                    _ConversionTime = stepn * 0.001;
                    GraphData_Vacuum2[0].Append(_ConversionTime, priair[i]);
                    GraphData_Vacuum2[1].Append(_ConversionTime, flovac[i]);
                    // 데이터 확인
                    if ((_ConversionTime > CheckTime_Start) && (_ConversionTime < CheckTime_End))
                    {
                        avgFLO = avgFLO + flovac[i];
                        avgDataCount++;
                    }
                }

                // 시험 스펙기준으로 진공값 구하기
                double ResultFLO_Vacuum = 0.0;
                if (avgDataCount > 0)
                {
                    ResultFLO_Vacuum = avgFLO / avgDataCount;
                }
                else
                {
                    ResultFLO_Vacuum = 0.0;
                }

                double Spec_Low     = Bc.CurMesSpec.SPEC.VacuumHold_Sec_Min;//38.0;
                double Spec_High    = Bc.CurMesSpec.SPEC.VacuumHold_Sec_Max;//380.0;
                R_VH_FLOMin.Content = string.Format("{0:F1}", Spec_Low);
                R_VH_FLOMax.Content = string.Format("{0:F1}", Spec_High);

                BCControl.VACUUM_SPEC vas = Bc.VacuumSpec;
                BCControl.GRIDLABEL   vaf = Bc.VacuumTestFlo;
                bool chk1 = false;
                if ((ResultFLO_Vacuum >= Spec_Low) && (ResultFLO_Vacuum <= Spec_High))
                {
                    vaf.MeasurementValue = ResultFLO_Vacuum;
                    VH_SECTotalResult.Content = "OK";
                    VH_SECTotalResult.Foreground = Brushes.Blue;
                    VH_SECResult.Foreground = Brushes.Blue;
                    vaf.Result = BCControl.TESTRESULT.OK;
                    chk1 = true;
                }
                else
                {
                    vaf.MeasurementValue = ResultFLO_Vacuum;
                    VH_SECTotalResult.Content = "NG";
                    VH_SECTotalResult.Foreground = Brushes.Red;
                    VH_SECResult.Foreground = Brushes.Red;
                    vaf.Result = BCControl.TESTRESULT.NG;
                }


                VH_SECTestVacuum.Content = "500.0";
                VH_SECTestBar.Content = "3.0";
                VH_SECResult.Content = string.Format("{0:F2}", ResultFLO_Vacuum);
                VH_SECTesTime.Content = string.Format("{0:F2}", Bc._SubTestTimer.ResultTime(TestTime.TESTNAME.VACUUMHOLD_SEC));


                if ((chk1) && (Bc.VacuumTestPri.Result == BCControl.TESTRESULT.OK))
                {
                    FINALLAMP_VA.Background = Brushes.Green;
                }
                else
                {
                    FINALLAMP_VA.Background = Brushes.Red;
                    Bc.TestLastResult = false; // 시험 NG시 메인쓰레드에게 알림.
                }


                // 결과 그리드에 적용할 값 변환
                vaf.Note             = "PRI 3.0bar , FLO 500mmHg";
                vaf.OKLowLimit       = Spec_Low;
                vaf.OKHighLimit      = Spec_High;
                vaf.MeasurementValue = ResultFLO_Vacuum;
                vaf.TestName         = "진공시험(FLO)";
                vaf.TestNo           = "6";
                vaf.TestUnit         = BCControl.TESTUNIT.mmHg;
                vaf.TestValue        = 500.00;

                Bc.VacuumTestFlo = vaf;

                if (Bc.LogEnable)
                {
                    Bc.Log.Info("[RESULT][Vacuum Test FLO] " + vaf.Result.ToString());
                    Bc.Log.Info("[SPEC] Low : " + string.Format("{0:F2}", vaf.OKLowLimit));
                    Bc.Log.Info("[SPEC] High : " + string.Format("{0:F2}", vaf.OKHighLimit));
                    if (vaf.Result == BCControl.TESTRESULT.OK) Bc.Log.Info("[측정값] Vacuum(mmHg) : " + string.Format("{0:F2}", vaf.MeasurementValue));
                    else                                       Bc.Log.Info("[측정값] Vacuum(mmHg) : NG ");
                }

                TestSpecGridMapping();
                GridResultReset();


                // Graph Refresh
                Graph_Vacuum2.DataSource = GraphData_Vacuum2;
                Graph_Vacuum2.Refresh();

                MainSubScreen.SelectedIndex = 8;

            }));
            // 스펙 검사 루틴 추가.
        }
        #endregion

        private void BCSystemClosing()
        {

            Bc.Log.Info("iMEB 백업실린더 프로그램을 종료합니다.");

            Config.SaveConfigData(SysConfig);
            LeakTest_Specification.SaveConfigData(Bc.LeakTestConfig);
            AllTest_Specification.SaveConfigData(Bc.AllTestConfig);
            Bc.M_MesLds.RequestStop_UDPServer();
            Bc.M_MesLds.UDPServerQuit();
            
            // COSMO Serial
            Bc.CosmoSerialAllClose();
            if (MainUITimer!=null)
            {
                MainUITimer.Stop();
                MainUITimer = null;
            }

            if (MainControlTimer!=null)
            {
                MainControlTimer.Stop();                
                MainControlTimer = null;
            }
            if (Bc !=null)
            {
                try
                {
                    Bc.RequestStop();
                    AutomaticThread.Abort();
                    Bc = null;
                }
                catch (Exception e1) 
                { 
                    if (Bc.LogEnable)
                    {
                        Bc.Log.Error("프로그램 종료중 Exception " + e1.Message);
                    }
                }
            }
        }
        /// <summary>
        /// 최초 실행시 프로그램관련 초기화
        /// </summary>
        /// <returns></returns>
        private bool BCSystemInitialize()
        {
            bool PLCisOK   = true;
            bool DAQisOK   = true;
            bool COSMOisOK = true;
            bool _chk      = false;

            Bc.Log.Info("iMEB 백업 실린더 성능 시험 프로그램을 시작합니다.");
            // 초기 설정화일 
            try
            {
                Bc.LeakTestConfig = LeakTest_Specification.GetConfigData();       // 리크테스트 설정화일 읽기
                Bc.AllTestConfig = AllTest_Specification.GetConfigData();        // 전체 시험설정정보 읽기

                _chk = Bc.CurMesSpec.GetConfigData();

                // 2017-10-30 MESLDS관련 전체 스펙데이터 로컬 PC에서 읽어 들임.
                if (_chk == false)
                {
                    Bc.Log.Error("MES Spec관련하여 iMEB_LT4_MESSpecification.xml 화일이 손상되었습니다.");
                }
                Bc.CurMesSpec.SPEC.ImsiBarCode = "12345678901";

                _chk = Bc.CurSysConfig.GetConfigData();
                // 2017-10-30 시험관련 전체 스펙데이터 로컬 PC에서 읽어 들임.
                if (_chk == false)
                {
                    Bc.Log.Error("MES Spec관련하여 iMEB_BCPB_SysConfig.xml.xml 화일이 손상되었습니다.");
                }
                SysConfig = Config.GetConfigData();                       // 설정화일 읽기(향후 안쓰는 것 빼고 정리)
                Bc.AirLeakTestSpec15Pri = SysConfig.IoMap.AirLeakTestSpec15Pri;
                Bc.AirLeakTestSpec15Flo = SysConfig.IoMap.AirLeakTestSpec15Flo;
                Bc.AirLeakTestSpec50Pri = SysConfig.IoMap.AirLeakTestSpec50Pri;
                Bc.AirLeakTestSpec50Flo = SysConfig.IoMap.AirLeakTestSpec50Flo;
                Bc.PistonMovingFWDTest = SysConfig.IoMap.PistonMovingBWDTest;
                Bc.PistonMovingBWDTest = SysConfig.IoMap.PistonMovingBWDTest;
                Bc.LostTravelTest = SysConfig.IoMap.LostTravelTest;
                Bc.PistonStrokeTest = SysConfig.IoMap.PistonStrokeTest;
                Bc.VacuumTestPri = SysConfig.IoMap.VacuumTestPri;
                Bc.VacuumTestFlo = SysConfig.IoMap.VacuumTestFlo;

                Bc.PistonMovingSpec = SysConfig.IoMap.PistonMovingSpec;
                Bc.LostTravelSpec = SysConfig.IoMap.LostTravelSpec;
                Bc.PistonStrokeSpec = SysConfig.IoMap.PistonStrokeSpec;
                Bc.VacuumSpec = SysConfig.IoMap.VacuumSpec;

                TestSpecGridMapping();
                GridResultReset();
                // PerformanceCOunter 설정
                cpuCounter = new PerformanceCounter();
                cpuCounter.CategoryName = "Processor";
                cpuCounter.CounterName = "% Processor Time";
                cpuCounter.InstanceName = "_Total";
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                // Main UI Refresh Timer 설정
                if (MainUITimer != null)
                {
                    MainUITimer.Stop();
                    MainUITimer = null;
                }
                MainUITimer = new DispatcherTimer();
                MainUITimer.Interval = TimeSpan.FromMilliseconds(100); // 0.1Sec
                MainUITimer.Tick += new EventHandler(MainUITimer_Elapsed);
                MainUITimer.Start();
                // Main Control Timer 설정
                if (MainControlTimer != null)
                {
                    MainControlTimer.Stop();
                    MainControlTimer = null;
                }
                MainControlTimer = new DispatcherTimer();
                MainControlTimer.Interval = TimeSpan.FromMilliseconds(50); // 0.05Sec
                MainControlTimer.Tick += new EventHandler(MainControlTimer_Elapsed);
                MainControlTimer.Start();
            }
            catch (Exception e1) { }
            // 디바이스 초기화
            FuncError fErr= FuncError.ERROR_Communication_fails;
            try
            {
                fErr = Bc.Init(BCControl.InitMode.PLC);
            }
            catch (Exception e1)
            {
                Bc.Log.Error("통신포트 초기화 실패 - " + e1.Message);
                fErr = FuncError.ERROR_PLC_Handle_open_fails; 
            }
            if (fErr == FuncError.OK) Led_PLC.Value = true;
            else
            {
                PLCisOK = false;
                Led_PLC.Value = false;
                Bc.Log.Error("PLC 초기화 실패 - 프로그램을 재시작하십시오.");
            }
            FuncError CosmoRet = Bc.Init(BCControl.InitMode.COSMO);
            if (CosmoRet == FuncError.OK) Led_Cosmo.Value = true;
            else
            {
                COSMOisOK = false;
                Led_Cosmo.Value = false;
                Bc.Log.Error("COSMO 통신 포트 및 디바이스 초기화 실패 - 프로그램을 재시작하십시오.");
            }

            //
            FuncError DAQHSInit = Bc.Init(BCControl.InitMode.DAQHIGHSPEED);
            if (DAQHSInit == FuncError.OK) Led_DAQHS.Value = true;
            else
            {
                DAQisOK = false;
                Led_DAQHS.Value = false;
                Bc.Log.Error("DAQ 고속 모드 초기화 실패 - 프로그램을 재시작하십시오.");
            }      
            Bc.PLC_TestEndSet((int)3);
            Bc.PLC_ClearWorkArea(3.0);

            #region MES/LDS/UDP 관련 초기화
            Bc.M_MesLds.LoggerSet(ref Bc.Log);
            bool chkUdp = Bc.M_MesLds.SetUDP(Bc.CurSysConfig.CurSysConfig.UDP_SERVERIP, Bc.CurSysConfig.CurSysConfig.UDP_SERVERPORT);
            Bc.Log.Info("[UDP] Server IP = " + Bc.CurSysConfig.CurSysConfig.UDP_SERVERIP);
            Bc.Log.Info("[UDP] Server Port = " + Bc.CurSysConfig.CurSysConfig.UDP_SERVERPORT);
            Bc.M_MesLds.CreateUDPServer();

            bool chkMes = Bc.M_MesLds.SetMES(Bc.CurSysConfig.CurSysConfig.MES_SERVERIP, Bc.CurSysConfig.CurSysConfig.MES_SERVERPORT);
            Bc.Log.Info("[MES] Server IP = " + Bc.CurSysConfig.CurSysConfig.MES_SERVERIP);
            Bc.Log.Info("[MES] Server Port = " + Bc.CurSysConfig.CurSysConfig.MES_SERVERPORT);

            bool chkLds = Bc.M_MesLds.SetLDS(Bc.CurSysConfig.CurSysConfig.LDS_SERVERIP,Bc.CurSysConfig.CurSysConfig.LDS_SERVERPORT);
            Bc.Log.Info("[LDS] Server IP = " + Bc.CurSysConfig.CurSysConfig.LDS_SERVERIP);
            Bc.Log.Info("[LDS] Server Port = " + Bc.CurSysConfig.CurSysConfig.LDS_SERVERPORT);

            // 기존 PLC에 등록된 제품코드 읽기
            // MES 정보 읽기
            string _MesModelCode = "";
            bool chkModelCode =  Bc.CMD_Read_ModelCode(2.0, ref _MesModelCode);
            Bc.Log.Info("[MES] PLC Model = " + _MesModelCode + " PC Model = " + Bc.CurMesSpec.SPEC.LastProductCode);
            if (chkModelCode)
            {
                if (!string.Equals(_MesModelCode, Bc.CurMesSpec.SPEC.LastProductCode, StringComparison.CurrentCultureIgnoreCase))
                {
                    bool _chk1 = MESUpdate(_MesModelCode, ref Bc.CurMesSpec);
                    Bc.SetAutoZeroResetFlag();
                }
                else
                {
                    Bc.Log.Info("[MES] MES 사양정보 업데이트 안함.");
                    D_ProductCode.Content = _MesModelCode;
                }
            }
            else
            {
                Bc.Log.Info("[MES] PLC에서 모델정보를 읽지 못했습니다.(통신오류) ");
            }
            #endregion


            if ((PLCisOK)&&(DAQisOK)&&(COSMOisOK))   Bc.Log.Info("프로그램이 정상적으로 실행되었습니다.");
            else Bc.Log.Info("프로그램 초기화 실패 - 문제점을 확인 후 다시 실행하십시오.");
            return true;
        }


        private bool BCReset() // 매뉴얼 모드에서 리셋버튼 누를시 동작
        {
            // PLC Memory CLear
            Bc.PLC_ClearWorkArea(3.0);
            return true;
        }
        private bool _LeakTestUI_View = false;

        private int _LogDateCheckInterval = 0; // 날짜가 변경되는 것을 확인하기 위함.
        private BCControl.DAQPM tmp = new BCControl.DAQPM();
        private void MainUITimer_Elapsed(object sender, EventArgs e)
        {

            // 100msec Interval
            if (Bc == null) return;
            tmp = Bc.CurDAQ;
            #region Log Date Check
            // Log 화일 보기 관련 날짜 변경 체크, 10초에 한번씩 화일 변경을 체크함.
            _LogDateCheckInterval++;
            if (_LogDateCheckInterval > 100)
            {
                string NowLogFilePath = System.Environment.CurrentDirectory + "\\Logs\\" + System.DateTime.Now.ToShortDateString() + ".log";
                bool ComResult = NowLogFilePath.Equals(CurLogFilePath, StringComparison.OrdinalIgnoreCase);
                if (!ComResult)
                {
                    Bc.Log.Info("[자동]일자변경으로 로그기능을 다시 시작합니다.");
                    CurLogFilePath = NowLogFilePath;
                    FileMonitors.Clear();
                    var monitorViewModel = new FileMonitorViewModel(NowLogFilePath, GetFileNameForPath(NowLogFilePath), "UTF-8", false);
                    monitorViewModel.Renamed += MonitorViewModelOnRenamed;
                    monitorViewModel.Updated += MonitorViewModelOnUpdated;
                    FileMonitors.Add(monitorViewModel);
                    SelectedItem = monitorViewModel;
                }
                _LogDateCheckInterval = 0;
            }
            #endregion
            #region UI(CPU/HDD/MEMORY)
            // UI(Memory,HDD usage)
            DriveInfo[] allDrives = DriveInfo.GetDrives();

            if (Bc.CurState.isDAQ == false) Led_DAQHS.Value = false;
            CPUUsage.Value = (int)cpuCounter.NextValue();
            MEMUsage.Value = (int)(ramCounter.NextValue() / 29.1);
            HDDUsage.Value = (int)((allDrives[1].TotalSize - allDrives[1].TotalFreeSpace) / (allDrives[1].TotalSize * 0.01));

            CH0.Text       = string.Format("{0:F0}", tmp.Force);
            CH1.Text       = string.Format("{0:F2}", tmp.Displacement);
            CH2.Text       = string.Format("{0:F2}", tmp.PriAir);
            CH3.Text       = string.Format("{0:F2}", tmp.FloAir);
            CH4.Text       = string.Format("{0:F1}", tmp.PriVacuum);
            CH5.Text       = string.Format("{0:F1}", tmp.FloVacuum);
           
            StripStatusUpdate(Bc.CurPLCState);
            #endregion   
        }

        #region 메인 제어 타이머 - 자동/수동/MES/LDS 관련
        /// <summary>
        /// 시험 시작시 LDS 정보저장
        /// 작업일시, 바코드, 제품코드등을 기록
        /// </summary>
        private void dele_LDSStart()
        {
            Bc.CurMesSpec.LocalVar.StartDate = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
            Bc.CurMesSpec.LocalVar.BarCode   = Bc.CurMesSpec.SPEC.ImsiBarCode;
            Bc.CurMesSpec.LocalVar.ModelCode = Bc.CurMesSpec.SPEC.LastProductCode;
        }
        /// <summary>
        /// 시험종료 결과를 LDS 서버에 전송
        /// 네트워크 에러시 로컬에 저장
        /// </summary>
        /// <param name="result"></param>
        /// <param name="resultcode"></param>
        private void dele_LDSStop(bool result, int resultcode)
        {
            if (_IsBarCodeRead != true)
            {
                // 바코드 읽기 모드가 없을 경우 바이패스
                return;
            }
            //string testresultmsg = "|";// 실제 세부 시험 결과 내용 임시적용
            string testresultmsg = "";// 실제 세부 시험 결과 내용 임시적용
            double LostTravel_CutOffHole = Bc.LostTravelTest.MeasurementValue;
            double LostTrvael_PPRI_11    = Bc.LostTravelTest1_1.MeasurementValue;
            
            double LostTravel_Flo        = Bc.LostTravelTest1.MeasurementValue;
            double LostTravel_Flo_1      = Bc.LostTravelTest1_1.MeasurementValue;

            double AirLeak_15_Pri        = Bc.AirLeakTestSpec15Pri.MeasurementValue;
            double AirLeak_15_Flo        = Bc.AirLeakTestSpec15Flo.MeasurementValue;
            double AirLeak_50_Pri        = Bc.AirLeakTestSpec50Pri.MeasurementValue;
            double AirLeak_50_Flo        = Bc.AirLeakTestSpec50Flo.MeasurementValue;

            double PistonMoving_FWD      = Bc.PistonMovingFWDTest.MeasurementValue;
            double PistonMoving_FWD_FullStroke = Bc.PistonMovingFWDTest_1.MeasurementValue;

            double PistonMoving_BWD      = Bc.PistonMovingBWDTest.MeasurementValue;

            double PistonStroke_Pri      = Bc.PistonStrokeTest.MeasurementValue;
            double PistonStroke_Pri_1    = Bc.PistonStrokeTest_1.MeasurementValue;

            double PistonStroke_Flo      = Bc.PistonStrokeVacTest.MeasurementValue;
            double PistonStroke_Flo_1    = Bc.PistonStrokeVacTest_1.MeasurementValue;


            double Vacuum_Pri            = Bc.VacuumTestPri.MeasurementValue;
            double Vacuum_Flo            = Bc.VacuumTestFlo.MeasurementValue;

            testresultmsg = testresultmsg + "LostTravel," + string.Format("{0:F2}", LostTravel_CutOffHole)+"|";
            testresultmsg = testresultmsg + "PPRI_11," + string.Format("{0:F1}", LostTrvael_PPRI_11) + "|";

            testresultmsg = testresultmsg + "PPRI_12," + string.Format("{0:F2}", LostTravel_Flo_1) + "|";
            testresultmsg = testresultmsg + "PSEC_12," + string.Format("{0:F2}", LostTravel_Flo) + "|";

            testresultmsg = testresultmsg + "LPri_11," + string.Format("{0:F2}", AirLeak_15_Pri) + "|";
            testresultmsg = testresultmsg + "Lsec_11," + string.Format("{0:F2}", AirLeak_15_Flo) + "|";

            testresultmsg = testresultmsg + "LPri_12," + string.Format("{0:F2}", AirLeak_50_Pri) + "|";
            testresultmsg = testresultmsg + "LSec_12," + string.Format("{0:F2}", AirLeak_50_Flo) + "|";

            testresultmsg = testresultmsg + "FWD_N," + string.Format("{0:F2}", PistonMoving_FWD) + "|";
            testresultmsg = testresultmsg + "FullStroke," + string.Format("{0:F2}", PistonMoving_FWD_FullStroke) + "|";
            testresultmsg = testresultmsg + "BWD_N," + string.Format("{0:F2}", PistonMoving_BWD) + "|";

            testresultmsg = testresultmsg + "PRI_PistonLength," + string.Format("{0:F2}", PistonStroke_Pri) + "|";
            testresultmsg = testresultmsg + "SEC_PistonLength," + string.Format("{0:F2}", PistonStroke_Pri_1) + "|";

            testresultmsg = testresultmsg + "PRI_Vac," + string.Format("{0:F1}",PistonStroke_Flo) + "|";
            testresultmsg = testresultmsg + "SEC_Vac," + string.Format("{0:F1}", PistonStroke_Flo_1) + "|";

            testresultmsg = testresultmsg + "HOLD_Vac_Pri," + string.Format("{0:F1}", Vacuum_Pri) + "|";
            testresultmsg = testresultmsg + "HOLD_Vac_Sec," + string.Format("{0:F1}", Vacuum_Flo) + "|";

            double[] PMT_Result = new double[15];
            if (_PMT.Count>=15)
            {
                for (int i=0; i<15; i++)
                {
                    PMT_Result[i] = Convert.ToDouble(_PMT[i].CheckN);
                }
            }
            else
            {
                for (int i = 0; i < 15; i++) PMT_Result[i] = 0.0;
            }
            testresultmsg = testresultmsg + "PMT_3,"    + string.Format("{0:F1}", PMT_Result[0]) + "|";
            testresultmsg = testresultmsg + "PMT_5,"    + string.Format("{0:F1}", PMT_Result[1]) + "|";
            testresultmsg = testresultmsg + "PMT_10,"   + string.Format("{0:F1}", PMT_Result[2]) + "|";
            testresultmsg = testresultmsg + "PMT_15,"   + string.Format("{0:F1}", PMT_Result[3]) + "|";
            testresultmsg = testresultmsg + "PMT_20,"   + string.Format("{0:F1}", PMT_Result[4]) + "|";
            testresultmsg = testresultmsg + "PMT_24,"   + string.Format("{0:F1}", PMT_Result[5]) + "|";
            testresultmsg = testresultmsg + "PMT_25,"   + string.Format("{0:F1}", PMT_Result[6]) + "|";
            testresultmsg = testresultmsg + "PMT_26,"   + string.Format("{0:F1}", PMT_Result[7]) + "|";
            testresultmsg = testresultmsg + "PMT_27,"   + string.Format("{0:F1}", PMT_Result[8]) + "|";
            testresultmsg = testresultmsg + "PMT_28,"   + string.Format("{0:F1}", PMT_Result[9]) + "|";
            testresultmsg = testresultmsg + "PMT_29,"   + string.Format("{0:F1}", PMT_Result[10]) + "|";
            testresultmsg = testresultmsg + "PMT_30,"   + string.Format("{0:F1}", PMT_Result[11]) + "|";
            testresultmsg = testresultmsg + "PMT_31,"   + string.Format("{0:F1}", PMT_Result[12]) + "|";
            testresultmsg = testresultmsg + "PMT_32,"   + string.Format("{0:F1}", PMT_Result[13]) + "|";
            testresultmsg = testresultmsg + "PMT_32p5," + string.Format("{0:F1}", PMT_Result[14]) + "|";

            //testresultmsg = "|";
            //testresultmsg = testresultmsg + "LostTravel "
            Bc.CurMesSpec.LocalVar.EndDate = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
            if (result)
            {
                Bc.CurMesSpec.LocalVar.TestResult = "01POK";
            }
            else Bc.CurMesSpec.LocalVar.TestResult = "01PNG";

            Bc.CurMesSpec.LocalVar.TestResult = Bc.CurMesSpec.LocalVar.TestResult + testresultmsg;

            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                string retMsg = "";
                int retCode = 0;
                string retMsg1 = "";
                int retCode1 = 0;
                // 시험 결과 전공
                bool chk = Bc.M_MesLds.LDS_Result(
                                                        Bc.CurMesSpec.LocalVar.BarCode,
                                                        Bc.CurMesSpec.LocalVar.StartDate,
                                                        Bc.CurMesSpec.LocalVar.EndDate,
                                                        Bc.CurMesSpec.LocalVar.TestResult,
                                                        ref retMsg,
                                                        ref retCode
                                                        );
                if (retCode != 91)
                {
                    // 에러 처리....
                }
                // 설비 상태 읽기
                string readPLCStatus = "";
                string LDSPLCStatus = "";
                bool chk3 = Bc.CMD_Read_SystemStatus(0.5, ref readPLCStatus);
                Bc.Log.Info("[MES] PLC -> PC 설비 상태 읽기 = " + readPLCStatus + ", Function = " + chk3.ToString());
                if ((chk3) & (readPLCStatus.Length != 0)) LDSPLCStatus = readPLCStatus;
                else LDSPLCStatus = "0";
                // 설비 상태 전송
                bool chk1 = Bc.M_MesLds.LDS_Status(
                                                        Bc.CurMesSpec.LocalVar.BarCode,
                                                        Bc.CurMesSpec.LocalVar.StartDate,
                                                        Bc.CurMesSpec.LocalVar.EndDate,
                                                        LDSPLCStatus,
                                                        ref retMsg1,
                                                        ref retCode1
                                                        );
            }));
            _IsBarCodeRead = false;
        }
        private void dele_Andon(string msg)
        {
            try
            {
                Char[] delimitersComma = { ',' };
                string[] subwordsSplit = msg.Split(delimitersComma, 17);
                int[] AndonCodes = new int[17];
                for (int i=1; i<subwordsSplit.Length; i++)  // 첫번째 무시
                {
                    bool chk = int.TryParse(subwordsSplit[i],out AndonCodes[i]);
                }
                Bc.CMD_Write_ANDON(AndonCodes,16);
            }
            catch (Exception e1)
            { 
                if (Bc.LogEnable)
                {
                    Bc.Log.Error("[ANDON] Exception " + e1.Message);
                }
                return; 
            }

            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                this.D_MES.Content = "ANDON = "+msg;
            }));



        }
        private bool _ModelChangeRunning = false;
        private bool DO_ModelChange()
        {
            // MES 정보 읽기
            bool chkCom = false;
            string _MesModelCode = "";
            _ModelChangeRunning = true;
            Bc.Log.Info("[MES] PLC->PC 모델 체인지 요청");
            chkCom = Bc.CMD_Read_ModelCode(0.5, ref _MesModelCode);
            Bc.Log.Info("[MES] PLC Model = " + _MesModelCode + "  기존 PC Model = " + Bc.CurMesSpec.SPEC.LastProductCode);

            if (chkCom)
            {

                if (Bc.CMD_Write_ModelBusy(0.5, 1))
                {
                    if (MESUpdate(_MesModelCode, ref Bc.CurMesSpec)) Bc.Log.Info("[MES] PLC->PC 모델 체인지 요청이 정상적으로 완료 되었습니다.");
                    else                                             Bc.Log.Info("[MES] PLC->PC 모델 체인지 에러  = " + _MesModelCode);
                }
                Bc.CMD_Write_ModelResult(0.5, 1);
                Bc.CMD_Write_ModelBusy(0.5, 0);
            }
            else
            {
                Bc.Log.Info("[MES] PLC에서 모델정보를 읽지 못했습니다.(통신오류) ");
            }
            _ModelChangeRunning = false;
            return true;
        }
        /// <summary>
        /// MES에서 해당 모델 사양정보 수신
        /// </summary>
        /// <param name="modelcode"></param>
        /// <param name="messpec"></param>
        /// <returns></returns>
        private bool MESUpdate(string modelcode, ref MESSPEC messpec)
        {
            string barcode   = "";
            string typestr   = modelcode;
            string resultmsg = "";
            int resultcode   = 0;

            if (Bc.CurMesSpec.SPEC.ImsiBarCode == null)
            {
                barcode = "01234567890";
            }
            else
            {
                barcode = Bc.CurMesSpec.SPEC.ImsiBarCode;
            }
            if (barcode.Length != 11) barcode = "01234567890";

            Bc.Log.Info("[MES] 모델 정보 요청");
            Bc.Log.Info("[MES] 모델코드 = " + modelcode);
            bool chk = Bc.M_MesLds.MES_RequestType(barcode, typestr, ref resultmsg, ref resultcode);
            if (!chk)
            {
                Bc.Log.Info("[MES] 모델 정보 요청시 응답이 없습니다.");
            }

            Bc.Log.Info("[MES] 모델 세부 사양 정보 요청 " + modelcode);
            Bc.Log.Info("[MES] 모델 응답 = " + string.Format("{0:D}",resultcode));
            
            // 모델 코드 업데이트 2017-11-02
            if (resultcode==92)
            {
                Char[] delimitersComma = { '|' };
                string[] subwordsSplit = resultmsg.Split(delimitersComma, 15);
                string modelcodenumber = subwordsSplit[0].Replace(modelcode, "");
                Int16 modelnumber = 0;
                Int16.TryParse(modelcodenumber, out modelnumber);
                Bc.CMD_Write_ModelNumber(0.5, modelnumber);
                Bc.Log.Info("[MES] PC -> PLC 모델 코드 전송 = " + string.Format("{0:D}", modelcodenumber));
            }

            chk = Bc.M_MesLds.MES_RequestSpec(barcode, modelcode, ref resultmsg, ref resultcode);
            if (!chk) Bc.Log.Info("[MES] 모델 세부 사양 정보 요청시 응답이 없습니다. = " + modelcode);
            else
            {
                // 업데이트 mes spec
                if (resultcode == 92)
                {
                    bool updatechk = CurSpecUpdate(resultmsg);
                    if (updatechk) Bc.Log.Info("[MES] 사양정보 적용 완료 ");
                    else
                    {

                        Bc.Log.Info("[MES] 사양정보 적용 실패 ");
                    }
                }

            }

            //
            D_ProductCode.Content = modelcode;
            D_BarCode.Content     = barcode;
            return true;
        }
        private bool DO_ChangeANDON(int mode)
        {
            Bc.M_MesLds.UDPSend(mode);
            return true;
        }
        private bool CurSpecUpdate(string msg)
        {
            Char[] delimiters = { '|' };
            Char[] delimitersComma = { ',' };
            double[] minValue = new double[100];
            double[] maxValue = new double[100];
            int _ConversionCount = 0;
            try
            {
                string[] wordsSplit = msg.Split(delimiters, 33);
                for (int i = 1; i < wordsSplit.Length; i++) // 첫번째는 무시
                {
                    string[] subwordsSplit = wordsSplit[i].Split(delimitersComma, 4);
                    string name = subwordsSplit[0];
                    string min = subwordsSplit[2];
                    string nom = subwordsSplit[1];
                    string max = subwordsSplit[3];
                    max = max.Replace("|", "");
                    bool chkminv = double.TryParse(min, out minValue[i]);
                    bool chkmaxv = double.TryParse(max, out maxValue[i]);
                    _ConversionCount++;
                }
            }
            catch (Exception e1)
            {
                if (Bc.LogEnable)
                {
                    Bc.Log.Error("[MES SPEC] Update Exception " + e1.Message);
                    Bc.Log.Error("[MES SPEC] 실 데이터 항목 수 = " + _ConversionCount.ToString());
                }
                // return false;
            }
            //if (_ConversionCount != 32) return false;
            // 내부 테이블(사양) 맵핑
            Bc.CurMesSpec.SPEC.LostTravel_PRI_CutOffHole_Min     = minValue[1];
            Bc.CurMesSpec.SPEC.LostTravel_PRI_CutOffHole_Max     = maxValue[1];
            Bc.CurMesSpec.SPEC.LostTravel_PRI_Sec_Min            = minValue[2];
            Bc.CurMesSpec.SPEC.LostTravel_PRI_Sec_Max            = maxValue[2];

            Bc.CurMesSpec.SPEC.LostTravel_SEC_Pri_Min            = minValue[3];
            Bc.CurMesSpec.SPEC.LostTravel_SEC_Pri_Max            = maxValue[3];
            Bc.CurMesSpec.SPEC.LostTravel_SEC_Sec_Min            = minValue[4];
            Bc.CurMesSpec.SPEC.LostTravel_SEC_Sec_Max            = maxValue[4];

            Bc.CurMesSpec.SPEC.LeakTest_15_Pri_Min               = minValue[5];
            Bc.CurMesSpec.SPEC.LeakTest_15_Pri_Max               = maxValue[5];
            Bc.CurMesSpec.SPEC.LeakTest_15_Sec_Min               = minValue[6];
            Bc.CurMesSpec.SPEC.LeakTest_15_Sec_Max               = maxValue[6];

            Bc.CurMesSpec.SPEC.LeakTest_50_Pri_Min               = minValue[7];
            Bc.CurMesSpec.SPEC.LeakTest_50_Pri_Max               = maxValue[7];
            Bc.CurMesSpec.SPEC.LeakTest_50_Sec_Min               = minValue[8];
            Bc.CurMesSpec.SPEC.LeakTest_50_Sec_Max               = maxValue[8];

            Bc.CurMesSpec.SPEC.PistonMoving_FWD_N_Min            = minValue[9];
            Bc.CurMesSpec.SPEC.PistonMoving_FWD_N_Max            = maxValue[9];
            Bc.CurMesSpec.SPEC.PistonMoving_FullStroke_Min       = minValue[10];
            Bc.CurMesSpec.SPEC.PistonMoving_FullStroke_Max       = maxValue[10];
            Bc.CurMesSpec.SPEC.PistonMoving_BWD_N_Min            = minValue[11];
            Bc.CurMesSpec.SPEC.PistonMoving_BWD_N_Max            = maxValue[11];

            Bc.CurMesSpec.SPEC.PistonStroke_PriLength_Min        = minValue[12];
            Bc.CurMesSpec.SPEC.PistonStroke_PriLength_Max        = maxValue[12];
            Bc.CurMesSpec.SPEC.PistonStroke_SecLength_Min        = minValue[13];
            Bc.CurMesSpec.SPEC.PistonStroke_SecLength_Max        = maxValue[13];

            Bc.CurMesSpec.SPEC.PistonStroke_PriVac_Min           = minValue[14];
            Bc.CurMesSpec.SPEC.PistonStroke_PriVac_Max           = maxValue[14];
            Bc.CurMesSpec.SPEC.PistonStroke_SecVac_Min           = minValue[15];
            Bc.CurMesSpec.SPEC.PistonStroke_SecVac_Max           = maxValue[15];

            Bc.CurMesSpec.SPEC.VacuumHold_Pri_Min                = minValue[16];
            Bc.CurMesSpec.SPEC.VacuumHold_Pri_Max                = maxValue[16];
            Bc.CurMesSpec.SPEC.VacuumHold_Sec_Min                = minValue[17];
            Bc.CurMesSpec.SPEC.VacuumHold_Sec_Max                = maxValue[17];

            if (_ConversionCount <= 17)
            {
                Bc.Log.Info("[MES SPEC] 스펙데이터 내부 구조체 변환 완료(변환 갯수 =  " + _ConversionCount.ToString() + ")");
                return true;  // 스펙 데이터 항목이 17개 이면, 스펙업데이트 전 갯수까지만...
            }

            Bc.CurMesSpec.SPEC.PistonMoving_Table_3_Min = minValue[18];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_3_Max = maxValue[18];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_5_Min = minValue[19];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_5_Max = maxValue[19];

            Bc.CurMesSpec.SPEC.PistonMoving_Table_10_Min = minValue[20];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_10_Max = maxValue[20];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_15_Min = minValue[21];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_15_Max = maxValue[21];

            Bc.CurMesSpec.SPEC.PistonMoving_Table_20_Min = minValue[22];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_20_Max = maxValue[22];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_24_Min = minValue[23];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_24_Max = maxValue[23];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_25_Min = minValue[24];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_25_Max = maxValue[24];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_26_Min = minValue[25];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_26_Max = maxValue[25];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_27_Min = minValue[26];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_27_Max = maxValue[26];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_28_Min = minValue[27];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_28_Max = maxValue[27];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_29_Min = minValue[28];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_29_Max = maxValue[28];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_30_Min = minValue[29];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_30_Max = maxValue[29];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_31_Min = minValue[30];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_31_Max = maxValue[30];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_32_Min = minValue[31];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_32_Max = maxValue[31];

            Bc.CurMesSpec.SPEC.PistonMoving_Table_32p5_Min = minValue[32];
            Bc.CurMesSpec.SPEC.PistonMoving_Table_32p5_Max = maxValue[32];

            string defaultFile = @"PedalForceSpec.txt";
            try
            {
                File.Delete(defaultFile);
                string SpecData = "";
                SpecData = string.Format("3.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_3_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_3_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("5.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_5_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_5_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("10.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_10_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_10_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("15.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_15_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_15_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("20.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_20_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_20_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("24.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_24_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_24_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("25.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_25_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_25_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("26.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_26_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_26_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("27.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_27_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_27_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("28.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_28_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_28_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("29.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_29_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_29_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("30.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_30_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_30_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("31.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_31_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_31_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("32.0, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_32_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_32_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                SpecData = string.Format("32.5, {0:F1}, {1:F1}\r\n", Bc.CurMesSpec.SPEC.PistonMoving_Table_32p5_Min, Bc.CurMesSpec.SPEC.PistonMoving_Table_32p5_Max);
                System.IO.File.AppendAllText(defaultFile, SpecData, Encoding.Default);
                Bc.Log.Info("[MES SPEC] 반력선도 스펙데이터를 로컬 텍스트 화일 변환 완료 " );
            }
            catch (Exception e1)
            {
                Bc.Log.Error("[MES SPEC] 반력선도 스펙데이터를 로컬 텍스트 화일 변환중 에러 발생 : " + e1.Message);
                return false;
            }
            return true;
        }
        private bool _LDS_Check_Running       = false;
        //private bool _MES_Check_ModelChanging = false;
        private bool _LastSystemMode          = false;
        private bool _IsBarCodeRead           = false; // 자동 운전중 PLC측에서 Barcode 읽기 모드를 수행했는지 여부, 시험 종료시 반드시 false할것.
        private bool _PLCModeChange           = false; // PLC에서 수동->자동으로 변경시 모델정보를 읽고 MES에서 업데이트를 수행하기 위한 플래그

        private bool _NGDisplay               = false; // NG시발생시 메세지 표시 팝업이 표시되었는지 여부
        private void NGShow(string msg)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                string curMsg = NGMessage1.Text;
                NGMessage1.Text = curMsg + "\r\n" + msg;
                NGPopup.IsOpen = true;
                _NGDisplay = true;
            }));
        }
        private void NGHide()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                NGPopup.IsOpen = false;
                NGMessage1.Text = "";
                _NGDisplay = false;
            }));
        }

        private bool _OKDisplay = false;
        private void OKShow(string msg)
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                string curMsg = OKMessage1.Text;
                OKMessage1.Text = curMsg + "\r\n" + msg;
                OKPopup.IsOpen = true;
                _OKDisplay = true;
            }));
        }
        private void OKHide()
        {
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new ThreadStart(delegate
            {
                OKPopup.IsOpen = false;
                OKMessage1.Text = "";
                _OKDisplay = false;
            }));
        }

        private void MainControlTimer_Elapsed(object sender, EventArgs e)
        {
            bool   Chk2       = false;
            bool   tmpResult  = false;
            bool   IsLDSJoin  = false;
            string BarCode    = "";
            string ResultMsg  = "";
            int    ResultCode = 0;
            // 100msec Interval
            if (Bc == null) return;
            #region Main Control Loop
            // CONTROL
            try
            {
                //if (Bc.CurState.isPLC != true) return;

                Bc.PLC_CurrentState();
            
                // 설비 상태 정보(안돈)
                if (Bc.CurPLCState.AutomManual != _LastSystemMode)
                {
                    _LastSystemMode = Bc.CurPLCState.AutomManual;
                    if (_LastSystemMode) DO_ChangeANDON(1);
                    else                 DO_ChangeANDON(2);
                }

                #region PLC에서 수동->자동으로 변경되었는지를 확인
                if ((!Bc.CurPLCState.Error) && (!Bc.CurPLCState.AutomManual)) // PLC 에러가 아니고 수동모드일경우 
                {
                    if (_PLCModeChange == true) _PLCModeChange = false;
                    if (_OKDisplay == true) OKHide();
                    if (_NGDisplay == true) NGHide();
                }
                if ((_PLCModeChange == false) && (!Bc.CurPLCState.Error) && (Bc.CurPLCState.AutomManual))
                {
                    // MES 업데이트
                    DO_ModelChange();
                    Bc.SetAutoZeroResetFlag();
                    _PLCModeChange = true;
                }
                #endregion

                // 바코드 정보를 통해 LDS/MES연동 여부를 확인. True일경우 바코드 정보를 LDS에 보내 작업 여부를 확인한다.
                if (Bc.CMD_Read_LDSSignal(ref tmpResult)) IsLDSJoin = tmpResult;

                // 수동모드이고 에러가 없을 경우 PLC에서 모델체인지 여부를 확인함.
                if ((!Bc.CurPLCState.AutomManual)&&(!Bc.CurPLCState.Error))
                {
                    // 에러 없고 매뉴얼 상화
                    int checkModelChange = 0;
                    Bc.CMD_Read_ModelChange(0.5, ref checkModelChange);
                    if ((checkModelChange == 1) && (_ModelChangeRunning == false))
                    {
                        DO_ModelChange();
                        Bc.SetAutoZeroResetFlag();
                    }
                }
                // 매뉴얼이고 리셋버튼시
                if ((Bc.CurPLCState.AutomManual) && (Bc.CurPLCState.Reset))
                {
                    if (_OKDisplay == true) OKHide();
                    if (_NGDisplay == true) NGHide();
                    Bc.PLC_COMMAND_PART_CLEAR();  // PLC 쪽 데이터 클리어
                    if (!Bc.CurState.isDAQ)
                    {
                        FuncError DAQHSInit = Bc.Init(BCControl.InitMode.DAQHIGHSPEED);
                        if (DAQHSInit == FuncError.OK) Led_DAQHS.Value = true;
                        else Led_DAQHS.Value = false;
                    }

                }
                // 자동모드이고 에러가 없고 PLC에서 LDS체크하고 PC내부에서 LDS가 체크 전일때
                if ((Bc.CurPLCState.AutomManual) && (!Bc.CurPLCState.Error) &&(IsLDSJoin)&&(!_LDS_Check_Running))
                {
                    _IsBarCodeRead = true;
                    _LDS_Check_Running = true;
                    bool _chk2 = Bc.CMD_Write_ModelLDSResult(0.2, 0);
                    Bc.Log.Info("[MES] LDS Result Clear = " + _chk2.ToString());
                    bool _chk1 = Bc.CMD_Write_ModelLDSBusy(0.2, 1);
                    Bc.Log.Info("[MES] LDS Busy set = " + _chk1.ToString());
                    Chk2 = Bc.CMD_Read_BarCode(1.0, ref BarCode);
                    Bc.Log.Info("[MES] BarCode(PLC->PC) = " + BarCode);
                    Bc.Log.Info("[MES] BarCode read fuction = " + Chk2.ToString());
                    string modelcode = "";
                    bool _chk5 = Bc.CMD_Read_ModelCode(0.2, ref modelcode);
                    Bc.CurMesSpec.SPEC.LastProductCode = modelcode;
                    if (BarCode.Length == 11)
                    {
                        this.D_BarCode.Content = BarCode;
                        bool _chk = Bc.M_MesLds.LDS_RequestInfomation(BarCode, ref ResultMsg, ref ResultCode);
                        this.D_MES.Content = ResultMsg + " result code=" + string.Format("{0:D}", ResultCode);
                        Bc.CurMesSpec.SPEC.ImsiBarCode = BarCode;
                        if ((ResultMsg.Contains("OK Preprocess") && (!ResultMsg.Contains("NOK Preprocess"))) || (ResultMsg.Contains("Re-operation")))
                        {
                            bool _chk3 = false;
                            if (BarCode.Contains(modelcode))
                            {
                                _chk3 = Bc.CMD_Write_ModelLDSResult(0.2, 1);
                                Bc.Log.Info("[MES] LDS Result Set(1=OK 작업진행,2=NG 작업안함) = 1, fuction = " + _chk3.ToString());
                            }
                            else
                            {
                                NGShow("모델 정보 불일치  " + modelcode + " / " + BarCode);
                                _chk3 = Bc.CMD_Write_ModelLDSResult(0.2, 2);
                                Bc.Log.Info("[MES] LDS Result Set(1=OK,2=NG) = 2, 모델정보 불일치 = " + modelcode + " / " + BarCode);
                            } 
                        }
                        else
                        {
                            NGShow("바코드 알람 : " + ResultMsg);
                            bool _chk3 = Bc.CMD_Write_ModelLDSResult(0.2, 2);
                            Bc.Log.Info("[MES] LDS Result Set(1=OK 작업진행,2=NG 작업안함) = 2, 바코드 알람 = " + ResultMsg);
                            _IsBarCodeRead = false;
                        }
                    }
                    else
                    {
                        this.D_BarCode.Content = (BarCode.Length > 0) ? BarCode : "?";
                        this.D_MES.Content = "BarCode Read Error.";
                        bool _chk3 = Bc.CMD_Write_ModelLDSResult(0.2, 2);
                        Bc.Log.Info("[MES] LDS Result Set(1=OK,2=NG) = 2, fuction = " + _chk3.ToString());
                        Bc.CurMesSpec.SPEC.ImsiBarCode = "99999999999";
                    }
                    bool _chk4 = Bc.CMD_Write_ModelLDSBusy(0.2, 0);
                    Bc.Log.Info("[MES] LDS Busy Clear set = " + _chk1.ToString());
                    _LDS_Check_Running = false;
                }
                if ((Bc.CurPLCState.Loading) && (Bc.CurPLCState.AutomManual) && (Bc.IsStart == false) && (Bc.CurPLCState.LeakTest == false))
                {
                    if (AutomaticThread != null)
                    {
                        Bc.Log.Info("[SYSTEM] AutomaticThread(DoWork) 기존 쓰레드 삭제.");
                        AutomaticThread.Abort();
                        AutomaticThread = null;
                    }
                    Bc.TestOnly_V_Offse = 0.0;
                    if (_OKDisplay == true) OKHide();
                    if (_NGDisplay == true) NGHide();
                    if ((Bc.CurSysConfig.CurSysConfig.PistonMovingDS >= 20.0) && (Bc.CurSysConfig.CurSysConfig.PistonMovingDS <= 33.0 ))
                    {
                        Bc.PistonMovingRepeateDisplacement = Bc.CurSysConfig.CurSysConfig.PistonMovingDS;
                    }
                    else { Bc.PistonMovingRepeateDisplacement = 20.0; }
                        AutomaticThread = new Thread(new ThreadStart(Bc.DoWork));
                    
                    AutomaticThread.Start();

                    Bc.Log.Info("[SYSTEM] AutomaticThread(DoWork) 쓰레드 시작.");
                }
                if ((Bc.CurPLCState.Loading) && (Bc.CurPLCState.AutomManual) && (Bc.IsStart == false) && (Bc.CurPLCState.LeakTest == true))
                {
                    if (!_LeakTestUI_View)
                    {
                        View_LeakTestUI();
                    }
                }
                if (Bc.CurPLCState.LeakTest == false)
                {
                    if (_LeakTestUI_View)
                    {
                        if (SubWIndow != null)
                        {
                            SubWIndow.Close();
                            SubWIndow = null;
                            _LeakTestUI_View = false;
                        }
                    }
                }
                if ((Bc.IsStart) && (Bc.CurPLCState.AutomManual == false))
                {
                    if (AutomaticThread != null)
                    {
                        Bc.RequestStop();
                    }
                }
            }
            catch (Exception e1)
            {
                if (Bc.LogEnable)
                {
                    Bc.Log.Error("[MainControlTimer_Elapsed] Exception " + e1.Message);
                }
            }
            #endregion
        } 
        #endregion
        private void View_LeakTestUI()
        {
            double pheight = this.WorkGrid.ActualHeight;
            double pwidth  = this.WorkGrid.ActualWidth;
            Point xx = new Point(0.0, 0.0);            
            Point  pp      = this.WorkGrid.PointToScreen(xx);
            if (SubWIndow != null)
            {
                SubWIndow.Show();
            }
            else
            {
                SubWIndow = new Window
                {
                    Title = "Self Test",
                    Content = new SelfTestUC(),
                    Height = pheight,
                    Width = pwidth,
                    WindowStyle = System.Windows.WindowStyle.None,
                    Top = pp.Y,
                    Left = pp.X,
                    ResizeMode = System.Windows.ResizeMode.NoResize
                };
                SubWIndow.Show();
            }            
            _LeakTestUI_View = true;
        }

        #region 주요 작업 클래스 및 펑션
        public class BCControl:IDisposable
        {
            #region Disposable
        private bool disposed = false;
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            protected virtual void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        if (Cosmo1.IsOpen) Cosmo1.CloseComm();
                        if (Cosmo2.IsOpen) Cosmo2.CloseComm();
                        Cosmo1 = null;
                        Cosmo2 = null;
                        M_MesLds.Dispose();                       
                    }
                    disposed = true;
                }
            }
            ~BCControl()
            {
                Dispose(false);
            }
        #endregion
            #region 클래스 변수 선언
            #region LOG 관련(2017/09/10 추가)
            public Logger Log = null;
            #endregion
            #region 화면 업데이트 및 메인폼관 데이터 동기 델리게이트
            //public event RealTimeDAQConversionDelegate realtimeDAQconversionCallback;
            public event UIClearDelegate               UIClearCallback;
            //public event LastErrorDelegate             LastErrorCallback;
            public event TestProgressDelegate          TestProgressCallBack;
            public event CosmoGraph1Delegate           CosmoGraph1CallBack;        // 1.5Bar
            public event CosmoGraph1Delegate           CosmoGraph2CallBack;        // 5.0Bar
            public event PistonMovingFWDDelegate       PistonMovingFWDCallBack;    // Piston Moving FWD
            public event PistonMovingBWDDelegate       PistonMovingBWDCallBack;    // Piston Moving BWD
            public event LostTravel1Delegate           LostTravel1CallBack;        // Lost Travel 1
            public event LostTravel2Delegate           LostTravel2CallBack;        // Lost Travel 2
            public event PistonStrokeDelegate          PistonStrokeCallBack;       // Piston Stroke
            public event PistonStrokeVACDelegate       PistonStrokeVACCallBack;    // Piston Stroke VAC
            public event VacuumDelegate                VacuumCallBack;             // Vacuum
            public event Vacuum2Delegate               Vacuum2CallBack;            // Vacuum 2
            public event rtLampDelegate                rtLampCallBack;             // 실시간 램프 표시용
            public event LDS_StartDelegate             LDS_StartCallBack;
            public event LDS_StopDelegate              LDS_StopCallBack;
            public event NGMessage                     NGMsg_CallBack;             // NG 발생시 메세지 표시용
            public event NGMessageHide                 NGMsgHide_CallBack; // NG 팝업화면 숨김
            public event OKMessage                     OKMsg_CallBack;             // OK 발생시 메시지 표시용
            public event OKMessageHide                 OKMsgHide_CallBack;         // OK 팝업화면 숨김  
            #endregion
            #region Leak Test Config 관련
            public BCPT.LeakTestDefine LeakTestConfig = new LeakTestDefine();
            #endregion
            #region 전체 시험 설정화일 관련
            public BCPT.TestSpecification AllTestConfig = new TestSpecification();
            #endregion
            #region NI-DAQ 데이터 수집용 채널 설정용
            private AIChannel Force, Displacement, PriAir, FloAir, PriVacuum, FloVacuum;
            // NI-DAQ Task 관련
            private Task                      inputTask;
            private AnalogMultiChannelReader  analogReader;
            private AsyncCallback             inputCallback;
            private Task                      HSrunningTask;

            public double TestOnly_V_Offse; 
            public struct DAQPM
            {
                public double Force;
                public double Displacement;
                public double PriAir;
                public double FloAir;
                public double PriVacuum;
                public double FloVacuum;
                public DAQPM(double force, double displacement,double priair,double floair,double privacuum,double flovacuum)
                {
                    this.Force        = force;
                    this.Displacement = displacement;
                    this.PriAir       = priair;
                    this.FloAir       = floair;
                    this.PriVacuum    = privacuum;
                    this.FloVacuum    = flovacuum;
                }
            }
            public DAQPM[] pmData1 = new DAQPM[1000000];   // 1Mega = 1,000,000 , 
            private int _pmData1Index = 0;
            public int LastpmData1IndexDataCount
            {
                get { return this._pmData1Index; }
            }            
            private DAQPM  _DAQData     = new DAQPM(0.0,0.0,0.0,0.0,0.0,0.0);
            private bool   _DAQDataSave = false;
            public bool curDAQDataSave
            {
                get { return _DAQDataSave; }
            }
            public DAQPM CurDAQ
            {
                get { return _DAQData; }
                set { _DAQData = value; }
            }
            #endregion
            #region COSMO 관련 변수
            private SerialComm Cosmo1; // RS232 PRI COSMO 연결용
            private SerialComm Cosmo2; // RS232 FLO COSMO 연결용
            public void CosmoSerialAllClose()
            {
                if (Cosmo1!=null)
                {
                    Cosmo1.CloseComm();
                }
                if (Cosmo2 != null)
                {
                    Cosmo2.CloseComm();
                }
            }
            public string      priBuf = System.String.Empty;
            public string      floBuf = System.String.Empty;
            // COSMO 데이터 저장용 배열
            public CosmoDFormat[] priData = new CosmoDFormat[1000];
            public CosmoDFormat[] floData = new CosmoDFormat[1000];
            private int _priDataIndex = 0;
            private int _floDataIndex = 0;
            private void ClearCosmoBufIndex()
            {
                
                this._priDataIndex = 0;
                this._floDataIndex = 0;
            }
            private int PriDataIndexReset
            {
                set { _priDataIndex = 0; }                
            }
            private int FloDataIndexReset
            {
                set { _floDataIndex = 0; }           

            }
            public int LastPriDataCount
            {
                get { return this._priDataIndex; }
            }
            public int LastFloDataCount
            {
                get { return this._floDataIndex; }
            }
            // COSMO D-Format 
            public struct CosmoDFormat // COSMO D Format Struct
            {
                public string TT;             // Test Timer
                public double LV;             // Leak Value
                public double NRDP;           // Notthing Revision Differential Pessure
                public double CQ;             // Compensating Quantity
                public double DP;             // Delta Pressure
                public double TP;             // Test Pressure
                public string TN;             // Test Name
                public int RC;                // Run CH (CH#)
                public CosmoDFormat (string testtimer, double leakvalue,double dp,double cq,double dp2,double tp,string tn,int rc)
                {
                    this.TT = testtimer;
                    this.LV = leakvalue;
                    this.NRDP = dp;
                    this.CQ = cq;
                    this.DP = dp2;
                    this.TP = tp;
                    this.TN = tn;
                    this.RC = rc;
                }
            }
            private CosmoDFormat Cosmo1ST;
            private CosmoDFormat Cosmo2ST;
            #endregion
            // enum 
            public enum CMDRET:int
            {
                DONE             = 0,
                DOING            = 1,
                TIMEOVER         = 2,
                ERROR_WRITE      = -1,
                ERROR_READ       = -2,
                ERROR_RW_COMPARE = -3
            }
            public struct BCSTATE
            {
                public bool isDAQ;       // DAQ init OK?
                public bool isDAQHIGH;   // DAQ High Mode?
                public bool isPLC;       // PLC Handle init OK?
                public bool isCOSMO;     // COSMO Serial port open OK?
                public BCSTATE(bool isdaq,bool isdaqhigh,bool isplc,bool iscosmo)
                {
                    this.isDAQ = isdaq;
                    this.isDAQHIGH = isdaqhigh;
                    this.isPLC = isplc;
                    this.isCOSMO = iscosmo;
                }
            }
            static BCSTATE _state = new BCSTATE(false,false,false,false);
            public BCSTATE CurState
            {
                get{return _state;}
                //set{_state = value;}
            }
            public struct PLCSTATE
            {
                public bool AutomManual;
                public bool Error;
                public bool Reset;
                public bool Loading;
                public bool LeakTest;
                public PLCSTATE (bool automanual,bool error,bool reset, bool loading, bool leaktest)
                {
                    this.AutomManual = automanual;
                    this.Error       = error;
                    this.Reset       = reset;
                    this.Loading     = loading;
                    this.LeakTest    = leaktest;
                }
            }
            static PLCSTATE _Plcstate = new PLCSTATE(false,false,false,false,false);
            public PLCSTATE CurPLCState
            {
                //set{_Plcstate=value;}
                get{return _Plcstate;}
            }
            public int LastSOLData0 = 0x0000; // 마지막 솔 출력 설정값
            public int LastSOLData1 = 0x0000; 
            public enum InitMode : byte
            {
                ALL          = 0x01,
                DAQHIGHSPEED = 0x02,
                DAQSWTRIGGER = 0x04,
                PLC          = 0x08,
                COSMO        = 0x10,
                ETC          = 0x20
            }
            private ActUtlTypeLib.ActUtlTypeClass PLC; // PLC connect Handle
            // PC - PLC alive signal 
            private System.Timers.Timer _PCLive;
            private bool _LastLiveSignal = false;
            // DoWork 쓰레드 관련
            private volatile bool _ShouldStop;
            private volatile bool _IsStart = false;
            private AutoStep _CurAutoStep = AutoStep.Loading_Wait;

            // 2017-10-30 추가
            public MESLDS        M_MesLds     = new MESLDS();          // MES/LDS 연결용 
            public MESSPEC       CurMesSpec   = new MESSPEC();         // 시험 사양 정보 저장용
            public SystemConfig1 CurSysConfig = new SystemConfig1();   // 전체 프로그램 운영 정보 저장용.
            #endregion
            public FuncError Init(InitMode Mode) // 디바이스 초기화,
            {
                bool ret;
                switch (Mode)
                {
                    case InitMode.ALL:
                        break;
                    case InitMode.COSMO:
                        ret = COSMOInit();
                        if (!ret)
                        {
                            _state.isCOSMO = false;
                            return FuncError.ERROR_Communication_fails;
                        }
                        _state.isCOSMO = true;
                        break;
                    case InitMode.DAQSWTRIGGER:
                        break;
                    case InitMode.PLC:
                        ret = PLC_Init();
                        if (!ret)
                        {
                            _state.isPLC = false;
                            return FuncError.ERROR_PLC_Handle_open_fails;
                        }
                        _state.isPLC = true;
                        break;
                    case InitMode.ETC:
                        break;
                    case InitMode.DAQHIGHSPEED:
                        ret = DAQ_HSInit();
                        if (!ret)
                        {
                            _state.isDAQ = false;
                            _state.isDAQHIGH = false;
                            return FuncError.ERROR_DAQ_Highspeed_mode_Initialize_fails;
                        }
                        _state.isDAQ = true;
                        _state.isDAQHIGH = true;
                        break;
                    default:
                        return FuncError.ERROR_Undefined_internal_function_call;
                }
                return FuncError.OK;
            }
            /// <summary>
            /// BCPT 자동운전 제어 중요 로직
            /// </summary>
            public void RequestStop()
            {
                this._ShouldStop = true;
            }
            public bool IsStart
            {
                //set { this._IsStart = value }
                get { return this._IsStart;}
            }
            private bool _TestLastResult = false;
            /// <summary>
            /// 시험 진행중 마지막 OK/NG여부 확인용
            /// </summary>
            public bool TestLastResult { set { this._TestLastResult = value; } get { return this._TestLastResult; } }
            public enum AutoStep:int // 자동운전시 내부 스텝 Enum
            {
                UI_Clear              = 0,
                Loading_Wait          = 1,
                PC_BusySet            = 2,
                Home_Position_Reading = 3,
                AutoZero_Force        = 4,   // Lost Travel 시행전 포스센서의 오토제로 수행(0.5)
                SensorZero_Reading    = 5,   // 현재 측정값을 기준값으로 정하기 위해서.
                Test_Leak_15          = 10,
                Test_Leak_50          = 20,
                Test_Piston_Moving    = 30,
                Test_Load_Travel1     = 40,
                Test_Load_Travel2     = 41,
                Test_Piston_Stroke    = 50,
                Test_Piston_StrokeVAC = 51,  // 2017-07 추가

   
                Test_Vacuum           = 60,
                Test_Vacuum2          = 61,
                Test_End              = 70,
                Request_Stop          = 1000,
                PC_Side_Error         = 10000,
                PLC_Side_Error        = 10001,
                Test_NG_Stop          = 10002 // 이전 시험에서 NG시 종료
            }
            public enum StepFuncRet:int // 내부 스텝진행용 함수의 결과
            {
                Finished     = 0,
                Doing        = 1,
                Error        = -1
            }
            // PLC SOL 명령 cmd 만들기
            public enum SOL : int
            {   // 16bit 16CH 기준으로 작성(워드 포멕으로 변경후 삭제.....)
                CLEAR                     = 0x0000,

                DOPRI_LOWAIR_OPEN           = 0x0001,
                DOPRI_LOWAIR_CLOSE          = 0x0002,
                DOPRI_VACUUMSENSOR_CLOSE    = 0x0004,
                DOPRI_VACUUMSENSOR_OPEN     = 0x0008,
                DOPRI_PROTECT_CLOSE         = 0x0010,
                DOPRI_PROTECT_OPEN          = 0x0020,
                DOPRI_AIRSENSOR_CLOSE       = 0x0040,
                DOPRI_AIRSENSOR_OPEN        = 0x0080,
                DOPRI_HIGHAIR_CLOSE         = 0x0100,
                DOPRI_HIGHAIR_OPEN          = 0x0200,
                DOPRI_NOTDEFINE_0           = 0x0400,
                DOPRI_NOTDEFINE_1           = 0x0800,
                DOPRI_NOTDEFINE_2           = 0x1000,
                DOPRI_NOTDEFINE_3           = 0x2000,

                DOFLO_HIGHAIR_CLOSE         = 0x4000,
                DOFLO_HIGHAIR_OPEN          = 0x8000,

                D1FLO_LOWAIR_OPEN           = 0x0001,
                D1FLO_LOWAIR_CLOSE          = 0x0002,
                D1FLO_VACUUMSENSOR_CLOSE    = 0x0004,
                D1FLO_VACUUMSENSOR_OPEN     = 0x0008,
                D1FLO_PROTECT_CLOSE         = 0x0010,
                D1FLO_PROTECT_OPEN          = 0x0020,
                D1FLO_AIRSENSOR_CLOSE       = 0x0040,
                D1FLO_AIRSENSOR_OPEN        = 0x0080,
                D1FLO_NOTDEFINE_0           = 0x0100,
                D1FLO_NOTDEFINE_1           = 0x0200,
                D1FLO_NOTDEFINE_2           = 0x0400,
                D1FLO_NOTDEFINE_3           = 0x0800

            }
            // PLC CLAMP 명령 cmd 만들기
            public enum CLAMP : int
            {
                CLEAR                  = 0x00000000,
                WORKCLAMP_BWD          = 0x00000001,
                WORKCLAMP_FWD          = 0x00000002,

                FLOINLETSEALING_UP     = 0x00000010,   // 어드레스 다시 확인요망
                FLOINLETSEALING_DOWN   = 0x00000020,
                PRINLETSEALING_UP      = 0x00000040,
                PRIINLETSEALING_DOWN   = 0x00000080,

                SUCTION_BWD            = 0x00000100,
                SUCTION_FWD            = 0x00000200,

                OUTLET_BWD             = 0x00001000,
                OUTLET_FWD             = 0x00002000,
                MARKING_DOWN           = 0x00004000,
                MAKING_UP              = 0x00008000,
            }
            private int    _AfterLoadingHomePosition = 0;          // 최초 제품이 로딩후 서보 홈위치.
            public int AfterLoadingHomePosition { get { return this._AfterLoadingHomePosition; } }
            private double HomeDisplacement          = -100.0;     // Displacement 센서로 부터 홈위치 값을 저장
            public double GetHomeDisplacement
            {
                get { return this.HomeDisplacement; }
            }
            private double _CutOffHolePositionPri     = -100.0;     // Lost Travel에서 검색한 컷오프홀 위치(PRI.DAQ)
            public double CutOffHolePositionPri
            { 
                set { this._CutOffHolePositionPri = value; }
                get { return this._CutOffHolePositionPri; }
            }
            private double _CutOffHolePositionFlo     = -100.0;     // Lost Travel에서 검색한 컷오프홀 위치(FLO.DAQ)
            public double CutOffHolePositionFlo
            {
                set { this._CutOffHolePositionFlo = value; }
                get { return this._CutOffHolePositionFlo; }
            }
            private double _FullStroke = 32.0;     // Lost Travel에서 검색한 컷오프홀 위치(FLO.DAQ)
            public double FullStroke
            {
                set { this._FullStroke = value; }
                get { return this._FullStroke; }
            }
            //private double PLCCutOffHolePosition     = -100.0;     // Lost Travel에서 검색한 컷오프홀 위치(PLC 기준으로 변환된 값저장)
            private double ForceZero                 = -100.0;     // 포스 센서 오프셋 값
            /// <summary>
            /// 자동 모드에서만 호출할것
            /// </summary>
            public void ForceSensorAutoZero()
            {
                // 자동모드에서만 호출 할것
                // 정압 및 부압 솔을 열어 압력를 제로로 만듬
                Bc.LastSOLData0 = 0x0000;
                Bc.LastSOLData1 = 0x0000;

                Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
                Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                Bc.PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 3.0);
                for (int i = 0; i < 1000; i++)
                {
                    Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                                              new Action(delegate { }));
                }
                // AUTO ZERO
                try
                {
                    using (Task digitalWriteTask = new Task())
                    {
                        //  Create an Digital Output channel and name it.
                        digitalWriteTask.DOChannels.CreateChannel("Dev1/port0", "port0", ChannelLineGrouping.OneChannelForAllLines);
                        DigitalSingleChannelWriter writer = new DigitalSingleChannelWriter(digitalWriteTask.Stream);
                        writer.WriteSingleSamplePort(true, (UInt32)0x0000FFFF);
                        for (int i = 0; i < 500; i++)
                        {
                            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                                                      new Action(delegate { }));
                        }
                        writer.WriteSingleSamplePort(true, (UInt32)0x00000000);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                Bc.LastSOLData0 = 0x0000;
                Bc.LastSOLData1 = 0x0000;

                Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
                Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                Bc.PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 3.0);

            }
            // 오토 리셋을 위한 조건........
            private int     _AutoResetCount     = 0;
            private bool    _FirstAutoReset     = false;
            // 외부에서 오토제로 기능을 강제로 수행시 호출(모델 변경시)
            public void SetAutoZeroResetFlag ()
            { 
                this._FirstAutoReset = false; 
            }
            const int       AutoResetCount      = 500;                        // 10번에 한번씩 리셋
            // 로그 기록 여부
            public bool     LogEnable           = true;            
            private int     _ImsiTestIndex      = 0; 
            public string   IMSI_DataFullPath   = "";
            public TestTime _SubTestTimer       = new TestTime();            // 각 단계별 시험의 시간 측정용      

            public double PistonMovingRepeateDisplacement = 20.0;
            public void DoWork()  // 자동운전 메인 로직
            {
                
                this._ShouldStop    = false;
                this._IsStart       = true;
                _CurAutoStep        = AutoStep.UI_Clear;  // 자동운전 최초 시작시 UI 정리 부터.
                int HomePos,HomeSpd;                      // 최초 로딩후 시작시 홈위치 및 속도 값을 PLC에서 읽어 저장함(속도는 필요없음)
                double HomeDisplacement;                  // Displacement 센서로 부터 홈위치 값을 저장
                string errmsg="";
                SubStep step             = SubStep.Init;
                SubStep LastDisplayStep  = SubStep.Sequence_End;
                AutoStep LastCurAutoStep = AutoStep.UI_Clear;
                StepFuncRet ret          = StepFuncRet.Finished;
                CMDRET cret              = CMDRET.ERROR_READ;
                AutoStep _LastAutoStep   = AutoStep.Loading_Wait;
                int              _ngCode = 0;
                #region Automatice Loop 
                while (_IsStart)
                {
                    Thread.Sleep(1);

                    if (_ShouldStop) _CurAutoStep = AutoStep.Request_Stop;                    
                    switch (_CurAutoStep)
                    {
                        case AutoStep.UI_Clear: // UI Clear
                            //Delay(10);
                            this._TestLastResult = true;  // 프로그램 진행시 마지막 시험 결과 값 확인용.
                            _CurAutoStep = AutoStep.Loading_Wait;
                            break;
                        case AutoStep.Loading_Wait:
                            #region 제품 로딩 완료 체크 및 대기
                            Delay(100);
                            cret = PLC_CurrentState();
                            if (cret == CMDRET.DONE)
                            {
                                if (_Plcstate.Loading==true)
                                {
                                    UIClearCallback();
                                    ClearCosmoBufIndex(); // Index init(COSMO RS232 RecBuf Index)
                                    _DAQDataSave   = false;                       // DAQ Data Save
                                    _pmData1Index  = 0;                           // DAQ Data Count Index reset
                                    if (LogEnable)  Bc.Log.Info("자동운전 시작");                                    
                                    // 로우 데이터 저장 전체 경로 및 서두 수정
                                    string RootPath   = "D:\\Data\\";
                                    string NowDate    = DateTime.Now.ToShortDateString();
                                    string pathString = System.IO.Path.Combine(RootPath,NowDate);
                                    System.IO.Directory.CreateDirectory(pathString);
                                    string fileName   = "IMSI_"+string.Format("{0:N3}",_ImsiTestIndex)+"_";
                                    IMSI_DataFullPath = System.IO.Path.Combine(pathString, fileName);
                                    if (LogEnable) Bc.Log.Info("임시 데이터 저장 관련 화일이름(해드파트):" + IMSI_DataFullPath);
                                    _ImsiTestIndex++;
                                    FirstTime();
                                    LastTime();                                    
                                    LDS_StartCallBack();
                                    _CurAutoStep = AutoStep.PC_BusySet;
                                    break;
                                }
                            }
                            break;
                            #endregion
                        case AutoStep.PC_BusySet:
                            #region PC->PLC Busy 신호 전송
                            PLC_BusySetting();
                            CMDRET cret1 = PLC_BusySettingRead(3.0);
                            CMDRET cret2 = PLC_ClampSetting(CLAMP.CLEAR, 3.0);
                            Bc.LastSOLData0 = 0x0000;
                            Bc.LastSOLData1 = 0x0000;
                            bool cret3 = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);
                            //CMDRET cret3 = PLC_SolSetting(SOL.CLEAR, 3.0);
                            if ((cret1==CMDRET.DONE)&&(cret2==CMDRET.DONE)&&(cret3==true))
                            {
                                LastTime();
                                _CurAutoStep = AutoStep.Home_Position_Reading;                                
                            }
                            else
                            {
                                if (LogEnable) Bc.Log.Error("[PC Busy Set] PC -> PLC BUSY 설정중 통신 에러");
                                //LastErrorCallback("[READY]PC Busy setting timeover.","");
                                NGMsg_CallBack("[PC] PC->PLC Busy 신호전송중 통신 에러가 발생하였습니다. 네트워크 연결 확인후 프로그램을 재시작 하십시오.");
                                _CurAutoStep = AutoStep.PC_Side_Error;
                            }
                            break;
                            #endregion
                        case AutoStep.Home_Position_Reading:
                            #region PLC 서보 현 위치 읽기
                            Delay(10);
                            cret = PLC_ServoPositionGet(out HomePos, out HomeSpd, 3.0);
                            Delay(10);
                            if (cret == CMDRET.DONE)
                            {
                                HomeDisplacement          = this._DAQData.Displacement;
                                _AfterLoadingHomePosition = HomePos;
                                _curstep = SubStep.Init;  // 최초 호출시 반드시 리셋모드로 호출!!!!!!!!
                                ClearCosmoBufIndex();     // Index init(COSMO RS232 RecBuf Index)
                                _CurAutoStep = AutoStep.AutoZero_Force;
                            }
                            else
                            {
                                if (LogEnable) Bc.Log.Error("[Home Position Read] PC -> PLC 서보 현 위치 읽기중 통신 에러");
                                //LastErrorCallback("[READY]PC Home position reading timeover.","");
                                NGMsg_CallBack("[PC] PLC에서 서보모터 현 위치정보를 읽는 도중 통신 에러가 발생하였습니다. 네트워크 연결 확인후 프로그램을 재시작 하십시오.");
                                _CurAutoStep = AutoStep.PC_Side_Error;
                            }
                            break;
                            #endregion
                        case AutoStep.AutoZero_Force:
                            #region 최초 혹은 10번에 한번씩 오토제로 기능 수행
                            if (!_FirstAutoReset)
                            {
                                ForceSensorAutoZero();
                                HomeDisplacement = this._DAQData.Displacement;
                                Delay(100);
                                ForceZero = this._DAQData.Force;
                                _FirstAutoReset = true;
                                if (LogEnable) Bc.Log.Info("[DAQ Auto Zero] 오토 제로 수행 완료");                            
                            }
                            if (_AutoResetCount > AutoResetCount)
                            {
                                ForceSensorAutoZero();
                                HomeDisplacement = this._DAQData.Displacement;
                                Delay(100);
                                ForceZero = this._DAQData.Force;
                                _FirstAutoReset = true;
                                _AutoResetCount = 0;
                                if (LogEnable) Bc.Log.Info("[DAQ Auto Zero] 오토 제로 수행 완료");    
                            }                            
                            _AutoResetCount++;                            
                            _CurAutoStep = AutoStep.Test_Load_Travel1;
                            break;
                            #endregion
                        case AutoStep.Test_Load_Travel1:
                            #region 시험 : 로스트 트레블 PRI(CutOffHole 측정)
                            ret = Test_LostTravel_1(_LostTravelSpec, out step, out errmsg);
                            if (ret == StepFuncRet.Error)
                            {
                                _LastAutoStep = _CurAutoStep;
                                NGMsg_CallBack("[시험] 로스트 트레블 PRI 시험중 " + errmsg + " 가 발생하였습니다.");
                                if (LogEnable) Bc.Log.Error("[Lost Travel PRI - " + step.ToString() + "] " + errmsg);    
                                _CurAutoStep = AutoStep.PC_Side_Error;
                                _ngCode = 11;
                            }
                            if (ret == StepFuncRet.Finished)
                            {
                                _curstep = SubStep.Init;
                                LastTime();
                                Delay(100);
                                if (this._TestLastResult) _CurAutoStep = AutoStep.Test_Load_Travel2;
                                else
                                {
                                    NGMsg_CallBack("[시험] 로스트 트레블 PRI 시험에서 스펙 NG.");
                                    _ngCode = 10;
                                    _CurAutoStep = AutoStep.Test_NG_Stop;
                                }
                            }
                            break;
                            #endregion
                        case AutoStep.Test_Load_Travel2:
                            #region 시험 : 로스트 트레블 FLO
                            ret = Test_LostTravel_2(_LostTravelSpec, out step, out errmsg);
                            if (ret == StepFuncRet.Error)
                            {
                                _LastAutoStep = _CurAutoStep;
                                NGMsg_CallBack("[시험] 로스트 트레블 FLO 시험중 " + errmsg + " 가 발생하였습니다.");
                                if (LogEnable) Bc.Log.Error("[Lost Travel FLO - " + step.ToString() + "] " + errmsg);
                                _ngCode = 21;
                                _CurAutoStep = AutoStep.PC_Side_Error;
                            }
                            if (ret == StepFuncRet.Finished)
                            {
                                _curstep = SubStep.Init;
                                LastTime();
                                Delay(100);
                                if (this._TestLastResult) _CurAutoStep = AutoStep.Test_Leak_15;
                                else
                                {
                                    NGMsg_CallBack("[시험] 로스트 트레블 FLO 시험에서 스펙 NG.");
                                    _ngCode = 20;
                                    _CurAutoStep = AutoStep.Test_NG_Stop;
                                }
                            }
                            break;
                            #endregion
                        case AutoStep.Test_Leak_15:
                            #region 시험 : 1.5바 리크 테스트
                            ret = Test_Leak_15(out step, out errmsg);
                            if (ret == StepFuncRet.Error)
                            {
                                _LastAutoStep = _CurAutoStep;
                                NGMsg_CallBack("[시험] 1.5바 리크 시험중 " + errmsg + " 가 발생하였습니다.");
                                if (LogEnable) Bc.Log.Error("[1.5바 리크 시험 - " + step.ToString() + "] " + errmsg);
                                _ngCode = 31;
                                _CurAutoStep = AutoStep.PC_Side_Error;
                            }
                            if (ret == StepFuncRet.Finished)
                            {
                                _curstep = SubStep.Init;
                                ClearCosmoBufIndex(); // Index init(COSMO RS232 RecBuf Index)
                                LastTime();
                                Delay(100);
                                if (this._TestLastResult) _CurAutoStep = AutoStep.Test_Leak_50;
                                else
                                {
                                    NGMsg_CallBack("[시험] 1.5바 리크 시험에서 스펙 NG.");
                                    _ngCode = 30;
                                    _CurAutoStep = AutoStep.Test_NG_Stop;
                                }
                            }                         
                            break;
                            #endregion
                        case AutoStep.Test_Leak_50:
                            #region 시험 : 5.0바 리크 테스트
                            ret = Test_Leak_50(out step, out errmsg);
                            if (ret == StepFuncRet.Error)
                            {
                                _LastAutoStep = _CurAutoStep;
                                NGMsg_CallBack("[시험] 5.0바 리크 시험중 " + errmsg + " 가 발생하였습니다.");
                                if (LogEnable) Bc.Log.Error("[5.0바 리크 시험 - " + step.ToString() + "] " + errmsg);
                                _ngCode = 41;
                                _CurAutoStep = AutoStep.PC_Side_Error;
                            }
                            if (ret == StepFuncRet.Finished)
                            {
                                _curstep = SubStep.Init;
                                LastTime();
                                Delay(100);
                                if (this._TestLastResult) _CurAutoStep = AutoStep.Test_Piston_Moving;
                                else
                                {
                                    NGMsg_CallBack("[시험] 5.0바 시험에서 스펙 NG.");
                                    _ngCode = 40;
                                    _CurAutoStep = AutoStep.Test_NG_Stop;
                                }
                            }
                            break;
                            #endregion
                        case AutoStep.Test_Piston_Moving:
                            #region 시험 : 피스톤 무빙
                            ret = Test_PistonMoving(_PistonMovingSpec,out step, out errmsg);
                            if (ret == StepFuncRet.Error)
                            {
                                _LastAutoStep = _CurAutoStep;
                                NGMsg_CallBack("[시험] 피스톤 무빙 시험중 " + errmsg + " 가 발생하였습니다.");
                                if (LogEnable) Bc.Log.Error("[Piston Moving Test] - " + step.ToString() + "] " + errmsg);
                                _ngCode = 51;
                                _CurAutoStep = AutoStep.PC_Side_Error;
                            }
                            if (ret == StepFuncRet.Finished)
                            {
                                _curstep = SubStep.Init;
                                LastTime();
                                Delay(100);
                                if (this._TestLastResult) _CurAutoStep = AutoStep.Test_Piston_StrokeVAC;  //2018-02-28 피스톤 스크로크 공압시험 안함
                                else
                                {
                                    NGMsg_CallBack("[시험] 피스톤 무빙 시험에서 스펙 NG.");
                                    _ngCode = 50;
                                    _CurAutoStep = AutoStep.Test_NG_Stop;
                                }
                            }
                            break;
                            #endregion
                        case AutoStep.Test_Piston_Stroke:
                            #region 시험 : 피스톤 스트로크 공압
                            
                            ret = Test_PistonStroke(_PistonStrokeSpec, out step, out errmsg);
                            if (ret == StepFuncRet.Error)
                            {
                                _LastAutoStep = _CurAutoStep;
                                NGMsg_CallBack("[시험] 피스톤 스트로크 공압 시험중 " + errmsg + " 가 발생하였습니다.");
                                if (LogEnable) Bc.Log.Error("Piston Stroke 5.0bar - " + step.ToString() + "] " + errmsg);
                                _ngCode = 61;
                                _CurAutoStep = AutoStep.PC_Side_Error;
                            }
                            if (ret == StepFuncRet.Finished)
                            {
                                _curstep = SubStep.Init;
                                LastTime();
                                Delay(100);
                                if (this._TestLastResult) _CurAutoStep = AutoStep.Test_Piston_StrokeVAC;
                                else
                                {
                                    NGMsg_CallBack("[시험] 피스톤 스트로크 공압 시험에서 스펙 NG.");
                                    _ngCode = 60;
                                    _CurAutoStep = AutoStep.Test_NG_Stop;
                                }
                            }
                            break;
                            #endregion
                        case AutoStep.Test_Piston_StrokeVAC:
                            #region 시험 : 피스톤 스트로크 진공
                            ret = Test_PistonStrokeVAC(_PistonStrokeSpec, out step, out errmsg);
                            if (ret == StepFuncRet.Error)
                            {
                                _LastAutoStep = _CurAutoStep;
                                NGMsg_CallBack("[시험] 피스톤 스트로크 진공 시험중 " + errmsg + " 가 발생하였습니다.");
                                if (LogEnable) Bc.Log.Error("[Piston Stroke 500mmHg - " + step.ToString() + "] " + errmsg);
                                _ngCode = 71;
                                _CurAutoStep = AutoStep.PC_Side_Error;
                            }
                            if (ret == StepFuncRet.Finished)
                            {
                                _curstep = SubStep.Init;
                                LastTime();
                                Delay(500);
                                if (this._TestLastResult)
                                {

                                    _CurAutoStep = AutoStep.Test_End;
                                }
                                else
                                {
                                    NGMsg_CallBack("[시험] 피스톤 스트로크 진공 시험에서 스펙 NG.");
                                    _ngCode = 70;
                                    _CurAutoStep = AutoStep.Test_NG_Stop;
                                }
                            }
                            break;
                            #endregion
                        case AutoStep.Test_Vacuum:
                            #region 시험 : 진공 시험 PRI
                            double voo = Bc.TestOnly_V_Offse;
                            voo = Bc.TestOnly_V_Offse;
                            ret = Test_Vacuum(_VacuumSpec, out step, out errmsg,voo);
                            if (ret == StepFuncRet.Error)
                            {
                                _LastAutoStep = _CurAutoStep;
                                NGMsg_CallBack("[시험] 진공 PRI 시험중 " + errmsg + " 가 발생하였습니다.");
                                if (LogEnable) Bc.Log.Error("[Vacuum PRI - " + step.ToString() + "] " + errmsg);
                                _ngCode = 81;
                                _CurAutoStep = AutoStep.PC_Side_Error;
                            }
                            if (ret == StepFuncRet.Finished)
                            {
                                _curstep = SubStep.Init;
                                LastTime();
                                Delay(100);
                                if (this._TestLastResult) _CurAutoStep = AutoStep.Test_Vacuum2;
                                else
                                {
                                    NGMsg_CallBack("[시험] 진공 PRI 시험에서 스펙 NG.");
                                    _CurAutoStep = AutoStep.Test_NG_Stop;
                                    _ngCode = 80;
                                }                                
                            }
                            break;
                            #endregion
                        case AutoStep.Test_Vacuum2:
                            #region 시험 : 진공 시험 FLO
                            double voo2 = Bc.TestOnly_V_Offse;
                            voo2 = Bc.TestOnly_V_Offse;
                            ret = Test_Vacuum2(_VacuumSpec, out step, out errmsg, voo2);
                            if (ret == StepFuncRet.Error)
                            {
                                _LastAutoStep = _CurAutoStep;
                                NGMsg_CallBack("[시험] 진공 FLO 시험중 " + errmsg + " 가 발생하였습니다.");
                                if (LogEnable) Bc.Log.Error("[Vacuum FLO - " + step.ToString() + "] " + errmsg);
                                _ngCode = 91;
                                _CurAutoStep = AutoStep.PC_Side_Error;
                            }
                            if (ret == StepFuncRet.Finished)
                            {
                                _curstep = SubStep.Init;
                                LastTime();
                                Delay(100);
                                if (this._TestLastResult) _CurAutoStep = AutoStep.Test_End;
                                else
                                {
                                    NGMsg_CallBack("[시험] 진공 FLO 시험에서 스펙 NG.");
                                    _ngCode = 90;
                                    _CurAutoStep = AutoStep.Test_NG_Stop;
                                }
                            }
                            break;
                            #endregion
                        case AutoStep.Test_End:
                            Bc.LastSOLData0 = 0x0000;
                            Bc.LastSOLData1 = 0x0000;

                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                            PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                            Delay(300);
                            
                            Bc.LastSOLData0 = 0x0000;
                            Bc.LastSOLData1 = 0x0000;

                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                            PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);
    
                            // 최종 결과 확인
                            int  ErrorCount = 0;
                            if (Bc.LostTravelTest.Result != TESTRESULT.OK)       { ErrorCount++; _ngCode = 10; }
                            if (Bc.LostTravelTest1.Result != TESTRESULT.OK)      { ErrorCount++; _ngCode = 20; }
                            if (Bc.AirLeakTestSpec15Pri.Result != TESTRESULT.OK) { ErrorCount++; _ngCode = 30; }
                            if (Bc.AirLeakTestSpec15Flo.Result != TESTRESULT.OK) { ErrorCount++; _ngCode = 30; }
                            if (Bc.AirLeakTestSpec50Pri.Result != TESTRESULT.OK) { ErrorCount++; _ngCode = 40; }
                            if (Bc.AirLeakTestSpec50Flo.Result != TESTRESULT.OK) { ErrorCount++; _ngCode = 40; }
                            if (Bc.PistonMovingFWDTest.Result != TESTRESULT.OK)  { ErrorCount++; _ngCode = 50; }
                            if (Bc.PistonMovingBWDTest.Result != TESTRESULT.OK)  { ErrorCount++; _ngCode = 60; }
                            if (Bc.PistonStrokeTest.Result != TESTRESULT.OK)     { ErrorCount++; _ngCode = 70; }
                            //if (Bc.VacuumTestPri.Result != TESTRESULT.OK)        { ErrorCount++; _ngCode = 80; }
                            //if (Bc.VacuumTestFlo.Result != TESTRESULT.OK)        { ErrorCount++; _ngCode = 90; }



                            if (ErrorCount > 0)
                            {
                                PLC_TestEndSet(2); //NG Set & Finished
                                PLC_TestEndNGCodeSet(_ngCode);
                                if (LogEnable) Bc.Log.Info("[시험종료] NG");
                                LDS_StopCallBack(false, 0);
                            }
                            else
                            {
                                PLC_TestEndSet(1);
                                PLC_TestEndNGCodeSet(0);
                                OKMsg_CallBack("시험이 완료되었습니다. 제품을 취출 하십시오");
                                if (LogEnable) Bc.Log.Info("[시험종료] OK");
                                LDS_StopCallBack(true, 0);
                            }
                            _DAQDataSave = false;
                            _CurAutoStep = AutoStep.Request_Stop; // 쓰레드 완전 종료...
                            break;
                        case AutoStep.Test_NG_Stop: //이전 시험에서 NG시 종료
                            PLC_ServoJogStop(1.0);
                            _DAQDataSave      = false;
                            Bc.TestLastResult = false;
                            PLC_TestEndSet(2);
                            PLC_TestEndNGCodeSet(_ngCode);
                            if (LogEnable) Bc.Log.Info("[시험종료] NG(NG Stop)");
                            LDS_StopCallBack(false, -1);
                            _CurAutoStep = AutoStep.Request_Stop;
                            break;
                        case AutoStep.PC_Side_Error:
                            PLC_TestEndSet(2);            //NG Set & Finished
                            PLC_TestEndNGCodeSet(_ngCode);
                            PLC_ServoJogStop(1.0);
                            _DAQDataSave      = false;    // DAQ Data Save
                            Bc.TestLastResult = false;
                            if (LogEnable) Bc.Log.Info("[시험종료] NG(PC Side Error)");
                            LDS_StopCallBack(false, -1);
                            _CurAutoStep = AutoStep.Request_Stop;                          
                            break;
                        case AutoStep.Request_Stop:
                            _IsStart = false;
                            break;
                    }
                    if ((step != LastDisplayStep)|(_CurAutoStep != LastCurAutoStep))
                    {
                        if (_CurAutoStep!=AutoStep.PC_Side_Error)   TestProgressCallBack(_CurAutoStep.ToString(), step.ToString());
                        else                                        TestProgressCallBack(_LastAutoStep.ToString()+"/"+step.ToString(), errmsg);
                        LastDisplayStep = step;
                        LastCurAutoStep = _CurAutoStep;
                    }
                }
                #endregion
                
                Bc.Log.Info("[SYSTEM] DoWork Thread 종료");
            }
            public enum SubStep : int 
            {
                Init                         = 0,
                Sol_Set                      = 1,
                Clamp_Set                    = 2,
                Sol_Set2                     = 501,
                Clamp_Set2                   = 502,
                Cosmo_CH_Set                 = 3,
                Cosmo_RUN_Set                = 4,
                Cosmo_STOP_Set               = 5,
                Cosmo_STOP_Check             = 6,
                Cosmo_Charge_Run             = 61,
                Cosmo_Charge_Check           = 62,
                Cosmo_Charge_Stop            = 63,
                Cosmo_Data_View              = 7,
                Servo_Now_Position_Get       = 8,
                Servo_Speed_set              = 9,
                Servo_Position_set           = 10,
                Servo_Jog_Forward_Speed_Set  = 11,
                Servo_Jog_Forward_Run        = 12,
                Servo_Jog_Forward_Stop       = 13,
                Servo_Home_Backward_Set      = 14,
                Servo_Home_Backward_Run      = 15,
                Servo_Jog_Backward_Speed_Set = 16,
                Servo_Forward_Pressure_Check = 17,
                DAQ_HighSpeed_Check          = 18, 
                Servo_Jog_Forwarding         = 19,
                Servo_Jog_Forwarding_Check   = 20,
                Servo_Jog_Forwarding_Check2  = 201,
                Servo_Jog_Forwarding_Result  = 21,
                Servo_Jog_Backwarding        = 22,
                Servo_Jog_Backwarding_Check  = 23,
                Servo_Jog_Backwarding_Result = 24,
                Repeat_Sqeuence_Check        = 25,
                Servo_Goto_Home              = 26,
                Servo_Goto_Position          = 27,
                Lost_Travel_Check            = 28,
                Piston_Stroke_Check          = 29,
                Piston_Stroke_Check2         = 30,
                Piston_Stroke_Check22        = 302,
                Piston_Stroke_Check3         = 301,
                Piston_Stroke_Sol_Set        = 31,
                Vacuum_Sol_Set               = 32,
                Vacuum_Check                 = 33,
                Vacuum_Check2                = 34,
                Vacuum_Clamp_Set             = 35,
                Vacuum_Check3                = 36,
                Servo_CurOffHole_Go          = 37,  // 컷오프홀까지 전진
                Servo_CurOffHole_Go_Check    = 38,  // 완료 체크

                Servo_FreeRun_Forward        = 39,
                Servo_FreeRun_Forward_Check  = 40,
                Servo_FreeRun_Backward       = 41,
                Servo_FreeRun_Backward_Check = 42,

                Sequence_End      = 100,
                Error             = -1
            }
            private SubStep _curstep;

            //  세부 시험별 시퀜스^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
            private StepFuncRet Test_Leak_15( out SubStep curstep, out string errmsg)
            {
                errmsg = "";
                //SOL Sol;
                CLAMP Clamp;
                CMDRET ret;
                SubStep LastStep = SubStep.Init;
                switch (_curstep)
                {
                    case SubStep.Init:
                        _SubTestTimer.Start(TestTime.TESTNAME.LEAKTEST_AIR_15);
                        _curstep = SubStep.Servo_CurOffHole_Go;
                        ClearCosmoBufIndex();
                        break;
                    case SubStep.Servo_CurOffHole_Go:
                        LastStep = _curstep;
                        int TargetPos = _AfterLoadingHomePosition + (int)(7.0 * 10.0);
                        ret = PLC_ServoHomeGo(TargetPos, 150, 10.0); 
                        if (ret == CMDRET.DONE) _curstep = SubStep.Sol_Set;
                        else
                        {
                            _curstep = SubStep.Error; errmsg = "PLC 7mm 전진 이동 안됨";
                        }
                        break;
                    case SubStep.Sol_Set:
                        LastStep = _curstep;
                        // 정압 테스트 솔
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        bool _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                        if (_Chk) _curstep = SubStep.Clamp_Set;
                        else      _curstep = SubStep.Error;
                        break;
                    case SubStep.Clamp_Set:
                        LastStep = _curstep;
                        Clamp = CLAMP.CLEAR;
                        Clamp = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_DOWN | CLAMP.PRIINLETSEALING_DOWN | CLAMP.SUCTION_FWD | CLAMP.OUTLET_FWD;
                        ret = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_CH_Set;
                        else                    _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_CH_Set:
                        LastStep = _curstep;
                        ret = PLC_CosmoChSetting(0, 0, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_RUN_Set;
                        else                    _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_RUN_Set:
                        LastStep = _curstep;
                        ret = PLC_CosmoRunSetting(3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_STOP_Set;
                        else                    _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_STOP_Set:
                        LastStep = _curstep;
                        Delay(300); // 시작 후 바로정지시 PLC loop delay문제로
                        ret = PLC_CosmoStopSetting(3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_STOP_Check;
                        else                    _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_STOP_Check:
                        LastStep = _curstep;
                        ret = PLC_CosmoStopChecking(20.0);  // 설비 테스트 진행 시간보다 많게
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_Data_View;
                        else                    _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_Data_View:
                        LastStep = _curstep;
                        Delay(100);  // 내부 통신 쓰레드 종료하기위한 시간을 주기위하여                         
                        if ((_priDataIndex>0)||(_floDataIndex>0))
                        {
                            _SubTestTimer.Stop(TestTime.TESTNAME.LEAKTEST_AIR_15);
                            CosmoGraph1CallBack(); // 그래프 표시 및 판정결과
                            Delay(100);
                        }
                        _curstep = SubStep.Sequence_End;
                        break;
                    case SubStep.Sequence_End:
                        _SubTestTimer.Stop(TestTime.TESTNAME.LEAKTEST_AIR_15);
                        errmsg = "";
                        curstep = SubStep.Sequence_End;
                        return StepFuncRet.Finished;
                    case SubStep.Error:
                        errmsg = "PC Timeout Error";
                        curstep = SubStep.Error;
                        return StepFuncRet.Error;                                               
                }
                curstep = LastStep;
                if (curstep== SubStep.Error)
                {
                    _SubTestTimer.Stop(TestTime.TESTNAME.LEAKTEST_AIR_15);
                    return StepFuncRet.Error;
                }
                return StepFuncRet.Doing;
            }
            private StepFuncRet Test_Leak_50( out SubStep curstep, out string errmsg)
            {
                errmsg = "";
                //SOL Sol;
                CLAMP Clamp;
                CMDRET ret;
                SubStep LastStep = SubStep.Init;
                switch (_curstep)
                {
                    case SubStep.Init:
                        _SubTestTimer.Start(TestTime.TESTNAME.LEAKTEST_AIR_50);
                        ClearCosmoBufIndex();
                        _curstep = SubStep.Sol_Set;
                        break;
                    case SubStep.Sol_Set:
                        LastStep = _curstep;
                        // 5.0Bar Sol 
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        bool _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 3.0);                                                

                        if (_Chk) _curstep = SubStep.Clamp_Set;
                        else      _curstep = SubStep.Error;
                        break;
                    case SubStep.Clamp_Set:
                        LastStep = _curstep;
                        Clamp = CLAMP.CLEAR;
                        Clamp = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_DOWN | CLAMP.PRIINLETSEALING_DOWN | CLAMP.SUCTION_FWD | CLAMP.OUTLET_FWD;
                        ret = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_CH_Set;
                        else _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_CH_Set:
                        LastStep = _curstep;
                        ret = PLC_CosmoChSetting(1, 1, 3.0);  // COSMO CH 번호 설정
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_RUN_Set;
                        else _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_RUN_Set:
                        LastStep = _curstep;
                        ret = PLC_CosmoRunSetting(3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_STOP_Set;
                        else _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_STOP_Set:
                        LastStep = _curstep;
                        Delay(300); // 시작 후 바로정지시 PLC loop delay문제로
                        ret = PLC_CosmoStopSetting(3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_STOP_Check;
                        else _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_STOP_Check:
                        LastStep = _curstep;
                        ret = PLC_CosmoStopChecking(20.0);  // 설비 테스트 진행 시간보다 많게
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_Data_View;
                        else _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_Data_View:
                        LastStep = _curstep;
                        Delay(100);  // 내부 통신 쓰레드 종료하기위한 시간을 주기위하여                         
                        if ((_priDataIndex > 0) || (_floDataIndex > 0))
                        {
                            _SubTestTimer.Stop(TestTime.TESTNAME.LEAKTEST_AIR_50);
                            CosmoGraph2CallBack(); // 그래프 표시 및 판정결과
                            Delay(100);
                        }
                        _curstep = SubStep.Sequence_End;
                        break;
                    case SubStep.Sequence_End:
                        int TargetPos = _AfterLoadingHomePosition ;
                        ret = PLC_ServoHomeGo(TargetPos, 150, 10.0);
                        if (ret == CMDRET.DONE) { }
                        else
                        {
                            _curstep = SubStep.Error; errmsg = "PLC 원점 복귀 이동 안됨";
                            break;
                        }                        
                        errmsg = "";
                        curstep = SubStep.Sequence_End;
                        return StepFuncRet.Finished;
                    case SubStep.Error:
                        errmsg = "PC Timeout Error";
                        curstep = SubStep.Error;
                        return StepFuncRet.Error;
                }
                curstep = LastStep;
                if (curstep == SubStep.Error)
                {
                    _SubTestTimer.Stop(TestTime.TESTNAME.LEAKTEST_AIR_50);
                    return StepFuncRet.Error;
                }
                return StepFuncRet.Doing;
            }

            private int    _Servo_Home          = 0;                // Piston Moving에서 기계부 홈위치
            private double _Servo_Displacement1 = 0.0;              // Piston Moving에서 사용
            //private int _RepeatCount = Bc.CurSysConfig.CurSysConfig.PistonMoving_RepeatCount;        // 반복 횟수
             private int _RepeatCount = 0;

            private int    _CurPos              = 0;                 // PistonMoving에서 현재위치 읽어 들일때 사용
            private StepFuncRet Test_PistonMoving(PISTONMOVING_SPEC ps, out SubStep curstep, out string errmsg)
            {
                errmsg = "";
                //SOL Sol;
                CLAMP Clamp;
                CMDRET ret;
                SubStep LastStep = SubStep.Init;
                switch (_curstep)
                {
                    case SubStep.Init:
                        #region 초기화
                        // 시험 시간 측정용 타이머 초기화, 고속데이터 저장 플래그 초기화
                            _SubTestTimer.Start(TestTime.TESTNAME.PISTON_MOVING);
                             LastStep     = _curstep;                      
                            _DAQDataSave  = false;                      // DAQ Data Save
                            _pmData1Index = 0;                          // DAQ Data Count Index reset                        
                            _curstep      = SubStep.Sol_Set;
                        break;
                        #endregion
                    case SubStep.Sol_Set:
                        #region 솔 사용유무에 따라 세팅
                            LastStep = _curstep;                        
                            Bc.LastSOLData0 = 0x0000;
                            Bc.LastSOLData1 = 0x0000;

                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;          // 에어 센서 측정용 사용
                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;      // 진공 센서 측정용 사용안함
                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;             // 공압라인 공급
                            Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;            //

                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                            Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;            // 2018-02-28 사용유무를 확인하고 정리 할 것.

                            bool _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);
                            if (_Chk) _curstep = SubStep.Clamp_Set;
                            else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨(PLC 및 통신라인 점검)"; }
                        break;
                        #endregion
                    case SubStep.Clamp_Set:
                        #region 클램프 관련 세팅
                        LastStep = _curstep;
                        Clamp = CLAMP.CLEAR;
                        Clamp = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_DOWN | CLAMP.PRIINLETSEALING_DOWN | CLAMP.SUCTION_FWD | CLAMP.OUTLET_BWD; // OUTLET PORT 개방!!!
                        ret = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Now_Position_Get;
                        else { _curstep = SubStep.Error; errmsg = "클램프 동작 안됨"; }
                        break;
                        #endregion
                    case SubStep.Servo_Now_Position_Get:
                        #region 현재 서보 위치 확인(PLC의 서보위치정보를 읽어 확인)
                        LastStep = _curstep;
                        int getSpd;
                        ret = PLC_ServoPositionGet(out _Servo_Home,out getSpd, 3.0);  // 현재 서보위치 확인
                        if (ret == CMDRET.DONE)
                        {
                            if ((_Servo_Home > -100) && (_Servo_Home < 500))  // 서보의 시작위차 홈 기준으로 -1 ~ +3 mm 내에 위치하여야 함.
                            {
                                _RepeatCount = 0;
                                _curstep = SubStep.Servo_FreeRun_Forward;
                            }
                            else { _curstep = SubStep.Error; errmsg = "서보 초기위치확인 안됨(LVDT 측정영역을 벗어남)"; }
                        }
                        else _curstep = SubStep.Error;
                        break;
                        #endregion
                    case SubStep.Servo_FreeRun_Forward:
                        #region 서보 전진(20mm까지 20mm/sec 전진)
                        LastStep = _curstep;
                        double move = PistonMovingRepeateDisplacement*10.0; 
                        int targetPos = _AfterLoadingHomePosition + (int)move;//+ 200;    // 홈위치 + 20.0mm                 
                        ret = PLC_ServoHomeGo(targetPos, 200, 10.0); 
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_FreeRun_Backward;
                        else
                        {
                            _curstep = SubStep.Error; errmsg = "PLC +20mm 위치 이동 안됨";
                        }
                        break;
                        #endregion
                    case SubStep.Servo_FreeRun_Backward:
                        #region 서보 후진(원점까지 20mm/sec)
                        LastStep = _curstep;
                        int targetPosHome = _AfterLoadingHomePosition ;
                        ret = PLC_ServoHomeGo(targetPosHome, 200, 10.0);  // 홈위치 
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_FreeRun_Backward_Check;
                        else
                        {
                            _curstep = SubStep.Error; errmsg = "PLC HOME 위치 이동 안됨";
                        }
                        break;
                        #endregion
                    case SubStep.Servo_FreeRun_Backward_Check:
                        #region 자유 전/후진 5회 실시 확인
                        //_RepeatCount--;
                        //if (_RepeatCount == 0)  // KS 수정 : 반복 횟수 설정
                        _RepeatCount++;
                        if (_RepeatCount>4)  // 5번 작동후
                        {
                            ret = PLC_ServoJogSpdSet(50, 3.0); // 시험 이동속도가 5mm/sec이므로 실제 전진모드전 속도설정을 한번더 실시함 - 통신오류 방지용
                            Delay(10);
                            _curstep = SubStep.Servo_Jog_Forward_Speed_Set;
                            break;
                        }
                        else
                        {
                            //ret = PLC_ServoJogSpdSet(50, 3.0);
                            //Delay(10);
                            _curstep = SubStep.Servo_FreeRun_Forward;
                        }
                        break;
                        #endregion
                    case SubStep.Servo_Jog_Forward_Speed_Set:
                        #region 서보 전진 모드 설정(5mm/sec)
                        LastStep = _curstep;
                        PLC_ServoJogSpdSet(50, 1.0);
                        Delay(30);
                        ret = PLC_ServoJogSpdSet(50, 3.0);                                            // 조그 속도 1mm/sec = 10 ===> 2017-07-27 5mm/sec 수정
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Jog_Forward_Run;
                        else { _curstep = SubStep.Error; errmsg = "조그 속도 설정안됨(1mm/sec)"; }
                        break;
                        #endregion
                    case SubStep.Servo_Jog_Forward_Run:
                        #region 서보 전진 명령 전송, 데이터 수집 시작
                        LastStep      = _curstep;
                        _DAQDataSave  = true;                          // DAQ Data Save
                        _pmData1Index = 0;                             // DAQ Data Count Index reset
                        ret = PLC_ServoJogForward(1.0);                // 조그 전진
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Jog_Forwarding_Check;
                        else                   { _curstep = SubStep.Error; errmsg = "조그 전진 안됨"; }
                        break;
                        #endregion
                    case SubStep.Servo_Jog_Forwarding_Check:
                        #region 서보 전진 위치 확인 및 전진포스의 1/3지점에서 정지, 고속이동모드 정지후 저속모드로 전진(0.5mm/sec)
                        LastStep = _curstep;
                        double _SetForce = 3000.0;
                        if (Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce>0.0)
                        {
                            _SetForce = Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce / 3.0;
                        }
                        if (_DAQData.Force>_SetForce)      //1000.0)  //5000.0)  //ps.CheckForwardForce)   ==> 5KN 임계점.
                        {
                            ret = PLC_ServoJogStop(1.0);   // 조그 모드이동시 PLC에서 정지후 속도 지령값을 변경해야 속도가 변경되므로...
                            Delay(10);
                            ret = PLC_ServoJogSpdSet(5, 3.0);
                            Delay(20);
                            ret = PLC_ServoJogSpdSet(5, 3.0);
                            Delay(20);
                            ret = PLC_ServoJogForward(1.0);
                            _curstep = SubStep.Servo_Jog_Forwarding_Check2;
                        }
                        if (_DAQData.Displacement > 50.0)  // 일정량(50mm)이상 전진되었을 경우 확인
                        {
                            ret = PLC_ServoJogStop(1.0);
                            _curstep = SubStep.Error;
                            errmsg   = ps.CheckForwardMaxLimit.ToString() + "mm이상 전진중 압력 발생안됨(설정압력 " + ps.CheckForwardForce.ToString() + ">)";
                        }
                        break;
                        #endregion
                    case SubStep.Servo_Jog_Forwarding_Check2:
                        #region 서보 서속모드 전진, 최종 설정 포스값까지 전진 후 정지 후 데이터 수집 모드 정지
                        LastStep = _curstep;
                        double _SetFinalForce = 3000.0;
                        if (Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce > 0.0)
                        {
                            _SetFinalForce = Bc.CurSysConfig.CurSysConfig.PistonMoving_FWDMaxForce;
                        }
                        if (_DAQData.Force > _SetFinalForce)           //2900.0)  // 9800...  ps.CheckForwardForce)   ==> 5KN 임계점.
                        {
                            _DAQDataSave = false;                      // DAQ Data Save off
                            ret          = PLC_ServoJogStop(1.0);
                            _curstep     = SubStep.Servo_Jog_Forwarding_Result;
                            ret          = PLC_ServoJogSpdSet(50, 3.0);
                            Delay(30);
                            ret          = PLC_ServoJogSpdSet(50, 3.0);
                            Delay(20);
                        }
                        if (_DAQData.Displacement > 50.0)// ps.CheckForwardMaxLimit)
                        {
                            ret      = PLC_ServoJogStop(1.0);
                            _curstep = SubStep.Error;
                            errmsg   = ps.CheckForwardMaxLimit.ToString() + "mm이상 전진중 압력 발생안됨(설정압력 " + ps.CheckForwardForce.ToString() + ">)";
                        }
                        break;
                        #endregion
                    case SubStep.Servo_Jog_Forwarding_Result:
                        #region 전진 결과 확인 및 다음 서보이동관련하여 속도 재설정
                        LastStep     = _curstep;
                        ret          = PLC_ServoJogSpdSet(10, 3.0);  // 1mm/sec후진
                        _DAQDataSave = false; 
                        PistonMovingFWDCallBack();  // 시험 결과 확인, 전진 포스
                        Delay(100);
                        ret          = PLC_ServoJogSpdSet(10, 3.0);
                        _curstep     = SubStep.Sol_Set2;
                        break;
                        #endregion


                    case SubStep.Sol_Set2:
                        #region 솔 사용유무에 따라 세팅(시험변경에 따른 2018-02-28)
                        LastStep = _curstep;
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;          // 에어 센서 측정용 사용
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;      // 진공 센서 측정용 사용안함
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_CLOSE;            //  2018-02-28
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_CLOSE;           //  2018-02-28

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;            // 2018-02-28 사용유무를 확인하고 정리 할 것.

                        bool _Chk2 = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);
                        if (_Chk2) _curstep = SubStep.Clamp_Set2;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨(PLC 및 통신라인 점검)"; }
                        break;
                        #endregion
                    case SubStep.Clamp_Set2:
                        #region 클램프 관련 세팅(시험변경에 따른 2018-02-28)
                        LastStep = _curstep;
                        Clamp    = CLAMP.CLEAR;
                        Clamp    = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_UP | CLAMP.PRINLETSEALING_UP | CLAMP.SUCTION_FWD | CLAMP.OUTLET_FWD;
                        ret      = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_CH_Set;
                        else { _curstep = SubStep.Error; errmsg = "클램프 동작 안됨"; }
                        break;
                        #endregion
                    case SubStep.Cosmo_CH_Set: 
                        #region 코스모 공압(5.0바) 가압 모드 채널 설정
                        LastStep = _curstep;
                        ret      = PLC_CosmoChSetting(3, 3, 3.0);           // COSMO 장치와 동일하게
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_Charge_Run;
                        else _curstep = SubStep.Error;
                        break;
                        #endregion
                    case SubStep.Cosmo_Charge_Run:
                        #region 코스코 차지 모드 실행
                        LastStep = _curstep;
                        ret      = PLC_CosmoChargeRun(3.0);
                        if (ret == CMDRET.DONE)
                        {
                            _TLT.Reset();
                            _TLT.Start();
                            _curstep = SubStep.Cosmo_Charge_Check;
                        }
                        else
                        {
                            errmsg = "Cosmo Charge Run Error";
                            _curstep = SubStep.Error;
                        }
                        break;
                        #endregion
                    case SubStep.Cosmo_Charge_Check:
                        #region 5.0바 가압 확인(3초이상 4.7바이상일 경우 통과), 6초동안 가압이 안될 경우 NG
                        LastStep = _curstep;
                        if ((_DAQData.FloAir > 4.7) && (_DAQData.PriAir > -1.0) && (_TLT.ElapsedMilliseconds >= 3000.0))
                        {
                            _TLT.Stop();
                            _curstep = SubStep.Piston_Stroke_Sol_Set;
                        }
                        if (_TLT.ElapsedMilliseconds > 7000.0)
                        {
                            _TLT.Stop();
                            errmsg = "Cosmo Charge 압력 생성 안됨";
                            _curstep = SubStep.Error;
                        }
                        break;
                        #endregion
                    case SubStep.Piston_Stroke_Sol_Set:
                        #region 가압 확인 후 솔 닫기
                        LastStep = _curstep;

                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_CLOSE;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                        if (_Chk) _curstep = SubStep.Cosmo_STOP_Set;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                        #endregion
                    case SubStep.Cosmo_STOP_Set:
                        #region 코스모 차지 모드 정지, 고속 데이터 모드 시작
                        LastStep = _curstep;
                        Delay(100);
                        ret = PLC_CosmoStopSetting(3.0);
                        if (ret == CMDRET.DONE)
                        {
                            _DAQDataSave  = true;                       // DAQ Data Save
                            _pmData1Index = 0;                          // DAQ Data Count Index reset                         
                            _curstep      = SubStep.Servo_Jog_Backwarding;
                        }
                        else { _curstep = SubStep.Error; errmsg = "코스모 정지 명령 동작 안됨"; }
                        Delay(20);
                        break;
                        #endregion


                    case SubStep.Servo_Jog_Backwarding:
                        #region 홈+20mm까지 1mm/sec이동 후 홈까지 10mm/sec이동
                        LastStep  = _curstep;   
                        int getSpd1;
                        ret = PLC_ServoPositionGet(out _CurPos,out getSpd1, 3.0);  // 현재 서보위치 확인
                        int BackMoveDisplacement = _AfterLoadingHomePosition;
                        if (ret== CMDRET.DONE)
                        {
                            BackMoveDisplacement = _Servo_Home + 200;
                            ret = PLC_ServoHomeGo(BackMoveDisplacement, 10, 30.0);
                            if (ret == CMDRET.DONE)
                            {
                                ret = PLC_ServoHomeGo(_AfterLoadingHomePosition, 100, 20.0);
                                if (ret==CMDRET.DONE)  _curstep = SubStep.Servo_Jog_Backwarding_Check;
                                else { _curstep = SubStep.Error; errmsg = "서보 후진(10mm/sec,0mmPos) 안됨"; }
                            }
                            else { _curstep = SubStep.Error; errmsg = "서보 후진(1mm/sec,20mmPos) 안됨"; }
                        }
                        else { _curstep = SubStep.Error; errmsg = "서보 현재 위치 확인 안됨(통신확인)"; }
                        break;
                        #endregion
                    case SubStep.Servo_Jog_Backwarding_Check:
                        #region LVDT 0.0<= 이하일경 홈 완료 인지후 시험판단. 2018-02-28 판단안함. 이전 위치가 홈지점으로 강제 이동되므로...
                        LastStep = _curstep;
                        Delay(100);
                        //if (_DAQData.Displacement <= _Servo_Displacement1)
                        //{
                            _DAQDataSave = false;               // DAQ Data Save off       
                            _curstep = SubStep.Servo_Jog_Backwarding_Result;

                        //}
                        break;
                        #endregion
                    case SubStep.Servo_Jog_Backwarding_Result:
                        LastStep = _curstep;
                        _SubTestTimer.Stop(TestTime.TESTNAME.PISTON_MOVING);
                        PistonMovingBWDCallBack();
                        Delay(100);
                        PistonStrokeCallBack();
                        Delay(100);
                        _curstep = SubStep.Repeat_Sqeuence_Check;
                        break;
                    case SubStep.Repeat_Sqeuence_Check:
                        LastStep = _curstep;
                        Delay(10);
                        _curstep = SubStep.Sequence_End; 
                        break;

                    case SubStep.Sequence_End:
                        Delay(100);
                        LastStep = _curstep;
                        errmsg = "";
                        curstep = SubStep.Sequence_End;
                        return StepFuncRet.Finished;
                    case SubStep.Error:
                        LastStep = _curstep;
                        if (errmsg.Length<=0) errmsg = "PC Timeout Error(설정된 에러내용이 없습니다.)";                        
                        _DAQDataSave  = false;                      // DAQ Data Save
                        _pmData1Index = 0;                          // DAQ Data Count Index reset
                        curstep = SubStep.Error;
                        PLC_ServoJogStop(1.0);  // Servo Stop
                        return StepFuncRet.Error;
                }
                curstep = LastStep;
                if (curstep == SubStep.Error)
                {
                    _SubTestTimer.Stop(TestTime.TESTNAME.PISTON_MOVING);
                    return StepFuncRet.Error;
                }
                return StepFuncRet.Doing;
            }
            private Stopwatch _TLT       = new Stopwatch();            // Lost Travel 용
            private bool      _PriPreChk = false, _FloPreChk = false;  // Lost Travel 용
            private StepFuncRet Test_LostTravel_1(LOSTTRAVEL_SPEC ps, out SubStep curstep, out string errmsg)
            {
                errmsg = "";
                //SOL Sol;
                CLAMP Clamp;
                CMDRET ret;
                SubStep LastStep = SubStep.Init;
                switch (_curstep)
                {
                    case SubStep.Init:
                        _SubTestTimer.Start(TestTime.TESTNAME.LOSTTRAVEL_PRI);
                        _DAQDataSave  = false;                      // DAQ Data Save
                        _pmData1Index = 0;                         // DAQ Data Count Index reset                        
                        LastStep      = _curstep;                    
                        _curstep      = SubStep.Sol_Set;
                        break;
                    case SubStep.Sol_Set:
                        LastStep = _curstep;
                        // 로스트 트레블 
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        bool _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                        if (_Chk) _curstep = SubStep.Clamp_Set;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                    case SubStep.Clamp_Set:
                        LastStep = _curstep;
                        Clamp = CLAMP.CLEAR; // FLO,PRI INLET UP
                        Clamp = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_DOWN | CLAMP.PRINLETSEALING_UP | CLAMP.SUCTION_FWD | CLAMP.OUTLET_FWD; 
                        ret = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Now_Position_Get;
                        else { _curstep = SubStep.Error; errmsg = "클램프 동작 안됨"; }
                        break;
                    case SubStep.Servo_Now_Position_Get:
                        LastStep = _curstep;
                        int getSpd;
                        ret = PLC_ServoPositionGet(out _Servo_Home, out getSpd, 3.0);  // 현재 서보위치 확인
                        if (ret == CMDRET.DONE)
                        {
                            if ((_Servo_Home > -100) && (_Servo_Home < 600))  // 서보의 시작위차 홈 기준으로 -1 ~ +3 mm 내에 위치하여야 함.
                            {
                                _curstep = SubStep.Cosmo_CH_Set;// 2017-09-10 구지 홈 위치로 갈필요가 없음, 최초 제품 로딩되고 첫 스텝이므로.....
                            }
                            else
                            {
                                _curstep = SubStep.Error; errmsg = "서보 초기위치확인 안됨";
                            }
                        }
                        else _curstep = SubStep.Error;
                        break;

                    case SubStep.Servo_Goto_Position:
                        LastStep = _curstep;
                            ret = PLC_ServoHomeGo(_AfterLoadingHomePosition, 150, 10.0);  // PLC홈위치에서 1mm전진, 추후 스펙데이터 값으로 변경
                            if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_CH_Set;
                            else
                            {
                                _curstep = SubStep.Error; errmsg = "PLC HOME 위치 이동 안됨";
                            }
                        break;
                    case SubStep.Cosmo_CH_Set:
                        LastStep = _curstep;
                        ret = PLC_CosmoChSetting(6, 6, 3.0);   // Secondary 3.0bar, Primary 1.0 으 세팅
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_Charge_Run;
                        else { errmsg = "COSMO CH 설정 안됨"; _curstep = SubStep.Error; }
                        break;
                    case SubStep.Cosmo_Charge_Run:
                        LastStep = _curstep;
                        ret = PLC_CosmoChargeRun(5.0);
                        if (ret == CMDRET.DONE)
                        {
                            _TLT.Reset();
                            _TLT.Start();
                            Delay(209);
                            _curstep = SubStep.Cosmo_Charge_Check;
                        }
                        else
                        {
                            errmsg = "Cosmo Charge Run Error";
                            _curstep = SubStep.Error;
                        }
                        break;
                    case SubStep.Cosmo_Charge_Check:
                        LastStep = _curstep;
                        if ((_DAQData.FloAir > 0.5) && (_DAQData.PriAir > 0.5) &&( _TLT.ElapsedMilliseconds > 3000.0))
                        {
                            _TLT.Stop();
                            _curstep = SubStep.Servo_Jog_Forward_Speed_Set;
                        }
                        if (_TLT.ElapsedMilliseconds>10000.0)
                        {
                            _TLT.Stop();
                            errmsg = "Cosmo Charge 압력 생성 안됨";
                            _curstep = SubStep.Error;
                        }
                        break;
                    case SubStep.Servo_Jog_Forward_Speed_Set:
                        LastStep = _curstep;
                        ret = PLC_ServoJogSpdSet(10, 3.0);  // 조그 속도 1mm/sec = 10
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Jog_Forward_Run;
                        else { _curstep = SubStep.Error; errmsg = "조그 속도 설정안됨(1mm/sec)"; }
                        break;
                    case SubStep.Servo_Jog_Forward_Run:
                        LastStep = _curstep;
                       
                            _DAQDataSave = true;                       // DAQ Data Save
                            _pmData1Index = 0;                         // DAQ Data Count Index reset
                        
                        HSDataSaveReset();
                        ret = PLC_ServoJogForward(1.0);  // 조그 전진
                        if (ret == CMDRET.DONE)
                        {
                            _PriPreChk = false;
                            _FloPreChk = false;
                            _TLT.Reset();
                            _TLT.Start();
                            _curstep = SubStep.Lost_Travel_Check;
                        }
                        else 
                        {
                            _curstep = SubStep.Error;
                            errmsg = "조그 전진 안됨"; 
                        }
                        break;
                    case SubStep.Lost_Travel_Check:
                        LastStep = _curstep;
                        if((_DAQData.PriAir>0.50)&&(_TLT.ElapsedMilliseconds>7000.0)) _PriPreChk = true;   // 7초동안 측정..1mm/sec=> 5mm이동
                        //if((_DAQData.FloAir>0.90)&&(_TLT.ElapsedMilliseconds>7000.0)) _FloPreChk = true;
                        _FloPreChk = true;
                        if ((_PriPreChk)&&(_FloPreChk))
                        {
                            _TLT.Reset();
                            _TLT.Stop();
                            _DAQDataSave = false;
                            ret = PLC_ServoJogStop(1.0);
                            _curstep = SubStep.Cosmo_STOP_Set;
                        }
                        if((_TLT.ElapsedMilliseconds>30*1000.0)||(_DAQData.Displacement>30.0)||(_DAQData.Force>2500.0))
                        {
                            _DAQDataSave = false;
                            _curstep = SubStep.Error;
                            errmsg = "압력 형성 안됨"; 
                        }
                        break;
                    case SubStep.Cosmo_STOP_Set:
                        LastStep = _curstep;
                        Delay(30); 
                        ret = PLC_CosmoStopSetting(3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Goto_Home;
                        else { _curstep = SubStep.Error; errmsg = "COSMO 정지 신호 확인 안됨"; }
                        break;
                    case SubStep.Servo_Goto_Home:
                        LastStep = _curstep;
                        ret = PLC_ServoHomeGo(_AfterLoadingHomePosition, 150, 10.0);  // 홈 이동 완료후 함수를 벗어나게 설계
                        if (ret == CMDRET.DONE) _curstep = SubStep.Sequence_End;
                        else
                        {
                            _curstep = SubStep.Error; errmsg = "PLC HOME 위치 이동 안됨";
                        }
                        _SubTestTimer.Stop(TestTime.TESTNAME.LOSTTRAVEL_PRI);
                        LostTravel1CallBack();
                        break;
                    case SubStep.Sequence_End:
                        LastStep = _curstep;
                        errmsg = "";
                        curstep = SubStep.Sequence_End;
                        return StepFuncRet.Finished;
                    case SubStep.Error:
                        LastStep = _curstep;
                        //if (errmsg.Length <= 0) errmsg = "PC Timeout Error(설정된 에러내용이 없습니다.)";
                        _DAQDataSave = false;                      // DAQ Data Save
                        _pmData1Index = 0;                          // DAQ Data Count Index reset
                        curstep = SubStep.Error;
                        PLC_ServoJogStop(1.0);  // Servo Stop
                        return StepFuncRet.Error;
                }
                curstep = LastStep;
                if (curstep == SubStep.Error)
                {
                    _SubTestTimer.Stop(TestTime.TESTNAME.LOSTTRAVEL_PRI);
                    return StepFuncRet.Error;
                }
                return StepFuncRet.Doing;
            }
            private StepFuncRet Test_LostTravel_2(LOSTTRAVEL_SPEC ps, out SubStep curstep, out string errmsg)
            {
                errmsg = "";
                //SOL Sol;
                CLAMP Clamp;
                CMDRET ret;
                SubStep LastStep = SubStep.Init;
                switch (_curstep)
                {
                    case SubStep.Init:
                        _SubTestTimer.Start(TestTime.TESTNAME.LOSTTRAVEL_SEC);
                        _DAQDataSave  = false;                      // DAQ Data Save
                        _pmData1Index = 0;                          // DAQ Data Count Index reset                        
                        LastStep      = _curstep;
                        _curstep      = SubStep.Sol_Set;
                        break;
                    case SubStep.Sol_Set:
                        LastStep = _curstep;
                        // 로스트 트레블 
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        bool _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                        if (_Chk) _curstep = SubStep.Clamp_Set;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                    case SubStep.Clamp_Set:
                        LastStep = _curstep;
                        Clamp = CLAMP.CLEAR; // FLO,PRI INLET UP
                        Clamp = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_UP | CLAMP.PRIINLETSEALING_DOWN | CLAMP.SUCTION_FWD | CLAMP.OUTLET_FWD;
                        ret = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Now_Position_Get;
                        else { _curstep = SubStep.Error; errmsg = "클램프 동작 안됨"; }
                        break;
                    case SubStep.Servo_Now_Position_Get:
                        LastStep = _curstep;
                        int getSpd;
                        ret = PLC_ServoPositionGet(out _Servo_Home, out getSpd, 3.0);  // 현재 서보위치 확인
                        if (ret == CMDRET.DONE)
                        {
                            if ((_Servo_Home > -100) && (_Servo_Home < 600))  // 서보의 시작위차 홈 기준으로 -1 ~ +3 mm 내에 위치하여야 함.
                            {
                                _curstep = SubStep.Servo_Goto_Position;
                            }
                            else
                            {
                                _curstep = SubStep.Error; errmsg = "서보 초기위치확인 안됨";
                            }
                        }
                        else _curstep = SubStep.Error;
                        break;

                    case SubStep.Servo_Goto_Position:
                        LastStep = _curstep;
                        ret = PLC_ServoHomeGo(_AfterLoadingHomePosition, 150, 10.0);  // PLC홈위치에서 1mm전진, 추후 스펙데이터 값으로 변경
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_CH_Set;
                        else
                        {
                            _curstep = SubStep.Error; errmsg = "PLC HOME 위치 이동 안됨";
                        }
                        break;
                    case SubStep.Cosmo_CH_Set:
                        LastStep = _curstep;
                        ret = PLC_CosmoChSetting(2, 2, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_Charge_Run;
                        else { errmsg = "COSMO CH 설정 안됨"; _curstep = SubStep.Error; }
                        break;
                    case SubStep.Cosmo_Charge_Run:
                        LastStep = _curstep;
                        ret = PLC_CosmoChargeRun(5.0);
                        if (ret == CMDRET.DONE)
                        {
                            _TLT.Reset();
                            _TLT.Start();
                            Delay(209);
                            _curstep = SubStep.Cosmo_Charge_Check;
                        }
                        else
                        {
                            errmsg = "Cosmo Charge Run Error";
                            _curstep = SubStep.Error;
                        }
                        break;
                    case SubStep.Cosmo_Charge_Check:
                        LastStep = _curstep;
                        if ((_DAQData.FloAir > 0.5) && (_DAQData.PriAir > 0.5) && (_TLT.ElapsedMilliseconds > 3000.0)) // 3초 가압후 측정
                        {
                            _PriPreChk = false;
                            _FloPreChk = false;
                            _DAQDataSave = true;                      // DAQ Data Save
                            _pmData1Index = 0;                        // DAQ Data Count Index reset     
                            _TLT.Reset();
                            _TLT.Start();
                            HSDataSaveReset();
                            _curstep = SubStep.Lost_Travel_Check;
                        }
                        if (_TLT.ElapsedMilliseconds > 12000.0)
                        {
                            _TLT.Stop();
                            errmsg = "Cosmo Charge 압력 생성 안됨";
                            _curstep = SubStep.Error;
                        }
                        break;

                    case SubStep.Lost_Travel_Check:
                        LastStep = _curstep;
                        if (_TLT.ElapsedMilliseconds > 7000.0) _PriPreChk = true;   // 7초동안 측정..1mm/sec=> 5mm이동
                        //if((_DAQData.FloAir>0.90)&&(_TLT.ElapsedMilliseconds>7000.0)) _FloPreChk = true;
                        _FloPreChk = true;
                        if ((_PriPreChk) && (_FloPreChk))
                        {
                            _TLT.Reset();
                            _TLT.Stop();
                            _DAQDataSave = false;
                            _curstep = SubStep.Cosmo_STOP_Set;
                        }
                        if ((_TLT.ElapsedMilliseconds > 30 * 1000.0) || (_DAQData.Displacement > 15.0) || (_DAQData.Force > 500.0))
                        {
                            _DAQDataSave = false;
                            _curstep = SubStep.Error;
                            errmsg = "압력 형성 안됨";
                        }
                        break;
                    case SubStep.Cosmo_STOP_Set:
                        LastStep = _curstep;
                        Delay(30);
                        ret = PLC_CosmoStopSetting(3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Goto_Home;
                        else { _curstep = SubStep.Error; errmsg = "COSMO 정지 신호 확인 안됨"; }
                        break;
                    case SubStep.Servo_Goto_Home:
                        LastStep = _curstep;
                        ret = PLC_ServoHomeGo(_AfterLoadingHomePosition, 150, 10.0);  // 홈 이동 완료후 함수를 벗어나게 설계
                        if (ret == CMDRET.DONE) _curstep = SubStep.Sequence_End;
                        else
                        {
                            _curstep = SubStep.Error; errmsg = "PLC HOME 위치 이동 안됨";
                        }
                        _SubTestTimer.Stop(TestTime.TESTNAME.LOSTTRAVEL_SEC);
                        LostTravel2CallBack();
                        break;
                    case SubStep.Sequence_End:
                        LastStep = _curstep;
                        errmsg = "";
                        curstep = SubStep.Sequence_End;
                        return StepFuncRet.Finished;
                    case SubStep.Error:
                        LastStep = _curstep;
                        //if (errmsg.Length <= 0) errmsg = "PC Timeout Error(설정된 에러내용이 없습니다.)";
                        _DAQDataSave = false;                      // DAQ Data Save
                        _pmData1Index = 0;                          // DAQ Data Count Index reset
                        curstep = SubStep.Error;
                        PLC_ServoJogStop(1.0);  // Servo Stop
                        return StepFuncRet.Error;
                }
                curstep = LastStep;
                if (curstep == SubStep.Error)
                {
                    _SubTestTimer.Stop(TestTime.TESTNAME.LOSTTRAVEL_SEC);
                    return StepFuncRet.Error;
                }
                return StepFuncRet.Doing;
            }
            private StepFuncRet Test_PistonStroke(PISTONSTROKE_SPEC ps, out SubStep curstep, out string errmsg)
            {
                errmsg = "";
                //SOL Sol;
                CLAMP Clamp;
                CMDRET ret;
                SubStep LastStep = SubStep.Init;
                //int curPos=0;
                switch (_curstep)
                {
                    case SubStep.Init:
                        _SubTestTimer.Start(TestTime.TESTNAME.PISTON_STROKE_5BAR);
                        LastStep      = _curstep;                      
                        _DAQDataSave  = false;                     // DAQ Data Save
                        _pmData1Index = 0;                         // DAQ Data Count Index reset                        
                        _curstep      = SubStep.Sol_Set;
                        break;
                    case SubStep.Sol_Set:
                        LastStep = _curstep;
                        // 피스톤 스트로크 테스트
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_CLOSE;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        bool _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                        if (_Chk) _curstep = SubStep.Clamp_Set;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                    case SubStep.Clamp_Set:
                        LastStep = _curstep;
                        Clamp = CLAMP.CLEAR; // FLO,PRI INLET UP
                        Clamp = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_UP | CLAMP.PRINLETSEALING_UP | CLAMP.SUCTION_FWD | CLAMP.OUTLET_FWD;
                        ret = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Now_Position_Get;
                        else { _curstep = SubStep.Error; errmsg = "클램프 동작 안됨"; }
                        break;
                    case SubStep.Servo_Now_Position_Get:
                        LastStep = _curstep;
                        int getSpd;
                        ret = PLC_ServoPositionGet(out _Servo_Home, out getSpd, 3.0);  // 현재 서보위치 확인
                        if (ret == CMDRET.DONE)
                        {
                            if ((_Servo_Home > -100) && (_Servo_Home < 500))  // 서보의 시작위차 홈 기준으로 -1 ~ +3 mm 내에 위치하여야 함.
                            {
                                _curstep = SubStep.Servo_Goto_Position;
                            }
                            else { _curstep = SubStep.Error; errmsg = "서보 초기위치확인 안됨"; }
                        }
                        else _curstep = SubStep.Error;
                        break;

                    case SubStep.Servo_Goto_Position:
                        LastStep = _curstep;                      

                        ret = PLC_ServoHomeGo(_AfterLoadingHomePosition + 300, 200, 10.0);  // PLC홈위치에서 20mm 고속전진, 추후 스펙데이터 값으로 변경
                        //ret = PLC_ServoHomeGo(_AfterLoadingHomePosition + 300, 200, 10.0);  // PLC홈위치에서 20mm 고속전진, 추후 스펙데이터 값으로 변경
                                                                                            // 2017-11-13, 30mm 20mm/sec고속이동
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Jog_Forward_Speed_Set;
                        else
                        {
                            _curstep = SubStep.Error; errmsg = "PLC 20mm전진 위치 이동 안됨";
                        }
                        break;
                    case SubStep.Servo_Jog_Forward_Speed_Set:
                        LastStep = _curstep;
                        ret = PLC_ServoJogSpdSet(10, 1.0);
                        Delay(10);
                        ret = PLC_ServoJogSpdSet(10, 3.0);  // 조그 속도 1mm/sec = 10
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Jog_Forward_Run;
                        else { _curstep = SubStep.Error; errmsg = "조그 속도 설정안됨(1mm/sec)"; }
                        break;
                    case SubStep.Servo_Jog_Forward_Run:
                        LastStep = _curstep;
                        ret = PLC_ServoJogForward(1.0);  // 조그 전진
                        if (ret == CMDRET.DONE)
                        {
                            _TLT.Reset();
                            _TLT.Start();
                            _curstep = SubStep.Piston_Stroke_Check;
                        }
                        else
                        {
                            _curstep = SubStep.Error;
                            errmsg = "조그 전진 안됨";
                        }
                        break;
                    case SubStep.Piston_Stroke_Check:
                        LastStep = _curstep;
                        if (_DAQData.Force > 9600.0) // 스펙데이터로 변경하고 삭제
                        {
                            _TLT.Reset();
                            _TLT.Stop();
                            ret          = PLC_ServoJogStop(1.0);
                            _DAQDataSave = false;                       // DAQ Data Save
                            _curstep     = SubStep.Cosmo_CH_Set;
                        }
                        if (_TLT.ElapsedMilliseconds > 30 * 1000.0)
                        {
                            _curstep = SubStep.Error;
                            errmsg   = "9600N 압력 형성 안됨";
                        }
                        break;


                    case SubStep.Cosmo_CH_Set: // 압력 세팅 ==> 고압 설정으로 변경
                        LastStep = _curstep;
                        ret = PLC_CosmoChSetting(3, 3, 3.0); // COSMO 장치와 동일하게
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_Charge_Run;
                        else _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_Charge_Run: // 고압 설정이면 차지 모드가 시간 설정으로 변경
                        LastStep = _curstep;
                        ret = PLC_CosmoChargeRun(3.0);
                        if (ret == CMDRET.DONE)
                        {
                            _TLT.Reset();
                            _TLT.Start();
                            _curstep = SubStep.Cosmo_Charge_Check;
                        }
                        else
                        {
                            errmsg = "Cosmo Charge Run Error";
                            _curstep = SubStep.Error;
                        }
                        break;
                    case SubStep.Cosmo_Charge_Check: // 고압 압력 측정으로....
                        LastStep = _curstep;
                        if ((_DAQData.FloAir > 4.7) && (_DAQData.PriAir > -1.0) && (_TLT.ElapsedMilliseconds >= 3000.0))
                        {
                            _TLT.Stop();
                            _curstep = SubStep.Piston_Stroke_Sol_Set;
                        }
                        if (_TLT.ElapsedMilliseconds > 6000.0)
                        {
                            _TLT.Stop();
                            errmsg = "Cosmo Charge 압력 생성 안됨";
                            _curstep = SubStep.Error;
                        }
                        break;
                    case SubStep.Piston_Stroke_Sol_Set:
                        LastStep = _curstep;

                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_CLOSE;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                        if (_Chk) _curstep = SubStep.Cosmo_STOP_Set;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                    case SubStep.Cosmo_STOP_Set:
                        LastStep = _curstep;
                        Delay(30);
                        ret = PLC_CosmoStopSetting(3.0);
                        if (ret == CMDRET.DONE)
                        {                           
                                _DAQDataSave = true;                       // DAQ Data Save
                                _pmData1Index = 0;                         // DAQ Data Count Index reset                         
                               _curstep = SubStep.Servo_Jog_Backwarding;                                                
                        }
                        else _curstep = SubStep.Error;
                        break;



                   case SubStep.Servo_Jog_Backwarding:
                        LastStep = _curstep;         
                        ret = PLC_ServoHomeGo(_AfterLoadingHomePosition+200 , 20, 60.0);                        // PLC홈위치로 1mm/sec로 이동
                                                                                                                // 2017-11-13 2mm/sec 이동, 20mm까지.
                        if (ret == CMDRET.DONE)
                        {                            
                            _curstep = SubStep.Piston_Stroke_Check22;
                        }
                        else { _curstep = SubStep.Error; errmsg = "조그 후진 안됨 1차"; }
                        break;
                    case SubStep.Piston_Stroke_Check22:
                        LastStep = _curstep;
                        ret = PLC_ServoHomeGo(_AfterLoadingHomePosition , 100, 60.0);  // PLC홈위치로 1mm/sec로 이동
                                                                                       // 2017-11-13 홈까지 10mm/sec
                        if (ret == CMDRET.DONE)
                        {                            
                            _curstep = SubStep.Piston_Stroke_Check2;
                        }
                        else { _curstep = SubStep.Error; errmsg = "조그 후진 안됨 2차"; }
                        break;                        
                    case SubStep.Piston_Stroke_Check2:
                        LastStep = _curstep;
                        // 결과 그래프
                        _DAQDataSave = false;
                        _SubTestTimer.Stop(TestTime.TESTNAME.PISTON_STROKE_5BAR);
                        PistonStrokeCallBack();
                        _curstep = SubStep.Piston_Stroke_Check3;
                        break;
                    case SubStep.Piston_Stroke_Check3:
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                        if (_Chk) _curstep = SubStep.Sequence_End;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                    case SubStep.Sequence_End:
                        LastStep = _curstep;
                        errmsg = "";
                        curstep = SubStep.Sequence_End;
                        return StepFuncRet.Finished;
                    case SubStep.Error:
                        if (errmsg.Length <= 0) errmsg = "PC Timeout Error(설정된 에러내용이 없습니다.)";
                        _DAQDataSave = false;                      // DAQ Data Save
                        _pmData1Index = 0;                          // DAQ Data Count Index reset
                        curstep = SubStep.Error;
                        PLC_ServoJogStop(1.0);  // Servo Stop
                        return StepFuncRet.Error;
                }
                curstep = LastStep;
                if (curstep == SubStep.Error)
                {
                    _SubTestTimer.Stop(TestTime.TESTNAME.PISTON_STROKE_5BAR);
                    return StepFuncRet.Error;
                }
                return StepFuncRet.Doing;
            }
            private StepFuncRet Test_PistonStrokeVAC(PISTONSTROKE_SPEC ps, out SubStep curstep, out string errmsg)
            {
                errmsg = "";
                //SOL Sol;
                CLAMP Clamp;
                CMDRET ret;
                SubStep LastStep = SubStep.Init;
                //int curPos = 0;
                switch (_curstep)
                {
                    case SubStep.Init:
                        _SubTestTimer.Start(TestTime.TESTNAME.PISTON_STROKE_500mmHg);
                        LastStep      = _curstep;
                        _DAQDataSave  = false;                      // DAQ Data Save
                        _pmData1Index = 0;                          // DAQ Data Count Index reset
                        _curstep      = SubStep.Sol_Set;
                        break;
                    case SubStep.Sol_Set:
                        LastStep = _curstep;
                        // 피스톤 스트로크 테스트 - 진공 모드
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        bool _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                        if (_Chk) _curstep = SubStep.Clamp_Set;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                    case SubStep.Clamp_Set:
                        LastStep = _curstep;
                        Clamp = CLAMP.CLEAR; // FLO,PRI INLET UP
                        Clamp = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_UP | CLAMP.PRINLETSEALING_UP | CLAMP.SUCTION_FWD | CLAMP.OUTLET_FWD;
                        ret = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Now_Position_Get;
                        else { _curstep = SubStep.Error; errmsg = "클램프 동작 안됨"; }
                        break;
                    case SubStep.Servo_Now_Position_Get:
                        LastStep = _curstep;
                        int getSpd;
                        ret = PLC_ServoPositionGet(out _Servo_Home, out getSpd, 3.0);  // 현재 서보위치 확인
                        if (ret == CMDRET.DONE)
                        {
                            if ((_Servo_Home > -100) && (_Servo_Home < 600))  // 서보의 시작위차 홈 기준으로 -1 ~ +3 mm 내에 위치하여야 함.
                            {
                                _curstep = SubStep.Servo_Goto_Position;
                            }
                            else { _curstep = SubStep.Error; errmsg = "서보 초기위치확인 안됨"; }
                        }
                        else _curstep = SubStep.Error;
                        break;

                    case SubStep.Servo_Goto_Position:
                        LastStep = _curstep;
                        int FullStroke = _AfterLoadingHomePosition + (int)(Bc.FullStroke*10.0);
                        int PressSpeed = Bc.CurSysConfig.CurSysConfig.PistonStrokeFSSpeed;
                        if ((PressSpeed <= 10)&&(PressSpeed>=600)) PressSpeed = 100;  // 1.0 ~60.0mm/sec범위를 벋어나면 10.0mm/sec 기본값적용
                        ret = PLC_ServoHomeGo(FullStroke, PressSpeed, 35.0);  // PLC홈위치에서 20mm 고속전진, 추후 스펙데이터 값으로 변경
                            //ret = PLC_ServoHomeGo(_AfterLoadingHomePosition + 300, 200, 10.0);  // PLC홈위치에서 20mm 고속전진, 추후 스펙데이터 값으로 변경
                            // 2017-11-13 30mm, 20mm/sec로 고속이동
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_CH_Set;
                        else
                        {
                            _curstep = SubStep.Error; errmsg = "PLC FullStroke 전진 위치 이동 안됨";
                        }                      
                        break;  
                    case SubStep.Cosmo_CH_Set: // 압력 세팅 ==> 진공 설정으로 변경
                        LastStep = _curstep;
                        ret = PLC_CosmoChSetting(4, 4, 3.0); // COSMO 장치와 동일하게 - 진공모드!!!!!!!!!!!
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_Charge_Run;
                        else _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_Charge_Run: 
                        LastStep = _curstep;
                        ret = PLC_CosmoChargeRun(3.0);
                        if (ret == CMDRET.DONE)
                        {
                            _TLT.Reset();
                            _TLT.Start();
                            _curstep = SubStep.Cosmo_Charge_Check;
                        }
                        else
                        {
                            _TLT.Stop();
                            errmsg = "Cosmo Charge Run Error";
                            _curstep = SubStep.Error;
                        }
                        break;
                    case SubStep.Cosmo_Charge_Check: // 고압 압력 측정으로....
                        LastStep = _curstep;
                        if ((_DAQData.FloVacuum > 480) && (_DAQData.PriVacuum > 480) && (_TLT.ElapsedMilliseconds >= 4000.0))
                        {
                            _TLT.Stop();
                            _curstep = SubStep.Piston_Stroke_Sol_Set;
                        }
                        if (_TLT.ElapsedMilliseconds > 8000.0)
                        {
                            _TLT.Stop();
                            errmsg = "Cosmo Charge 압력 생성 안됨";
                            _curstep = SubStep.Error;
                        }
                        break;
                    case SubStep.Piston_Stroke_Sol_Set:
                        LastStep = _curstep;

                        Bc.LastSOLData0 = 0x0000; // 솔 닫기
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);
                        Delay(200);
                        if (_Chk) _curstep = SubStep.Cosmo_STOP_Set;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                    case SubStep.Cosmo_STOP_Set:
                        LastStep = _curstep;
                        Delay(30);
                        ret = PLC_CosmoStopSetting(3.0);
                        if (ret == CMDRET.DONE)
                        {

                            _DAQDataSave = true;                       // DAQ Data Save
                            _pmData1Index = 0;                         // DAQ Data Count Index reset

                            _curstep = SubStep.Servo_Jog_Backwarding;
                        }
                        else _curstep = SubStep.Error;
                        break;
                    case SubStep.Servo_Jog_Backwarding:
                        LastStep = _curstep;
                        ret = PLC_ServoHomeGo(_AfterLoadingHomePosition, 20, 60.0);  // PLC홈위치로 1mm/sec로 이동
                                                                                     // 2017-11-13, 2mm/sec
                        if (ret == CMDRET.DONE)
                        {                            
                            _curstep = SubStep.Piston_Stroke_Check2;
                        }
                        else { _curstep = SubStep.Error; errmsg = "조그 후진 안됨"; }
                        break;
                    case SubStep.Piston_Stroke_Check2:
                        LastStep = _curstep;
                        // 결과 그래프
                        _DAQDataSave = false;
                        _SubTestTimer.Stop(TestTime.TESTNAME.PISTON_STROKE_500mmHg);
                        PistonStrokeVACCallBack();
                        Delay(500);
                        _curstep = SubStep.Sequence_End;
                        break;
                    case SubStep.Sequence_End:
                        LastStep = _curstep;
                        errmsg = "";
                        curstep = SubStep.Sequence_End;
                        return StepFuncRet.Finished;
                    case SubStep.Error:
                        if (errmsg.Length <= 0) errmsg = "PC 가압중 리크 발생 Error(타임 오버)";
                        _DAQDataSave = false;                      // DAQ Data Save
                        _pmData1Index = 0;                          // DAQ Data Count Index reset
                        curstep = SubStep.Error;
                        PLC_ServoJogStop(1.0);  // Servo Stop
                        return StepFuncRet.Error;
                }
                curstep = LastStep;
                if (curstep == SubStep.Error)
                {
                    _SubTestTimer.Stop(TestTime.TESTNAME.PISTON_STROKE_500mmHg);
                    return StepFuncRet.Error;
                }
                return StepFuncRet.Doing;
            }
            private StepFuncRet Test_Vacuum(VACUUM_SPEC ps, out SubStep curstep, out string errmsg,double VOffset)
            {
                errmsg = "";
                //SOL Sol;
                CLAMP Clamp;
                CMDRET ret;
                SubStep LastStep = SubStep.Init;
                int HOffset = (int)(VOffset * 10.0);
                switch (_curstep)
                {
                    case SubStep.Init:
                        _SubTestTimer.Start(TestTime.TESTNAME.VACUUMHOLD_PRI);
                        LastStep      = _curstep;                       
                        _DAQDataSave  = false;                      // DAQ Data Save
                        _pmData1Index = 0;                          // DAQ Data Count Index reset
                        HOffset       = (int)(5.0 * 10.0);
                        _curstep      = SubStep.Sol_Set;
                        break;
                    case SubStep.Sol_Set: // 진공 센서 주의 요망.!!!!!
                        LastStep = _curstep;
                        // 진공 테스트
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        bool _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                        if (_Chk) _curstep = SubStep.Clamp_Set;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                    case SubStep.Clamp_Set:
                        LastStep = _curstep;
                        Clamp = CLAMP.CLEAR; 
                        Clamp = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_UP | CLAMP.PRIINLETSEALING_DOWN | CLAMP.SUCTION_FWD | CLAMP.OUTLET_FWD;
                        ret = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Now_Position_Get;
                        else { _curstep = SubStep.Error; errmsg = "클램프 동작 안됨"; }
                        break;
                    case SubStep.Servo_Now_Position_Get:
                        LastStep = _curstep;
                        int getSpd;
                        ret = PLC_ServoPositionGet(out _Servo_Home, out getSpd, 3.0);  // 현재 서보위치 확인
                        if (ret == CMDRET.DONE)
                        {
                             _curstep = SubStep.Servo_Goto_Position;
                        }
                        else _curstep = SubStep.Error;
                        break;

                    case SubStep.Servo_Goto_Position:
                        LastStep = _curstep;
                        //double MoveInit = Bc.CutOffHolePositionPri;
                        //HOffset =(int)(MoveInit*10.0) + 10; // 1mm 추가
                        ret = PLC_ServoHomeGo(_AfterLoadingHomePosition + 50, 150, 90.0);  // PLC홈위치에서 5mm 임의 컷오프 위치, 추후 스펙데이터로...++> 2017 10mm 이동
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_CH_Set;
                        else
                        {
                            _curstep = SubStep.Error; errmsg = "PLC 10mm(C/O)전진 위치 이동 안됨";
                        }
                        break;                  
                    case SubStep.Cosmo_CH_Set:
                        LastStep = _curstep;
                        ret = PLC_CosmoChSetting(4, 4, 3.0); // COSMO 장치와 동일하게
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_Charge_Run;
                        else                    _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_Charge_Run:
                        LastStep = _curstep;
                        ret = PLC_CosmoChargeRun(3.0);
                        if (ret == CMDRET.DONE)
                        {
                            _TLT.Reset();
                            _TLT.Start();
                            _curstep = SubStep.Cosmo_Charge_Check;
                        }
                        else
                        {
                            errmsg = "Cosmo Charge Run Error";
                            _curstep = SubStep.Error;
                        }
                        break;
                    case SubStep.Cosmo_Charge_Check:
                        LastStep = _curstep;
                        //if ((_DAQData.PriVacuum > 475.0) && (_DAQData.FloVacuum > 475.0))
                        if ( (_DAQData.PriVacuum > 475.0)&&(_TLT.ElapsedMilliseconds>3000.0) )
                        {
                            _TLT.Stop();
                            _curstep = SubStep.Vacuum_Sol_Set;
                        }
                        if (_TLT.ElapsedMilliseconds > 6000.0)
                        {
                            _TLT.Stop();
                            errmsg = "Cosmo Charge 진공 생성 안됨";
                            _curstep = SubStep.Error;
                        }
                        break;
                    case SubStep.Vacuum_Sol_Set:
                        LastStep = _curstep;
                        // Outlet 닫고 진공 체트
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                        if (_Chk) _curstep = SubStep.Cosmo_STOP_Set;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                    case SubStep.Cosmo_STOP_Set:
                        LastStep = _curstep;
                        Delay(30);
                        ret = PLC_CosmoStopSetting(3.0);
                        if (ret == CMDRET.DONE)
                        {
                            _curstep = SubStep.Vacuum_Clamp_Set;
                        }
                        else _curstep = SubStep.Error;
                        break;
                    case SubStep.Vacuum_Clamp_Set:
                        LastStep = _curstep;
                        Clamp = CLAMP.CLEAR; 
                        Clamp = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_UP | CLAMP.PRINLETSEALING_UP | CLAMP.SUCTION_FWD | CLAMP.OUTLET_FWD;
                        ret = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE)
                        {
                            
                                _DAQDataSave = true;                       // DAQ Data Save
                                _pmData1Index = 0;                         // DAQ Data Count Index reset
                            
                            _TLT.Reset();
                            _TLT.Start();
                            _curstep = SubStep.Vacuum_Check2;
                        }
                        else { _curstep = SubStep.Error; errmsg = "클램프 동작 안됨"; }
                        break;
                    case SubStep.Vacuum_Check2:
                        LastStep = _curstep;
                        if (_TLT.ElapsedMilliseconds>7.5*1000.0)  // 2017-11-13 5초로 변경
                        {
                            _DAQDataSave = false;
                            _TLT.Reset();
                            _curstep = SubStep.Vacuum_Check3;
                        }
                        break;
                    case SubStep.Vacuum_Check3:
                        LastStep = _curstep;
                        _SubTestTimer.Stop(TestTime.TESTNAME.VACUUMHOLD_PRI);
                        VacuumCallBack();
                        _curstep = SubStep.Sequence_End;
                        break;
                    case SubStep.Sequence_End:
                        LastStep = _curstep;
                        errmsg = "";
                        curstep = SubStep.Sequence_End;
                        return StepFuncRet.Finished;
                    case SubStep.Error:
                        LastStep = _curstep;
                        if (errmsg.Length <= 0) errmsg = "PC Timeout Error(설정된 에러내용이 없습니다.)";
                        _DAQDataSave = false;                      // DAQ Data Save
                        _pmData1Index = 0;                          // DAQ Data Count Index reset
                        curstep = SubStep.Error;
                        PLC_ServoJogStop(1.0);  // Servo Stop
                        return StepFuncRet.Error;
                }
                curstep = LastStep;
                if (curstep == SubStep.Error)
                {
                    _SubTestTimer.Stop(TestTime.TESTNAME.VACUUMHOLD_PRI);
                    return StepFuncRet.Error;
                }
                return StepFuncRet.Doing;
            }
            private StepFuncRet Test_Vacuum2(VACUUM_SPEC ps, out SubStep curstep, out string errmsg, double VOffset)
            {
                errmsg = "";
                //SOL Sol;
                CLAMP Clamp;
                CMDRET ret;
                SubStep LastStep = SubStep.Init;
                int HOffset = (int)(VOffset * 10.0);
                switch (_curstep)
                {
                    case SubStep.Init:
                        _SubTestTimer.Start(TestTime.TESTNAME.VACUUMHOLD_SEC);
                        LastStep      = _curstep;
                        _DAQDataSave  = false;                     // DAQ Data Save
                        _pmData1Index = 0;                         // DAQ Data Count Index reset
                        HOffset       = (int)(5.0 * 10.0);
                        _curstep      = SubStep.Sol_Set;
                        break;
                    case SubStep.Sol_Set: // 진공 센서 주의 요망.!!!!!
                        LastStep = _curstep;
                        // 진공 테스트
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        bool _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);


                        if (_Chk) _curstep = SubStep.Clamp_Set;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                    case SubStep.Clamp_Set:
                        LastStep = _curstep;
                        Clamp = CLAMP.CLEAR;
                        Clamp = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_DOWN | CLAMP.PRIINLETSEALING_DOWN | CLAMP.SUCTION_FWD | CLAMP.OUTLET_FWD;
                        ret = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE) _curstep = SubStep.Servo_Now_Position_Get;
                        else { _curstep = SubStep.Error; errmsg = "클램프 동작 안됨"; }
                        break;
                    case SubStep.Servo_Now_Position_Get:
                        LastStep = _curstep;
                        int getSpd;
                        ret = PLC_ServoPositionGet(out _Servo_Home, out getSpd, 3.0);  // 현재 서보위치 확인
                        if (ret == CMDRET.DONE)
                        {
                                _curstep = SubStep.Servo_Goto_Position;
                        }
                        else _curstep = SubStep.Error;
                        break;

                    case SubStep.Servo_Goto_Position:
                        LastStep = _curstep;
                        ret = PLC_ServoHomeGo(_AfterLoadingHomePosition , 150, 90.0);  // PLC홈위치에서 5mm 임의 컷오프 위치, 추후 스펙데이터로...++> 2017 10mm 이동
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_CH_Set;
                        else
                        {
                            _curstep = SubStep.Error; errmsg = "PLC 10mm(C/O)전진 위치 이동 안됨";
                        }
                        break;
                    case SubStep.Cosmo_CH_Set:
                        LastStep = _curstep;
                        ret = PLC_CosmoChSetting(2, 4, 3.0); // COSMO 장치와 동일하게
                        if (ret == CMDRET.DONE) _curstep = SubStep.Cosmo_Charge_Run;
                        else _curstep = SubStep.Error;
                        break;
                    case SubStep.Cosmo_Charge_Run:
                        LastStep = _curstep;
                        ret = PLC_CosmoChargeRun(3.0);
                        if (ret == CMDRET.DONE)
                        {
                            _TLT.Reset();
                            _TLT.Start();
                            _curstep = SubStep.Cosmo_Charge_Check;
                        }
                        else
                        {
                            errmsg = "Cosmo Charge Run Error";
                            _curstep = SubStep.Error;
                        }
                        break;
                    case SubStep.Cosmo_Charge_Check:
                        LastStep = _curstep;
                        if ((_DAQData.PriAir > 1.0) && (_DAQData.FloVacuum > 400.0) && (_TLT.ElapsedMilliseconds > 3000.0))
                        {
                            _TLT.Stop();
                            _curstep = SubStep.Vacuum_Sol_Set;
                        }
                        if (_TLT.ElapsedMilliseconds > 8000.0)
                        {
                            _TLT.Stop();
                            errmsg = "Cosmo 정압 및 진공 생성 안됨";
                            _curstep = SubStep.Error;
                        }
                        break;
                    case SubStep.Vacuum_Sol_Set:
                        LastStep = _curstep;
                        // Outlet 닫고 진공 체트
                        Bc.LastSOLData0 = 0x0000;
                        Bc.LastSOLData1 = 0x0000;

                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_AIRSENSOR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_VACUUMSENSOR_OPEN;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_LOWAIR_CLOSE;
                        Bc.LastSOLData0 = Bc.LastSOLData0 | (int)BCControl.SOL.DOPRI_PROTECT_OPEN;

                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_AIRSENSOR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_VACUUMSENSOR_OPEN;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_LOWAIR_CLOSE;
                        Bc.LastSOLData1 = Bc.LastSOLData1 | (int)BCControl.SOL.D1FLO_PROTECT_OPEN;

                        _Chk = PLC_SOLSET(Bc.LastSOLData0, Bc.LastSOLData1, 2.0);

                        if (_Chk) _curstep = SubStep.Cosmo_STOP_Set;
                        else { _curstep = SubStep.Error; errmsg = "솔 동작 안됨"; }
                        break;
                    case SubStep.Cosmo_STOP_Set:
                        LastStep = _curstep;
                        Delay(30);
                        ret = PLC_CosmoStopSetting(3.0);
                        if (ret == CMDRET.DONE)
                        {
                            _curstep = SubStep.Vacuum_Clamp_Set;
                        }
                        else _curstep = SubStep.Error;
                        break;
                    case SubStep.Vacuum_Clamp_Set:
                        LastStep = _curstep;
                        Clamp = CLAMP.CLEAR;
                        Clamp = Clamp | CLAMP.WORKCLAMP_FWD | CLAMP.FLOINLETSEALING_UP | CLAMP.PRIINLETSEALING_DOWN | CLAMP.SUCTION_FWD | CLAMP.OUTLET_FWD;
                        ret = PLC_ClampSetting(Clamp, 3.0);
                        if (ret == CMDRET.DONE)
                        {

                            _DAQDataSave = true;                       // DAQ Data Save
                            _pmData1Index = 0;                         // DAQ Data Count Index reset

                            _TLT.Reset();
                            _TLT.Start();
                            _curstep = SubStep.Vacuum_Check2;
                        }
                        else { _curstep = SubStep.Error; errmsg = "클램프 동작 안됨"; }
                        break;
                    case SubStep.Vacuum_Check2:
                        LastStep = _curstep;
                        if (_TLT.ElapsedMilliseconds > 7.5 * 1000.0)
                        {
                            _DAQDataSave = false;
                            _TLT.Reset();
                            _curstep = SubStep.Vacuum_Check3;
                        }
                        break;
                    case SubStep.Vacuum_Check3:
                        LastStep = _curstep;
                        _SubTestTimer.Stop(TestTime.TESTNAME.VACUUMHOLD_SEC);
                        Vacuum2CallBack();
                        _curstep = SubStep.Sequence_End;
                        break;
                    case SubStep.Sequence_End:
                        LastStep = _curstep;
                        errmsg = "";
                        curstep = SubStep.Sequence_End;
                        return StepFuncRet.Finished;
                    case SubStep.Error:
                        LastStep = _curstep;
                        if (errmsg.Length <= 0) errmsg = "PC Timeout Error(설정된 에러내용이 없습니다.)";
                        _DAQDataSave = false;                      // DAQ Data Save
                        _pmData1Index = 0;                          // DAQ Data Count Index reset
                        curstep = SubStep.Error;
                        PLC_ServoJogStop(1.0);  // Servo Stop
                        return StepFuncRet.Error;
                }
                curstep = LastStep;
                if (curstep == SubStep.Error)
                {
                    _SubTestTimer.Stop(TestTime.TESTNAME.VACUUMHOLD_SEC);
                    return StepFuncRet.Error;
                }
                return StepFuncRet.Doing;
            }
            // ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^

            #region DoWork내부에서 사용하는 타이머
            DateTime _FirstTime = DateTime.Now;
            DateTime _LastTime  = DateTime.Now;
            DateTime _EndTime   = DateTime.Now;
            //double _DoingTime = 0.0;
            private void FirstTime()
            {
                this._FirstTime = DateTime.Now;
            }
            private void LastTime()
            {
                this._LastTime = DateTime.Now;
            }
            private double DoTime()
            { 
                DateTime curtime = DateTime.Now;
                return (double)(curtime-_LastTime).Ticks/10000000.0F;
            }
            #endregion

            // 시험 테스트 스펙
            public enum TESTRESULT : int                                             // 시험결과
            {
                NONE,              // 시험전
                OK,                // 합격
                NG,                // 불합격
                HOLD,              // 보류
                SYSTEM_ERROR,      // 시스템 에러
                ETC                // 기타사항
            }
            public enum TESTUNIT : int                                               // 시험 데이터 단위
            {
                mbar,        // 1Bar = 1000mbar = 100,000Pa = 14.504psi
                bar,
                mmHg,
                mmH2O,
                mLPers,      // mL / s 
                mlPermin,    // mL / min
                N,
                kN,
                mm
            }
            public struct GRIDLABEL                                                  // 그리드테이블에 표시될 공용
            {
                // 공용
                public string TestNo;            // 시험번호 : 향후 번호 및 순서명으로 사용하기 위하여 문자열로 한다.
                public string TestName;          // 시험명
                public double TestValue;         // 시험 기준값(예 1.5bar test => 1.5,  600mmHg Test => 600.0......)
                public double OKLowLimit;        // 판정하한값
                public double OKHighLimit;       // 판정상한값
                public double MeasurementValue;  // 측정값
                public TESTRESULT Result;        // 시험결과
                public TESTUNIT TestUnit;        // 측정값 단위
                public string Note;              // 시험 비고 사항
                public GRIDLABEL(string TN, string TN1, double TV, double OLL, double OHL, double MV, TESTRESULT R, TESTUNIT TU, string N)
                {
                    TestNo = TN;
                    TestName = TN1;
                    TestValue = TV;
                    OKLowLimit = OLL;
                    OKHighLimit = OHL;
                    MeasurementValue = MV;
                    Result = R;
                    TestUnit = TU;
                    Note = N;
                }
            }
            public struct PISTONMOVING_SPEC                                          // 피스톤 무빙 시험 조건
            {
                // 시험 조건
                public double CheckForwardForce;     // 최대 전진압(N, 5000=>5KN)
                public double CheckForwardRange1;    // 전진시 Range1 ~ Range2 구간 확인, ex) 0.2mm ~ 0.5mm 구간 Range1=0.2, Range2=0.5
                public double CheckForwardRange2;    //
                public double CheckForwardValueMin;  // 압력 검출 최소값
                public double CheckForwardValueMax;  // 압력 검출 최대값
                public double CheckForwardMaxLimit;  // 최대 전진 길이
                public double CheckBackwardRange1;   // 후진시 Range1 ~ Range2 구간 확인, ex) 0.2mm ~ 0.5mm 구간 Range1=0.2, Range2=0.5
                public double CheckBackwardRange2;   
                public double CheckBackwardValueMin;
                public double CheckBackwardValueMax;
                public PISTONMOVING_SPEC(double cff,double cfr1,double cfr2,double cfvmin,double cfvmax,double cfml,double cbr1,double cbr2,double cbvmin,double cbvmax)
                {
                    this.CheckForwardForce    = cff;
                    this.CheckForwardRange1    = cfr1;
                    this.CheckForwardRange2    = cfr2;
                    this.CheckForwardValueMin  = cfvmin;
                    this.CheckForwardValueMax  = cfvmax;
                    this.CheckForwardMaxLimit  = cfml;
                    this.CheckBackwardRange1   = cbr1;
                    this.CheckBackwardRange2   = cbr2;
                    this.CheckBackwardValueMin = cbvmin;
                    this.CheckBackwardValueMax = cbvmax;
                }
            }
            public struct LOSTTRAVEL_SPEC                                            // 로스트 트레블 시험 조건
            {
                // 시험 조건
                public double CheckForwardForce;     // 최대 전진압(N, 5000=>5KN)
                public double CheckForwardRange1;    // 전진시 Range1 ~ Range2 구간 확인
                public double CheckForwardRange2;    //
                public double CheckForwardValueMin;  // 
                public double CheckForwardValueMax;  //
                public double CheckForwardMaxLimit;  // 최대 전진 길이
                public double CheckBackwardRange1;
                public double CheckBackwardRange2;
                public double CheckBackwardValueMin;
                public double CheckBackwardValueMax;
                public LOSTTRAVEL_SPEC(double cff, double cfr1, double cfr2, double cfvmin, double cfvmax, double cfml, double cbr1, double cbr2, double cbvmin, double cbvmax)
                {
                    this.CheckForwardForce = cff;
                    this.CheckForwardRange1 = cfr1;
                    this.CheckForwardRange2 = cfr2;
                    this.CheckForwardValueMin = cfvmin;
                    this.CheckForwardValueMax = cfvmax;
                    this.CheckForwardMaxLimit = cfml;
                    this.CheckBackwardRange1 = cbr1;
                    this.CheckBackwardRange2 = cbr2;
                    this.CheckBackwardValueMin = cbvmin;
                    this.CheckBackwardValueMax = cbvmax;
                }
            }
            public struct PISTONSTROKE_SPEC                                          // 피스톤 스트로크 시험 조건
            {
                // 시험 조건
                public double CheckForwardForce;     // 최대 전진압(N, 5000=>5KN)
                public double CheckForwardRange1;    // 전진시 Range1 ~ Range2 구간 확인
                public double CheckForwardRange2;    //
                public double CheckForwardValueMin;  // 
                public double CheckForwardValueMax;  //
                public double CheckForwardMaxLimit;  // 최대 전진 길이
                public double CheckBackwardRange1;
                public double CheckBackwardRange2;
                public double CheckBackwardValueMin;
                public double CheckBackwardValueMax;
                public PISTONSTROKE_SPEC(double cff, double cfr1, double cfr2, double cfvmin, double cfvmax, double cfml, double cbr1, double cbr2, double cbvmin, double cbvmax)
                {
                    this.CheckForwardForce = cff;
                    this.CheckForwardRange1 = cfr1;
                    this.CheckForwardRange2 = cfr2;
                    this.CheckForwardValueMin = cfvmin;
                    this.CheckForwardValueMax = cfvmax;
                    this.CheckForwardMaxLimit = cfml;
                    this.CheckBackwardRange1 = cbr1;
                    this.CheckBackwardRange2 = cbr2;
                    this.CheckBackwardValueMin = cbvmin;
                    this.CheckBackwardValueMax = cbvmax;
                }
            }
            public struct VACUUM_SPEC                                                // 진공 시험 조건
            {
                // 시험 조건
                public double CheckForwardForce;     // 최대 전진압(N, 5000=>5KN)
                public double CheckForwardRange1;    // 전진시 Range1 ~ Range2 구간 확인
                public double CheckForwardRange2;    //
                public double CheckForwardValueMin;  // 
                public double CheckForwardValueMax;  //
                public double CheckForwardMaxLimit;  // 최대 전진 길이
                public double CheckBackwardRange1;
                public double CheckBackwardRange2;
                public double CheckBackwardValueMin;
                public double CheckBackwardValueMax;
                public VACUUM_SPEC(double cff, double cfr1, double cfr2, double cfvmin, double cfvmax, double cfml, double cbr1, double cbr2, double cbvmin, double cbvmax)
                {
                    this.CheckForwardForce = cff;
                    this.CheckForwardRange1 = cfr1;
                    this.CheckForwardRange2 = cfr2;
                    this.CheckForwardValueMin = cfvmin;
                    this.CheckForwardValueMax = cfvmax;
                    this.CheckForwardMaxLimit = cfml;
                    this.CheckBackwardRange1 = cbr1;
                    this.CheckBackwardRange2 = cbr2;
                    this.CheckBackwardValueMin = cbvmin;
                    this.CheckBackwardValueMax = cbvmax;
                }
            }

            private GRIDLABEL _AirLeakTestSpec15Pri = new GRIDLABEL();
            private GRIDLABEL _AirLeakTestSpec15Flo = new GRIDLABEL();
            private GRIDLABEL _AirLeakTestSpec50Pri = new GRIDLABEL();
            private GRIDLABEL _AirLeakTestSpec50Flo = new GRIDLABEL();
            private GRIDLABEL _PistonMovingFWDTest  = new GRIDLABEL();
            private GRIDLABEL _PistonMovingFWDTest_1 = new GRIDLABEL();  // 풀스트로크 저장용
            private GRIDLABEL _PistonMovingBWDTest  = new GRIDLABEL();
            private GRIDLABEL _LostTravelTest       = new GRIDLABEL();
            private GRIDLABEL _LostTravelTest_1     = new GRIDLABEL();   // FLO 측 압력 저장용
            private GRIDLABEL _LostTravelTest1      = new GRIDLABEL();   // PRI 측 압력 저장용
            private GRIDLABEL _LostTravelTest1_1    = new GRIDLABEL();   // FLO 측 압력 저장용
            private GRIDLABEL _PistonStrokeTest     = new GRIDLABEL();   // 프라이머리 유효 거리
            private GRIDLABEL _PistonStrokeTest_1   = new GRIDLABEL();   // 세컨더리 유효 거리
            private GRIDLABEL _PistonStrokeVacTest  = new GRIDLABEL();   // 프라이머리 진공량
            private GRIDLABEL _PistonStrokeVacTest_1 = new GRIDLABEL();   // 세컨더리 진공량
            private GRIDLABEL _VacuumTestPri        = new GRIDLABEL();
            private GRIDLABEL _VacuumTestFlo        = new GRIDLABEL();

            private PISTONMOVING_SPEC _PistonMovingSpec = new PISTONMOVING_SPEC();
            private LOSTTRAVEL_SPEC     _LostTravelSpec = new LOSTTRAVEL_SPEC();
            private PISTONSTROKE_SPEC _PistonStrokeSpec = new PISTONSTROKE_SPEC();
            private VACUUM_SPEC             _VacuumSpec = new VACUUM_SPEC(); 
            public PISTONMOVING_SPEC PistonMovingSpec
            {
                set { _PistonMovingSpec = value; }
                get { return this._PistonMovingSpec; }
            }
            public LOSTTRAVEL_SPEC LostTravelSpec
            {
                set { _LostTravelSpec = value; }
                get { return this._LostTravelSpec; }
            }
            public PISTONSTROKE_SPEC PistonStrokeSpec
            {
                set { _PistonStrokeSpec = value; }
                get { return this._PistonStrokeSpec; }
            }
            public VACUUM_SPEC VacuumSpec
            {
                set { _VacuumSpec = value; }
                get { return this._VacuumSpec; }
            }
            public GRIDLABEL AirLeakTestSpec15Pri
            {
                set { _AirLeakTestSpec15Pri = value; }
                get { return this._AirLeakTestSpec15Pri; }
            }
            public GRIDLABEL AirLeakTestSpec15Flo
            {
                set { _AirLeakTestSpec15Flo = value; }
                get { return this._AirLeakTestSpec15Flo; }
            }
            public GRIDLABEL AirLeakTestSpec50Pri
            {
                set { _AirLeakTestSpec50Pri = value; }
                get { return this._AirLeakTestSpec50Pri; }
            }
            public GRIDLABEL AirLeakTestSpec50Flo
            {
                set { _AirLeakTestSpec50Flo = value; }
                get { return this._AirLeakTestSpec50Flo; }
            }
            public GRIDLABEL PistonMovingFWDTest
            {
                set { _PistonMovingFWDTest = value; }
                get { return this._PistonMovingFWDTest; }
            }
            public GRIDLABEL PistonMovingFWDTest_1
            {
                set { _PistonMovingFWDTest_1 = value; }
                get { return this._PistonMovingFWDTest_1; }
            }
            public GRIDLABEL PistonMovingBWDTest
            {
                set { _PistonMovingBWDTest = value; }
                get { return this._PistonMovingBWDTest; }
            }
            public GRIDLABEL LostTravelTest
            {
                set { _LostTravelTest = value; }
                get { return this._LostTravelTest; }
            }
            public GRIDLABEL LostTravelTest_1
            {
                set { _LostTravelTest_1 = value; }
                get { return this._LostTravelTest_1; }
            }
            public GRIDLABEL LostTravelTest1
            {
                set { _LostTravelTest1 = value; }
                get { return this._LostTravelTest1; }
            }
            public GRIDLABEL LostTravelTest1_1
            {
                set { _LostTravelTest1_1 = value; }
                get { return this._LostTravelTest1_1; }
            }
            public GRIDLABEL PistonStrokeTest
            {
                set { _PistonStrokeTest = value; }
                get { return this._PistonStrokeTest; }
            }
            public GRIDLABEL PistonStrokeTest_1
            {
                set { _PistonStrokeTest_1 = value; }
                get { return this._PistonStrokeTest_1; }
            }
            public GRIDLABEL PistonStrokeVacTest
            {
                set { _PistonStrokeVacTest = value; }
                get { return this._PistonStrokeVacTest; }
            }
            public GRIDLABEL PistonStrokeVacTest_1
            {
                set { _PistonStrokeVacTest_1 = value; }
                get { return this._PistonStrokeVacTest_1; }
            }
            public GRIDLABEL VacuumTestPri
            {
                set { _VacuumTestPri = value; }
                get { return this._VacuumTestPri; }
            }
            public GRIDLABEL VacuumTestFlo
            {
                set { _VacuumTestFlo = value; }
                get { return this._VacuumTestFlo; }
            }


            /// <summary>
            /// NI-DAQ 관련 
            /// </summary>
            /// <returns></returns>
            #region NI-DAQ Subroutine
            private bool DAQ_HSInit() // 고속 모드용 DAQ 초기화
            {
                bool ret = true;

                try
                {
                    if ( inputTask != null )
                    {
                        inputTask.Stop();
                        inputTask.Dispose();
                        inputTask = null;
                    }
                    inputTask = new Task("BCPT NI-DAQ Input Task");       // Task 생성
                    ret = DAQ_ChannelSet(inputTask);                      // 채널 설정
                          DAQ_SampleRate(inputTask);                      // 샘플링 속도 설정
                    inputTask.Control(TaskAction.Verify);                 // Task 검사
                    HSStartTask();                                        
                    inputTask.Start();
                    inputCallback = new AsyncCallback(HSRead);
                    analogReader = new AnalogMultiChannelReader(inputTask.Stream);
                    analogReader.SynchronizeCallbacks = true;
                    analogReader.BeginReadMultiSample(2000, inputCallback, inputTask);
                   // analogReader.BeginReadMultiSample(DAQSAMPLE_COUNT, inputCallback, inputTask);
                }
                catch (Exception e1)
                {
                    string ErrMsg = e1.ToString();
                    ret = false;
                }
                return ret;
            }
            private bool DAQ_ChannelSet(Task task) // NI-DAQ CH 설정
            {
                #region NI-DAQ Task 초기화
                try
                {
                    if (Force == null)
                    {
                        Force = task.AIChannels.CreateVoltageChannel(
                            "dev1/ai0",
                            "Force",
                            //AITerminalConfiguration.Differential,
                            AITerminalConfiguration.Differential,
                            0.0,
                            10.0,
                            AIVoltageUnits.Volts
                            );
                    }
                    if (Displacement == null)
                    {
                        Displacement = task.AIChannels.CreateVoltageChannel(
                            "dev1/ai1",
                            "Displacement",
                            AITerminalConfiguration.Differential,
                            0,
                            10,
                            AIVoltageUnits.Volts
                            );
                    }
                    if (PriAir == null)
                    {
                        PriAir = task.AIChannels.CreateVoltageChannel(
                            "dev1/ai2",
                            "PriAir",   
                            AITerminalConfiguration.Differential,
                            0,
                            10,
                            AIVoltageUnits.Volts
                            );
                    }
                    if (FloAir == null)
                    {
                        FloAir = task.AIChannels.CreateVoltageChannel(
                            "dev1/ai3",
                            "FloAir",
                            AITerminalConfiguration.Differential,
                            0,
                            10,
                            AIVoltageUnits.Volts
                            );
                    }
                    if (PriVacuum == null)
                    {
                        PriVacuum = task.AIChannels.CreateVoltageChannel(
                            "dev1/ai4",
                            "PriVacuum",
                            AITerminalConfiguration.Differential,
                            0,
                            10,
                            AIVoltageUnits.Volts
                            );
                    }
                    if (FloVacuum == null)
                    {
                        FloVacuum = task.AIChannels.CreateVoltageChannel(
                            "dev1/ai5",
                            "FloVacuum",
                            AITerminalConfiguration.Differential,
                            0,
                            10,
                            AIVoltageUnits.Volts
                            );
                    }
                }
                catch (Exception e1)
                {
                    if (Bc.LogEnable)
                    {
                        Bc.Log.Error("[DAQ] DAQ_ChannelSet Exception :  " + e1.Message);
                    }
                    return false;
                }
                #endregion
                return true;
            }
            public const double DAQSAMPLE_HZ = 1000.0; // 6CH * 10000
            public const int DAQSAMPLE_COUNT = 10000;
            private void DAQ_SampleRate(Task task) // NI-DAQ 샘플링 속도 설정
            {
                // 10Khz, 10msec ticks data read....
                task.Timing.ConfigureSampleClock("",
                                                 DAQSAMPLE_HZ,
                                                 SampleClockActiveEdge.Rising,
                                                 SampleQuantityMode.ContinuousSamples,
                                                 DAQSAMPLE_COUNT);
            }
            private void HSStartTask() // 고속모드용 태스크 시작
            {
                if ( HSrunningTask == null )
                {
                    HSrunningTask = inputTask;
                }
            }
            private void HSStopTask() // 고속모드용 태스크 정지
            {
                _state.isDAQ = false;
                _state.isDAQHIGH = false;
                HSrunningTask = null;
                inputTask.Stop();
                inputTask.Dispose();                
            }
            public void HSDataSaveStart(int no) // 고속 데이터 수집지 메모리 배열로 저장
            {
                lock(this)
                {
                    _DAQDataSave = true;
                }
            }
            public void HSDataSaveStop() // 고속 데이터 수집지 메모리 배열로 저장하는 것을 정지
            {
                lock (this) { _DAQDataSave = false; }
            }
            public void HSDataSaveReset() // 고속 데이터 수집지 메모리 배열 내부 인덱스 값을 초기화
            {
                _pmData1Index = 0;
            }
            private void HSRead(IAsyncResult ar) // 고속모드 데이터 리더
            {
                try
                {
                    if (HSrunningTask != null && HSrunningTask == ar.AsyncState)
                    {
                        // Read the data
                        double[,] data = analogReader.EndReadMultiSample(ar);
                        int DataLength = data.GetLength(1);
                        int every10times = 0;
                        // Conversion data      
                        bool DataSave = _DAQDataSave; // 루프 중간에 데이터 세이브 시그널이 들어올 경우 문제 발생요지가 있어서...                  
                        for (int i = 0; i < DataLength; i++)
                        {
                            DAQPM tmp = new DAQPM(data[0, i]*2000.0, data[1, i]*(-10.0), data[2, i]*2.0, data[3, i]*2.0, data[4, i]*51.7006*3.0, data[5, i]*51.7006*3.0);
                            every10times++;
                            if (every10times>4)
                            {
                                    _DAQData.Force        = data[0, i] * 2000.0;
                                    _DAQData.Displacement = data[1, i] * (-10.0);
                                    _DAQData.PriAir       = data[2, i] * 2.0;
                                    _DAQData.FloAir       = data[3, i] * 2.0;
                                    _DAQData.PriVacuum    = data[4, i] * 51.7006 * 3.0;
                                    _DAQData.FloVacuum    = data[5, i] * 51.7006 * 3.0;              
                                every10times = 0;
                            }
                            lock (this)
                            {
                                if (DataSave)
                                {
                                    pmData1[_pmData1Index] = tmp;
                                    if (_pmData1Index >= (1000000 - 1))  _pmData1Index = 0;                                    
                                    else                                 _pmData1Index++;
                                }
                            }
                        }
                        analogReader.BeginReadMultiSample(10, inputCallback, inputTask);                          
                    }
                }
                catch (Exception ex)
                {
                    if (Bc.LogEnable)
                    {
                        Bc.Log.Error("[HSRead] 고속 DAQ엔진 펑션에서 Exception 발생 :  " + ex.Message);
                    }
                   HSStopTask();                    
                }
            }

            public void TestDAQStart()
            {
                _DAQDataSave = true;
            }
            public void TestDAQStop()
            {
                _DAQDataSave = false;
            }
            #endregion
            #region PLC Subroutine
            private bool PLC_Init() // PLC(act util driver) 초기화
            {              
                try
                {
                    if (PLC != null)
                    {
                        PLC.Close();
                        PLC = null;
                    }
                    PLC = new ActUtlTypeLib.ActUtlTypeClass();
                    PLC.ActLogicalStationNumber = 0;  //  PLC ST  qjsgh( ACT utility 설정 정보와 같아야 한다.
                    PLC.ActPassword = "";
                    int iRet = PLC.Open();
                    if (iRet.Equals(0))
                    {
                        return true;
                    }
                    else return false;
                }
                catch (Exception e1) 
                {
                    string errmsg = e1.Message;
                    return false; 
                }
            }
            public CMDRET PLC_ClearWorkArea(double timeoversec) // PLC I/F Memory area clear
            {
                Stopwatch st = new Stopwatch();
                bool ChkError = false;
                string DCode = "D100";
                if (timeoversec <= 0.0) timeoversec = 1.0;
                st.Start();
                while(true)
                {
                    if (st.ElapsedMilliseconds < timeoversec / 1000.0)
                    {
                        bool loopReadChk = true;
                        for (int i = 10; i < 40; i++)
                        {
                            DCode = string.Format("D100{0:D2}", i);
                            int Ret = PLC.WriteDeviceBlock(DCode, 1, 0);
                            if (Ret != 0) ChkError = true;
                            if (ChkError != false) return CMDRET.ERROR_WRITE;
                        }
                        Delay(50);
                        for (int i = 10; i < 40; i++)
                        {
                            int GetData;
                            DCode = string.Format("D100{0:D2}", i);
                            int Ret = PLC.ReadDeviceBlock(DCode, 1, out GetData);
                            if ((Ret != 0) || (GetData != 0)) loopReadChk = false;
                        }
                        if (loopReadChk) break;
                    }
                    else return CMDRET.TIMEOVER;
                }
                return CMDRET.DONE;
            }
            public CMDRET PLC_CurrentState() // PLC현재 조작상태 값을 읽어 내부변수에 반영
            {
                int PlcReadStatus;
                short[] PlcData = new short[2];
                
                if (PLC == null) return CMDRET.ERROR_READ;

                PlcReadStatus = PLC.ReadDeviceBlock2("D10100", 2, out PlcData[0]); // D10100, D10101 읽기
                if (PlcReadStatus.Equals(0))
                {
                    int Chk1 = (int)PlcData[0];
                    if ((Chk1 & 0x00000001) == 1) _Plcstate.AutomManual = true;
                    else _Plcstate.AutomManual = false;
                    if ((Chk1 & 0x00000004) == 1) _Plcstate.Reset = true;
                    else _Plcstate.Reset = false;
                    if ((Chk1 & 0x00000100) == 1) _Plcstate.LeakTest = true;
                    else _Plcstate.LeakTest = false;

                    int Chk2 = (int)PlcData[1];
                    if ((Chk2 & 0x00000001) == 1) _Plcstate.Loading = true;
                    else _Plcstate.Loading = false;


                    return CMDRET.DONE;
                }
                else return CMDRET.ERROR_READ;
            }
            public void PLC_PCRunSignlaSet()
            {
                if (PLC != null)
                {
                    int ret = PLC.WriteDeviceBlock("D10006", 1, 0x00000001);
                }
            }
            public void PLC_PCRunSignlaReset()
            {
                if (PLC != null)
                {
                    int ret = PLC.WriteDeviceBlock("D10006", 1, 0x00000000);
                }
            }
            /// <summary>
            /// PLC에 PC->PLC로 제어하는 코멘드 값을 클리어함.
            /// </summary>
            public void PLC_COMMAND_PART_CLEAR()
            {
                int ret = 0;
                ret = PLC.WriteDeviceBlock("D10011", 1, 0x00000000);
                Delay(20);
                ret = PLC.WriteDeviceBlock("D10012", 1, 0x00000000);
                Delay(20);
                ret = PLC.WriteDeviceBlock("D10013", 1, 0x00000000);
                Delay(20);
                ret = PLC.WriteDeviceBlock("D10040", 1, 0x00000000);
                Delay(20);
                ret = PLC.WriteDeviceBlock("D10041", 1, 0x00000000);
                Delay(20);
                ret = PLC.WriteDeviceBlock("D10000", 1, 0x00000001);
            }
            public CMDRET PLC_TestEndSet(int testresult)
            {
                int ret=0;
                ret = PLC.WriteDeviceBlock("D10011", 1, 0x00000000);
                Delay(20);
                ret = PLC.WriteDeviceBlock("D10012", 1, 0x00000000);
                Delay(20);
                ret = PLC.WriteDeviceBlock("D10013", 1, 0x00000000);
                Delay(20);
                ret = PLC.WriteDeviceBlock("D10040", 1, 0x00000000);
                Delay(20);
                ret = PLC.WriteDeviceBlock("D10041", 1, 0x00000000);
                Delay(20);
                PLC.WriteDeviceBlock("D10002", 1, testresult);
                Delay(20);
                ret = PLC.WriteDeviceBlock("D10000", 1, 0x00008001);
                Delay(30);
                return CMDRET.DONE;
            }
            public CMDRET PLC_TestEndNGCodeSet(int NGCode)
            {

                Delay(20);
                PLC.WriteDeviceBlock("D10009", 1, NGCode);
                Delay(20);
                return CMDRET.DONE;
            }




            public CMDRET PLC_BusySetting()
            {
                int ret = PLC.WriteDeviceBlock("D10000", 1, 0x00000003);
                Delay(100);
                return CMDRET.DONE;
            }
            private CMDRET PLC_BusySettingRead(double timeout)
            {
                short Data;
                int chk=0;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    int ret = PLC.ReadDeviceBlock2("D10000", 1, out Data);
                    if (ret.Equals(0))
                    {
                        if ((Data & 0x00000002) > 0) { chk = 1; break; }                                               
                    }
                    if (s1.ElapsedMilliseconds>timeout*1000.0)
                    {
                        chk = 0;
                        break;
                    }
                    Delay(100);
                }
                if (chk == 1) return CMDRET.DONE;
                else return CMDRET.TIMEOVER;
            }
            private CMDRET PLC_ClampSetting(CLAMP Cmd,double timeout)
            {              
                int chk = 0;
                int cmd = ((int)Cmd)&0x00003FFF;
                Stopwatch s1 = new Stopwatch();                
                s1.Start();
                while (true)
                {
                    PLC.WriteDeviceBlock("D10012", 1, (int)cmd);
                    if (cmd ==(int)CLAMP.CLEAR)
                    { 
                        chk = 1;
                        break;
                    }                                       
                    cmd = cmd & (0x00003FFF);
                    Delay(100);
                    short Data1;
                    int Data2;
                    PLC.ReadDeviceBlock2("D10112", 1, out Data1);
                    Data2 = (int)Data1;
                    Data2 = Data2 & (0x00003FFF);
                    if (cmd == (int)Data2) { chk = 1; break; }
                    if (s1.ElapsedMilliseconds > timeout * 1000.0)
                    {
                        chk = 0;
                        break;
                    }
                }
                if (chk == 1) return CMDRET.DONE;
                else return CMDRET.TIMEOVER;
            }
            public bool PLC_SOLSET(int Data0,int Data1,double Timeout)
            {
                // 2017/06/21 다시만듬
                // 32bit Word 기준으로...
                bool _Chk = false;
                Stopwatch _Stopwatch = new Stopwatch();
                _Stopwatch.Start();
                int _Readdata0 = 0xFFFFF;
                int _Readdata1 = 0xFFFFF;
                // Cmd Conversion

                while(true)
                {
                    try
                    {
                        PLC.WriteDeviceBlock("D10010", 1,Data0);
                        PLC.WriteDeviceBlock("D10011", 1,Data1);
                        Delay(50);
                        PLC.ReadDeviceBlock("D10110", 1, out _Readdata0);
                        PLC.ReadDeviceBlock("D10111", 1, out _Readdata1);
                        if ((Data0==_Readdata0)&&(Data1==_Readdata1))
                        {
                            _Chk = true;
                            break;
                        }
                        long durationmilliseconds = _Stopwatch.ElapsedMilliseconds;
                        double durationsecs       = durationmilliseconds / 1000.0;
                        if (durationsecs>Timeout)
                        {
                            _Chk = true;
                            break;
                        }
                    } catch (Exception e1)
                    {
                        string errmsg = e1.Message;
                        _Chk = false;
                        break;
                    }
                }
                _Stopwatch.Stop();
                if (_Chk) return true;
                else      return false;
            }
            public CMDRET PLC_SolSetting(SOL Cmd, double timeout)
            {
                int chk = 0;
                int cmd = ((int)Cmd)&0x0000FFFF;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    PLC.WriteDeviceBlock("D10011", 1, (int)cmd);
                    if (cmd == (int)SOL.CLEAR) 
                    { 
                        chk = 1;
                        break;
                    }  
                    cmd = cmd & (0x0000FFFF);
                    Delay(100);
                    short Data1;
                    int Data2;
                    PLC.ReadDeviceBlock2("D10111", 1, out Data1);
                    Data2 = (int)Data1;
                    Data2 = Data2 & (0x0000FFFF);
                    if (cmd == (int)Data2) { chk = 1; break; }
                    if (s1.ElapsedMilliseconds > timeout * 1000.0)
                    {
                        chk = 0;
                        break;
                    }
                }
                if (chk == 1) return CMDRET.DONE;
                else return CMDRET.TIMEOVER;
            }
            public CMDRET PLC_CosmoChSetting(int PriCh,int FloCh,double timeout)
            {
                int chk = 0;
                short Data1, Data2;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    PLC.WriteDeviceBlock("D10042", 1, PriCh);
                    PLC.WriteDeviceBlock("D10046", 1, FloCh);
                    Delay(50);       
                    PLC.ReadDeviceBlock2("D10142", 1, out Data1);
                    PLC.ReadDeviceBlock2("D10146", 1, out Data2);
                    Delay(10);
                    if ((PriCh == (int)Data1) && (FloCh == (int)Data2)) { chk = 1; break; }
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { chk = 0; break; }
                }
                if (chk == 1) return CMDRET.DONE;
                else          return CMDRET.TIMEOVER;
            }
            public CMDRET PLC_CosmoRunSetting(double timeout)
            {
                int chk = 0;
                short Data1, Data2;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    PLC.WriteDeviceBlock("D10040", 1, 0x00000001);
                    PLC.WriteDeviceBlock("D10041", 1, 0x00000001);
                    Delay(20);
                    PLC.ReadDeviceBlock2("D10040", 1, out Data1);
                    PLC.ReadDeviceBlock2("D10041", 1, out Data2);
                    Delay(10);
                    if ((0x00000001 == (int)Data1) && (0x00000001 == (int)Data2)) { chk = 1; break; }
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { chk = 0; break; }
                }
                if (chk == 1) return CMDRET.DONE;
                else return CMDRET.TIMEOVER;
            }
            public CMDRET PLC_CosmoStopSetting(double timeout)
            {
                int chk = 0;
                short Data1, Data2;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    PLC.WriteDeviceBlock("D10040", 1, 0x00000000);
                    PLC.WriteDeviceBlock("D10041", 1, 0x00000000);
                    Delay(50);
                    PLC.ReadDeviceBlock2("D10040", 1, out Data1);
                    PLC.ReadDeviceBlock2("D10041", 1, out Data2);                    
                    if ((0x00000000 == (int)Data1) && (0x00000000 == (int)Data2)) { chk = 1; break; }
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { chk = 0; break; }
                }
                if (chk == 1) return CMDRET.DONE;
                else return CMDRET.TIMEOVER;
            }
            /// <summary>
            /// 코스모 장치 시험 결과에서 에러를 확인하는 루틴
            /// </summary>
            /// <param name="timeout"></param>
            /// <param name="PriSide"></param>
            /// <param name="FloSide"></param>
            /// <returns></returns>
            public bool PLC_CosmoErrorCheck(double timeout,ref bool PriSide, ref bool FloSide)
            {
                bool Readchk = false;
                int ret1, ret2;
                short Data1, Data2;
                bool PRI_Error = false;
                bool FLO_Error = false;

                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    ret1 = PLC.ReadDeviceBlock2("D10140", 1, out Data1);  //PRI
                    Delay(10);
                    ret2 = PLC.ReadDeviceBlock2("D10141", 1, out Data2);  //FLO
                    Delay(10);
                    if ((ret1.Equals(0)) && (ret2.Equals(0)))
                    {
                        // 진행 스테이지가 없으면 정지
                        if ((Data1 & 0x00000004) > 0)  PRI_Error = true;
                        else                           PRI_Error = false;
                            
                        if ((Data2 & 0x000000004) > 0) FLO_Error = true;
                        else                           FLO_Error =false;

                        Readchk = true;
                        break; 
                    }
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { Readchk = false; break; }
                }
                if (Readchk)
                {
                    PriSide = PRI_Error;
                    FloSide = FLO_Error;
                }
                return Readchk;
            }
            public CMDRET PLC_CosmoStopChecking(double timeout)
            {
                int chk = 0;
                int ret1, ret2;
                short Data1, Data2;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    ret1 = PLC.ReadDeviceBlock2("D10140", 1, out Data1);
                    ret2 = PLC.ReadDeviceBlock2("D10141", 1, out Data2);
                    Delay(100);
                    if ((ret1.Equals(0))&&(ret2.Equals(0)))
                    {
                        // 진행 스테이지가 없으면 정지
                        if (((Data1 & 0x00000080) == 0) && ((Data2 & 0x000000080) == 0))
                        { 
                            chk = 1;
                            break;
                        }
                        // 에러로 인한 
                        //if (((Data1 & 0x00000040) > 0) && ((Data2 & 0x00000040) > 0)) { chk = 1; break; }   
                    }
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { chk = 0; break; }                    
                }
                if (chk == 1) return CMDRET.DONE;
                else return CMDRET.TIMEOVER;
            }
            public CMDRET PLC_CosmoChargeRun(double timeout)
            {
                int chk = 0;
                short Data1, Data2;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    PLC.WriteDeviceBlock("D10040", 1, 0x00000004);
                    PLC.WriteDeviceBlock("D10041", 1, 0x00000004);
                    Delay(50);
                    PLC.ReadDeviceBlock2("D10040", 1, out Data1);
                    PLC.ReadDeviceBlock2("D10041", 1, out Data2);
                    Delay(20);
                    if ((0x00000004 == (int)Data1) && (0x00000004 == (int)Data2)) { chk = 1; break; }
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { chk = 0; break; }
                }
                if (chk == 1) return CMDRET.DONE;
                else return CMDRET.TIMEOVER;
            }

            private CMDRET PLC_ServoPositionGet(out int pos,out int spd,double timeout)
            {
                int chk = 0;
                int ret1, ret2;
                short Data1, Data2;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    ret1 = PLC.ReadDeviceBlock2("D10128", 1, out Data1);
                    ret2 = PLC.ReadDeviceBlock2("D10132", 1, out Data2);
                    Delay(100);
                    if ((ret1.Equals(0)) && (ret2.Equals(0)))
                    {
                        chk = 1;
                        break; 
                    }
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { chk = 0; break; }
                }
                if (chk == 1) { pos = (int)Data1; spd = (int)Data2; return CMDRET.DONE; }
                else          { pos = 0; spd = 0; return CMDRET.TIMEOVER; }
            }
            private CMDRET PLC_ServoJogSpdSet(int Spd, double timeout)
            {
                int chk = 0;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    int chk1 = PLC.WriteDeviceBlock("D10028", 1, Spd);
                    // 추후 읽고 데이터가 정상적으로 들어 갔는지 확인하는 기능 삽입후 메세지 삭제.
                    if (chk1.Equals(0))
                    {
                        chk = 1;
                        break;
                    }
                    Delay(100);
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { chk = 0; break; }
                }
                if (chk != 1) return CMDRET.TIMEOVER; 
                else return CMDRET.DONE;
            }
            private CMDRET PLC_ServoJogForward(double timeout)
            {
                int chk = 0;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    int chk1 = PLC.WriteDeviceBlock("D10013", 1, 0x00000080);
                    // 추후 읽고 데이터가 정상적으로 들어 갔는지 확인하는 기능 삽입후 메세지 삭제.
                    if (chk1.Equals(0))
                    {
                        chk = 1;
                        break;
                    }
                    Delay(100);
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { chk = 0; break; }
                }
                if (chk != 1) return CMDRET.TIMEOVER; 
                else return CMDRET.DONE;
            }
            private CMDRET PLC_ServoJogBackward(double timeout)
            {
                int chk = 0;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    int chk1 = PLC.WriteDeviceBlock("D10013", 1, 0x00000100);
                    // 추후 읽고 데이터가 정상적으로 들어 갔는지 확인하는 기능 삽입후 메세지 삭제.
                    if (chk1.Equals(0))
                    {
                        chk = 1;
                        break;
                    }
                    Delay(100);
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { chk = 0; break; }
                }
                if (chk != 1) return CMDRET.TIMEOVER;
                else return CMDRET.DONE;
            }
            private CMDRET PLC_ServoJogStop(double timeout)
            {
                int chk = 0;
                Stopwatch s1 = new Stopwatch();
                s1.Start();
                while (true)
                {
                    int chk1 = PLC.WriteDeviceBlock("D10013", 1, 0x00000000);
                    // 추후 읽고 데이터가 정상적으로 들어 갔는지 확인하는 기능 삽입후 메세지 삭제.
                    if (chk1.Equals(0))
                    {
                        chk = 1;
                        break;
                    }
                    Delay(100);
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { chk = 0; break; }
                }
                if (chk != 1) return CMDRET.TIMEOVER; 
                else return CMDRET.DONE;
            }
            private CMDRET PLC_ServoHomeGo(int pos, int spd, double timeout)
            {
                int chk = 0;
                int ret1, ret2, ret3;
                short Data1;
                Stopwatch s1 = new Stopwatch();

                ret1 = PLC.WriteDeviceBlock("D10032", 1, pos);
                ret2 = PLC.WriteDeviceBlock("D10036", 1, spd);
                Delay(100);
                ret3 = PLC.WriteDeviceBlock("D10013", 1, 0x00000200);
                Delay(100);
                s1.Start();
                while (true)
                {
                    ret1 = PLC.ReadDeviceBlock2("D10113", 1, out Data1);
                    if (ret1.Equals(0))
                    {
                        int Check = (int)Data1 & 0x00000400;
                        if (Check > 0)
                        {
                            chk = 1;
                            ret3 = PLC.WriteDeviceBlock("D10013", 1, 0x00000400);
                            Delay(100);
                            break;
                        }                        
                    }
                    if (s1.ElapsedMilliseconds > timeout * 1000.0) { chk = 0; break; }
                    Delay(100);
                }

                if (chk == 1)
                {
                    PLC_ServoJogStop(1.0);
                    return CMDRET.DONE;
                }
                else
                {
                    PLC_ServoJogStop(1.0);
                    return CMDRET.TIMEOVER;
                }
            }

            // 2017-1024 MES/LDS관련 추가
            /// <summary>
            /// PLC -> PC LDS와 연동 신호 확인(10107)
            /// 상기 신호가 True일 경우 MES/LDS와 연동하여 해당 작업 진행 유무 결정 프로세스를 진행하기 위한 정보를 읽음
            /// 메인 타이머에서 구동되므로 실시간 처리로 타임아웃 없음
            /// </summary>
            /// <param name="result"></param>
            /// <returns></returns>
            public bool CMD_Read_LDSSignal(ref bool result)
            {
                int PLC_Chk = -1;
                short PLCcmd = 0x0000;
                bool Read_OK = false;
                Stopwatch s1 = new Stopwatch();

                PLC_Chk = PLC.ReadDeviceBlock2("D10107", 1, out PLCcmd);
                if (PLC_Chk.Equals(0))
                {
                    if (PLCcmd == 0x0001) result = true;
                    if (PLCcmd == 0x0000) result = false;
                    Read_OK = true;
                }

                if (Read_OK) return true;
                else return false;
            }
            public bool CMD_Read_SystemStatus(double timeout, ref string status)
            {
                int PLC_Chk = -1;
                short PLCcmd = 0;
                bool Read_OK = false;
                Stopwatch s1 = new Stopwatch();
                byte bLow = 0x00;

                s1.Start();

                while (true)
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10165", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        bLow = (byte)(PLCcmd & 0x00FF);
                        status = (PLCcmd < 10) ? PLCcmd.ToString() : "0";
                        Read_OK = true;
                        break;
                    }
                    if (s1.ElapsedMilliseconds >= (timeout * 1000))
                    {
                        break;
                    }
                    Delay(10);
                }
                if (Read_OK)
                {
                    return true;
                }
                else return false;
            }
            /// <summary>
            /// PLC -> PC 바코드 정보 읽기
            /// </summary>
            /// <param name="timeout"></param>
            /// <param name="barcode"></param>
            /// <returns></returns>
            public bool CMD_Read_BarCode(double timeout, ref string barcode)
            {
                int PLC_Chk = -1;
                short[] PLCcmd = new short[10];
                bool Read_OK = false;
                Stopwatch s1 = new Stopwatch();
                string readBarCode = "";
                byte bHigh = 0x00;
                byte bLow = 0x00;

                s1.Start();

                while (true)
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10150", 10, out PLCcmd[0]);
                    Delay(10);
                    PLC_Chk = PLC.ReadDeviceBlock2("D10150", 10, out PLCcmd[0]);
                    if (PLC_Chk.Equals(0))
                    {
                        readBarCode = "";
                        for (int i = 0; i < 6; i++)
                        {
                            bHigh = (byte)((PLCcmd[i] & 0xFF00) >> 8);
                            bLow = (byte)(PLCcmd[i] & 0x00FF);
                            if (i != 5) readBarCode = readBarCode + System.Text.Encoding.ASCII.GetString(new[] { bLow }) + System.Text.Encoding.ASCII.GetString(new[] { bHigh });
                            if (i == 5) readBarCode = readBarCode + System.Text.Encoding.ASCII.GetString(new[] { bLow });
                        }

                        Read_OK = true;
                        break;
                    }
                    if (s1.ElapsedMilliseconds >= (timeout * 1000))
                    {
                        break;
                    }
                    Delay(10);
                }
                if (Read_OK)
                {
                    barcode = readBarCode;
                    return true;
                }
                else return false;
            }
            public bool CMD_Read_ModelLDSCheck(double timeout, ref string modelCode)
            {
                // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
                int PLC_Chk = -1;               
                short PLCcmd = 0x0000;
                bool Read_OK = false;
                Stopwatch s1 = new Stopwatch();
                string readModel = "";
                s1.Start();
                while (true)
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10160", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        byte DataHigh = (byte)((PLCcmd & 0xFF00) >> 8);
                        byte DataLow = (byte)(PLCcmd & 0x00FF);
                        readModel = System.Text.Encoding.ASCII.GetString(new[] { DataHigh }) + System.Text.Encoding.ASCII.GetString(new[] { DataLow });
                        Read_OK = true;
                        break;
                    }
                    if (s1.ElapsedMilliseconds >= (timeout * 1000))
                    {
                        break;
                    }
                    Delay(10);
                }
                if (Read_OK) return true;
                else return false;

            }
            public bool CMD_Read_ModelCode(double timeout, ref string modelCode)
            {


                // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
                int PLC_Chk = -1;
                short PLCcmd = 0x0000;
                bool Read_OK = false;
                Stopwatch s1 = new Stopwatch();
                string readModel = "";
                modelCode = readModel;
                s1.Start();
                while (true)
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10160", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        byte DataHigh = (byte)((PLCcmd & 0xFF00) >> 8);
                        byte DataLow = (byte)(PLCcmd & 0x00FF);
                        readModel = System.Text.Encoding.ASCII.GetString(new[] { DataLow }) + System.Text.Encoding.ASCII.GetString(new[] { DataHigh });
                        modelCode = readModel;
                        Read_OK = true;
                        break;
                    }
                    if (s1.ElapsedMilliseconds >= (timeout * 1000))
                    {
                        break;
                    }
                    Delay(10);
                }
                if (Read_OK) return true;
                else return false;

            }
            public bool CMD_Read_ModelChange(double timeout, ref int result)
            {
                int   PLC_Chk  = -1;
                short PLCcmd   = 0x0000;
                bool   Read_OK = false;
                Stopwatch s1 = new Stopwatch();
      

                result = -1;

                s1.Start();
                while (true)
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10161", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        if (PLCcmd == 0x0001) result = 1;
                        if (PLCcmd == 0x0000) result = 0;
                        Read_OK = true;
                        break;
                    }
                    if (s1.ElapsedMilliseconds >= (timeout * 1000))
                    {
                        break;
                    }
                    Delay(10);
                }
                if (Read_OK) return true;
                else return false;
            }
            public bool CMD_Read_TyepRequest(double timeout, ref int result)
            {
                int   PLC_Chk = -1;
                short PLCcmd  = 0x0000;
                bool  Read_OK = false;
                Stopwatch s1  = new Stopwatch();

                result = -1;

                s1.Start();
                while (true)
                {
                    PLC_Chk = PLC.ReadDeviceBlock2("D10163", 1, out PLCcmd);
                    if (PLC_Chk.Equals(0))
                    {
                        if (PLCcmd == 0x0001) result = 1;
                        if (PLCcmd == 0x0000) result = 0;
                        Read_OK = true;
                        break;
                    }
                    if (s1.ElapsedMilliseconds >= (timeout * 1000))
                    {
                        break;
                    }
                    Delay(10);
                }
                if (Read_OK) return true;
                else return false;
            }
            public bool CMD_Write_ModelLDSBusy(double timeout, int setValue)
            {
                // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
                int PLC_Chk = -1;
                int PC_Chk = -1;
                int PCcmd = 0x00000000;  // COSMO CHARGE MDOE START=10040.2
                short PLCcmd = 0x0000;
                bool Send_OK = false;
                Stopwatch s1 = new Stopwatch();
                s1.Start();

                if (setValue == 0) PCcmd = 0x00000000;
                if (setValue == 1) PCcmd = 0x00000001;

                while (true)
                {
                    PC_Chk = PLC.WriteDeviceBlock("D10007", 1, (int)PCcmd);
                    if (PC_Chk.Equals(0))
                    {
                        PLC_Chk = PLC.ReadDeviceBlock2("D10007", 1, out PLCcmd);
                        if (PLC_Chk.Equals(0))
                        {
                            if (PCcmd == PLCcmd)
                            {
                                Send_OK = true;
                                break;
                            }
                        }
                    }
                    if (s1.ElapsedMilliseconds >= (timeout * 1000))
                    {
                        break;
                    }
                    Delay(10);
                }
                if (Send_OK) return true;
                else return false;
            }
            public bool CMD_Write_ModelLDSResult(double timeout, int setValue)
            {
                // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
                int PLC_Chk = -1;
                int PC_Chk = -1;
                int PCcmd = 0x00000000;  // COSMO CHARGE MDOE START=10040.2
                short PLCcmd = 0x0000;
                bool Send_OK = false;
                Stopwatch s1 = new Stopwatch();
                s1.Start();

                if (setValue == 0) PCcmd = 0x00000000;  // 초기화 ++
                if (setValue == 1) PCcmd = 0x00000001;  // OK
                if (setValue == 2) PCcmd = 0x00000002;  // NG

                while (true)
                {
                    PC_Chk = PLC.WriteDeviceBlock("D10008", 1, (int)PCcmd);
                    if (PC_Chk.Equals(0))
                    {
                        PLC_Chk = PLC.ReadDeviceBlock2("D10008", 1, out PLCcmd);
                        if (PLC_Chk.Equals(0))
                        {
                            if (PCcmd == PLCcmd)
                            {
                                Send_OK = true;
                                break;
                            }
                        }
                    }
                    if (s1.ElapsedMilliseconds >= (timeout * 1000))
                    {
                        break;
                    }
                    Delay(10);
                }
                if (Send_OK) return true;
                else return false;
            }
            public bool CMD_Write_ModelBusy(double timeout, int setValue)
            {
                // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
                int PLC_Chk = -1;
                int PC_Chk = -1;
                int PCcmd = 0x00000000;  // COSMO CHARGE MDOE START=10040.2
                short PLCcmd = 0x0000;
                bool Send_OK = false;
                Stopwatch s1 = new Stopwatch();
                s1.Start();

                if (setValue == 0) PCcmd = 0x00000000;
                if (setValue == 1) PCcmd = 0x00000001;

                while (true)
                {
                    PC_Chk = PLC.WriteDeviceBlock("D10060", 1, (int)PCcmd);
                    if (PC_Chk.Equals(0))
                    {
                        PLC_Chk = PLC.ReadDeviceBlock2("D10060", 1, out PLCcmd);
                        if (PLC_Chk.Equals(0))
                        {
                            if (PCcmd == PLCcmd)
                            {
                                Send_OK = true;
                                break;
                            }
                        }
                    }
                    if (s1.ElapsedMilliseconds >= (timeout * 1000))
                    {
                        break;
                    }
                    Delay(10);
                }
                if (Send_OK) return true;
                else return false;
            }
            public bool CMD_Write_ModelResult(double timeout, int setValue)
            {
                // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
                int PLC_Chk = -1;
                int PC_Chk = -1;
                int PCcmd = 0x00000000;  // COSMO CHARGE MDOE START=10040.2
                short PLCcmd = 0x0000;
                bool Send_OK = false;
                Stopwatch s1 = new Stopwatch();
                s1.Start();

                if (setValue == 0) PCcmd = 0x00000000;
                if (setValue == 1) PCcmd = 0x00000001;

                while (true)
                {
                    PC_Chk = PLC.WriteDeviceBlock("D10061", 1, (int)PCcmd);
                    if (PC_Chk.Equals(0))
                    {
                        PLC_Chk = PLC.ReadDeviceBlock2("D10060", 1, out PLCcmd);
                        if (PLC_Chk.Equals(0))
                        {
                            if (PCcmd == PLCcmd)
                            {
                                Send_OK = true;
                                break;
                            }
                        }
                    }
                    if (s1.ElapsedMilliseconds >= (timeout * 1000))
                    {
                        break;
                    }
                    Delay(10);
                }
                if (Send_OK) return true;
                else return false;
            }
            public bool CMD_Write_ANDON(int[] setValue,int count)
            {    
                int PC_Chk = 0;

                for (int i=0; i<count; i++)
                {
                    string addressStr = string.Format("D{0:D5}",i+10065);
                    PC_Chk = PLC.WriteDeviceBlock(addressStr, 1, (int)setValue[i]);
                    Delay(10);
                }
            return true;
            }
           public bool CMD_Write_ModelNumber(double timeout, Int16 setValue)
            {
                // 시작신호후 감지신호가 없으므로 PLC에 쓰기한 데이터 영역만 재확인하여 처리함.
                int PLC_Chk = -1;
                int PC_Chk = -1;
                int PCcmd = 0x00000000;  // COSMO CHARGE MDOE START=10040.2
                short PLCcmd = 0x0000;
                bool Send_OK = false;
                Stopwatch s1 = new Stopwatch();
                s1.Start();

                PCcmd = (int)setValue;

                while (true)
                {
                    PC_Chk = PLC.WriteDeviceBlock("D10062", 1, (int)PCcmd);
                    if (PC_Chk.Equals(0))
                    {
                        PLC_Chk = PLC.ReadDeviceBlock2("D10062", 1, out PLCcmd);
                        if (PLC_Chk.Equals(0))
                        {
                            if (PCcmd == PLCcmd)
                            {
                                Send_OK = true;
                                break;
                            }
                        }
                    }
                    if (s1.ElapsedMilliseconds >= (timeout * 1000))
                    {
                        break;
                    }
                    Delay(10);
                }
                if (Send_OK) return true;
                else return false;
            }

            #endregion
            #region COSMO Subroutine
            // COSMO 초기화
            private bool COSMOInit()
            {               
                #region COSMO #1,#2 => COM3,COM4 포트초기화 및 변수 정리
                if (Cosmo1 != null)
                {
                    if (Cosmo1.IsOpen) Cosmo1.CloseComm();
                    Cosmo1 = null;
                }
                if (Cosmo2 != null)
                {
                    if (Cosmo2.IsOpen) Cosmo1.CloseComm();
                    Cosmo2 = null;
                }
                Cosmo1 = new SerialComm();
                Cosmo1.DataReceivedHandler = Cosmo1DRH; // Cosmo 1 Data Received Handler
                Cosmo1.DisconnectedHandler = Cosmo1DH;  // Cosmo 1 Disconnected Handler

                Cosmo2 = new SerialComm();
                Cosmo2.DataReceivedHandler = Cosmo2DRH; // Cosmo 2 Data Received Handler
                Cosmo2.DisconnectedHandler = Cosmo2DH;  // Cosmo 2 Disconnected Handler
                
                bool comChk1 = Cosmo1.OpenComm("COM3", 115200, 8, StopBits.One, Parity.None, Handshake.None);  // PRI             
                bool comChk2 = Cosmo2.OpenComm("COM4", 115200, 8, StopBits.One, Parity.None, Handshake.None);  // FLO
                if ((comChk1) && (comChk2)) _state.isCOSMO = true;
                else                        _state.isCOSMO = false;

                Cosmo1ST = new CosmoDFormat("", 0.0, 0.0, 0.0, 0.0, 0.0, "", 0);
                Cosmo2ST = new CosmoDFormat("", 0.0, 0.0, 0.0, 0.0, 0.0, "", 1);
                #endregion
                return _state.isCOSMO;
            }
            // Cosmo 1(PRI) 데이터 수신후 버퍼로 이동
            private void Cosmo1DRH(byte[] receiveData)
            {
                string Msg = Encoding.Default.GetString(receiveData); // 문자열 변환
                lock (this) { priBuf += Msg; }
                if (priBuf.IndexOf("\r") > 0)
                {
                    rtLampCallBack(true, false, false);
                    while (true)
                    {
                        String t = priBuf.Substring(0, priBuf.IndexOf("\r") + 1);
                        var parts = t.Split(',');
                        if (parts.Length == 8)
                        {
                            if (_priDataIndex < 1000)
                            {  // D Format 일 경우
                                priData[_priDataIndex] = new CosmoDFormat(parts[0], double.Parse(parts[1]), double.Parse(parts[2]), double.Parse(parts[3]),
                                                                          double.Parse(parts[4]), double.Parse(parts[5]), parts[6], int.Parse(parts[7]));
                                _priDataIndex++;
                            }
                            else _priDataIndex = 0;// Auto circulation clear buffer
                            lock (this)
                            {
                                priBuf = priBuf.Replace(t, "");  // 8개의 데이터 항목을 읽은 후 버퍼에서 해당자료를 삭제한다.
                            }
                        }
                        if (priBuf.IndexOf("\r") <= 0) break; // 버퍼중 더 변환할 데이터 유무를 조사
                    }
                    rtLampCallBack(false, false, false);
                }
            }
            // Cosmo 1(PRI) 연결 종료시 실행
            private void Cosmo1DH()
            {
                PriDataIndexReset = 0;
            }
            // Cosmo 2(FLO) 데이터 수신후 버퍼로 이동
            private void Cosmo2DRH(byte[] receiveData)
            {
                string Msg = Encoding.Default.GetString(receiveData); // 문자열 변환
                floBuf += Msg; 
                if (floBuf.IndexOf("\r") > 0)
                {
                    while (true)
                    {
                        rtLampCallBack(false, true, false);
                        String t = floBuf.Substring(0, floBuf.IndexOf("\r") + 1);
                        var parts = t.Split(',');
                        if (parts.Length == 8)
                        {
                            if (_floDataIndex < 1000)
                            {  // D Format 일 경우
                                floData[_floDataIndex] = new CosmoDFormat(parts[0], double.Parse(parts[1]), double.Parse(parts[2]), double.Parse(parts[3]),
                                                                          double.Parse(parts[4]), double.Parse(parts[5]), parts[6], int.Parse(parts[7]));
                                _floDataIndex++;
                            }
                            else _floDataIndex = 0;// Auto circulation clear buffer
                     
                                floBuf = floBuf.Replace(t, "");  // 8개의 데이터 항목을 읽은 후 버퍼에서 해당자료를 삭제한다.                            
                        }
                        if (floBuf.IndexOf("\r") <= 0) break; // 버퍼중 더 변환할 데이터 유무를 조사
                    }
                    rtLampCallBack(false, false, false);
                }
            }
            // Cosmo 2(FLO) 연결 종료시 실행
            private void Cosmo2DH()
            {
                FloDataIndexReset = 0;
            }
            #endregion
            #region 기타
            public void PCAliveRun() // PC-PLC Alive singnal gene
            {
                if (_PCLive!=null)
                {
                    _PCLive.Stop();
                    _PCLive = null;
                }
                _PCLive = new System.Timers.Timer();
                _PCLive.Interval = 2000;
                _PCLive.Elapsed += new System.Timers.ElapsedEventHandler(_PCLive_Elapsed);
                _PCLive.Start();    
            }
            void _PCLive_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
            {
                if (_LastLiveSignal)
                {
                    PLC_PCRunSignlaReset();
                    _LastLiveSignal = false;
                }
                else
                {
                    PLC_PCRunSignlaSet();
                    _LastLiveSignal = true;
                }
            }
            public void PCAliveStop()
            {
                _PCLive.Stop();
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
            #endregion

        }
        #endregion
        #region SerialComm, IOMAP, Config, LeakTest_Specification, AllTest_Specification
        public class IOMAP
        {
            public string PlcTcpip                          = "192.168.0.10";
            public double LowPassFilterHz                   = 60.0;
            public double LowPassFilterSampleRate           = 1000.0;
            public double MasterSampleRate                  = 6000.0; // 6ch * 1Kh = 6000.0;
            public int    MasterSampleCount                 = 600;    // 0.5sec interval task running.... 
            // Grid 표시용
            public BCControl.GRIDLABEL AirLeakTestSpec15Pri = new BCControl.GRIDLABEL();
            public BCControl.GRIDLABEL AirLeakTestSpec15Flo = new BCControl.GRIDLABEL();
            public BCControl.GRIDLABEL AirLeakTestSpec50Pri = new BCControl.GRIDLABEL();
            public BCControl.GRIDLABEL AirLeakTestSpec50Flo = new BCControl.GRIDLABEL();
            public BCControl.GRIDLABEL PistonMovingFWDTest  = new BCControl.GRIDLABEL();
            public BCControl.GRIDLABEL PistonMovingBWDTest  = new BCControl.GRIDLABEL();
            public BCControl.GRIDLABEL LostTravelTest       = new BCControl.GRIDLABEL();
            public BCControl.GRIDLABEL PistonStrokeTest     = new BCControl.GRIDLABEL();
            public BCControl.GRIDLABEL VacuumTestPri        = new BCControl.GRIDLABEL();
            public BCControl.GRIDLABEL VacuumTestFlo        = new BCControl.GRIDLABEL();
            // 실제 시험 내부 확인용
            public BCControl.PISTONMOVING_SPEC PistonMovingSpec = new BCControl.PISTONMOVING_SPEC();
            public BCControl.LOSTTRAVEL_SPEC   LostTravelSpec   = new BCControl.LOSTTRAVEL_SPEC();
            public BCControl.PISTONSTROKE_SPEC PistonStrokeSpec = new BCControl.PISTONSTROKE_SPEC();
            public BCControl.VACUUM_SPEC       VacuumSpec       = new BCControl.VACUUM_SPEC();
        }
        public class Config                        // 저장이 필요한 데이터만 Config에 정의후 읽기/쓰기
        {
            // name of the .xml file
            public static string CONFIG_FNAME = "BCPTconfig.xml";
            public static ConfigData GetConfigData()
            {
                if (!File.Exists(CONFIG_FNAME)) // create config file with default values
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Create))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(ConfigData));
                        ConfigData sxml = new ConfigData();
                        xs.Serialize(fs, sxml);
                        return sxml;
                    }
                }
                else // read configuration from file
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Open))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(ConfigData));
                        ConfigData sc = (ConfigData)xs.Deserialize(fs);
                        return sc;
                    }
                }
            }
            public static bool SaveConfigData(ConfigData config)
            {
                if (File.Exists(CONFIG_FNAME)) File.Delete(CONFIG_FNAME);

                using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.OpenOrCreate))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(ConfigData));
                    xs.Serialize(fs, config);
                    return true;
                }
            }
            // this class holds configuration data
            public class ConfigData
            {
                // 저장이 필요한 자료만 IOMAP에서 발취하여 관리한다.
                public IOMAP IoMap = new IOMAP();
                public ConfigData()
                {   // BCPTCOnfig.xml 화일이 없을 경우 아래의 값으로 초기화되어 설정화일이 만들어짐.
                    IoMap.PlcTcpip                = "192.168.0.10";
                    IoMap.LowPassFilterHz         = 60.0; // Low Pass Filter Vaule : 60Hz
                    IoMap.LowPassFilterSampleRate = 1000.0;
                    IoMap.MasterSampleRate        = 6000.0;
                    IoMap.MasterSampleCount       = 600;
                    // 그리드 결과 테이블 및 제어판단기준
                    IoMap.AirLeakTestSpec15Pri    = new BCControl.GRIDLABEL("1", "1.5bar 리크 테스트(PRI)",  0.0, 0.0, 100.0, 0.0, BCControl.TESTRESULT.NONE, BCControl.TESTUNIT.mbar, "");
                    IoMap.AirLeakTestSpec15Flo    = new BCControl.GRIDLABEL("2", "1.5bar 리크 테스트(FLO)",  0.0, 0.0, 100.0, 0.0, BCControl.TESTRESULT.NONE, BCControl.TESTUNIT.mbar, "");
                    IoMap.AirLeakTestSpec50Pri    = new BCControl.GRIDLABEL("3", "5.0bar 리크 테스트(PRI)",  0.0, 0.0, 100.0, 0.0, BCControl.TESTRESULT.NONE, BCControl.TESTUNIT.mbar, "");
                    IoMap.AirLeakTestSpec50Flo    = new BCControl.GRIDLABEL("4", "5.0bar 리크 테스트(FLO)",  0.0, 0.0, 100.0, 0.0, BCControl.TESTRESULT.NONE, BCControl.TESTUNIT.mbar, "");
                    IoMap.PistonMovingFWDTest     = new BCControl.GRIDLABEL("5", "피스톤 무빙 테스트(전진)", 0.0, 0.3, 100.0, 0.0, BCControl.TESTRESULT.NONE, BCControl.TESTUNIT.N, "");
                    IoMap.PistonMovingBWDTest     = new BCControl.GRIDLABEL("6", "피스톤 무빙 테스트(후진)", 0.0, 0.3, 10.0,  0.0, BCControl.TESTRESULT.NONE, BCControl.TESTUNIT.N, "");
                    IoMap.LostTravelTest          = new BCControl.GRIDLABEL("7", "로스트 트레블 테스트",     0.0, 0.0, 1.5,   0.0, BCControl.TESTRESULT.NONE, BCControl.TESTUNIT.mm, "");
                    IoMap.PistonStrokeTest        = new BCControl.GRIDLABEL("8", "피스톤 스트로크 테스트",   0.0, 0.0, 10.0,  0.0, BCControl.TESTRESULT.NONE, BCControl.TESTUNIT.mm, "");
                    IoMap.VacuumTestPri           = new BCControl.GRIDLABEL("9", "진공 테스트(PRI)",         38.0, 0.0, 380.0, 0.0, BCControl.TESTRESULT.NONE, BCControl.TESTUNIT.mmHg, "");
                    IoMap.VacuumTestFlo           = new BCControl.GRIDLABEL("10", "진공 테스트(FLO)",        38.0, 0.0, 380.0, 0.0, BCControl.TESTRESULT.NONE, BCControl.TESTUNIT.mmHg, "");  
                    // 상세 제어 판단 기준
                    IoMap.PistonMovingSpec        = new BCControl.PISTONMOVING_SPEC(5000.0, 0.5, 1.0, 0.0, 100.0, 50.0, 0.0, 1.0, 0.0, 10.0);
                    IoMap.LostTravelSpec          = new BCControl.LOSTTRAVEL_SPEC();  // 초기화 지정하고 삭제
                    IoMap.PistonStrokeSpec        = new BCControl.PISTONSTROKE_SPEC();
                    IoMap.VacuumSpec              = new BCControl.VACUUM_SPEC();
                }
            }
        }
        public class LeakTest_Specification        // 리크테스트 시험 기본사양 읽기/쓰기
        {
            // name of the .xml file
            public static string CONFIG_FNAME = "LeakTestDefine.xml";
            public static LeakTestDefine GetConfigData()
            {
                if (!File.Exists(CONFIG_FNAME)) // create config file with default values
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Create))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(LeakTestDefine));
                        LeakTestDefine sxml = new LeakTestDefine();
                        xs.Serialize(fs, sxml);
                        return sxml;
                    }
                }
                else // read configuration from file
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Open))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(LeakTestDefine));
                        LeakTestDefine sc = (LeakTestDefine)xs.Deserialize(fs);
                        return sc;
                    }
                }
            }
            public static bool SaveConfigData(LeakTestDefine config)
            {
                if (File.Exists(CONFIG_FNAME)) File.Delete(CONFIG_FNAME);

                using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.OpenOrCreate))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(LeakTestDefine));
                    xs.Serialize(fs, config);
                    return true;
                }
            }
        }
        public class AllTest_Specification         // 전체 테스트 시험 기본사양 읽기/쓰기
        {
            // name of the .xml file
            public static string CONFIG_FNAME = "AllTestDefine.xml";
            public static TestSpecification GetConfigData()
            {
                if (!File.Exists(CONFIG_FNAME)) // create config file with default values
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Create))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(TestSpecification));
                        TestSpecification sxml = new TestSpecification();
                        xs.Serialize(fs, sxml);                        
                        return sxml;
                    }
                }
                else // read configuration from file
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Open))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(TestSpecification));
                        TestSpecification sc = (TestSpecification)xs.Deserialize(fs);
                        return sc;
                    }
                }
            }
            public static bool SaveConfigData(TestSpecification config)
            {
                if (File.Exists(CONFIG_FNAME)) File.Delete(CONFIG_FNAME);

                using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.OpenOrCreate))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(TestSpecification));
                    xs.Serialize(fs, config);
                    return true;
                }
            }
        }
        #endregion
        #region 단계 시험별로 시간 측정용 클래스
        public class TestTime
        {

            public const int MAX_TESTCCOUNTS = 9;
            public enum TESTNAME : int
            {
                LOSTTRAVEL_PRI        = 0,
                LOSTTRAVEL_SEC        = 1,
                LEAKTEST_AIR_15       = 2,
                LEAKTEST_AIR_50       = 3,
                PISTON_MOVING         = 4,
                PISTON_STROKE_5BAR    = 5,
                PISTON_STROKE_500mmHg = 6,
                VACUUMHOLD_PRI        = 7,
                VACUUMHOLD_SEC        = 8,

            }
            private double[] MeasurementTime = new double[MAX_TESTCCOUNTS];
            private Stopwatch[] _TestTimer   = new Stopwatch[MAX_TESTCCOUNTS];

            public void Start(TESTNAME testname)
            {
                _TestTimer[(int)testname].Restart();
            }
            public void Stop(TESTNAME testname)
            {
                _TestTimer[(int)testname].Stop();
            }
            public double ResultTime(TESTNAME testname)
            {
                double ResultTime;
                ResultTime = _TestTimer[(int)testname].ElapsedMilliseconds / 1000.0;
                return ResultTime;
            }
            public TestTime()
            {
                for (int i = 0; i < MAX_TESTCCOUNTS; i++)
                {
                    _TestTimer[i] = new Stopwatch();
                    _TestTimer[i].Stop();
                }
            }
        }
        #endregion
        /// <summary>
        /// 시스템 운영에 필요한 전반적인 정보 저장/복원용
        /// DAQ,PLC, IP....MES/LDS 정보등등
        /// </summary>
        public class SystemConfig1
        {
            public class SYSTEM_CONFIG
            {
                // 프로그램 전반적으로 사용되는 변수 및 상수등을 화일에 기록하기 위한 클래스
                #region 전역 변수 및 상수
                private string _Test_Information = "iMEB BackUp Cylinder EOL Test Program by KMTS 2017";
                private string _SW_Version = "1.602";
                // 1.501 : 2017-11-30
                //         MES 스펙변경에 따른 프로그램 변경
                //         피스톤무빙 시험시 내부 제어값을 시스템 설정창에서 변경가능한 구조로 변경
                // 1.602 : 2018-05-01
                //         모델 변경시 첫 생산시 오토제로 기능 수행                           

                #endregion
                #region 프로그램 내부에서 사용하는 변수 및 상수
                #endregion
                #region 사용자 수정 가능한 변수(프로퍼티 그리드에서 수정할수 있음)
                [Category("1. 시험 정보")]
                [DisplayName("1-1. 시험기명 및 제작코드")]
                [Description("본 항목은 제작사에서 발급된 설정 정보입니다.")]
                public string TestInforamtion { get { return this._Test_Information; } }

                [Category("1. 시험 정보")]
                [DisplayName("1-2. 프로그램 버전")]
                [Description("본 항목은 제작사에서 발급된 설정 정보입니다.")]
                public string SW_Version { get { return this._SW_Version; } }

                [Category("2. PCI-6259 관련")]
                [DisplayName("2-1. Analog Input CH0-Factor")]
                [Description("AI CH0 - FACTOR")]
                public double AI_CH0_Factor { get; set; }
                [Category("2. PCI-6259 관련")]
                [DisplayName("2-2. Analog Input CH0-Offset")]
                [Description("AI CH0 - OFFSET")]
                public double AI_CH0_Offset { get; set; }

                [Category("2. PCI-6259 관련")]
                [DisplayName("2-3. Analog Input CH1-Factor")]
                [Description("AI CH1 - FACTOR")]
                public double AI_CH1_Factor { get; set; }
                [Category("2. PCI-6259 관련")]
                [DisplayName("2-4. Analog Input CH1-Offset")]
                [Description("AI CH1 - OFFSET")]
                public double AI_CH1_Offset { get; set; }

                [Category("2. PCI-6259 관련")]
                [DisplayName("2-5. Analog Input CH2-Factor")]
                [Description("AI CH2 - FACTOR")]
                public double AI_CH2_Factor { get; set; }
                [Category("2. PCI-6259 관련")]
                [DisplayName("2-6. Analog Input CH2-Offset")]
                [Description("AI CH2 - OFFSET")]
                public double AI_CH2_Offset { get; set; }

                [Category("2. PCI-6259 관련")]
                [DisplayName("2-7. Analog Input CH3-Factor")]
                [Description("AI CH3 - FACTOR")]
                public double AI_CH3_Factor { get; set; }
                [Category("2. PCI-6259 관련")]
                [DisplayName("2-8. Analog Input CH3-Offset")]
                [Description("AI CH3 - OFFSET")]
                public double AI_CH3_Offset { get; set; }

                [Category("2. PCI-6259 관련")]
                [DisplayName("2-7. Analog Input CH4-Factor")]
                [Description("AI CH4 - FACTOR")]
                public double AI_CH4_Factor { get; set; }
                [Category("2. PCI-6259 관련")]
                [DisplayName("2-8. Analog Input CH4-Offset")]
                [Description("AI CH4 - OFFSET")]
                public double AI_CH4_Offset { get; set; }

                [Category("2. PCI-6259 관련")]
                [DisplayName("2-7. Analog Input CH5-Factor")]
                [Description("AI CH5 - FACTOR")]
                public double AI_CH5_Factor { get; set; }
                [Category("2. PCI-6259 관련")]
                [DisplayName("2-8. Analog Input CH5-Offset")]
                [Description("AI CH3 - OFFSET")]
                public double AI_CH5_Offset { get; set; }




                [Category("3. COSMO(AIR LEAK TESTER) 관련")]
                [DisplayName("3-1. PRI Com Port Number")]
                [Description("PC Serial Port Number(1~n),COSMO장치와 연결된 시리얼 포트번호를 입력하십시오.")]
                public int COSMO_PortNumber1 { get; set; }
                [DisplayName("3-2. FLO Com Port Number")]
                [Description("PC Serial Port Number(1~n),COSMO장치와 연결된 시리얼 포트번호를 입력하십시오.")]
                public int COSMO_PortNumber2 { get; set; }


                [Category("4. 내부 동작 타이머 설정")]
                [DisplayName("4-1. UI Refresh Timer Set(ms)")]
                [Description("UI 및 기타 이벤트 확인용 타이머 설정(50~250ms)")]
                public double UI_RefreshTimerValue { get; set; }
                [Category("4. 내부 동작 타이머 설정")]
                [DisplayName("4-2. MainControl Loop Timer Set(ms)")]
                [Description("메인 세부 스템 제어용 타이머 설정(10~100ms)")]
                public double MC_LoopTimerValue { get; set; }



                [Category("5. 네트워크 설정")]
                [DisplayName("5-1. MES 서버 주소")]
                [Description("서버 IP를 입력하십시오(xxx.x.x.xxx")]
                public string MES_SERVERIP { get; set; }
                [Category("5. 네트워크 설정")]
                [DisplayName("5-2. MES 서버 포트 번호")]
                [Description("서버 Port를 입력하십시오(xxxx")]
                public string MES_SERVERPORT { get; set; }
                [Category("5. 네트워크 설정")]
                [DisplayName("5-3. LDS 서버 주소")]
                [Description("서버 IP를 입력하십시오(xxx.x.x.xxx")]
                public string LDS_SERVERIP { get; set; }
                [Category("5. 네트워크 설정")]
                [DisplayName("5-4. LDS 서버 포트 번호")]
                [Description("서버 Port를 입력하십시오(xxxx")]
                public string LDS_SERVERPORT { get; set; }
                [Category("5. 네트워크 설정")]
                [DisplayName("5-5. UDP 서버 주소")]
                [Description("서버 IP를 입력하십시오(xxx.x.x.xxx")]
                public string UDP_SERVERIP { get; set; }
                [Category("5. 네트워크 설정")]
                [DisplayName("5-6. UDP 포트 번호")]
                [Description("UDP Port를 입력하십시오(xxxx")]
                public string UDP_SERVERPORT { get; set; }



                [Category("6. 시험기 동작 관련 설정")]
                [DisplayName("피스톤 무빙시험시 전진 힘 측정지점(mm)")]
                [Description("피스톤 무빙 전진 힘 측정지점(0.3 ~ 10.0).")]
                public double PistonMoving_FWDFindPosition { get; set; }
                [Category("6. 시험기 동작 관련 설정")]
                [DisplayName("피스톤 무빙시험시 후진 힘 측정지점(mm)")]
                [Description("피스톤 무빙 후진 힘 측정지점(0.3 ~ 10.0).")]
                public double PistonMoving_BWDFindPosition { get; set; }
                [Category("6. 시험기 동작 관련 설정")]
                [DisplayName("피스톤 무빙시험시 최대 전진 포스(N)")]
                [Description("피스톤 무빙 전진중 최대 전진 포스(3000~12,000N), 설정범위를 벋어나게 설정할 경우 기계적 오류가 발생합니다.")]
                public double PistonMoving_FWDMaxForce { get; set; }
                [Category("6. 시험기 동작 관련 설정")]
                [DisplayName("피스톤 무빙시험시 풀스트로크 측정 힘값(N)")]
                [Description("피스톤 무빙 시험중 풀스트로크 지점 계산시 힘 기준값(1000~5000N)")]
                public double PistonMoving_FullStrokeFindForce { get; set; }

                [Category("6. 시험기 동작 관련 설정")]
                [DisplayName("피스톤 스트로크 풀스트로크 측정 압력값(bar)")]
                [Description("피스톤 스트로크 시험중 풀스트로크 지점 계산시 압력 기준값(3.0 ~ 4.0 bar)")]
                public double PistonStroke_FullStrokeFindBar { get; set; }

                [Category("6. 시험기 동작 관련 설정")]
                [DisplayName("피스톤 스트로크 진공력 PRI 기준 위치 (mm)")]
                [Description("피스톤 스트로크 진공력 시험시 PRI 설정 기준위치 이상에서 MES 설정압력값 시험범위 기준값(5.0 ~ 20.0 mm)")]
                public double PistonStroke_VacPriPosition { get; set; }
                [Category("6. 시험기 동작 관련 설정")]
                [DisplayName("피스톤 스트로크 진공력 SEC 기준 위치 (mm)")]
                [Description("피스톤 스트로크 진공력 시험시 SEC 설정 기준위치 이상에서 MES 설정압력값 시험범위 기준값(5.0 ~ 35.0 mm)")]
                public double PistonStroke_VacSecPosition { get; set; }
                [Category("6. 시험기 동작 관련 설정")]
                [DisplayName("진공 유지 압력 시험시 측정 시작 시간(sec)")]
                [Description("진공 유지 압력 시험시 측정 시작 기준값(0.5 ~ 5.0sec)")]
                public double VacuumHold_CheckStart { get; set; }
                [Category("6. 시험기 동작 관련 설정")]
                [DisplayName("진공 유지 압력 시험시 측정 종료 시간(sec)")]
                [Description("진공 유지 압력 시험시 측정 종료 기준값(측정시작시간 ~ 7.0sec)")]
                public double VacuumHold_CheckEnd { get; set; }
                [Category("6. 시험기 동작 관련 설정")]
                [DisplayName("피스톤 무빙 시험시 전/후진 웨어링 거리(mm)")]
                [Description("반드시 설정범위 내에서 설정하십시오.(20~30mm)")]
                public double PistonMovingDS { get; set; }
                [Category("6. 시험기 동작 관련 설정")]
                [DisplayName("피스톤 스트로크 진공시험시 풀스트로크 전진시 속도 설정값(mm/sec")]
                [Description("350 설정시 35.0mm/sec로 설정됩니다(0~400이내에서 설정하십시오.)")]
                public int  PistonStrokeFSSpeed { get; set; }
                /*[Category("6. 시험기 동작 관련 설정")]
                [DisplayName("무빙 피스톤 반복 횟수 (회")]
                [Description("무빙피스톤 반복 횟수가 설정됩니다(1~10회이내에서 설정하십시오.)")]
                public int PistonMoving_RepeatCount { get; set; }*/
                
                #endregion
            }
            public SYSTEM_CONFIG CurSysConfig = new SYSTEM_CONFIG();

            private static string CONFIG_FNAME = "iMEB_BCPB_SysConfig.xml"; // 화일명
            public bool GetConfigData()
            {
                if (!File.Exists(CONFIG_FNAME)) // create config file with default values
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Create))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(SYSTEM_CONFIG));
                        SYSTEM_CONFIG sxml = new SYSTEM_CONFIG();
                        xs.Serialize(fs, sxml);
                        this.CurSysConfig = sxml;
                    }
                }
                else // read configuration from file
                {
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Open))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(SYSTEM_CONFIG));
                        SYSTEM_CONFIG sc = (SYSTEM_CONFIG)xs.Deserialize(fs);
                        this.CurSysConfig = sc;
                    }
                }
                return true;
            }
            public bool SaveConfigData()
            {
                try
                {
                    if (File.Exists(CONFIG_FNAME))
                    {
                        File.Delete(CONFIG_FNAME);
                    }
                    using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.OpenOrCreate))
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(SYSTEM_CONFIG));
                        xs.Serialize(fs, this.CurSysConfig);
                        fs.Flush();
                        fs.Close();
                        return true;
                    }
                }
                catch (Exception e1)
                {
                    string errMsh = e1.Message;
                    return false;
                }
            }
        }
        //////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// 2017-10-16
        /// MES/LDS 연결용 TCP/IP , UDP 서버/클라이언트
        /// </summary>
        public class MESLDS : IDisposable
        {
            #region Disposable
            private bool disposed = false;
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            protected virtual void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        if (UDPServerThread != null)
                        {
                            try
                            {
                                _RequestStop = true;
                                Delay(100);
                                UDPServerThread.Abort();
                                UDPServerThread = null;
                            }
                            catch (Exception e1)
                            {
                                if (this._LogEnable)
                                {
                                    this._Log.Info("[UDP Server] MESLDS Dispose Exception : " + e1.Message);
                                } 
                            }
                        }
                        if (UdpServer != null) UdpServer.Close();
                        if (LDSTcpClient != null) LDSTcpClient.Close();
                    }
                    disposed = true;
                }
            }
            ~MESLDS()
            {
                Dispose(false);
            }
            #endregion

            /*
             * 
    1. SPEC
        시작(1) / 분류(2) / 공정(3) / 길이(4) / 바코드(11) / 전공정(1) / 시작시간(14) / 종료시간(14) / 데이터(..) / 종료(1)
                        헤더(50)           
        예) 2 00 030 0026 A07AA090001 1 20070101123033 20070101123033 #ABCDEFGHIJ..# 3
             
    2. 내용 
        1) 시    작: STX(0x02)
        2) 분    류: 타입요청:00 / 정보요청:01 / 작업결과:02 / 사양 및 SPEC:03 / 설비 알람:04
                     응답(NG):91 / 응답(OK):92 / 정상수신:98 / 재전송:99 
        3) 공    정: 각 공정번호
        4) 길    이: 헤더 내용을 뺀 송수신 데이터 총길이(바코드부터..종료까지)
        5) 바 코 드: 차종 및 모델(1),연도(2),월(1),일(2),라인 및 Shift(1),생산시리얼번호(4)
        6) 전 공 정: 전공정 처리여부(NG:0, OK:1, 시작:2)
        7) 시작시간: 작업 시작시간
        8) 종료시간: 작업 종료시간
        9) 데 이 터: 타입 / 정보 / 작업결과 데이터 / 사양 및 SPEC / 설비알람
        10) 종    료: ETX(0x03)

             */





            public enum LDS_SEND : int
            {
                Request_Infomation = 1,
                Result = 2,
                System_Status = 3
            }
            /// <summary>
            /// LDS/MES 공용으로 사용됨. 동일 프로토콜 사용함.
            /// </summary>
            public class LDS_MSG_SPEC
            {
                public const byte   LDS_STX           = 0x02;
                public const byte   LDS_ETX           = 0x03;
                public const string LDS_BODY          = "#";
                public const string LDS_CDOE_REQ_INFO = "01";
                public const string LDS_CDOE_RESULT   = "02";
                public const string LDS_CDOE_STATUS   = "17";
                public const string LDS_CDOE_REQ_TYPE = "00";
                public const string LDS_CDOE_REQ_SPEC = "03";

                public const string LDS_PROCESS_NUM   = "080"; // 020 = LEAK TEST 공정번호, 본 클래스 사용시 공정번호는 맞게 수정후 사용.

                public const string LDS_PART_REQ    = "1";
                public const string LDS_PART_RESULT = "E";

                private string STX             = System.Text.Encoding.ASCII.GetString(new[] { LDS_STX });
                private string CODE            = "";
                private string PROCESS_NUM     = LDS_PROCESS_NUM;
                private string BODY_LENGTH     = "0002";
                private string BARCODE         = "12345678901";
                private string RCODE           = LDS_PART_REQ;
                private string JOBSTARTDATE    = "YYYYMMDDHHNNSS";
                private string JOBENDDATE      = "YYYYMMDDHHNNSS";
                private string BODY_START_CHAR = "#";
                private string BODY_MSG        = "01";
                private string BODY_END_CHAR   = "#";
                private string EXT             = System.Text.Encoding.ASCII.GetString(new[] { LDS_ETX });

                private string SendMsg = "";

                public void GenMessage_TestOnly()
                {
                    CODE         = LDS_CDOE_REQ_INFO;
                    PROCESS_NUM  = LDS_PROCESS_NUM;
                    BODY_LENGTH  = "0002";
                    BARCODE      = "12345678901";
                    RCODE        = "1";
                    JOBSTARTDATE = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
                    JOBENDDATE   = JOBSTARTDATE;
                    BODY_MSG     = "01";
                    this.SendMsg = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                }
                /// <summary>
                /// GM = Generate Message Request Infomation
                /// 메세지 생성 - 정보요청
                /// </summary>
                /// <param name="sendType"></param>
                /// <param name="barCode"></param>
                /// <param name="Msg"></param>
                /// <returns></returns>
                public string GM_ReqInfo(string barCode)
                {
                    string ResultMsg = "";

                    this.BODY_LENGTH  = "0002";
                    this.CODE         = LDS_CDOE_REQ_INFO;
                    if (barCode.Length != 11) barCode = "99999999999";
                    this.BARCODE      = barCode;
                    this.BODY_MSG     = "01";
                    this.JOBSTARTDATE = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
                    this.JOBENDDATE   = JOBSTARTDATE;

                    ResultMsg = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                    return ResultMsg;
                }
                /// <summary>
                /// 타입식별 요청
                /// </summary>
                /// <param name="barCode"></param>
                /// <param name="typeStr"></param>
                /// <returns></returns>
                public string GM_ReqType(string barCode, string typeStr)
                {
                    string ResultMsg = "";

                    this.CODE = LDS_CDOE_REQ_TYPE;
                    if (barCode.Length != 11) barCode = "99999999999";
                    this.BARCODE         = barCode;
                    this.BODY_MSG        = typeStr;
                    string bodyLengthStr = string.Format("{0:D4}", BODY_MSG.Length);
                    this.BODY_LENGTH     = bodyLengthStr;
                    this.JOBSTARTDATE    = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
                    this.JOBENDDATE      = JOBSTARTDATE;

                    ResultMsg = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                    return ResultMsg;
                }
                public string GM_ReqSpec(string barCode, string modelStr)
                {
                    string ResultMsg = "";

                    this.CODE = LDS_CDOE_REQ_SPEC;
                    if (barCode.Length != 11) barCode = "99999999999";
                    this.BARCODE         = barCode;
                    this.BODY_MSG        = modelStr;
                    string bodyLengthStr = string.Format("{0:D4}", BODY_MSG.Length);
                    this.BODY_LENGTH     = bodyLengthStr;
                    this.JOBSTARTDATE    = string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
                    this.JOBENDDATE      = JOBSTARTDATE;

                    ResultMsg = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                    return ResultMsg;
                }


                public string GM_Result(string barCode, string startTime, string endTime, string bodyMsg)
                {
                    string ResultMsg = "";

                    this.CODE = LDS_CDOE_RESULT;
                    if (barCode.Length != 11) barCode = "99999999999";
                    this.BARCODE         = barCode;
                    this.BODY_MSG        = bodyMsg;
                    int bodyLength       = BODY_MSG.Length;
                    string bodyLengthStr = string.Format("{0:D4}", bodyLength);
                    this.BODY_LENGTH     = bodyLengthStr;
                    this.JOBSTARTDATE    = startTime;
                    this.RCODE           = "E"; // 결과 전송시 E
                    this.JOBENDDATE      = endTime;

                    ResultMsg = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                    return ResultMsg;
                }
                public string GM_Status(string barCode, string startTime, string endTime, string bodyMsg)
                {
                    string ResultMsg = "";

                    this.CODE            = LDS_CDOE_STATUS;
                    if (barCode.Length != 11) barCode = "99999999999";
                    this.BARCODE         = barCode;
                    this.BODY_MSG        = bodyMsg;
                    int bodyLength       = BODY_MSG.Length;
                    string bodyLengthStr = string.Format("{0:D4}", bodyLength);
                    this.BODY_LENGTH     = bodyLengthStr;
                    this.JOBSTARTDATE    = startTime;// string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
                    this.RCODE           = "E"; // 결과 전송시 E
                    this.JOBENDDATE      = endTime;//JOBSTARTDATE;

                    ResultMsg            = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                    return ResultMsg;
                }
                public string GM_Status(string bodyMsg)
                {
                    string ResultMsg = "";

                    this.CODE            = LDS_CDOE_STATUS;
                    this.BARCODE         = "01234567890";
                    this.BODY_MSG        = bodyMsg;
                    int bodyLength       = BODY_MSG.Length;
                    string bodyLengthStr = string.Format("{0:D4}", bodyLength);
                    this.BODY_LENGTH     = bodyLengthStr;
                    this.JOBSTARTDATE    =  string.Format("{0:yyyyMMddHHmmss}", DateTime.Now);
                    this.RCODE           = "E"; // 결과 전송시 E
                    this.JOBENDDATE      = JOBSTARTDATE;

                    ResultMsg            = STX + CODE + PROCESS_NUM + BODY_LENGTH + BARCODE + RCODE + JOBSTARTDATE + JOBENDDATE + BODY_START_CHAR + BODY_MSG + BODY_END_CHAR + EXT;

                    return ResultMsg;
                }
                public string GetMsg()
                {
                    return SendMsg;
                }
            }

            #region 내부 사용 변수들
            private string MES_ServerIp;
            private string MES_ServetPort;
            private int _MES_ServerPort;

            private string LDS_ServerIp;
            private string LDS_ServetPort;
            private int _LDS_ServerPort;

            private string UDP_ServerIp;
            private string UDP_ServetPort;
            private int _UDP_ServerPort;

            private Logger _Log = null;
            private bool _LogEnable = false;

            private IPEndPoint remoteEP = null;
            private UdpClient UdpServer = null;
            private Thread UDPServerThread = null;
            private bool _IsUDPServerRun = false;
            private bool _RequestStop = false;

            //private bool _IsMESServerRun = false;
            private TcpClient MESTcpClient = null;
            private TcpClient LDSTcpClient = null;

            private LDS_MSG_SPEC LDSMsg = new LDS_MSG_SPEC();
            private LDS_MSG_SPEC MESMsg = new LDS_MSG_SPEC();

            public event ANDON_Delegate AndonCallBack;
            #endregion
            #region 기타 딜레이 및 참조용
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
            public void LoggerSet(ref Logger log)
            {
                this._Log = log;
                this._LogEnable = true;
                _Log.Info("[MES/LDS] 로그 핸들이 활성화 되었습니다.");
            }
            // 바이트 배열을 String으로 변환
            private string ByteToString(byte[] strByte)
            {
                string str = Encoding.Default.GetString(strByte);
                return str;
            }
            // String을 바이트 배열로 변환
            private byte[] StringToByte(string str)
            {
                byte[] StrByte = Encoding.UTF8.GetBytes(str);
                return StrByte;
            }


            #endregion
            #region 외부 설정값 관련
            public bool SetMES(string IP, string Port)
            {
                this.MES_ServerIp = IP;
                this.MES_ServetPort = Port;

                if (int.TryParse(Port, out this._MES_ServerPort)) return true;
                else return false;
            }
            public bool SetLDS(string IP, string Port)
            {
                this.LDS_ServerIp = IP;
                this.LDS_ServetPort = Port;

                if (int.TryParse(Port, out this._LDS_ServerPort)) return true;
                else return false;
            }
            public bool SetUDP(string IP, string Port)
            {
                this.UDP_ServerIp = IP;
                this.UDP_ServetPort = Port;

                if (int.TryParse(Port, out this._UDP_ServerPort)) return true;
                else return false;
            }
            #endregion

            #region UDP 서버 관련
            public void RequestStop_UDPServer()
            {
                this._RequestStop = true;
            }
            public void UDPServerQuit()
            {
                this._RequestStop = true;
                if (UDPServerThread != null)
                {
                    try
                    {
                        UDPServerThread.Abort();
                        UDPServerThread = null;
                    }
                    catch (Exception e1) 
                    {
                        if (this._LogEnable)
                        {
                            this._Log.Info("[UDP Server] UDPServerQuit Exception : " +e1.Message);
                        } 
                    }
                }
            }
            public bool CreateUDPServer()
            {

                if (UDPServerThread != null)
                {
                    UDPServerThread.Abort();
                    UDPServerThread = null;
                    Delay(100);
                }
                UDPServerThread = new Thread(new ThreadStart(DoUDPWork));
                //AutomaticThread.Priority = ThreadPriority.Highest;
                UDPServerThread.Start();
                Delay(50);
                if (this._LogEnable)
                {
                    this._Log.Info("[UDP Server] UDP Server가 실행되었습니다.(true/false) = " + this._IsUDPServerRun.ToString());
                }
                return this._IsUDPServerRun;
            }
            private void DoUDPWork()
            {
                if (UdpServer != null)
                {
                    try
                    {
                        UdpServer.Close();
                        UdpServer = null;
                    }
                    catch (Exception e1)
                    {
                        if (this._LogEnable)
                        {
                            this._Log.Info("[UDP Server] DoUDPWork Exception : " + e1.Message);
                        }
                        this._IsUDPServerRun = false;
                        return;
                    }
                }

                UdpServer = new UdpClient(this._UDP_ServerPort);                
                // 클라이언트 IP를 담을 변수
                IPAddress UdpAddress = null;
                if (!(IPAddress.TryParse(this.UDP_ServerIp, out UdpAddress)))
                {
                    this._IsUDPServerRun = false;
                    return;
                }
                remoteEP = new IPEndPoint(UdpAddress, 0);

                if (this._LogEnable)
                {
                    this._Log.Info("[UDP Server] Create UDP Server IP = " + this.UDP_ServerIp);
                    this._Log.Info("[UDP Server] Create UDP Server Port = " + string.Format("{0:D}", this._UDP_ServerPort));
                }

                this._IsUDPServerRun = true;

                while (true)
                {
                    if (this._RequestStop)
                    {
                        this._IsUDPServerRun = false;
                        break;
                    }
                    // (2) 데이타 수신
                    byte[] dgram = UdpServer.Receive(ref remoteEP);
                    string andonMsg = ByteToString(dgram);
                    if (andonMsg.IndexOf("STATUS", 0) > 0) AndonCallBack(andonMsg);
                    if (this._LogEnable)
                    {                        
                        //this._Log.Info("[UDP Server] Msg Recieve = " +andonMsg);
                        //this._Log.Info("[UDP Server] Msg IP = " + remoteEP.ToString());                      
                    }
                 
                }
            }

            public bool UDPSend(int mode)
            {
                if (!_IsUDPServerRun) return false;
                try
                {
                    string sendmsg = LDSMsg.GM_Status(string.Format("{0:D}",mode));
                    byte[] senddata = StringToByte(sendmsg);
                    IPAddress UdpAddress = null;
                    if (!(IPAddress.TryParse(this.UDP_ServerIp, out UdpAddress)))
                    {
                        return false;
                    }
                    int port = 2011;
                    int.TryParse(this.UDP_ServetPort, out port);
                    IPEndPoint remoteEP1 = new IPEndPoint(UdpAddress, port);
                    UdpServer.Send(senddata, senddata.Length, remoteEP1);
                    this._Log.Info("[UDP Server] Msg Send = " + sendmsg);
                    this._Log.Info("[UDP Server] Msg Send IP = " + remoteEP1.ToString());
                }
                catch (Exception e1)
                {
                    if (this._LogEnable)
                    {
                        _Log.Info("[MES] UDPSend Exception : " + e1.Message);
                    }
                    return false;
                }
                //Console.WriteLine("[Send] {0} 로 {1} 바이트 송신", remoteEP.ToString(), dgram.Length);

                return true;
            }
            #endregion
            #region MES 서버 관련
            /// <summary>
            /// PC -> MES 타입요청
            /// </summary>
            /// <param name="barCode"></param>
            /// <param name="resultMsg"></param>
            /// <param name="resultCode"></param>
            /// <returns></returns>
            public bool MES_RequestType(string barCode, string typeStr, ref string resultMsg, ref int resultCode)
            {
                string SendMsg = LDSMsg.GM_ReqType(barCode, typeStr);
                byte[] RecieveMsg = new byte[1024];

                bool chk = MES_TcpSend(SendMsg, ref RecieveMsg);

                int resCode = -1;
                if (chk)
                {
                    resultMsg = MES_Parse(RecieveMsg, ref resCode);
                    resultCode = resCode;
                }
                else
                {
                    resultMsg = "TCP 수신 오류";
                    resultCode = -1;
                }

                if (_LogEnable)
                {
                    _Log.Info("[MES] Send Msg = " + SendMsg);
                    if (RecieveMsg != null) _Log.Info("[MES] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                    else _Log.Info("[MES] Receive Msg = null");
                    _Log.Info("[MES] Receive Parse(Result Msg) = " + resultMsg);
                    _Log.Info("[MES] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
                }
                if (resultCode == -1) return false;
                else return true;
            }
            /// <summary>
            /// PC -> MES 모델 사양 및 스펙 요청
            /// </summary>
            /// <param name="barCode"></param>
            /// <param name="modelStr"></param>
            /// <param name="resultMsg"></param>
            /// <param name="resultCode"></param>
            /// <returns></returns>
            public bool MES_RequestSpec(string barCode, string modelStr, ref string resultMsg, ref int resultCode)
            {
                string SendMsg = LDSMsg.GM_ReqSpec(barCode, modelStr);
                byte[] RecieveMsg = new byte[1024];

                bool chk = MES_TcpSend(SendMsg, ref RecieveMsg);

                int resCode = -1;
                if (chk)
                {
                    resultMsg = MES_Parse(RecieveMsg, ref resCode);
                    resultCode = resCode;
                }
                else
                {
                    resultMsg = "TCP 수신 오류";
                    resultCode = -1;
                }

                if (_LogEnable)
                {
                    _Log.Info("[MES] Send Msg = " + SendMsg);
                    if (RecieveMsg != null) _Log.Info("[MES] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                    else _Log.Info("[MES] Receive Msg = null");
                    _Log.Info("[MES] Receive Parse(Result Msg) = " + resultMsg);
                    _Log.Info("[MES] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
                }
                if (resultCode == -1) return false;
                else return true;
            }
            /// <summary>
            /// MES에서 읽어 들인 데이터 문자열을 스펙데이터 배열로 변환
            /// </summary>
            /// <param name="specStr"></param>
            /// <param name="delimiters"></param>
            /// <param name="masterCountNum"></param>
            /// <param name="dataValue"></param>
            /// <returns></returns>
            public bool MES_SpecConverter(string specStr, char[] delimiters, int masterCountNum, ref double[] dataValue)
            {
                double[] conValue = new double[100]; // 최대 100까지 변환
                if (masterCountNum > 100) return false;

                try
                {
                    string[] wordSplit = specStr.Split(delimiters); // 구분자로 문자열 분리
                    int splitCount = wordSplit.Length;          // 구분된 문자열 갯수
                    if (splitCount != masterCountNum) return false;  // 구분된 문자열 갯수와 설정된 갯수가 다를 경우 변환 안함
                    for (int i = 0; i < masterCountNum; i++)
                    {
                        bool chk = double.TryParse(wordSplit[i], out conValue[i]);
                    }
                    dataValue = conValue;
                }
                catch (Exception e1)
                {
                    if (this._LogEnable)
                    {
                        _Log.Info("[MES] MES_SpecConverter Exception : " + e1.Message);
                    }
                    return false;
                }
                return true;
            }



            public bool MES_RequestInfomation(string barCode, ref string resultMsg, ref int resultCode)
            {
                // 
                string SendMsg = LDSMsg.GM_ReqInfo(barCode);
                byte[] RecieveMsg = new byte[1024];

                bool chk = MES_TcpSend(SendMsg, ref RecieveMsg);

                int resCode = -1;
                if (chk)
                {
                    resultMsg = MES_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                    resultCode = resCode;
                }
                else
                {
                    resultMsg = "TCP 수신 오류";
                    resultCode = -1;
                }

                if (_LogEnable)
                {
                    _Log.Info("[MES] Send Msg = " + SendMsg);
                    if (RecieveMsg != null) _Log.Info("[MES] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                    else _Log.Info("[MES] Receive Msg = null");
                    _Log.Info("[MES] Receive Parse(Result Msg) = " + resultMsg);
                    _Log.Info("[MES] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
                }

                return true;
            }
            private bool MES_TcpSend(string SendMsg, ref byte[] RecieveMsg)
            {
                bool StxFind = false;
                int StxIndex = -10;
                bool EtxFind = false;
                int EtxIndex = -10;
                Stopwatch _st = new Stopwatch();
                byte[] outbuf = new byte[1024];
                byte[] outbytes = new byte[1024];
                try
                {
                    MESTcpClient = new TcpClient(this.MES_ServerIp, this._MES_ServerPort);
                    if (!MESTcpClient.Connected)
                    {
                        if (this._LogEnable)
                        {
                            _Log.Info("[MES] MES_TcpSend(TcpClient) 연결이 되지 않습니다.");
                        }
                        return false;
                    }

                    byte[] buff = Encoding.ASCII.GetBytes(SendMsg);

                    NetworkStream stream = MESTcpClient.GetStream();

                    stream.ReadTimeout = 1000;
                    stream.WriteTimeout = 1000;

                    stream.Write(buff, 0, buff.Length);

                    int nbytes;
                    MemoryStream mem = new MemoryStream();


                    _st.Restart();
                    while ((nbytes = stream.Read(outbuf, 0, outbuf.Length)) > 0)
                    {
                        mem.Write(outbuf, 0, nbytes);
                        for (int i = 0; i < outbuf.Length; i++)
                        {
                            if ((outbuf[i] == 0x02) && (!StxFind))
                            {
                                StxFind = true;
                                StxIndex = i;
                            }
                            if ((outbuf[i] == 0x03) && (!EtxFind))
                            {
                                EtxFind = true;
                                EtxIndex = i;
                            }
                        }
                        if ((StxFind) && (EtxFind)) break;
                        if (_st.ElapsedMilliseconds > 5000) break;
                    }
                    outbytes = mem.ToArray();
                    mem.Close();

                    stream.Close();
                    MESTcpClient.Close();
                }
                catch (Exception e1)
                {
                    if (this._LogEnable)
                    {
                        _Log.Info("[MES] MES_TcpSend Exception : " + e1.Message);
                    }
                    RecieveMsg = null;
                    return false;
                }

                if ((StxFind) && (EtxFind))
                {
                    RecieveMsg = outbytes;
                    return true;
                }
                else
                {
                    RecieveMsg = null;
                    return false;
                }
            }
            private string MES_Parse(byte[] msg, ref int resultCode)
            {
                string strMsg = Encoding.ASCII.GetString(msg);
                Char[] delimiters = { '#' };
                string[] wordsSplit = strMsg.Split(delimiters, 3);


                string resultCodeStr = System.Text.Encoding.ASCII.GetString(msg, 1, 2);
                if (resultCodeStr.Length > 0)
                {
                    if (!int.TryParse(resultCodeStr, out resultCode)) resultCode = 0;
                    // resultCode = 91=NG, 92=OK, 98
                }
                string FindMsg = "";
                if (wordsSplit[1].Length > 0) FindMsg = wordsSplit[1];
                else FindMsg = "No Msg";
                FindMsg = FindMsg.Trim();
                return FindMsg;
            }
            public bool MES_Result(string barCode, string startTime, string endTime, string TestResult, ref string resultMsg, ref int resultCode)
            {
                string SendMsg = LDSMsg.GM_Result(barCode, startTime, endTime, TestResult);
                byte[] RecieveMsg = new byte[1024];

                bool chk = LDS_TcpSend(SendMsg, ref RecieveMsg);

                int resCode = -1;
                if (chk)
                {
                    resultMsg = LDS_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                    resultCode = resCode;
                }
                else
                {
                    resultMsg = "TCP 수신 오류";
                    resultCode = -1;
                }

                if (_LogEnable)
                {
                    _Log.Info("[MES] Send Msg = " + SendMsg);
                    if (RecieveMsg != null) _Log.Info("[MES] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                    else _Log.Info("[MES] Receive Msg = null");
                    _Log.Info("[MES] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
                }

                return true;
            }
            public bool MES_Status(string barCode, string startTime, string endTime, string TestResult, ref string resultMsg, ref int resultCode)
            {

                string SendMsg = LDSMsg.GM_Status(barCode, startTime, endTime, TestResult);
                byte[] RecieveMsg = new byte[1024];

                bool chk = LDS_TcpSend(SendMsg, ref RecieveMsg);

                int resCode = -1;
                if (chk)
                {
                    resultMsg = LDS_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                    resultCode = resCode;
                }
                else
                {
                    resultMsg = "TCP 수신 오류";
                    resultCode = -1;
                }
                // 문자열 파싱

                if (_LogEnable)
                {

                    _Log.Info("[MES] Send Msg = " + SendMsg);
                    if (RecieveMsg != null) _Log.Info("[LDSMES Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                    else _Log.Info("[MES] Receive Msg = null");
                    _Log.Info("[MES] Receive Parse(Result Msg) = " + resultMsg);
                    _Log.Info("[MES] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
                }

                return true;
            }

            #endregion
            #region LDS 서버 관련
            public bool LDS_RequestInfomation(string barCode, ref string resultMsg, ref int resultCode)
            {
                string SendMsg = LDSMsg.GM_ReqInfo(barCode);
                byte[] RecieveMsg = new byte[1024];

                bool chk = LDS_TcpSend(SendMsg, ref RecieveMsg);

                int resCode = -1;
                if (chk)
                {
                    resultMsg = LDS_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                    resultCode = resCode;
                }
                else
                {
                    resultMsg = "TCP 수신 오류";
                    resultCode = -1;
                }

                if (_LogEnable)
                {
                    _Log.Info("[LDS] Send Msg = " + SendMsg);
                    if (RecieveMsg != null) _Log.Info("[LDS] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                    else _Log.Info("[LDS] Receive Msg = null");
                    _Log.Info("[LDS] Receive Parse(Result Msg) = " + resultMsg);
                    _Log.Info("[LDS] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
                }

                return true;
            }
            private bool LDS_TcpSend(string SendMsg, ref byte[] RecieveMsg)
            {
                bool StxFind = false;
                int StxIndex = -10;
                bool EtxFind = false;
                int EtxIndex = -10;
                Stopwatch _st = new Stopwatch();
                byte[] outbuf = new byte[1024];
                byte[] outbytes = new byte[1024];
                try
                {
                    LDSTcpClient = new TcpClient(this.LDS_ServerIp, this._LDS_ServerPort);
                    if (!LDSTcpClient.Connected)
                    {
                        if (this._LogEnable)
                        {
                            _Log.Info("[LDS] LDS_TcpSend(TcpClient) 연결이 되지 않습니다.");
                        }
                        return false;
                    }

                    byte[] buff = Encoding.ASCII.GetBytes(SendMsg);

                    NetworkStream stream = LDSTcpClient.GetStream();

                    stream.ReadTimeout = 1000;
                    stream.WriteTimeout = 1000;

                    stream.Write(buff, 0, buff.Length);

                    int nbytes;
                    MemoryStream mem = new MemoryStream();


                    _st.Restart();
                    while ((nbytes = stream.Read(outbuf, 0, outbuf.Length)) > 0)
                    {
                        mem.Write(outbuf, 0, nbytes);
                        for (int i = 0; i < outbuf.Length; i++)
                        {
                            if ((outbuf[i] == 0x02) && (!StxFind))
                            {
                                StxFind = true;
                                StxIndex = i;
                            }
                            if ((outbuf[i] == 0x03) && (!EtxFind))
                            {
                                EtxFind = true;
                                EtxIndex = i;
                            }
                        }
                        if ((StxFind) && (EtxFind)) break;
                        if (_st.ElapsedMilliseconds > 5000) break;
                    }
                    outbytes = mem.ToArray();
                    mem.Close();

                    stream.Close();
                    LDSTcpClient.Close();
                }
                catch (Exception e1)
                {
                    if (this._LogEnable)
                    {
                        _Log.Info("[LDS] LDS_TcpSend Function Exception : " + e1.Message);
                    }
                    RecieveMsg = null;
                    return false;
                }

                if ((StxFind) && (EtxFind))
                {
                    RecieveMsg = outbytes;
                    return true;
                }
                else
                {
                    RecieveMsg = null;
                    return false;
                }
            }
            private string LDS_Parse(byte[] msg, ref int resultCode)
            {
                string strMsg = Encoding.ASCII.GetString(msg);
                Char[] delimiters = { '#' };
                string[] wordsSplit = strMsg.Split(delimiters, 3);


                string resultCodeStr = System.Text.Encoding.ASCII.GetString(msg, 1, 2);
                if (resultCodeStr.Length > 0)
                {
                    if (!int.TryParse(resultCodeStr, out resultCode)) resultCode = 0;
                }
                string FindMsg = "";
                if (wordsSplit[1].Length > 0) FindMsg = wordsSplit[1];
                else FindMsg = "No Msg";
                FindMsg = FindMsg.Trim();
                return FindMsg;
            }
            public bool LDS_Result(string barCode, string startTime, string endTime, string TestResult, ref string resultMsg, ref int resultCode)
            {
                string SendMsg = LDSMsg.GM_Result(barCode, startTime, endTime, TestResult);
                byte[] RecieveMsg = new byte[1024];

                bool chk = LDS_TcpSend(SendMsg, ref RecieveMsg);

                int resCode = -1;
                if (chk)
                {
                    resultMsg = LDS_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                    resultCode = resCode;
                }
                else
                {
                    resultMsg = "TCP 수신 오류";
                    resultCode = -1;
                }

                if (_LogEnable)
                {
                    _Log.Info("[LDS] Send Msg = " + SendMsg);
                    if (RecieveMsg != null) _Log.Info("[LDS] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                    else _Log.Info("[LDS] Receive Msg = null");
                    _Log.Info("[LDS] Receive Parse(Result Msg) = " + resultMsg);
                    _Log.Info("[LDS] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
                }

                return true;
            }
            public bool LDS_Status(string barCode, string startTime, string endTime, string TestResult, ref string resultMsg, ref int resultCode)
            {

                string SendMsg = LDSMsg.GM_Status(barCode, startTime, endTime, TestResult);
                byte[] RecieveMsg = new byte[1024];

                bool chk = LDS_TcpSend(SendMsg, ref RecieveMsg);

                int resCode = -1;
                if (chk)
                {
                    resultMsg = LDS_Parse(RecieveMsg, ref resCode);  //Encoding.ASCII.GetString(outbytes);
                    resultCode = resCode;
                }
                else
                {
                    resultMsg = "TCP 수신 오류";
                    resultCode = -1;
                }
                // 문자열 파싱

                if (_LogEnable)
                {

                    _Log.Info("[LDS] Send Msg = " + SendMsg);
                    if (RecieveMsg != null) _Log.Info("[LDS] Receive Msg = " + Encoding.ASCII.GetString(RecieveMsg));
                    else _Log.Info("[LDS] Receive Msg = null");
                    _Log.Info("[LDS] Receive Parse(Result Msg) = " + resultMsg);
                    _Log.Info("[LDS] Receive Parse(응답코드) = " + string.Format("{0:D2}", resultCode));
                }

                return true;
            }
            #endregion

        }
        /// <summary>
        /// MES 연동용 사양정의 클래스
        /// </summary>
        public class MESSPEC
        {
            public class Specification
            {

                [Category("1. 생산 정보")]
                [DisplayName("1-1. 모델 이름")]
                [Description("PLC에 적용된 최근 모델 정보")]
                public string LastProductCode { get; set; }
                [Category("1. 생산 정보")]
                [DisplayName("1-2. 바코드")]
                [Description("임시 바코드 정보 - 비정규 시험테스트시만 사용되는 코드")]
                public string ImsiBarCode { get; set; }





                [Category("2. 백업실린더 로스트 트레블")]
                [DisplayName("2-1. PRI : LostTravel  Min")]
                [Description("프라이머리측 CutOffHole 위치 최소, LostTravel")]
                public double LostTravel_PRI_CutOffHole_Min { get; set; }
                [Category("2. 백업실린더 로스트 트레블")]
                [DisplayName("2-2. PRI : LostTravel  Max")]
                [Description("프라이머리측 CutOffHole 위치 최대, LostTravel")]
                public double LostTravel_PRI_CutOffHole_Max { get; set; }
                [Category("2. 백업실린더 로스트 트레블")]
                [DisplayName("2-3. PRI: Sec 유지압력 Min")]
                [Description("세컨더리측 유지 압력 최소, PPRI_11")]
                public double LostTravel_PRI_Sec_Min { get; set; }
                [Category("2. 백업실린더 로스트 트레블")]
                [DisplayName("2-4. PRI: Sec 유지압력 Max")]
                [Description("세컨더리측 유지 압력 최대, PPRI_11")]
                public double LostTravel_PRI_Sec_Max { get; set; }

                [Category("2. 백업실린더 로스트 트레블")]
                [DisplayName("2-5. SEC: Pri 유지압력 Min")]
                [Description("프라이머리측 유지 압력 최소, PPRI_12")]
                public double LostTravel_SEC_Pri_Min { get; set; }
                [Category("2. 백업실린더 로스트 트레블")]
                [DisplayName("2-6. SEC: Pri 유지압력 Max")]
                [Description("프라이머리측 유지 압력 최대, PPRI_12")]
                public double LostTravel_SEC_Pri_Max { get; set; }
                [Category("2. 백업실린더 로스트 트레블")]
                [DisplayName("2-7. SEC: Sec 유지압력 Min")]
                [Description("세컨더리측 유지 압력 최소, PSEC_12")]
                public double LostTravel_SEC_Sec_Min { get; set; }
                [Category("2. 백업실린더 로스트 트레블")]
                [DisplayName("2-8. SEC: Sec 유지압력 Max")]
                [Description("세컨더리측 유지 압력 최대, PSEC_12")]
                public double LostTravel_SEC_Sec_Max { get; set; }



                [Category("3. 백업실린더 리크 테스트")]
                [DisplayName("3-1. 1.5바 프라이머리 최소")]
                [Description("프라이머리측 리크 최소, LPri_11")]
                public double LeakTest_15_Pri_Min { get; set; }
                [Category("3. 백업실린더 리크 테스트")]
                [DisplayName("3-2. 1.5바 프라이머리 최대")]
                [Description("프라이머리측 리크 최소, LPri_11")]
                public double LeakTest_15_Pri_Max { get; set; }
                [Category("3. 백업실린더 리크 테스트")]
                [DisplayName("3-3. 1.5바 세컨더리 최소")]
                [Description("세컨더리측 리크 최소, LSec_11")]
                public double LeakTest_15_Sec_Min { get; set; }
                [Category("3. 백업실린더 리크 테스트")]
                [DisplayName("3-4. 1.5바 세컨더리 최대")]
                [Description("세컨더리측 리크 최대, LSec_11")]
                public double LeakTest_15_Sec_Max { get; set; }

                [Category("3. 백업실린더 리크 테스트")]
                [DisplayName("3-5. 5.0바 프라이머리 최소")]
                [Description("프라이머리측 리크 최소, LPri_12")]
                public double LeakTest_50_Pri_Min { get; set; }
                [Category("3. 백업실린더 리크 테스트")]
                [DisplayName("3-6. 5.0바 프라이머리 최대")]
                [Description("프라이머리측 리크 최소, LPri_12")]
                public double LeakTest_50_Pri_Max { get; set; }
                [Category("3. 백업실린더 리크 테스트")]
                [DisplayName("3-7. 5.0바 세컨더리 최소")]
                [Description("세컨더리측 리크 최소, LSec_12")]
                public double LeakTest_50_Sec_Min { get; set; }
                [Category("3. 백업실린더 리크 테스트")]
                [DisplayName("3-8. 5.0바 세컨더리 최대")]
                [Description("세컨더리측 리크 최대, LSec_12")]
                public double LeakTest_50_Sec_Max { get; set; }


                [Category("4. 백업실린더 피스톤 무빙 테스트")]
                [DisplayName("4-1. 전진 힘 최소")]
                [Description("전진 행정중 전진감지 최대 힘의 최소, FWD_N")]
                public double PistonMoving_FWD_N_Min { get; set; }
                [Category("4. 백업실린더 피스톤 무빙 테스트")]
                [DisplayName("4-2. 전진 힘 최대")]
                [Description("전진 행정중 전진감지 최대 힘의 최대, FWD_N")]
                public double PistonMoving_FWD_N_Max { get; set; }

                [Category("4. 백업실린더 피스톤 무빙 테스트")]
                [DisplayName("4-3. 풀 스트로크 최소")]
                [Description("내부 설정된 힘 측정 위치 최소, FullStroke")]
                public double PistonMoving_FullStroke_Min { get; set; }
                [Category("4. 백업실린더 피스톤 무빙 테스트")]
                [DisplayName("4-4. 풀 스트로크 최대")]
                [Description("내부 설정된 힘 측정 위치 최대, FullStroke")]
                public double PistonMoving_FullStroke_Max { get; set; }

                [Category("4. 백업실린더 피스톤 무빙 테스트")]
                [DisplayName("4-5. 후진 힘 최소")]
                [Description("후진 행정중 전진감지 최대 힘의 최소, BWD_N")]
                public double PistonMoving_BWD_N_Min { get; set; }
                [Category("4. 백업실린더 피스톤 무빙 테스트")]
                [DisplayName("4-6. 후진 힘 최대")]
                [Description("후진 행정중 전진감지 최대 힘의 최대, BWD_N")]
                public double PistonMoving_BWD_N_Max { get; set; }



                [Category("5. 백업실린더 피스톤 스트로크 테스트")]
                [DisplayName("5-1. 프라이머리 유효 거리 최소")]
                [Description("측정된 프라이머리 유효 거리 최소, PRI_PistonLength")]
                public double PistonStroke_PriLength_Min { get; set; }
                [Category("5. 백업실린더 피스톤 스트로크 테스트")]
                [DisplayName("5-2. 프라이머리 유효 거리 최대")]
                [Description("측정된 프라이머리 유효 거리 최대, PRI_PistonLength")]
                public double PistonStroke_PriLength_Max { get; set; }
                [Category("5. 백업실린더 피스톤 스트로크 테스트")]
                [DisplayName("5-3. 세컨더리 유효 거리 최소")]
                [Description("측정된 세컨더리 유효 거리 최소, SEC_PistonLength")]
                public double PistonStroke_SecLength_Min { get; set; }
                [Category("5. 백업실린더 피스톤 스트로크 테스트")]
                [DisplayName("5-4. 세컨더리 유효 거리 최대")]
                [Description("측정된 세컨더리 유효 거리 최대, SEC_PistonLength")]
                public double PistonStroke_SecLength_Max { get; set; }

                [Category("5. 백업실린더 피스톤 스트로크 테스트")]
                [DisplayName("5-5. 프라이머리 진공 최소")]
                [Description("측정된 프라이머리 진공량 최소, PRI_Vac")]
                public double PistonStroke_PriVac_Min { get; set; }
                [Category("5. 백업실린더 피스톤 스트로크 테스트")]
                [DisplayName("5-6. 프라이머리 진공 최대")]
                [Description("측정된 프라이머리 진공량 최대, PRI_Vac")]
                public double PistonStroke_PriVac_Max { get; set; }
                [Category("5. 백업실린더 피스톤 스트로크 테스트")]
                [DisplayName("5-7. 세컨더리 진공 최소")]
                [Description("측정된 세컨더리 진공량 최소, SEC_Vac")]
                public double PistonStroke_SecVac_Min { get; set; }
                [Category("5. 백업실린더 피스톤 스트로크 테스트")]
                [DisplayName("5-8. 세컨더리 진공 최대")]
                [Description("측정된 세컨더리 진공량 최대, SEC_Vac")]
                public double PistonStroke_SecVac_Max { get; set; }

                [Category("6. 백업실린더 진공유지력 테스트")]
                [DisplayName("6-1. 프리이머리 진공압 최소")]
                [Description("측정된 프라이머리 진공량 최소, HOLD_Vac_Pri")]
                public double VacuumHold_Pri_Min { get; set; }
                [Category("6. 백업실린더 진공유지력 테스트")]
                [DisplayName("6-2. 프리이머리 진공압 최대")]
                [Description("측정된 프라이머리 진공량 최대, HOLD_Vac_Pri")]
                public double VacuumHold_Pri_Max { get; set; }
                [Category("6. 백업실린더 진공유지력 테스트")]
                [DisplayName("6-3. 세컨더리 진공압 최소")]
                [Description("측정된 세컨더리 진공량 최소, HOLD_Vac_Sec")]
                public double VacuumHold_Sec_Min { get; set; }
                [Category("6. 백업실린더 진공유지력 테스트")]
                [DisplayName("6-4. 세컨더리 진공압 최대")]
                [Description("측정된 세컨더리 진공량 최대, HOLD_Vac_Sec")]
                public double VacuumHold_Sec_Max { get; set; }

                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-01. 3.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_3_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-02. 3.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_3_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-03. 5.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_5_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-04. 5.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_5_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-05. 10.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_10_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-06. 10.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_10_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-07. 15.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_15_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-08. 15.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_15_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-09. 20.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_20_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-10. 20.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_20_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-11. 24.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_24_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-12. 24.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_24_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-13. 25.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_25_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-14. 25.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_25_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-15. 26.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_26_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-16. 26.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_26_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-17. 27.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_27_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-18. 27.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_27_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-19. 28.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_28_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-20. 28.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_28_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-21. 29.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_29_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-22. 29.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_29_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-23. 30.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_30_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-24. 30.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_30_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-25. 31.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_31_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-26. 31.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_31_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-27. 32.0 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_32_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-28. 32.0 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_32_Max { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-29. 32.5 mm Min")]
                [Description("Force min")]
                public double PistonMoving_Table_32p5_Min { get; set; }
                [Category("7. 백업실린더 PM 반력선도 ")]
                [DisplayName("7-30. 32.5 mm Max")]
                [Description("Force max")]
                public double PistonMoving_Table_32p5_Max { get; set; }
            }
            public class Local_Var
            {
                public string StartDate = "";
                public string EndDate = "";
                public string ModelCode = "";
                public string BarCode = "";
                public string TestResult = "OK";
            }
            // name of the .xml file
            public Specification SPEC = new Specification();
            public Local_Var LocalVar = new Local_Var();
            private string CONFIG_FNAME = "iMEB_LT4_MESSpecification.xml";
            /// <summary>
            /// 내부 정의된 SPEC 데이터로 xml화일에서 정보 읽기
            /// </summary>
            /// <returns></returns>
            public bool GetConfigData()
            {
                try
                {
                    if (!File.Exists(CONFIG_FNAME)) // create config file with default values
                    {
                        using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Create))
                        {
                            XmlSerializer xs = new XmlSerializer(typeof(Specification));
                            Specification sxml = new Specification();
                            xs.Serialize(fs, sxml);
                            this.SPEC = sxml;
                        }
                    }
                    else // read configuration from file
                    {
                        using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.Open))
                        {
                            XmlSerializer xs = new XmlSerializer(typeof(Specification));
                            Specification sc = (Specification)xs.Deserialize(fs);
                            this.SPEC = sc;
                        }
                    }
                }
                catch (Exception e1) { return false; }
                return true;
            }
            /// <summary>
            /// 내부 정의된 SPEC 데이터를 화일에 저장
            /// </summary>
            /// <returns></returns>
            public bool SaveConfigData()
            {
                if (File.Exists(CONFIG_FNAME))
                {
                    File.Delete(CONFIG_FNAME);
                }
                using (FileStream fs = new FileStream(CONFIG_FNAME, FileMode.OpenOrCreate))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(Specification));
                    xs.Serialize(fs, this.SPEC);
                    fs.Flush();
                    fs.Close();
                    return true;
                }
            }
        }




























        // 메인 UI 생성 
        private void Img_Mobis_Loaded(object sender, RoutedEventArgs e)
        {
            BitmapImage MobisImg = new BitmapImage();
            MobisImg.BeginInit();
            MobisImg.UriSource = new Uri("pack://siteoforigin:,,,/Resources/colorTone_img1.jpg");
            MobisImg.EndInit();
        }

        private void Img_Product_Loaded(object sender, RoutedEventArgs e)
        {
            BitmapImage MobisImg = new BitmapImage();
            MobisImg.BeginInit();
            MobisImg.UriSource = new Uri("pack://siteoforigin:,,,/Resources/BackupCylinder.png");
            MobisImg.EndInit();
        }

        private void Img_ProductSmall_Loaded(object sender, RoutedEventArgs e)
        {
            BitmapImage MobisImg = new BitmapImage();
            MobisImg.BeginInit();
            MobisImg.UriSource = new Uri("pack://siteoforigin:,,,/Resources/BackupCylinder_small.png");
            MobisImg.EndInit();
        }

        private void Img_User_Loaded(object sender, RoutedEventArgs e)
        {
            BitmapImage MobisImg = new BitmapImage();
            MobisImg.BeginInit();
            MobisImg.UriSource = new Uri("pack://siteoforigin:,,,/Resources/Users.png");
            MobisImg.EndInit();
        }

        private void Img_Barcode_Loaded(object sender, RoutedEventArgs e)
        {
            BitmapImage MobisImg = new BitmapImage();
            MobisImg.BeginInit();
            MobisImg.UriSource = new Uri("pack://siteoforigin:,,,/Resources/BarCode.png");
            MobisImg.EndInit();
        }

        private void Buttton_User_Click(object sender, RoutedEventArgs e)
        {
           
        }

        private void Buttton_Quit_Click(object sender, RoutedEventArgs e)
        {
            BCSystemClosing();
            Application.Current.Shutdown();
        }

        private void HiddenMenu_TouchDown(object sender, TouchEventArgs e)
        {  
            // 메뉴로그 클릭시 오토제로 기능 수행
            MessageBoxResult result = MessageBox.Show("오토 제로기능을 수행합니까?", "AUTO ZERO", MessageBoxButton.YesNo);
            if (result== MessageBoxResult.OK)
            {
                try
                {
                    using (Task digitalWriteTask = new Task())
                    {
                        //  Create an Digital Output channel and name it.
                        digitalWriteTask.DOChannels.CreateChannel("Dev1/port0", "port0",ChannelLineGrouping.OneChannelForAllLines);
                        DigitalSingleChannelWriter writer = new DigitalSingleChannelWriter(digitalWriteTask.Stream);
                        writer.WriteSingleSamplePort(true, (UInt32)0xFFFFFFFF);
                        for (int i=0; i<100; i++)
                        {
                            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                                                      new Action(delegate { }));
                        }
                        writer.WriteSingleSamplePort(true, (UInt32)0x00000000);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

            }
            

        }

        private void Buttton_Test_Click(object sender, RoutedEventArgs e)
        {                      

           
        }

        private void Buttton_Result_Click(object sender, RoutedEventArgs e)
        {
            double pheight = this.WorkGrid.ActualHeight;
            double pwidth = this.WorkGrid.ActualWidth;
            Point pp = this.WorkGrid.PointToScreen(new Point(0.0, 0.0));
            if (SubWIndow != null)
            {
                SubWIndow.ShowActivated = true;
                SubWIndow.ShowDialog();               
            }
            else
            {
                SubWIndow = new Window
                {
                    Title = "Self Test",
                    Content = new SelfTestUC(),
                    Height = pheight,
                    Width = pwidth,
                    WindowStyle = System.Windows.WindowStyle.None,
                    Top = pp.Y,
                    Left = pp.X,
                    ResizeMode = System.Windows.ResizeMode.NoResize,


                };
                SubWIndow.ShowDialog();
            }
            

        }


        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            MainSubScreen.SelectedIndex = 10;
        }

        private void Buttton_AutoZero_Click(object sender, RoutedEventArgs e)
        {
            Bc.ForceSensorAutoZero();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
         
            if (Bc!=null)
            {
                try
                {
                    Bc.CurSysConfig.SaveConfigData();
                    Bc.CurMesSpec.SaveConfigData();
                    if (Bc.IsStart)
                    {
                        Bc.RequestStop();                        
                    }
                    Bc.Dispose();
                    Bc = null;
                }
                catch (Exception e1) 
                { 
                    if (Bc.LogEnable)
                    {
                        Bc.Log.Error("원도우 종료중 Exception 발생 : " + e1.Message);
                    }
                }
            }
        }

        private void Buttton_Log_Click(object sender, RoutedEventArgs e)
        {
            // 메인 화면에서 LOG 버튼 클릭시
            MainSubScreen.SelectedIndex = 9;
        }

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)(sender);
            var fileMonitorViewModel = ((FileMonitorViewModel)textBox.DataContext);

            if (fileMonitorViewModel != null)
            {
                if (!fileMonitorViewModel.IsFrozen)
                {
                    textBox.ScrollToEnd();
                }
                textBox.ScrollToEnd();
            }
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void SystemConfig_Click(object sender, RoutedEventArgs e)
        {
            // 상단 관리자 메뉴 클릭
            if (Bc.CurPLCState.AutomManual)
            {
                MessageBox.Show("자동운전중에는 시스템 설정이 불가합니다. 매뉴얼 상태에서 다시 하십시오.", "알림", MessageBoxButton.OK);
                return;
            }

            //Bc.CurMesSpec.GetConfigData();                                    // 2017-10-30 MESLDS관련 전체 스펙데이터 로컬 PC에서 읽어 들임.
            //Bc.CurSysConfig.GetConfigData();                                  // 2017-10-30 시험관련 전체 스펙데이터 로컬 PC에서 읽어 들임.
            // 메인 메뉴 상단 우측 - 시스템 설정 버튼 
            if (ThisSubForm == null)
            {
                ThisSubForm = new SystemConfig.SystemConfig(ref Bc.CurMesSpec.SPEC,ref Bc.CurSysConfig.CurSysConfig);
            }
            ThisSubForm.Owner = this;
            ThisSubForm.Closed += (o, args) => ThisSubForm = null;
            ThisSubForm.Left = this.ActualWidth / 4.0;  // this.Left + this.ActualWidth / 2.0;
            ThisSubForm.Top = this.ActualHeight / 4.0; // this.Top + this.ActualHeight / 2.0;
            ThisSubForm.Width = 800;
            ThisSubForm.Height = 600;
            ThisSubForm.Show();
        }

        private void Btn_Log_Click(object sender, RoutedEventArgs e)
        {
            MainSubScreen.SelectedIndex = 9;
        }

        private void okpopup_close_Click(object sender, RoutedEventArgs e)
        {
            OKHide();
        }

    }
}
