using ModelAIIntegrationCore.GigaChat;
using ModelAIIntegrationCore.Knowledge;
using ModelAIIntegrationCore.Tutor.Models;
using ModelAIIntegrationCore.GigaChat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Tutor
{
    internal class FunctionExecutor
    {
        private readonly StudyPlan _plan;
                
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public FunctionExecutor(StudyPlan plan)
        {
            _plan = plan;   
        }

        public async Task<string> ExecuteAsync(FunctionCall call)
        {
            return call.Name switch
            {
                "add_topic" => ExecuteAddTopic(call.Arguments),
                "list_topics" => _plan.ListAsJson(),
                "mark_studied" => ExecuteMarkStudied(call.Arguments),
                _ => JsonSerializer.Serialize(new { error = $"Неизвестная функция: {call.Name}" }, JsonOpts)
            };
        }

        private string ExecuteAddTopic(JsonElement args)
        {
            string title = GetStr(args, "title") ?? "(без названия)";
            string priority = GetStr(args, "priority") ?? "средний";
            string? note = GetStr(args, "note");
            _plan.Add(title, priority, note);
            return JsonSerializer.Serialize(new { status = "ok", added = title, total = _plan.Topics.Count }, JsonOpts);
        }

        private string ExecuteMarkStudied(JsonElement args)
        {
            string title = GetStr(args, "title") ?? "";
            bool ok = _plan.MarkStudied(title);
            return JsonSerializer.Serialize(new { status = ok ? "ok" : "not_found", title, studied = ok }, JsonOpts);
        }

                
        private static string? GetStr(JsonElement obj, string field) =>
            obj.ValueKind == JsonValueKind.Object
            && obj.TryGetProperty(field, out var v)
            && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }
}
