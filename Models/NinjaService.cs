using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using System.Linq;

namespace tgbot.Models
{
    public static class NinjaService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string NinjaApiKey = "jdOorjbyYm4SSP9G8itWGfOXWKRW67qeYDzPqmtk";

        public static async Task<string> GetExerciseDetailsAsync(string query)
        {
            try
            {
                string englishQuery = await TranslateToEnglish(query);
                Console.WriteLine($"[Ninja] Запит перекладено: {query} -> {englishQuery}");

                string url = $"https://api.api-ninjas.com/v1/exercises?name={Uri.EscapeDataString(englishQuery)}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-Api-Key", NinjaApiKey);

                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                    return "❌ Не вдалося отримати дані з сервера вправ.";

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);

                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var ex = doc.RootElement[0];

                    string officialName = ex.TryGetProperty("name", out var pName) ? pName.GetString() : englishQuery;
                    string muscle = ex.TryGetProperty("muscle", out var pMuscle) ? pMuscle.GetString() : "не вказано";
                    string equipment = ex.TryGetProperty("equipment", out var pEquip) ? pEquip.GetString() : "не вказано";
                    string instructions = ex.TryGetProperty("instructions", out var pInstr) ? pInstr.GetString() : "";

                    string fullInfo = $"🏋️ *Вправа:* {officialName}\n" +
                                      $"💪 *М'язи:* {muscle}\n" +
                                      $"⚙️ *Обладнання:* {equipment}\n\n" +
                                      $"📝 *Інструкція:* {instructions}";

                    return await TranslateToUkrainian(fullInfo);
                }
                
                return $"🤷‍♂️ Вправу '{query}' не знайдено. Спробуйте іншу назву.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ninja Exception]: {ex.Message}");
                return "⚠️ Помилка при завантаженні опису.";
            }
        }

        private static async Task<string> TranslateToEnglish(string text)
        {
            try
            {
                string encodedText = HttpUtility.UrlEncode(text);
                string url = $"https://api.mymemory.translated.net/get?q={encodedText}&langpair=uk|en";
                
                var response = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                return doc.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString();
            }
            catch { return text; } 
        }

        private static async Task<string> TranslateToUkrainian(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            try
            {
                if (text.Length <= 450) return await CallTranslationApi(text, "en|uk");

                var parts = new List<string>();
                for (int i = 0; i < text.Length; i += 450)
                {
                    int length = Math.Min(450, text.Length - i);
                    string part = text.Substring(i, length);
                    parts.Add(await CallTranslationApi(part, "en|uk"));
                }
                return string.Join(" ", parts);
            }
            catch { return text; }
        }

        private static async Task<string> CallTranslationApi(string text, string langPair)
        {
            string encodedText = HttpUtility.UrlEncode(text);
            string url = $"https://api.mymemory.translated.net/get?q={encodedText}&langpair={langPair}";

            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            string translated = doc.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString();
            
            return System.Net.WebUtility.HtmlDecode(translated);
        }
    }
}