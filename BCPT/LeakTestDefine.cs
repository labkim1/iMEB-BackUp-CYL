using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Design;
using System.ComponentModel;
namespace BCPT
{
    public class LeakTestDefine
    {
        #region 로컬 변수(내부적으로 저장만 필요한 데이터)
        public DateTime _LastWriteDate;   // 마지막 저장일시
        public string   _LastUser;        // 마지막 저장사용자
        #endregion
        #region 전역 프로퍼티(프로퍼티 그리드에서 수정가능한 구역)
        [Category("1. 마스터 설정")]
        [DisplayName("1-1. 고압 공급 ")]
        [Description("고압(10bar이상) 공급부의 레귤레이터 설정값을 입력하십시오.")]
        public double HighAirMaster { get; set;}
        [Category("1. 마스터 설정")]
        [DisplayName("1-2. 저압 공급(코스모 채널 번호) ")]
        [Description("코스모 장치의 채널 번호를 입력하십시오.")]
        public int LowAirCosmoCh { get; set; }
        [Category("1. 마스터 설정")]
        [DisplayName("1-3. 저압 공급(정압) ")]
        [Description("코스모 장치의 채널 번호에 설정된 압력값 입력하십시오.")]
        public double LowAirMaster { get; set; }
        [Category("1. 마스터 설정")]
        [DisplayName("1-4. 진공 공급(코스모 채널 번호) ")]
        [Description("코스모 장치의 채널 번호를 입력하십시오.")]
        public int VacuumAirCosmoCh { get; set; }
        [Category("1. 마스터 설정")]
        [DisplayName("1-5. 진공 공급(부압) ")]
        [Description("코스모 장치의 채널 번호에 설정된 진공압력값 입력하십시오.")]
        public double VacuumAirMaster { get; set; }

        [Category("2. 고압 테스트 설정")]
        [DisplayName("2-1. 가압 시간(초)")]
        [Description("고압가압 시간을 입력하십시오.")]
        public double HighAir_PressTime { get; set; }
        [Category("2. 고압 테스트 설정")]
        [DisplayName("2-2. 측정 시간(초)")]
        [Description("테스트 측정 시간을 입력하십시오.")]
        public double HighAir_CheckTime { get; set; }
        [Category("2. 고압 테스트 설정")]
        [DisplayName("2-3. 합격 기준 압력(Bar)")]
        [Description("측정 시간 동안 압력차이값을 입력하십시오. 설정값 이상 차이가 날 경우 NG처리 됩니다.")]
        public double HighAir_DiffBar { get; set; }

        [Category("3. 저압 테스트 설정")]
        [DisplayName("3-1. 가압 시간(초)")]
        [Description("저압가압 시간을 입력하십시오.")]
        public double LowAir_PressTime { get; set; }
        [Category("3. 저압 테스트 설정")]
        [DisplayName("3-2. 측정 시간(초)")]
        [Description("테스트 측정 시간을 입력하십시오.")]
        public double LowAir_CheckTime { get; set; }
        [Category("3. 저압 테스트 설정")]
        [DisplayName("3-3. 합격 기준 압력(Bar)")]
        [Description("측정 시간 동안 압력차이값을 입력하십시오. 설정값 이상 차이가 날 경우 NG처리 됩니다.")]
        public double LowAir_DiffBar { get; set; }

        [Category("4. 진공 테스트 설정")]
        [DisplayName("4-1. 감압 시간(초)")]
        [Description("진공 감압 시간을 입력하십시오.")]
        public double Vacuum_PressTime { get; set; }
        [Category("4. 진공 테스트 설정")]
        [DisplayName("4-2. 측정 시간(초)")]
        [Description("테스트 측정 시간을 입력하십시오.")]
        public double Vacuum_CheckTime { get; set; }
        [Category("4. 진공 테스트 설정")]
        [DisplayName("4-3. 합격 기준 압력(mmHg)")]
        [Description("측정 시간 동안 압력차이값을 입력하십시오. 설정값 이상 차이가 날 경우 NG처리 됩니다.")]
        public double Vacuum_DiffBar { get; set; }

        [Category("5. 공압 센서측 테스트 설정")]
        [DisplayName("5-1. 가압 시간(초)")]
        [Description("가압 시간을 입력하십시오.")]
        public double AirSensor_PressTime { get; set; }
        [Category("5. 공압 센서측 테스트 설정")]
        [DisplayName("5-2. 측정 시간(초)")]
        [Description("테스트 측정 시간을 입력하십시오.")]
        public double AirSensor_CheckTime { get; set; }
        [Category("5. 공압 센서측 테스트 설정")]
        [DisplayName("5-3. 합격 기준 압력(Bar)")]
        [Description("측정 시간 동안 압력차이값을 입력하십시오. 설정값 이상 차이가 날 경우 NG처리 됩니다.")]
        public double AirSensor_DiffBar { get; set; }

        [Category("6. 진공 센서측 테스트 설정")]
        [DisplayName("6-1. 감압 시간(초)")]
        [Description("진공 감압 시간을 입력하십시오.")]
        public double VacuumSensor_PressTime { get; set; }
        [Category("6. 진공 센서측 테스트 설정")]
        [DisplayName("6-2. 측정 시간(초)")]
        [Description("테스트 측정 시간을 입력하십시오.")]
        public double VacuumSensor_CheckTime { get; set; }
        [Category("6. 진공 센서측 테스트 설정")]
        [DisplayName("6-3. 합격 기준 압력(Bar)")]
        [Description("측정 시간 동안 압력차이값을 입력하십시오. 설정값 이상 차이가 날 경우 NG처리 됩니다.")]
        public double VacuumSensor_DiffBar { get; set; }
        #endregion
    }
    // Grid Display용
    public class LeakTestGrid
    {
        string _TestName;
        string _Description;
        double _StandardValue;
        double _MeasurementValue;
        double _LeakValue;
        double _LeakLimit;
        bool   _Result;

        public string TestName { get { return _TestName; } set { _TestName = value; Notify("TestName"); } }
        public string Description { get { return _Description; } set { _Description = value; Notify("Description"); } }
        public double StandardValue { get { return _StandardValue; } set { _StandardValue = value; Notify("StandardValue"); } }
        public double MeasurementValue { get { return _MeasurementValue; } set { _MeasurementValue = value; Notify("MeasurementValue"); } }
        public double LeakValue { get { return _LeakValue; } set { _LeakValue = value; Notify("LeakValue"); } }
        public double LeakLimit { get { return _LeakLimit; } set { _LeakLimit = value; Notify("LeakLimit"); } }

        public bool Result { get { return _Result; } set { _Result = value; Notify("Result"); } }

        public LeakTestGrid(string testname,string description, double standardvalue,double measurementvalue,double leakvalue,double leaklimit,bool result)
        {
            this.TestName = testname;
            this.Description = description;
            this.StandardValue = standardvalue;
            this.MeasurementValue = measurementvalue;
            this.LeakValue = leakvalue;
            this.LeakLimit = leaklimit;
            this.Result = result;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Notify(string propName)
        {
            if (this.PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
}
