using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EscapeHookah.Web.Services
{
    public class FcmService
    {
        private readonly ILogger<FcmService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string? _serverKey;

        public FcmService(ILogger<FcmService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _serverKey = configuration["Fcm:ServerKey"] ?? Environment.GetEnvironmentVariable("FCM_SERVER_KEY");
        }

        public bool IsConfigured => !string.IsNullOrEmpty(_serverKey);

        public async Task<bool> SendNotificationAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("FCM not configured - skipping notification");
                return false;
            }

            try
            {
                var payload = new Dictionary<string, object>
                {
                    ["to"] = deviceToken,
                    ["notification"] = new { title, body }
                };

                if (data != null)
                    payload["data"] = data;

                var json = JsonSerializer.Serialize(payload);
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://fcm.googleapis.com/fcm/send")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("key", _serverKey);

                var resp = await client.SendAsync(request);
                if (!resp.IsSuccessStatusCode)
                {
                    var respText = await resp.Content.ReadAsStringAsync();
                    _logger.LogWarning("FCM send failed: {Status} {Response}", resp.StatusCode, respText);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending FCM notification");
                return false;
            }
        }
    }
}
