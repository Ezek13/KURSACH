using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace tgbot.Models
{
    public static class SpotifyService
    {
        private static readonly string _clientId = "063ed381f5b8406a8cc83e6b4a724c14";
        private static readonly string _clientSecret = "6ad27df2eed444e884026e9c52b9baa8";
        
        private static SpotifyClient _spotify;
        private static DateTime _tokenExpireTime;

        private static async Task EnsureClientAsync()
        {
            if (_spotify == null || DateTime.Now >= _tokenExpireTime)
            {
                var config = SpotifyClientConfig.CreateDefault();
                var request = new ClientCredentialsRequest(_clientId, _clientSecret);
                var response = await new OAuthClient(config).RequestToken(request);

                _spotify = new SpotifyClient(config.WithToken(response.AccessToken));
                _tokenExpireTime = DateTime.Now.AddSeconds(response.ExpiresIn - 60);
            }
        }

        public static async Task<string> GetMusicForExercise(string category)
        {
            try
            {
                await EnsureClientAsync();

                string searchQuery = category switch
                {
                    "Силові 💪" => "Gym Beast Mode",
                    "Розтяжка 🧩" => "Yoga & Meditation",
                    "Кардіо ❤️" => "Cardio 2026",
                    "HIIT ⚡" => "Phonk Workout",
                    _ => "Workout Hits"
                };

                var searchRequest = new SearchRequest(SearchRequest.Types.Playlist, searchQuery)
                {
                    Limit = 10,
                    Market = "US" 
                };

                var result = await _spotify.Search.Item(searchRequest);

                if (result?.Playlists?.Items?.Count > 0)
                {
                    var random = new Random();
                    var playlist = result.Playlists.Items[random.Next(result.Playlists.Items.Count)];

                    return $"https://open.spotify.com/playlist/{playlist.Id}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Spotify Error]: {ex.Message}");
            }

            return "https://open.spotify.com/playlist/37i9dQZF1DX76W9Sjom64M";
        }
    }
}