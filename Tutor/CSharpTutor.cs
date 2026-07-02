using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ConsoleAppAPI_II_GigaChat.GigaChat;
using ConsoleAppAPI_II_GigaChat.GigaChat.Models;
using ConsoleAppAPI_II_GigaChat.Knowledge;
using Serilog;

namespace ConsoleAppAPI_II_GigaChat.Tutor
{
    internal class CSharpTutor
    {
        private readonly GigaChatClient _gc;
        private readonly KnowledgeBase _kb;
        private readonly FunctionExecutor _executor;
        private readonly Action<string> _write;
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public CSharpTutor(GigaChatClient gc, KnowledgeBase kb)
        {
            _gc = gc;
            _kb = kb;

            // Определяем делегаты, привязанные к консоли
            Action<string> write = Console.WriteLine;
            Func<string> read = () => Console.ReadLine() ?? "";
            _write = write;

            var plan = new StudyPlan(write);                  // передаём write
            var quiz = new QuizEngine(gc);
            _executor = new FunctionExecutor(plan, quiz, kb, gc, write, read);
        }

        // Память диалога. Системный промпт — "характер и правила" наставника.
        private readonly List<ChatMessage> _history = new()
    {
        new("system",
        "Ты — преподаватель-наставник со стажем 40 лет по обучению C# для начинающего разработчика. " +
        "Ты ведёшь его личный план изучения тем и помогаешь проверять знания. " +
        "У тебя есть инструменты (функции), которыми ты управляешь сам:\n" +
        "• add_topic — когда ученик хочет что-то изучить, просит добавить тему, или ты сам по ходу разговора считаешь тему важной.\n" +
        "• list_topics — когда ученик спрашивает, что у него в плане, что осталось или с чего начать.\n" +
        "• mark_studied — когда ученик говорит, что разобрался с темой, прошёл её или выучил.\n" +
        "• quiz_me — когда ученик просит проверить знания («проверь меня», «дай тест», «я готов по теме X») ИЛИ когда он уверяет, что разобрался, и это стоит подтвердить мини-тестом.\n" +
        "• search_documents — когда вопрос по теории C#, лекциям, или когда нужен ответ на основе учебных материалов. Вызывай её, и затем отвечай строго по найденным фрагментам.\n" +   // <-- добавлено
        "Правила:\n" +
        "- Вызывай функцию ТОЛЬКО когда она действительно нужна. За один ответ — не больше одного вызова функции.\n" +
        "- Не придумывай сам текст тест-вопроса и варианты — их ВСЕГДА формирует функция quiz_me. Ты лишь объявляешь о начале проверки и комментируешь её результат.\n" +
        "- Если в результате выполнения quiz_me пришло поле milestone_reached равное true, ты должен немедленно вызвать mark_studied для указанной темы, не спрашивая ученика.\n" +
        "- Когда пришёл результат quiz_me: если ответ верный — кратко похвали и предложи отметить тему изученной (mark_studied) или добавить смежную; если неверный — мягко разбери ошибку одним предложением и предложи оставить тему на повтор.\n" +
        "- Никогда не выдумывай результат функции — дождись, что вернёт программа.\n" +
        "- Если функции не нужны — просто ответь словами. Отвечай расширенно, не выдумывая, несколько раз перепроверяя информацию, доброжелательно и на русском."),
    };

        // Функции, которые разрешаем модели вызывать
        private static readonly List<FunctionDef> Functions = new()
    {
        new("add_topic",
            "Добавляет тему в личный план изучения C#.",
            new
            {
                type = "object",
                properties = new
                {
                    title    = new { type = "string", description = "Тема для изучения, напр. «делегаты»" },
                    priority = new { type = "string", @enum = new[] { "высокий", "средний", "низкий" },
                                     description = "Насколько важно изучить тему" },
                    note     = new { type = "string", description = "Заметка/зачем изучать (свободная форма). Может отсутствовать." },
                },
                required = new[] { "title" },
            }),
        new("list_topics",
            "Возвращает текущий план изучения ученика (что в плане и что уже изучено).",
            new { type = "object", properties = new { } }),
        new("mark_studied",
            "Помечает тему в плане как изученную (когда ученик говорит, что разобрался с ней).",
            new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string", description = "Какую тему из плана отметить изученной" },
                },
                required = new[] { "title" },
            }),
        new("quiz_me",
            "Проводит мини-тест по заданной теме C# (количество вариантов зависит от сложности: easy – 3, medium – 4, hard – 5). " +
            "Ученик видит вопрос, варианты и вводит номер ответа. Программа возвращает результат и пояснение.",
            new
            {
                type = "object",
                properties = new
                {
                    topic = new { type = "string", description = "Тема теста, напр. «разница между struct и class»" },
                    difficulty = new { type = "string", @enum = new[] { "easy", "medium", "hard" },
                                       description = "Желаемая сложность теста (по-умолчанию medium)" }
                },
                required = new[] { "topic" },
            }),
        new("search_documents",
        "Ищет ответ в лекциях по C# (документах) по смыслу. Вызывай, когда вопрос по теории языка, синтаксису, особенностям, или когда нужно объяснить что-то из учебных материалов.",
        new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "Суть вопроса для поиска в документах" }
            },
            required = new[] { "query" }
        }),
    };

        

        public void HandleUserInput(string input)
        {
            _history.Add(new ChatMessage("user", input));
            Log.Information("Вопрос пользователя: {Question}", input);

            var reply = _gc.ChatWithFunctions(_history, Functions);

            string answer;
            if (reply.FunctionCall is not null)
            {
                // Модель решила вызвать функцию
                _history.Add(reply with { Content = reply.Content ?? "" });
                string result = _executor.Execute(reply.FunctionCall);
                _history.Add(new ChatMessage("function", result, Name: reply.FunctionCall.Name));

                // Финальный ответ УЖЕ БЕЗ функций (чтобы не зациклилась)
                answer = _gc.Chat(_history);
            }
            else
            {
                answer = reply.Content ?? "";
            }

            _history.Add(new ChatMessage("assistant", answer));
            _write($"\nНаставник: {answer}\n");
        }
    }
}

