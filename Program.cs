using Serilog;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.SystemConsole.Themes;
using ModelAIIntegrationCore.GigaChat.Models;
using ModelAIIntegrationCore.Knowledge.Models;
using ModelAIIntegrationCore.Knowledge;
using ModelAIIntegrationCore.Tutor;
using ModelAIIntegrationCore.Tutor.Models;
using ModelAIIntegrationCore.Ollama;
using ModelAIIntegrationCore.Gemini;
using ModelAIIntegrationCore;
using ModelAIIntegrationCore.GigaChat;


namespace ModelAIIntegration
{
    internal class Program
    {
        private const string DocsFolder = "docs";
                
        static async Task Main(string[] args)
        {
            // Логгер 
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    theme: SystemConsoleTheme.Literate)
                .CreateLogger();
            Log.Information("Приложение GigaChat запущено");

            var configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())   // указываем папку, где лежит appsettings.json
              .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
              .Build();

            
            string chatUrl = configuration["GigaChat:ChatUrl"]!;
            string authUrl = configuration["GigaChat:AuthUrl"]!;
            string embUrl = configuration["GigaChat:EmbeddingsUrl"]!;
            string authKey = configuration["GigaChat:AuthKey"];  // Базовый ключ авторизации для API GigaChat
            string apiKey = configuration["Gemini:ApiKey"]; // Базовый ключ авторизации API для Gemini


            Console.WriteLine("Подключаюсь к ИИ..."); // собираем зависимости
            Console.Write("Выберите модель (1 – GigaChat, 2 – Ollama, 3 – Gemini): ");
            var choice = Console.ReadLine();

            ILanguageModel llm;

            if (choice == "3")
                llm = new GeminiClient(apiKey);
            else if(choice == "2")
                llm = new OllamaClient("qwen2.5:1.5b", "nomic-embed-text");
            else
                llm = await GigaChatClient.CreateAsync(authKey, chatUrl, authUrl, embUrl);


            // Индекс уже посчитан? Загружаем с диска (мгновенно). Нет, тогда строим из документов
            // и сохраняем, чтобы при следующем запуске не платить за эмбеддинги снова.
            // Определяем имя файла индекса в зависимости от выбранной модели
            string indexFile = choice switch
            {
                "2" => "index.Ollama.json",
                "3" => "index.Gemini.json",
                _ => "index.GigaChat.json"
            };
                
            Console.WriteLine($"Использую индекс: {indexFile}");

            var kb = new KnowledgeBase();

            if (File.Exists(indexFile))
            {
                kb.Load(indexFile);
                Console.WriteLine($"Загрузил индекс с диска: {kb.Count} кусков.");
                Console.WriteLine($"(изменил документы? удали {indexFile} — пересоберётся заново)\n");
            }
            else
            {
                Console.WriteLine($"Индексирую документы из папки {DocsFolder}/ ...");
                await kb.BuildFromFolderAsync(DocsFolder, llm);
                kb.Save(indexFile);
                Console.WriteLine($"Готово: {kb.Count} кусков, индекс сохранён в {indexFile}.\n");
            }

            var tutor = new CSharpTutor(llm, kb);

            Console.WriteLine("=== ИИ-наставник по C# ===");
            Console.WriteLine("Спроси что угодно по C#. Примеры:");
            Console.WriteLine("  • «хочу разобраться с делегатами»   (добавит в план)");
            Console.WriteLine("  • «что у меня в плане?»             (покажет план)");
            Console.WriteLine("  • «проверь меня по struct и class»  (устроит тест)");
            Console.WriteLine("  • «чем массив отличается от словаря?» (ответит по лекциям)");
            Console.WriteLine("'выход' — закончить.\n");

            while (true)
            {
                Console.Write("Сообщение: ");
                var input = Console.ReadLine();
                if (string.Equals(input, "выход", StringComparison.OrdinalIgnoreCase)) break;
                if (string.IsNullOrWhiteSpace(input)) continue;

                var (answer, sources, quiz) = await tutor.AskAsync(input);

                if (quiz != null)
                {
                    // Это вопрос теста – выводим его и ждём ответ
                    Console.WriteLine(answer);
                    Console.Write("Ваш ответ (номер): ");
                    var userAnswer = Console.ReadLine();
                    (answer, sources) = await tutor.SubmitQuizAnswerAsync(input, userAnswer, quiz);
                }

                Console.WriteLine($"\nНаставник: {answer}\n");
                if (sources.Any())
                    Console.WriteLine("Источники: " + string.Join(", ", sources.Select(s => $"{s.Chunk.Source} ({s.Score:0.00})")) + "\n");
            }

        }
                
    }
}
