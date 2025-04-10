using System.Text.Json.Serialization;

namespace SharpReader
{
    public class ReportData
    {
        public bool Sent { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum SubjectType
        {
            BUG,
            FEATURE,
            OTHER,
        }
        public SubjectType Subject { get; set; }
        public string Description { get; set; }
        public ReportData()
        {
            Sent = false;
            Subject = SubjectType.OTHER;
            Description = "";
        }
    }
}
