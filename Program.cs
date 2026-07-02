using Serilog;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.SystemConsole.Themes;
using GigaChatIntegrationCore.GigaChat.Models;
using GigaChatIntegrationCore.GigaChat;
using GigaChatIntegrationCore.Knowledge.Models;
using GigaChatIntegrationCore.Knowledge;
using GigaChatIntegrationCore.Tutor;
using GigaChatIntegrationCore.Tutor.Models;


namespace GigaChatIntegration
{
    internal class Program
    {
        private const string DocsFolder = "docs";
        private const string IndexFile = "index.json";

        static void Main(string[] args)
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
            string authKey = configuration["GigaChat:AuthKey"];  // Базовый ключ авторизации для API          


            Console.WriteLine("Подключаюсь к GigaChat..."); // собираем зависимости
            var gc = new GigaChatClient(authKey, chatUrl, authUrl, embUrl);
            var kb = new KnowledgeBase();

            // Индекс уже посчитан? Загружаем с диска (мгновенно). Нет — строим из документов
            // и сохраняем, чтобы при следующем запуске не платить за эмбеддинги снова.
            if (File.Exists(IndexFile))
            {
                kb.Load(IndexFile);
                Console.WriteLine($"Загрузил индекс с диска: {kb.Count} кусков.");
                Console.WriteLine($"(изменил документы? удали {IndexFile} — пересоберётся заново)\n");
            }
            else
            {
                Console.WriteLine($"Индексирую документы из папки {DocsFolder}/ ...");
                kb.BuildFromFolder(DocsFolder, gc);
                kb.Save(IndexFile);
                Console.WriteLine($"Готово: {kb.Count} кусков, индекс сохранён в {IndexFile}.\n");
            }

            var tutor = new CSharpTutor(gc, kb);

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

                try
                {
                    tutor.HandleUserInput(input);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка в цикле диалога");
                    Console.WriteLine("Произошла ошибка. Проверьте логи.");
                }
            }

        }
                
    }
}
