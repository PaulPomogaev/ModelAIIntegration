using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ModelAIIntegrationCore.GigaChat;
using ModelAIIntegrationCore.Knowledge;
using ModelAIIntegrationCore.Knowledge.Models;
using ModelAIIntegrationCore.Tutor.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using ModelAIIntegrationCore.GigaChat.Models;
using Serilog;

namespace ModelAIIntegrationCore.Tutor
{
    public class CSharpTutor
    {
        private readonly ILanguageModel _llm;
        private readonly KnowledgeBase _kb;
        private readonly FunctionExecutor _executor;
        private readonly QuizEngine _quiz;
        private readonly StudyPlan _plan;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        private const string HistoryKey = "TutorHistory";
        private const string PendingKey = "TutorPending";

        // Поля для хранения состояния в консольном режиме
        private List<ChatMessage> _historyField = new();
        private ChatMessage? _pendingField = null;


        // Свойство History – работает с сессией (веб) или с полем (консоль)
        private List<ChatMessage> History
        {
            get
            {
                if (_httpContextAccessor != null)
                {
                    // Веб-режим: читаем из сессии
                    var session = _httpContextAccessor.HttpContext?.Session;
                    if (session == null) return new List<ChatMessage>();
                    var json = session.GetString(HistoryKey);
                    if (string.IsNullOrEmpty(json))
                    {
                        var initial = GetSystemMessages();
                        session.SetString(HistoryKey, JsonSerializer.Serialize(initial));
                        return initial;
                    }
                    return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new();
                }
                else
                {
                    // Консольный режим: используем поле
                    if (_historyField.Count == 0)
                        _historyField = GetSystemMessages();
                    return _historyField;
                }
            }
            set
            {
                if (_httpContextAccessor != null)
                {
                    var session = _httpContextAccessor.HttpContext?.Session;
                    if (session != null)
                        session.SetString(HistoryKey, JsonSerializer.Serialize(value));
                }
                else
                {
                    _historyField = value;
                }
            }
        }

        private ChatMessage? PendingFunctionCall
        {
            get
            {
                if (_httpContextAccessor != null)
                {
                    var session = _httpContextAccessor.HttpContext?.Session;
                    if (session == null) return null;
                    var json = session.GetString(PendingKey);
                    return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<ChatMessage>(json);
                }
                else
                {
                    return _pendingField;
                }
            }
            set
            {
                if (_httpContextAccessor != null)
                {
                    var session = _httpContextAccessor.HttpContext?.Session;
                    if (session == null) return;
                    if (value == null)
                        session.Remove(PendingKey);
                    else
                        session.SetString(PendingKey, JsonSerializer.Serialize(value));
                }
                else
                {
                    _pendingField = value;
                }
            }
        }

        private List<ChatMessage> GetSystemMessages()
        {
            return new List<ChatMessage>
            {
        new("system",
        "Ты — преподаватель-наставник со стажем 40 лет по обучению C# для начинающего разработчика. " +
        "Ты ведёшь его личный план изучения тем и помогаешь проверять знания. " +
        "У тебя есть инструменты (функции), которыми ты управляешь сам:\n" +
        "• add_topic — когда ученик хочет что-то изучить, просит добавить тему, или ты сам по ходу разговора считаешь тему важной.\n" +
        "• list_topics — когда ученик спрашивает, что у него в плане, что осталось или с чего начать.\n" +
        "• mark_studied — когда ученик говорит, что разобрался с темой, прошёл её или выучил.\n" +
        "• quiz_me — когда ученик просит проверить знания («проверь меня», «дай тест», «я готов по теме X») ИЛИ когда он уверяет, что разобрался, и это стоит подтвердить мини-тестом.\n" +
        "• search_documents — когда вопрос по теории C#, лекциям, или когда нужен ответ на основе учебных материалов. Вызывай её, и затем отвечай строго по найденным фрагментам.\n" +
        "Правила:\n" +
        "- Вызывай функцию ТОЛЬКО когда она действительно нужна. За один ответ — не больше одного вызова функции.\n" +
        "- Не придумывай сам текст тест-вопроса и варианты — их ВСЕГДА формирует функция quiz_me. Ты лишь объявляешь о начале проверки и комментируешь её результат.\n" +
        "- Если в результате выполнения quiz_me пришло поле milestone_reached равное true, ты должен немедленно вызвать mark_studied для указанной темы, не спрашивая ученика.\n" +
        "- Когда пришёл результат quiz_me: если ответ верный — кратко похвали и предложи отметить тему изученной (mark_studied) или добавить смежную; если неверный — мягко разбери ошибку одним предложением и предложи оставить тему на повтор.\n" +
        "- Никогда не выдумывай результат функции — дождись, что вернёт программа.\n" +
        "- Если функции не нужны — просто ответь словами. Отвечай расширенно, не выдумывая, несколько раз перепроверяя информацию, доброжелательно и на русском.")
            };
        }

        
        // конструктор для консоли, инициализирующий два параметра
        public CSharpTutor(ILanguageModel llm, KnowledgeBase kb) : this(llm, kb, null) 
        { }


        public CSharpTutor(ILanguageModel llm, KnowledgeBase kb, IHttpContextAccessor? httpContextAccessor)
        {
            _llm = llm;
            _kb = kb;
            _httpContextAccessor = httpContextAccessor;
            
            _plan = new StudyPlan();          // StudyPlan теперь без write делегата (может логировать через Serilog)
            _quiz = new QuizEngine(llm);
            _executor = new FunctionExecutor(_plan);
        }

    
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

               

