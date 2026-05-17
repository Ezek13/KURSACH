using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace tgbot.Models
{
    public static class SettingsService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<bool> UpdateUserWeight(long userId, double newWeight, string currentGoal)
        {
            try 
            {
                string url = $"https://jsonplaceholder.typicode.com/posts/1"; 
                var data = new { 
                    id = userId, 
                    weight = newWeight, 
                    goal = currentGoal, 
                    updatedAt = DateTime.Now 
                };

                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> UpdateUserGoal(long userId, string newGoal, double currentWeight)
        {
            try 
            {
                string url = $"https://jsonplaceholder.typicode.com/posts/1"; 
                var data = new { 
                    id = userId, 
                    goal = newGoal, 
                    weight = currentWeight, 
                    updatedAt = DateTime.Now 
                };

                var json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}