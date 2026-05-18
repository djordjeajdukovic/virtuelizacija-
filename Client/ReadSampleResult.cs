using Common;

namespace Client
{
    public class ReadSampleResult
    {
        public bool IsValid { get; set; }
        public int RowIndex { get; set; }
        public string ErrorMessage { get; set; }
        public WindTurbineSample Sample { get; set; }
    }
}
