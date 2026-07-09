using ModelAIIntegrationCore.Ollama.Models;
using ModelAIIntegrationCore;
using ModelAIIntegrationCore.GigaChat.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModelAIIntegrationCore.Ollama
{
    public class OllamaClient : ILanguageModel
    {
        private readonly string chatModel;
        private readonly string embedModel;
        private readonly string chatUrl;
        private readonly string embedUrl;

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly HttpClient http = new()
        {
            Timeout = TimeSpan.FromMinutes(60)   // 60 минут таймаут, модель тяжёлая
        };

        public OllamaClient(string chatModel, string embedModel, string baseUrl = "http://localhost:11434")
        {
            this.chatModel = chatModel;
            this.embedModel = embedModel;
            string root = baseUrl.TrimEnd('/');
            chatUrl = $"{root}/api/chat";
            embedUrl = $"{root}/api/embed";
        }
        public async Task<string> ChatAsync(List<ChatMessage> messages, double? temperature = null)
        {
            var reply = await PostAsync<OllamaChatResponse>(chatUrl,
                new OllamaChatRequest(chatModel, messages.Select(ToOllama).ToList()));
            return reply.Message.Content ?? "";
        }

        private static OllamaMessage ToOllama(ChatMessage m) => new(
           m.Role == "function" ? "tool" : m.Role,
           m.Content,
           m.FunctionCall is { } fc
               ? new List<OllamaToolCall> { new(new OllamaFunctionCall(fc.Name, fc.Arguments)) }
               : null);

        public async Task<ChatMessage> ChatWithFunctionsAsync(List<ChatMessage> messages, List<FunctionDef> functions)
        {
            var tools = functions
                   .Select(f => new OllamaTool("function", new OllamaFunctionDef(f.Name, f.Description, f.Parameters)))
                   .ToList();
            var reply = await PostAsync<OllamaChatResponse>(chatUrl,
                    new OllamaChatRequest(chatModel, messages.Select(ToOllama).ToList(), tools));
            return FromOllama(reply.Message);
        }

        // Ответ Ollama → наш общий ChatMessage. Просим только одну функцию (search_documents),
        // поэтому если модель вызвала инструмент — берём первый tool_call, второй не ждём.
        private static ChatMessage FromOllama(OllamaMessage m)
        {
            var call = m.ToolCalls?.FirstOrDefault();
            return new ChatMessage("assistant", m.Content,
                call is null ? null : new FunctionCall(call.Function.Name, call.Function.Arguments));
        }

        public async Task<float[][]> EmbedAsync(List<string> texts)
        {
            var result = await PostAsync<OllamaEmbedResponse>(embedUrl, new OllamaEmbedRequest(embedModel, texts));
            return result.Embeddings.ToArray();
        }

        private async Task<T> PostAsync<T>(string url, object body)
        {
            string json = JsonSerializer.Serialize(body, JsonOpts);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            using HttpResponseMessage response = await SendOrThrowAsync(request);
            response.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<T>(ReadBody(response), JsonOpts)!;
        }

        private async Task<HttpResponseMessage> SendOrThrowAsync(HttpRequestMessage request)
        {
            try
            {
                return await http.SendAsync(request);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Не достучался до Ollama по адресу {request.RequestUri}. Проверь: запущен ли " +
                    $"«ollama serve», скачаны ли модели («ollama pull {chatModel}» и «ollama pull {embedModel}»).",
                    ex);
            }
        }

        private static string ReadBody(HttpResponseMessage response)
        {
            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static async Task<List<string>> ListModels(string baseUrl)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/api/tags");
                using var response = await http.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var tags = JsonSerializer.Deserialize<OllamaTagsResponse>(ReadBody(response), JsonOpts);
                return tags?.Models?.Select(m => m.Name).ToList() ?? [];
            }
            catch
            {
                return new List<string>();
            }
        }
    }

   
}
