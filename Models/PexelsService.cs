using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace tgbot.Models
{
    public static class PexelsService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string _apiKey = "nIOCbfYTG4yujs6MRxXnyRzcqMGqf0M9dLs2L2ETMgANfi73cCaV4Xuh"; 

        public static async Task<string> GetExerciseVideoAsync(string query)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", "nIOCbfYTG4yujs6MRxXnyRzcqMGqf0M9dLs2L2ETMgANfi73cCaV4Xuh");

                string url = $"https://api.pexels.com/videos/search?query={Uri.EscapeDataString(query)}&per_page=1";
                var response = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
        
                var videos = doc.RootElement.GetProperty("videos");
                if (videos.GetArrayLength() > 0)
                {
                    var videoFiles = videos[0].GetProperty("video_files");
            
                    foreach (var file in videoFiles.EnumerateArray())
                    {
                        if (file.GetProperty("quality").GetString() == "sd")
                            return file.GetProperty("link").GetString();
                    }
                    return videoFiles[0].GetProperty("link").GetString();
                }
            }
            catch { return null; }
            return null;
        }
    }
}