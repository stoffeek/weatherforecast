using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using thule.Models;

namespace thule.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public WeatherController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpGet("partOne")]
        public IEnumerable<Weather> GetFromFile()
        {
            var jsonData = System.IO.File.ReadAllText("C:\\Users\\stoff\\Documents\\weather.json");
            var weatherForecast = JsonSerializer.Deserialize<List<Weather>>(jsonData);

            var latestTenDays = weatherForecast
                .OrderByDescending(p => p.date)
                .Take(10)
                .ToList();
            return latestTenDays;
        }

        [HttpGet("partTwo")]
        public async Task<IEnumerable<Weather>> GetFromApi([FromQuery] double latitude, [FromQuery] double longitude)
        {
            var endDate = DateTime.UtcNow.Date; 
            var startDate = endDate.AddDays(-9);
            var startDateString = startDate.ToString("yyyy-MM-dd"); 
            var endDateString = endDate.ToString("yyyy-MM-dd");

            var url = $"https://archive-api.open-meteo.com/v1/archive" +
                      $"?latitude={latitude}&longitude={longitude}" +
                      $"&start_date={startDateString}&end_date={endDateString}" +
                      $"&daily=temperature_2m_mean,rain_sum&timezone=auto";

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();

                Console.WriteLine(jsonResponse);

                var meteoData = JsonSerializer.Deserialize<MeteoResponse>(jsonResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (meteoData?.Daily?.Time == null || meteoData.Daily.Time.Count == 0)
                {
                    return Enumerable.Empty<Weather>();
                }

                var dailyWeather = meteoData.Daily.Time
                    .Select((time, index) => new Weather
                    {
                        date = DateTime.Parse(time),
                        averageTemperature = (float)(meteoData.Daily.Temperature_2m_mean[index] ?? 0),
                        totalRainMm = (float)(meteoData.Daily.Rain_sum[index] ?? 0)
                    })
                    .OrderByDescending(p => p.date)
                    .Take(10)
                    .ToList();

                return dailyWeather;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return Enumerable.Empty<Weather>();
            }
        }
    }
}
