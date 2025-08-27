using System.Text.Json.Serialization;

namespace thule.Models
{
    public class MeteoResponse
    {
        [JsonPropertyName("daily")]
        public DailyData Daily { get; set; }
    }

    public class DailyData
    {
        [JsonPropertyName("time")]
        public List<string> Time { get; set; }

        [JsonPropertyName("temperature_2m_mean")]
        public List<double?> Temperature_2m_mean { get; set; }

        [JsonPropertyName("rain_sum")]
        public List<double?> Rain_sum { get; set; }
    }
}
