using ModelAIIntegrationCore.GigaChat;
using ModelAIIntegrationCore.Tutor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using ModelAIIntegrationCore.GigaChat.Models;

namespace ModelAIIntegrationCore.Tutor
{
    internal class QuizEngine
    {
        private readonly ILanguageModel _llm;
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public QuizEngine(ILanguageModel llm) => _llm = llm;

        public async Task<QuizQuestion> GenerateAsync(string topic, string difficulty = "medium")
        {
            string systemPrompt = difficulty switch
            {
                "easy" =>
                    "Ты — генератор простых тест-вопросов для начинающих C#. " +
                    "Задавай только элементарные вопросы про ключевые слова, базовый синтаксис и простые типы. " +
                    "Верни ТОЛЬКО JSON-объект по схеме:\n" +
                    "{ \"question\": строка, \"options\": [ровно 3 строки], \"correctIndex\": число 0..2, \"explanation\": строка }\n" +
                    "correctIndex — позиция верного варианта, нумерация с НУЛЯ. Без markdown, без лишнего текста.",
                "hard" =>
                    "Ты — генератор сложных тест-вопросов по C#. " +
                    "Задавай углублённые вопросы про внутреннее устройство языка (ref/in/out, Span<T>, аллокации, boxing, замыкания, GC, многопоточность). " +
                    "Верни ТОЛЬКО JSON-объект по схеме:\n" +
                    "{ \"question\": строка, \"options\": [ровно 5 строк], \"correctIndex\": число 0..4, \"explanation\": строка }\n" +
                    "correctIndex — позиция верного варианта, нумерация с НУЛЯ. Без markdown.",
                _ =>
                    "Ты — генератор тест-вопросов про язык C# для начинающих. " +
                    "Спрашивай про факты языка: ключевые слова (var, const, readonly, static, ref, out), " +
                    "типы (struct/class, nullable), синтаксис, операторы, базовые коллекции (List<T>, Dictionary), " +
                    "строки, исключения, ООП в C#. " +
                    "Сначала ВЫБЕРИ один факт, в котором уверен, сделай его верным ответом, затем придумай 3 правдоподобных, но неверных. " +
                    "Вопрос должен быть КОНКРЕТНЫМ, а не общим рассуждением. " +
                    "Верни ТОЛЬКО JSON-объект по схеме:\n" +
                    "{ \"question\": строка, \"options\": [ровно 4 строки], \"correctIndex\": число 0..3, \"explanation\": строка }\n" +
                    "Ровно один верный вариант. Без markdown, без текста до или после JSON."
            };

            double temp = difficulty switch { "easy" => 0.3, "hard" => 0.5, _ => 0.2 };

            var messages = new List<ChatMessage>
            {
              new("system", systemPrompt),
              new("user", $"Тема для теста (про язык C#): {topic}"),
            };

            int maxAttempts = 3;
            string? raw = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    raw = await _llm.ChatAsync(messages, temperature: temp);
                    string json = ExtractJson(raw);
                    var quiz = JsonSerializer.Deserialize<QuizQuestion>(json, JsonOpts);
                    if (quiz != null)
                        return quiz;
                }
                catch (JsonException ex)
                {
                    Log.Warning(ex, "Ошибка десериализации JSON при попытке {Attempt}. Ответ: {Response}", attempt, raw);
                    if (attempt == maxAttempts)
                        throw new InvalidOperationException("Модель вернула невалидный JSON после нескольких попыток.", ex);
                    await Task.Delay(200 * attempt);
                }
            }

            throw new InvalidOperationException("Не удалось сгенерировать тест.");
        }

        private static string ExtractJson(string raw) // добавил новый метод ввиду появления ошибок JSON при генерации тестов
        {
            // Обрезаем возможный маркер ```json ... ```
            if (raw.Contains("```json"))
            {
                int start = raw.IndexOf("```json") + 7;
                int end = raw.LastIndexOf("```");
                if (end > start)
                    raw = raw.Substring(start, end - start).Trim();
            }
            else if (raw.Contains("```"))
            {
                int start = raw.IndexOf("```") + 3;
                int end = raw.LastIndexOf("```");
                if (end > start)
                    raw = raw.Substring(start, end - start).Trim();
            }

            // Ищем первую '{' и последнюю '}'
            int firstBrace = raw.IndexOf('{');
            int lastBrace = raw.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
                raw = raw.Substring(firstBrace, lastBrace - firstBrace + 1);

            return raw;
        }
    }
}
