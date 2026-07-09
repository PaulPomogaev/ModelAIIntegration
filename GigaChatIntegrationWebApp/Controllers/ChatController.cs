using Microsoft.AspNetCore.Mvc;
using ModelAIIntegrationCore.Tutor;
using ModelAIIntegrationCore.Knowledge.Models;
using ModelAIIntegrationCore.Tutor.Models;
using System.Text.Json;
using ModelAIIntegrationCore.Knowledge;
using ModelAIIntegrationCore.Ollama;
using ModelAIIntegrationWebApp.Models;
using ModelAIIntegrationWebApp;
using ModelAIIntegrationCore;

namespace GigaChatIntegrationWebApp.Controllers
{
    public class ChatController : Controller
    {
        private readonly LanguageModelRegistry _registry;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
        private const string TurnsKey = "chat_turns"; // константа для ленты истории

        public ChatController(LanguageModelRegistry registry, IHttpContextAccessor httpContextAccessor)
        {
            _registry = registry;
            _httpContextAccessor = httpContextAccessor;
        }


        // ─── Загрузка начальной страницы ──────────────────────────────────────
        // При первом входе мы пытаемся определить, запущена ли локальная Ollama.
        // Если да, то по-умолчанию выбираем её, если нет, то GigaChat.
        public async Task<IActionResult> Index()
        {
            var modelChoice = await BuildSelectionAsync("Ollama", ollamaModel: null);
            modelChoice.Turns = LoadTurns(); // загружаем ленту истории сообщений
            return View(modelChoice);
        }


        // ─── Обработка вопроса пользователя ──────────────────────────────────
        // Принимает текст вопроса, выбранного провайдера и модель Ollama (если выбрана).
        // Создаёт CSharpTutor для этой модели и получает ответ (или тест).

        [HttpPost]
        public async Task<IActionResult> Ask(string question, string provider, string? ollamaModel)
        {
            // 1. Восстанавливаем состояние выбора (список моделей Ollama, что выбрано)
            var modelChoice = await BuildSelectionAsync(provider, ollamaModel);
            modelChoice.Question = question;

            if (string.IsNullOrWhiteSpace(question))
                return View("Index", new ChatViewModel());

            try
            {
                // 2. Получаем реализацию ILanguageModel и KnowledgeBase для выбранного провайдера
                (ILanguageModel llm, KnowledgeBase kb) = await _registry.GetAsync(provider, ollamaModel);

                // 3. Создаём экземпляр CSharpTutor. История сохраняется в сессии благодаря IHttpContextAccessor
                var tutor = new CSharpTutor(llm, kb, _httpContextAccessor);

                // 4. Задаём вопрос
                (string answer, List<Scored> sources, QuizQuestion? quiz) = await tutor.AskAsync(question);

                // 5. Заполняем модель представления
                modelChoice.Answer = answer;
                modelChoice.Sources = sources.Select(s => $"{s.Chunk.Source} ({s.Score:0.00})").ToList();


                // 6. Загрузка истории
                var newTurn = new ChatTurn
                {
                    Question = question,
                    Answer = answer,
                    ProviderName = modelChoice.SelectedProvider,
                    ProviderModel = modelChoice.SelectedProvider switch
                    {
                        "Ollama" => modelChoice.SelectedOllamaModel,
                        "Gemini" => "gemini-2.5-flash", // или можно прочитать из реестра
                        _ => "GigaChat",
                    },
                    Sources = sources.Select(s => $"{s.Chunk.Source} ({s.Score:0.00})").ToList()
                };
                modelChoice.Turns = LoadTurns();   // загружаем предыдущие
                modelChoice.Turns.Add(newTurn);
                SaveTurns(modelChoice.Turns);
                modelChoice.Question = null;       // очищаем поле вопроса для следующего ввода

                
                // 7. Если это тест — сохраняем дополнительную информацию
                if (quiz != null)
                {
                    modelChoice.IsQuizQuestion = true;
                    modelChoice.QuizQuestionJson = JsonSerializer.Serialize(quiz, JsonOpts);
                    modelChoice.OriginalQuestion = question;
                }
            }
            catch (Exception ex)
            {
                // Любая ошибка (нет сети, неверный ключ, Ollama не отвечает) — показываем в интерфейсе
                modelChoice.Error = ex.Message;
            }

            return View("Index", modelChoice);
        }

