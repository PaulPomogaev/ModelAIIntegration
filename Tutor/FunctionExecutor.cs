using ConsoleAppAPI_II_GigaChat.GigaChat;
using ConsoleAppAPI_II_GigaChat.GigaChat.Models;
using ConsoleAppAPI_II_GigaChat.Knowledge;
using ConsoleAppAPI_II_GigaChat.Tutor.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConsoleAppAPI_II_GigaChat.Tutor
{
    internal class FunctionExecutor
    {
        private readonly StudyPlan _plan;
        private readonly QuizEngine _quiz;
        private readonly Action<string> _writeLine;   // вывод
        private readonly Func<string> _readLine;      // ввод (только для тестов)
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public FunctionExecutor(StudyPlan plan, QuizEngine quiz, Action<string> writeLine, Func<string> readLine)
        {
            _plan = plan;
            _quiz = quiz;
            _writeLine = writeLine;
            _readLine = readLine;
        }

        public string Execute(FunctionCall call)
        {
            return call.Name switch
            {
                "add_topic" => ExecuteAddTopic(call.Arguments),
                "list_topics" => _plan.ListAsJson(),
                "mark_studied" => ExecuteMarkStudied(call.Arguments),
                "quiz_me" => ExecuteQuizMe(call.Arguments),
                _ => JsonSerializer.Serialize(new { error = $"Неизвестная функция: {call.Name}" }, JsonOpts)
            };
        }

        private string ExecuteAddTopic(JsonElement args)
        {
            string title = GetStr(args, "title") ?? "(без названия)";
            string priority = GetStr(args, "priority") ?? "средний";
            string? note = GetStr(args, "note");
            _plan.Add(title, priority, note);
            return JsonSerializer.Serialize(
                new { status = "ok", added = title, total = _plan.Topics.Count }, JsonOpts);
        }

        private string ExecuteMarkStudied(JsonElement args)
        {
            string title = GetStr(args, "title") ?? "";
            bool ok = _plan.MarkStudied(title);
            return JsonSerializer.Serialize(
                new { status = ok ? "ok" : "not_found", title, studied = ok }, JsonOpts);
        }

        private string ExecuteQuizMe(JsonElement args)
        {
            string topic = GetStr(args, "topic") ?? "C#";
            string difficulty = GetStr(args, "difficulty") ?? "medium";
            difficulty = difficulty switch { "easy" => "easy", "hard" => "hard", _ => "medium" };

            _writeLine($"\n[запускаю тест по теме: {topic}, сложность: {difficulty}]");

            QuizQuestion quiz;
            try
            {
                quiz = _quiz.Generate(topic, difficulty);
            }
            catch (Exception ex)
            {
                _writeLine($"  [не удалось собрать тест: {ex.Message}]\n");
                return JsonSerializer.Serialize(
                    new { topic, error = "Не удалось сгенерировать тест. Предложи попробовать ещё раз." }, JsonOpts);
            }

            if (quiz.Options is not { Length: > 0 })
            {
                _writeLine("  [тест пришёл без вариантов ответа]\n");
                return JsonSerializer.Serialize(
                    new { topic, error = "Тест без вариантов. Предложи попробовать ещё раз." }, JsonOpts);
            }

            _writeLine("  [вопрос сгенерирован вживую — структурированный вывод по схеме]");
            _writeLine($"❓ {quiz.Question}");
            for (int i = 0; i < quiz.Options.Length; i++)
                _writeLine($"    {i + 1}. {quiz.Options[i]}");

            _writeLine($"Твой ответ (1-{quiz.Options.Length}): ");
            bool parsed = int.TryParse(_readLine(), out int num);
            bool correct = parsed && num >= 1 && num <= quiz.Options.Length && num - 1 == quiz.CorrectIndex;

            bool milestoneReached = false;
            string canonicalTopic = topic;

            if (correct)
            {
                var matched = _plan.FindByTitle(topic);
                if (matched != null)
                {
                    canonicalTopic = matched.Title;
                    _plan.IncrementCorrect(matched.Title, out milestoneReached);
                }
            }

            var result = new
            {
                topic = canonicalTopic,
                question = quiz.Question,
                userAnswer = parsed ? num : (int?)null,
                correct,
                correctOption = quiz.Options[quiz.CorrectIndex],
                explanation = quiz.Explanation,
                milestone_reached = milestoneReached,
                message = milestoneReached
                    ? $"🎉 Вы правильно ответили на 5 тестов по теме «{canonicalTopic}». Тему можно отметить как изученную."
                    : null
            };

            _writeLine(correct ? "✅ Верно!" : "❌ Неверно.");
            _writeLine($"   Разбор: {quiz.Explanation}\n");

            return JsonSerializer.Serialize(result, JsonOpts);
        }

        
        private static string? GetStr(JsonElement obj, string field) =>
            obj.ValueKind == JsonValueKind.Object
            && obj.TryGetProperty(field, out var v)
            && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }
}
