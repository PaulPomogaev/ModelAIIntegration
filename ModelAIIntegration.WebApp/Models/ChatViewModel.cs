using System.Collections.Specialized;

namespace ModelAIIntegrationWebApp.Models
{
    public class ChatViewModel
    {
        public string? Question { get; set; }
        public string? Answer { get; set; }
        public List<string> Sources { get; set; } = new();

        // Для тестов
        public bool IsQuizQuestion { get; set; }
        public string? QuizQuestionJson { get; set; }
        public string? OriginalQuestion { get; set; }

        // Выбор провайдера
        public string SelectedProvider { get; set; } = "GigaChat";
        public string? SelectedOllamaModel { get; set; }
        public List<string> OllamaModels { get; set; } = new();

        // Для отображения, какая модель ИИ ответила
        public string? ProviderName { get; set; }
        public string? ProviderModel { get; set; }

        // Ошибки
        public string? Error { get; set; }

        // Вычисляемые свойства
        public bool HasAnswer => !string.IsNullOrEmpty(Answer);
        public bool HasError => !string.IsNullOrEmpty(Error);

        // ЛЕНТА ДИАЛОГА. Каждый ход: вопрос, ответ, кто ответил,
        // источники. Раньше показывали только последний ответ — теперь виден весь разговор,
        // и по нему заметно, что помощник ПОМНИТ контекст (можно спросить «а подробнее?»).
        public List<ChatTurn> Turns { get; set; } = new();
        public bool HasHistory => Turns.Count > 0; 
    }
}