        // ─── Обработка ответа на тест ────────────────────────────────────────
        // Сюда приходит ответ пользователя, когда активен тест.
        [HttpPost]
        public async Task<IActionResult> AnswerQuiz(string originalQuestion, string userAnswer,
            string quizQuestionJson, string provider, string? ollamaModel)
        {
            // 1. Восстанавливаем состояние выбора
            var modelChoice = await BuildSelectionAsync(provider, ollamaModel);
            modelChoice.Question = originalQuestion;

            if (string.IsNullOrWhiteSpace(userAnswer) || string.IsNullOrWhiteSpace(quizQuestionJson))
                return RedirectToAction("Index");

            var quiz = JsonSerializer.Deserialize<QuizQuestion>(quizQuestionJson, JsonOpts);
            if (quiz == null)
                return RedirectToAction("Index");

            try
            {
                // 2. Получаем модель и базу знаний
                (ILanguageModel llm, KnowledgeBase kb) = await _registry.GetAsync(provider, ollamaModel);

                // 3. Создаём тьютора и передаём ответ
                var tutor = new CSharpTutor(llm, kb, _httpContextAccessor);
                var (answer, sources) = await tutor.SubmitQuizAnswerAsync(originalQuestion, userAnswer, quiz);

                var newTurn = new ChatTurn
                {
                    Question = "Тест: " + originalQuestion,
                    Answer = answer,
                    ProviderName = modelChoice.SelectedProvider,
                    ProviderModel = modelChoice.SelectedProvider switch
                    {
                        "Ollama" => modelChoice.SelectedOllamaModel,
                        "Gemini" => "gemini-2.5-flash",
                        _ => "GigaChat",
                    },
                    Sources = sources.Select(s => $"{s.Chunk.Source} ({s.Score:0.00})").ToList()
                };
                modelChoice.Turns = LoadTurns();
                modelChoice.Turns.Add(newTurn);
                SaveTurns(modelChoice.Turns);
                                
            }
            catch (Exception ex)
            {
                modelChoice.Error = ex.Message;
            }

            return View("Index", modelChoice);
        }

        // очистка истории сообщений
        [HttpPost]
        public IActionResult Reset()  
        {
            HttpContext.Session.Remove("TutorHistory");
            HttpContext.Session.Remove("TutorPending");
            HttpContext.Session.Remove(TurnsKey);
            return RedirectToAction("Index");
        }

        // ─── Вспомогательный метод: сборка состояния выбора нейросети ─────────
        // Опрашивает локальную Ollama о доступных моделях и подготавливает ChatViewModel
        // с предустановленными значениями SelectedProvider, SelectedOllamaModel и списком моделей.
        private async Task<ChatViewModel> BuildSelectionAsync(string provider, string? ollamaModel)
        {
            // Пытаемся получить список моделей Ollama (если сервер запущен)
            string baseUrl = "http://localhost:11434";
            List<string> models = await OllamaClient.ListModels(baseUrl);

            models = models.Where(m => !m.Contains("embed")).ToList();

            // Если модель не указана явно — берём первую из доступных или пустую строку
            string selectedModel = !string.IsNullOrWhiteSpace(ollamaModel)
                ? ollamaModel
                : models.FirstOrDefault() ?? "";

            string selectedProvider =
              string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase) ? "Ollama" :
              string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase) ? "Gemini" :
              "GigaChat";

            return new ChatViewModel
            {
                SelectedProvider = selectedProvider,
                SelectedOllamaModel = selectedModel,
                OllamaModels = models,
            };
        }

        private List<ChatTurn> LoadTurns() =>
        HttpContext.Session.GetString(TurnsKey) is string json
        ? JsonSerializer.Deserialize<List<ChatTurn>>(json) ?? new()
        : new();

        private void SaveTurns(List<ChatTurn> turns) =>
            HttpContext.Session.SetString(TurnsKey, JsonSerializer.Serialize(turns));
    }
}
    

