using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Tutor.Models
{
    public record StudyTopic(string Title, string Priority, string? Note, bool Studied = false, int CorrectAnswers = 0); // Тема в плане изучения. Studied ставит функция mark_studied; изученные темы — поиск по смыслу «повтори пройденное».
}