        public async Task<(string Answer, List<Scored> Sources, QuizQuestion? Quiz)> AskAsync(string question, Action<string>? onSearch = null)
        {
            var history = History;
            history.Add(new ChatMessage("user", question));
            Log.Information("Вопрос пользователя: {Question}", question);

            var reply = await _llm.ChatWithFunctionsAsync(history, Functions);

            if (reply.FunctionCall is { Name: "search_documents" })
            {
                // Модель решила искать в документах
                history.Add(reply with { Content = reply.Content ?? "" });
                string query = GetStr(reply.FunctionCall.Arguments, "query") ?? question;
                onSearch?.Invoke(query);

                // Поиск по смыслу
                List<Scored> top = await _kb.SearchAsync(query, _llm, topK: 3);

                // Возвращаем результат как ответ функции
                string result = JsonSerializer.Serialize(new
                {
                    results = top.Select(s => new { source = s.Chunk.Source, text = s.Chunk.Text })
                }, JsonOpts);
                history.Add(new ChatMessage("function", result, Name: "search_documents"));

                // Финальный ответ БЕЗ функций
                string answer = await _llm.ChatAsync(history);
                history.Add(new ChatMessage("assistant", answer));
                History = history;  // сохраняем
                return (answer, top, null);
            }
            else if (reply.FunctionCall is { Name: "quiz_me" })
            {
                PendingFunctionCall = reply; // Сохраняем ответ с function_call в сессию

                // Генерируем вопрос
                string topic = GetStr(reply.FunctionCall.Arguments, "topic") ?? "C#";
                string difficulty = GetStr(reply.FunctionCall.Arguments, "difficulty") ?? "medium";
                var quiz = await _quiz.GenerateAsync(topic, difficulty);

                // Формируем читаемый текст вопроса для пользователя
                string questionText = $"❓ {quiz.Question}\n\nВарианты:\n" +
                    string.Join("\n", quiz.Options.Select((o, i) => $"{i + 1}. {o}")) +
                    "\n\nВведите номер ответа.";

                History = history; // сохраняем историю без лишнего

                // Возвращаем сам объект QuizQuestion, чтобы вызывающий код мог его сохранить
                return (questionText, new List<Scored>(), quiz);
            }
            else if (reply.FunctionCall is not null)
            {
                // Другие функции (add_topic, mark_studied, list_topics) без изменений
                history.Add(reply with { Content = reply.Content ?? "" });
                string functionResult = await _executor.ExecuteAsync(reply.FunctionCall);
                history.Add(new ChatMessage("function", functionResult, Name: reply.FunctionCall.Name));
                string answer = await _llm.ChatAsync(history);
                history.Add(new ChatMessage("assistant", answer));
                History = history;
                return (answer, new List<Scored>(), null);
            }
            else
            {
                // Обычный ответ без функций
                string answer = reply.Content ?? "";
                history.Add(new ChatMessage("assistant", answer));
                History = history;
                return (answer, new List<Scored>(), null);
            }
        }

        public async Task<(string Answer, List<Scored> Sources)> SubmitQuizAnswerAsync(string originalQuestion, string userAnswer, QuizQuestion quizQuestion)
        {
            var pending = PendingFunctionCall;
            if (pending == null)
                throw new InvalidOperationException("Нет ожидающего вызова функции для теста.");
                        
            var history = History;
            history.Add(pending); // добавляем ассистента с function_call

            bool parsed = int.TryParse(userAnswer, out int num);

            bool correct = parsed && num >= 1 && num <= quizQuestion.Options.Length && num - 1 == quizQuestion.CorrectIndex;

            bool milestoneReached = false;
            string canonicalTopic = originalQuestion;

            if (correct)
            {
                var matched = _plan.FindByTitle(originalQuestion);
                if (matched != null)
                {
                    canonicalTopic = matched.Title;
                    _plan.IncrementCorrect(matched.Title, out milestoneReached);
                }
            }

            // 3. Формируем сообщение для пользователя
            string resultMessage = correct
                ? $"✅ Верно! {quizQuestion.Explanation}"
                : $"❌ Неверно. Правильный ответ: {quizQuestion.Options[quizQuestion.CorrectIndex]}. {quizQuestion.Explanation}";

            if (milestoneReached)
                resultMessage += $" 🎉 Вы правильно ответили на 5 тестов по теме «{canonicalTopic}». Тему можно отметить как изученную.";

            // 4. Добавляем результат функции в историю (для контекста)
            string functionResult = JsonSerializer.Serialize(new
            {
                topic = canonicalTopic,
                question = quizQuestion.Question,
                userAnswer = parsed ? num : (int?)null, correct,
                correctOption = quizQuestion.Options[quizQuestion.CorrectIndex],
                explanation = quizQuestion.Explanation,
                milestone_reached = milestoneReached,
                message = milestoneReached
                    ? $"🎉 Вы правильно ответили на 5 тестов по теме «{canonicalTopic}». Тему можно отметить как изученную."
                    : null
            });

            history.Add(new ChatMessage("function", functionResult, Name: "quiz_me"));

            // 5. Добавляем итоговый ответ в историю как сообщение ассистента
            string finalAnswer = await _llm.ChatAsync(history);
            history.Add(new ChatMessage("assistant", finalAnswer));

            PendingFunctionCall = null; // очищаем
            History = history; // сохраняем

            return (finalAnswer, new List<Scored>());
        }

        private static string? GetStr(JsonElement obj, string field) =>
            obj.ValueKind == JsonValueKind.Object
            && obj.TryGetProperty(field, out var v)
            && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }
}

