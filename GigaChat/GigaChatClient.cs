using ConsoleAppAPI_II_GigaChat.GigaChat.Models;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static System.Net.WebRequestMethods;


namespace ConsoleAppAPI_II_GigaChat.GigaChat
{
    internal class GigaChatClient
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        private readonly HttpClient _httpClient = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        private readonly string _chatUrl;
        private readonly string _embeddingsUrl;

        public GigaChatClient(string authKey, string chatUrl, string authUrl, string embeddingsUrl)
        {
            _chatUrl = chatUrl;
            _embeddingsUrl = embeddingsUrl;

            string token = GetAccessToken(authKey, authUrl);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public float[][] Embed(List<string> texts)
        {
            var result = Post<EmbeddingResponse>(
                _embeddingsUrl, new EmbeddingRequest("Embeddings", texts));
            return result.Data.OrderBy(d => d.Index).Select(d => d.Embedding).ToArray();
        }

        public ChatMessage ChatWithFunctions(List<ChatMessage> messages, List<FunctionDef> functions)
        {
            var body = new ChatRequest("GigaChat", messages, functions, FunctionCallMode: "auto");
            return Post<ChatResponse>(_chatUrl, body).Choices[0].Message;
        }

        // Обычный чат — для финального ответа после функции И для QuizEngine
        public string Chat(List<ChatMessage> messages, double? temperature = null)
        {
            var body = new ChatRequest("GigaChat", messages, Temperature: temperature);
            return Post<ChatResponse>(_chatUrl, body).Choices[0].Message.Content ?? "";
        }


        // Общий POST: сериализуем тело, шлём, разбираем ответ в T.
        private T Post<T>(string url, object body)
        {
            string json = JsonSerializer.Serialize(body, JsonOpts);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            using var response = _httpClient.Send(request);
            response.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<T>(ReadBody(response), JsonOpts)!;
        }

        private string GetAccessToken(string authKey, string authUrl) // Метод получения Bearer-токена по базовому ключу
        {
            Log.Debug("Начинаем получение токена доступа");
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, authUrl); // Создаём POST-запрос к эндпоинту авторизации

                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authKey);  // Устанавливаем заголовок Authorization: Basic <ключ>

                request.Headers.Add("RqUID", Guid.NewGuid().ToString());  // Добавляем уникальный идентификатор запроса (RqUID)

                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["scope"] = "GIGACHAT_API_PERS",  // Тело запроса содержит параметр scope со значением для физических лиц
                });


                using var response = _httpClient.Send(request); // Отправляем запрос
                Log.Debug("Ответ от сервера авторизации. StatusCode: {StatusCode}", response.StatusCode);

                response.EnsureSuccessStatusCode();  // Проверяем успешность ответа

                var token = JsonSerializer.Deserialize<TokenResponse>(ReadBody(response), JsonOpts); // Десериализуем тело ответа в объект TokenResponse

                return token.AccessToken; // Возвращаем полученный access_token
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при получении токена доступа");
                throw;
            }

        }

        private static string ReadBody(HttpResponseMessage response)
        {
            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }  // Вспомогательный метод: читает тело ответа как строку
    }
}
