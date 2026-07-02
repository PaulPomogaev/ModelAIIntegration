using ConsoleAppAPI_II_GigaChat.GigaChat.Models;
using ConsoleAppAPI_II_GigaChat.GigaChat;
using ConsoleAppAPI_II_GigaChat.Tutor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConsoleAppAPI_II_GigaChat.Tutor
{
    internal class QuizEngine
    {
        private readonly GigaChatClient _gc;
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public QuizEngine(GigaChatClient gc) => _gc = gc;

        public QuizQuestion Generate(string topic, string difficulty = "medium")
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

            string clean = _gc.Chat(messages, temperature: temp);
            return JsonSerializer.Deserialize<QuizQuestion>(clean, JsonOpts)
                ?? throw new InvalidOperationException("Модель вернула пустой JSON вместо тест-вопроса.");
        }
    }
}
