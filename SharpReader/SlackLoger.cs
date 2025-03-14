using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;

namespace SharpReader
{
    internal class SlackLoger
    {
        private static readonly HttpClient _client = new HttpClient();
        private static readonly string SlackWebhookUrl = "https://hooks.slack.com/services/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";

        public static async Task SendMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine("Błąd: Wiadomość jest pusta!");
                return;
            }

            try
            {
                var payload = new { text = message };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Console.WriteLine($"Wysyłany JSON: {json}"); // Sprawdzamy, co wysyłamy

                HttpResponseMessage response = await _client.PostAsync(SlackWebhookUrl, content);
                string responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("✅ Raport wysłany!");
                }
                else
                {
                    Console.WriteLine($"❌ Błąd wysyłania: {response.StatusCode}");
                    Console.WriteLine($"🛑 Slack Response: {responseText}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Błąd wysyłania: {ex.Message}");
            }
        }
    }
}
