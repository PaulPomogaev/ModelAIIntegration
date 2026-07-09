using ModelAIIntegrationCore.Gemini.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelAIIntegrationCore;
using ModelAIIntegrationCore.GigaChat.Models;
using ModelAIIntegrationCore.Gemini.Models;

namespace ModelAIIntegrationCore.Gemini
{
    public class GeminiClient : ILanguageModel
    {
        private readonly string chatModel;
        private readonly string embedModel;
        private readonly string generateUrl;
        private readonly string batchEmbedUrl;

        // camelCase (Web) сам переводит systemInstruction/functionCall/functionResponse/toolConfig;
        // WhenWritingNull — чтобы пустые варианты part (text/functionCall/functionResponse) не улетали
        // как null и не ломали запрос.
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly HttpClient http = new();

        public GeminiClient(string apiKey, string chatModel = "gemini-2.5-flash", string embedModel = "gemini-embedding-001", string baseUrl = "https://generativelanguage.googleapis.com")
        {
            this.chatModel = chatModel;
            this.embedModel = embedModel;
            string root = baseUrl.TrimEnd('/');
            generateUrl = $"{root}/v1beta/models/{chatModel}:generateContent";
            batchEmbedUrl = $"{root}/v1beta/models/{embedModel}:batchEmbedContents";

            // Ключ — заголовком на всё соединение (в конструкторе сети НЕТ, только настройка).
            http.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
        }

        public async Task<string> ChatAsync(List<ChatMessage> messages, double? temperature = null)
        {
            var (system, contents) = ToGemini(messages);
            object? generationConfig = null;
            if (temperature.HasValue)
                generationConfig = new { temperature = temperature.Value };

            var body = new GeminiGenerateRequest(contents, system, GenerationConfig: generationConfig);
            var reply = await PostAsync<GeminiGenerateResponse>(generateUrl, body);
            return FromGemini(reply).Content ?? "";
        }

        public async Task<ChatMessage> ChatWithFunctionsAsync(List<ChatMessage> messages, List<FunctionDef> functions)
        {
            var (system, contents) = ToGemini(messages);
            var tools = new List<GeminiTool>
            {
                new(functions.Select(f => new GeminiFunctionDeclaration(f.Name, f.Description, f.Parameters)).ToList()),
            };

            var body = new GeminiGenerateRequest(contents, system, tools, new GeminiToolConfig(new GeminiFunctionCallingConfig("AUTO")));
            var reply = await PostAsync<GeminiGenerateResponse>(generateUrl, body);
            return FromGemini(reply);
        }

        public async Task<float[][]> EmbedAsync(List<string> texts)
        {
            var requests = texts.Select(t => new GeminiEmbedRequestItem($"models/{embedModel}", new GeminiContent(null, new List<GeminiPart> { new(Text: t) }))).ToList();
            var result = await PostAsync<GeminiBatchEmbedResponse>(batchEmbedUrl, new GeminiBatchEmbedRequest(requests));
            return result.Embeddings.Select(e => e.Values).ToArray();
        }

        // Наш общий ChatMessage[] → формат Gemini: системный промпт уходит в systemInstruction,
        // остальное — в contents (role user/model, результат функции — role:"user" + functionResponse).
        private static (GeminiContent? System, List<GeminiContent> Contents) ToGemini(List<ChatMessage> messages)
        {
            GeminiContent? system = null;
            var contents = new List<GeminiContent>();

            foreach (var m in messages)
            {
                if (m.Role == "system")
                {
                    system = new GeminiContent(null, new List<GeminiPart> { new(Text: m.Content ?? "") });
                }
                else if (m.Role == "function")
                {
                    // Результат нашей функции. Gemini ждёт его как part functionResponse c role:"user".
                    // Тело результата у нас — JSON-строка ({"results":[...]}), Gemini хочет объект.
                    contents.Add(new GeminiContent("user", new List<GeminiPart>
                    {
                        new(FunctionResponse: new GeminiFunctionResponse(m.Name ?? "", ParseResponse(m.Content))),
                    }));
                }
                else
                {
                    string role = m.Role == "assistant" ? "model" : "user";
                    GeminiPart part = m.FunctionCall is { } fc
                        ? new GeminiPart(FunctionCall: new GeminiFunctionCall(fc.Name, fc.Arguments))
                        : new GeminiPart(Text: m.Content ?? "");
                    contents.Add(new GeminiContent(role, new List<GeminiPart> { part }));
                }
            }

            return (system, contents);
        }

        // Ответ Gemini → наш общий ChatMessage. Если среди parts есть functionCall — берём его
        // (просим одну функцию), иначе склеиваем текстовые parts.
        private static ChatMessage FromGemini(GeminiGenerateResponse reply)
        {
            var parts = reply.Candidates?.FirstOrDefault()?.Content?.Parts ?? new List<GeminiPart>();

            var call = parts.FirstOrDefault(p => p.FunctionCall is not null)?.FunctionCall;
            if (call is not null)
                return new ChatMessage("assistant", null, new FunctionCall(call.Name, call.Args));

            string text = string.Concat(parts.Where(p => p.Text is not null).Select(p => p.Text));
            return new ChatMessage("assistant", text);
        }

        // Тело результата функции: строка → JSON-объект (Gemini ждёт объект в response).
        // Не распарсилось (вдруг не JSON) — заворачиваем в { result: "..." }.
        private static object ParseResponse(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new { };
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(content);
            }
            catch (JsonException)
            {
                return new { result = content };
            }
        }

        private async Task<T> PostAsync<T>(string url, object body)
        {
            string json = JsonSerializer.Serialize(body, JsonOpts);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            using HttpResponseMessage response = await SendOrThrowAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                // Тело ошибки Gemini содержательное (в т.ч. «User location is not supported» из РФ) —
                // показываем его, а не голый статус.
                string error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Gemini ответил {(int)response.StatusCode}. {Shorten(error)}");
            }

            string respBody = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(respBody, JsonOpts)!;
        }

        // Нет сети / DNS / гео-блок на уровне соединения → понятная подсказка (частый случай из РФ).
        private async Task<HttpResponseMessage> SendOrThrowAsync(HttpRequestMessage request)
        {
            try
            {
                return await http.SendAsync(request);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    "Не достучался до Gemini API (generativelanguage.googleapis.com). Проверь интернет; " +
                    "из России API заблокирован по IP — нужен VPN/прокси в поддерживаемой стране.",
                    ex);
            }
        }

        private static string Shorten(string s)
        {
            s = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return s.Length <= 300 ? s : s[..300] + "…";
        }
    }
}
