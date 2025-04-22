using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace SharpReader
{
    internal class SlackLoger
    {
        private static readonly HttpClient _client = new HttpClient();
        private static readonly string SlackWebhookUrl;
        //SLACK_WEBHOOK_URL

        static SlackLoger()
        {
            var appFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var builder = new ConfigurationBuilder()
                .SetBasePath(appFolder) // Katalog, w którym jest aplikacja appFolder | Directory.GetCurrentDirectory()
                .AddJsonFile("Settings.json", optional: false, reloadOnChange: true);

            var configuration = builder.Build();
            SlackWebhookUrl = configuration["SlackWebhookUrl"];
        }

        
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
