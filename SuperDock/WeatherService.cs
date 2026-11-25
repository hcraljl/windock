using System.Net.Http;
using System.Text.Json;
using System.Globalization;
namespace SuperDock;
internal record WeatherPlace(string Name,double Latitude,double Longitude);
internal record WeatherReading(double Temperature,int Code,double Wind,string Time)
{
    public string Description => Code switch {0=>"晴",1 or 2=>"多云",3=>"阴",45 or 48=>"雾",>=51 and <=67=>"雨",>=71 and <=77=>"雪",>=80 and <=82=>"阵雨",>=85 and <=86=>"阵雪",>=95=>"雷雨",_=>"天气变化"};
}
internal static class WeatherService
{
    static readonly HttpClient client=new(){Timeout=TimeSpan.FromSeconds(12)};
    public static async Task<List<WeatherPlace>> Search(string city,CancellationToken token)
    {
        using var response=await client.GetAsync("https://geocoding-api.open-meteo.com/v1/search?count=8&language=zh&name="+Uri.EscapeDataString(city),token);response.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        if(!json.RootElement.TryGetProperty("results",out var results))return [];
        return results.EnumerateArray().Select(p=>new WeatherPlace(p.GetProperty("name").GetString()+" · "+(p.TryGetProperty("admin1",out var region)?region.GetString():"")+" · "+p.GetProperty("country").GetString(),p.GetProperty("latitude").GetDouble(),p.GetProperty("longitude").GetDouble())).ToList();
    }
    public static async Task<WeatherReading> Read(double latitude,double longitude,CancellationToken token)
    {
        var url=FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,weather_code,wind_speed_10m&timezone=auto");
        using var response=await client.GetAsync(url,token);response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var current=json.RootElement.GetProperty("current");return new(current.GetProperty("temperature_2m").GetDouble(),current.GetProperty("weather_code").GetInt32(),current.GetProperty("wind_speed_10m").GetDouble(),current.GetProperty("time").GetString()??"");
    }
}

