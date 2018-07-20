using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Design;
using System.ComponentModel;
namespace BCPT
{
    /// <summary>
    /// 전체 시험관련 사양정의 클래스
    /// </summary>
    public class TestSpecification
    {
        #region 전역 프로퍼티(프로퍼티 그리드에서 수정가능한 구역)
        [Category("1. LOST TRAVEL PRI")]
        [DisplayName("1-1. CutOff Hole Min(mm) ")]
        [Description("컷 오프 홀 최저값을 입력하십시오(단위 : mm).")]
        public double LTP_CutOffHole_Min { get; set; }
        [Category("1. LOST TRAVEL PRI")]
        [DisplayName("1-2. CutOff Hole Max(mm) ")]
        [Description("컷 오프 홀 최대값을 입력하십시오(단위 : mm).")]
        public double LTP_CutOffHole_Max { get; set; }
        [Category("1. LOST TRAVEL PRI")]
        [DisplayName("1-3. FLO Pressure Low(bar) ")]
        [Description("FLO 설정압 이상시 OK처리 됩니다.")]
        public double LTP_FLOPressure_Low { get; set; }
        [Category("1. LOST TRAVEL PRI")]
        [DisplayName("1-4. 최대 전진 거리 ")]
        [Description("압력 형성의 유무와 관계없이 시험시 최대 전진 거리를 입력하십시오.(mm)")]
        public double LTP_MaxStroke { get; set; }


        [Category("2. LOST TRAVEL FLO")]
        [DisplayName("1-1. TEST Time(sec) ")]
        [Description("테스트 시험 시간을 입력하십시오.")]
        public double LTP_CutOffHole_Min1 { get; set; }
        [Category("1. LOST TRAVEL PRI")]
        [DisplayName("1-2. CutOff Hole Max(mm) ")]
        [Description("컷 오프 홀 최대값을 입력하십시오(단위 : mm).")]
        public double LTP_CutOffHole_Max1 { get; set; }
        [Category("1. LOST TRAVEL PRI")]
        [DisplayName("1-3. FLO Pressure Low(bar) ")]
        [Description("FLO 설정압 이상시 OK처리 됩니다.")]
        public double LTP_FLOPressure_Low1 { get; set; }

        #endregion
    }
    /// <summary>
    /// 테스트 스펙 정의 그리드용 클래스
    /// </summary>
    public class TestSpecGrid
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

        public TestSpecGrid(string testname, string description, double standardvalue, double measurementvalue, double leakvalue, double leaklimit, bool result)
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
