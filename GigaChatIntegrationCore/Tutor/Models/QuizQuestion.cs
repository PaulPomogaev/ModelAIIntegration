using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Tutor.Models
{
    public record QuizQuestion(string Question, string[] Options, int CorrectIndex, string Explanation); // Тест-вопрос, который достаём структурированным выводом в GenerateQuiz (движок инструмента quiz_me).
}
