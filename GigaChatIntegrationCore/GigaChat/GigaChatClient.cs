using Microsoft.Extensions.Configuration;
using ModelAIIntegrationCore;
using ModelAIIntegrationCore.GigaChat.Models;
using Serilog;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Net.WebRequestMethods;


namespace ModelAIIntegrationCore.GigaChat
{
    public class GigaChatClient : ILanguageModel
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        private readonly HttpClient _httpClient = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        private readonly string _chatUrl;
        private readonly string _embeddingsUrl;

        private GigaChatClient(string chatUrl, string embeddingsUrl) 
        {
            _chatUrl = chatUrl;
            _embeddingsUrl = embeddingsUrl;
        }
       
        public static async Task<GigaChatClient> CreateAsync(string authKey, string chatUrl, string authUrl, string embeddingsUrl)
        {
            var client = new GigaChatClient(chatUrl, embeddingsUrl);
            string token = await client.GetAccessTokenAsync(authKey, authUrl);
            client._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        public async Task<float[][]> EmbedAsync(List<string> texts)
        {
            var result = await PostAsync<EmbeddingResponse>(
                _embeddingsUrl, new EmbeddingRequest("Embeddings", texts));
            return result.Data.OrderBy(d => d.Index).Select(d => d.Embedding).ToArray();
        }

        public async Task<ChatMessage> ChatWithFunctionsAsync(List<ChatMessage> messages, List<FunctionDef> functions)
        {
            var body = new ChatRequest("GigaChat", messages, functions, FunctionCallMode: "auto");
            return (await PostAsync<ChatResponse>(_chatUrl, body)).Choices[0].Message;
        }

        // Обычный чат — для финального ответа после функции И для QuizEngine
        public async Task<string> ChatAsync(List<ChatMessage> messages, double? temperature = null)
        {
            var body = new ChatRequest("GigaChat", messages, Temperature: temperature);
            return (await PostAsync<ChatResponse>(_chatUrl, body)).Choices[0].Message.Content ?? "";
        }


        // Общий POST: сериализуем тело, шлём, разбираем ответ в T.
        private async Task<T> PostAsync<T>(string url, object body)
        {
            string json = JsonSerializer.Serialize(body, JsonOpts);
            int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };

                using var response = await _httpClient.SendAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    // Пытаемся прочитать Retry-After из заголовков
                    int delaySeconds = 2; // по умолчанию
                    if (response.Headers.TryGetValues("Retry-After", out var values))
                    {
                        var retryAfter = values.FirstOrDefault();
                        if (int.TryParse(retryAfter, out int sec))
                            delaySeconds = sec;
                    }

                    // На последней попытке не ждём, выбрасываем исключение
                    if (attempt == maxRetries)
                        throw new HttpRequestException("Превышен лимит запросов к GigaChat после повторных попыток.");

                    Log.Warning("Получен 429, жду {Delay} сек. (попытка {Attempt}/{MaxRetries})", delaySeconds, attempt, maxRetries);
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    continue;
                }

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                return await JsonSerializer.DeserializeAsync<T>(stream, JsonOpts) ?? throw new InvalidOperationException("Ответ содержит null");
            }

            throw new InvalidOperationException("Не удалось выполнить запрос после повторных попыток.");
        }

        private async Task<string> GetAccessTokenAsync(string authKey, string authUrl) // Метод получения Bearer-токена по базовому ключу
        {
            Log.Debug("Начинаем получение токена доступа");
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, authUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authKey);
                request.Headers.Add("RqUID", Guid.NewGuid().ToString());
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["scope"] = "GIGACHAT_API_PERS",
                });

                using var response = await _httpClient.SendAsync(request); 
                Log.Debug("Ответ от сервера авторизации. StatusCode: {StatusCode}", response.StatusCode);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync(); 
                var token = JsonSerializer.Deserialize<TokenResponse>(json, JsonOpts);
                return token!.AccessToken;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при получении токена доступа");
                throw;
            }

        }
       
    }
}
