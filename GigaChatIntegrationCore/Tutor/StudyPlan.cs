using ModelAIIntegrationCore.Tutor.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Tutor
{
    internal class StudyPlan
    {
        private readonly List<StudyTopic> _plan = new();
        public IReadOnlyList<StudyTopic> Topics => _plan;

       
        public StudyPlan()
        {
            
        }

        public void Add(string title, string priority = "средний", string? note = null)
        {
            _plan.Add(new StudyTopic(title, priority, note));
            Log.Information("Добавил в план: {Title}", title);  // логируем вместо Console.WriteLine();
        }

        public string ListAsJson()
        {
            Log.Information("Показываю план");
            return JsonSerializer.Serialize(new
            {
                topics = _plan,
                total = _plan.Count,
                studied = _plan.Count(t => t.Studied)
            });
        }

        public bool MarkStudied(string title)
        {
            int idx = _plan.FindIndex(t =>
                t.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _plan[idx] = _plan[idx] with { Studied = true, CorrectAnswers = 0 };
                Log.Information("Отметил изученным");
                return true;
            }
            return false;
        }

        public StudyTopic? FindByTitle(string title) =>
            _plan.FirstOrDefault(t => t.Title.Contains(title, StringComparison.OrdinalIgnoreCase));

        public void IncrementCorrect(string exactTitle, out bool milestoneReached)
        {
            int idx = _plan.FindIndex(t => t.Title == exactTitle);
            if (idx < 0) { milestoneReached = false; return; }

            int newCount = _plan[idx].CorrectAnswers + 1;
            _plan[idx] = _plan[idx] with { CorrectAnswers = newCount };
            milestoneReached = newCount >= 5 && !_plan[idx].Studied;
        }
    }
}
