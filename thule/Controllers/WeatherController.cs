using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using System.Text.Json;
using thule.Models; 
using System.Text;

namespace thule.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<WeatherController> _logger;

        public WeatherController(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<WeatherController> logger  )
        {
            _httpClient = httpClientFactory.CreateClient();
            _cache = cache;
            _logger = logger;

        }

        [HttpGet("partOne")]
        public IEnumerable<Weather> GetFromFile()
        {
            var jsonData = System.IO.File.ReadAllText("Data/weather.json");
            var weatherForecast = JsonSerializer.Deserialize<List<Weather>>(jsonData);

            var latestTenDays = weatherForecast
                .OrderByDescending(p => p.date)
                .Take(10)
                .ToList();

            latestTenDays.ForEach(w => w.source = "file");


            return latestTenDays;
        }

        [HttpGet("partTwo")]
        public async Task<IEnumerable<Weather>> GetFromApi([FromQuery] double latitude, [FromQuery] double longitude)
        {
            var cacheKey = $"weather_{latitude}_{longitude}";
            if (_cache.TryGetValue(cacheKey, out List<Weather> cachedWeather))
            {
                cachedWeather.ForEach(w => w.source = "cache");

                _logger.LogInformation("Returning cached weather data for {lat}, {lon}", latitude, longitude);
                return cachedWeather;
            }

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

                _logger.LogInformation("Fetched data from API: {url}", url);

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
                        totalRainMm = (float)(meteoData.Daily.Rain_sum[index] ?? 0),
                        source = "api"
                    })
                    .OrderByDescending(p => p.date)
                    .Take(10)
                    .ToList();

                _cache.Set(cacheKey, dailyWeather, TimeSpan.FromHours(1));

                return dailyWeather;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching weather data from API");
                return Enumerable.Empty<Weather>();
            }


        }

        [HttpGet("exportCsv")]
        public async Task<IActionResult> ExportCsv([FromQuery] double latitude, [FromQuery] double longitude)
        {
            var weatherData = await GetFromApi(latitude, longitude);

            if (weatherData == null || !weatherData.Any())
            {
                return NotFound("No weather data available to export.");
            }

            var sb = new StringBuilder();
            sb.AppendLine("Date,AverageTemperature,TotalRainMm,Source");
            foreach (var w in weatherData)
            {
                sb.AppendLine($"{w.date:yyyy-MM-dd},{w.averageTemperature},{w.totalRainMm},{w.source}");
            }

            var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"weather_{DateTime.UtcNow:yyyy-MM-dd}.csv";

            return File(csvBytes, "text/csv", fileName);
        }


    }

}
