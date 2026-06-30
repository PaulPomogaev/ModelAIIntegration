using Serilog;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.SystemConsole.Themes;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsoleAppAPI_II_GigaChat
{
    internal class Program
    {
        static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web); // Настройки сериализации JSON с поведением по умолчанию для веб-API

        private static readonly HttpClient _httpClient = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        }); // Единственный экземпляр HttpClient на всё приложение
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug() // чтобы видеть все DBG
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    theme: SystemConsoleTheme.Literate // цветной вывод
                )
                .CreateLogger();

            Log.Information("Приложение GigaChat запущено");

            var configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())   // указываем папку, где лежит appsettings.json
              .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
              .Build();

            string authKey = configuration["GigaChat:AuthKey"];  // Базовый ключ авторизации для API
            var chatUrl = configuration["GigaChat:ChatUrl"];
            var authUrl = configuration["GigaChat:AuthUrl"];

            var accessToken = GetAccessToken(authKey, authUrl); // Получаем временный токен доступа (Bearer) по ключу

            Console.WriteLine("Готово!\n");
            Console.WriteLine("=== ИИ-наставник по C#: ведёт план изучения и сам проверяет тестами ===");
            Console.WriteLine("Примеры:"); // ниже просто примеры для пользователя
            Console.WriteLine("  • «хочу разобраться с делегатами, это важно»  (добавит в план)");
            Console.WriteLine("  • «что у меня в плане?»                        (покажет план)");
            Console.WriteLine("  • «проверь меня по разнице struct и class»     (устроит мини-тест)");
            Console.WriteLine("  • «я разобрался с делегатами»                  (отметит изученным)");
            Console.WriteLine("'выход' — закончить.\n");

            // Системный промпт — «характер и правила» наставника (День 2: системные промпты).
            var history = new List<ChatMessage>
            {
                new("system",
                    "Ты — преподаватель-наставник со стажем 40 лет по обучению C# для начинающего разработчика. " +
                    "Ты ведёшь его личный план изучения тем и помогаешь проверять знания. " +
                    "У тебя есть инструменты (функции), которыми ты управляешь сам:\n" +
                    "• add_topic — когда ученик хочет что-то изучить, просит добавить тему, " +
                    "или ты сам по ходу разговора считаешь тему важной.\n" +
                    "• list_topics — когда ученик спрашивает, что у него в плане, что осталось или с чего начать.\n" +
                    "• mark_studied — когда ученик говорит, что разобрался с темой, прошёл её или выучил.\n" +
                    "• quiz_me — когда ученик просит проверить знания («проверь меня», «дай тест», " +
                    "«я готов по теме X») ИЛИ когда он уверяет, что разобрался, и это стоит подтвердить мини-тестом.\n" +
                    "Правила:\n" +
                    "- Вызывай функцию ТОЛЬКО когда она действительно нужна. За один ответ — не больше одного вызова функции.\n" +
                    "- Не придумывай сам текст тест-вопроса и варианты — их ВСЕГДА формирует функция quiz_me. " +
                    "Ты лишь объявляешь о начале проверки и комментируешь её результат.\n" +
                    "- Когда пришёл результат quiz_me: если ответ верный — кратко похвали и предложи отметить тему " +
                    "изученной (mark_studied) или добавить смежную; если неверный — мягко разбери ошибку одним " +
                    "предложением и предложи оставить тему на повтор.\n" +
                    "- Никогда не выдумывай результат функции — дождись, что вернёт программа.\n" +
                    "- Если функции не нужны — просто ответь словами. Отвечай расширенно, не выдумывая, несколько раз перепроверяя информацию, доброжелательно и на русском."),
            };
            // Начальное сообщение задаёт роль и стиль ответа ИИ
            Log.Debug("Системный промпт установлен, длина истории: {Count}", history.Count);

            // Функции, которые мы разрешаем модели вызывать (имя + описание + схема аргументов). Всё из документации GigaChat.
            var functions = new List<FunctionDef>
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
                    "Проводит мини-тест (1 вопрос с 4 вариантами) по заданной теме C# и проверяет ответ ученика.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            topic = new { type = "string", description = "Тема теста, напр. «разница между struct и class»" },
                        },
                        required = new[] { "topic" },
                    }),
            };

            while (true) // Бесконечный цикл диалога
            {
                Console.Write("Сообщение пользователя: ");
                var userInput = Console.ReadLine(); // ввод ообщения пользователя

                if (string.Equals(userInput, "выход", StringComparison.OrdinalIgnoreCase)) break; // Выход из цикла общения с ИИ по команде
                if (string.Equals(userInput, "очистить историю", StringComparison.OrdinalIgnoreCase))
                {
                    history.Clear();
                    // Заново добавим системное сообщение, чтобы не потерять роль
                    history.Add(new ChatMessage("system",
                    "Ты — преподаватель-наставник со стажем 40 лет по обучению C# для начинающего разработчика. " +
                    "Ты ведёшь его личный план изучения тем и помогаешь проверять знания. " +
                    "У тебя есть инструменты (функции), которыми ты управляешь сам:\n" +
                    "• add_topic — когда ученик хочет что-то изучить, просит добавить тему, " +
                    "или ты сам по ходу разговора считаешь тему важной.\n" +
                    "• list_topics — когда ученик спрашивает, что у него в плане, что осталось или с чего начать.\n" +
                    "• mark_studied — когда ученик говорит, что разобрался с темой, прошёл её или выучил.\n" +
                    "• quiz_me — когда ученик просит проверить знания («проверь меня», «дай тест», " +
                    "«я готов по теме X») ИЛИ когда он уверяет, что разобрался, и это стоит подтвердить мини-тестом.\n" +
                    "Правила:\n" +
                    "- Вызывай функцию ТОЛЬКО когда она действительно нужна. За один ответ — не больше одного вызова функции.\n" +
                    "- Не придумывай сам текст тест-вопроса и варианты — их ВСЕГДА формирует функция quiz_me. " +
                    "Ты лишь объявляешь о начале проверки и комментируешь её результат.\n" +
                    "- Когда пришёл результат quiz_me: если ответ верный — кратко похвали и предложи отметить тему " +
                    "изученной (mark_studied) или добавить смежную; если неверный — мягко разбери ошибку одним " +
                    "предложением и предложи оставить тему на повтор.\n" +
                    "- Никогда не выдумывай результат функции — дождись, что вернёт программа.\n" +
                    "- Если функции не нужны — просто ответь словами. Отвечай расширенно, не выдумывая, несколько раз перепроверяя информацию, доброжелательно и на русском.")
                    );
                    continue;
                }

                if (string.Equals(userInput, "помощь", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Доступные команды:");
                    Console.WriteLine("  выход - завершить программу");
                    Console.WriteLine("  очистить историю - очистить историю диалога");
                    Console.WriteLine("  помощь  - показать эту справку");
                    continue;
                }

                history.Add(new ChatMessage("user", userInput)); // Добавление сообщения пользователя в историю всех сообщений для понимания чатом контекста
                Log.Information("Вопрос пользователя: {Question}", userInput);

                try
                {
                    var answer = AskGigaChat(history, accessToken, chatUrl); // Отправляем всю историю в GigaChat и получаем ответ

                    history.Add(new ChatMessage("assistant", answer)); // Сохраняем ответ ассистента в историю
                    Console.WriteLine($"GigaChat: {answer}"); // Выводим ответ пользователю
                }
                catch(Exception ex)
                {
                    Log.Error(ex, "Ошибка при получении ответа");
                    Console.WriteLine("Произошла ошибка. Проверьте логи.");
                }
                
            }
        }

        static string AskGigaChat(List<ChatMessage> history, string accessToken, string chatUrl) // Метод отправки запроса к чат-модели GigaChat
        {
            Log.Debug("Отправка запроса к GigaChat. История содержит {Count} сообщений", history.Count);

            try
            {
                string json = JsonSerializer.Serialize(new ChatRequest("GigaChat", history), JsonOpts); // Сериализуем объект запроса в JSON-строку

                
                using var request = new HttpRequestMessage(HttpMethod.Post, chatUrl) // Создаём POST-запрос с JSON-телом
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"), // Cодержанием запроса будет тот json файл, который мы сериализовали
                };


                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken); // Добавляем токен авторизации в заголовок Authorization

                using var response = _httpClient.Send(request); // Отправляем запрос и получаем ответ
                Log.Debug("Получен ответ от GigaChat. StatusCode: {StatusCode}", response.StatusCode);

                response.EnsureSuccessStatusCode(); // Проверяем, что статус ответа успешный (200)

                var result = JsonSerializer.Deserialize<ChatResponse>(ReadBody(response), JsonOpts); // Десериализуем тело ответа в объект ChatResponse
                Log.Debug("Ответ успешно десериализован");

                return result!.Choices[0].Message.Content; // Возвращаем содержимое первого сообщения от ассистента
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Ошибка при обращении к GigaChat");
                throw;
            }
            
        }

        static string GetAccessToken(string authKey, string authUrl) // Метод получения Bearer-токена по базовому ключу
        {
            Log.Debug("Начинаем получение токена доступа");
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, authUrl); // Создаём POST-запрос к эндпоинту авторизации

                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authKey);  // Устанавливаем заголовок Authorization: Basic <ключ>

                request.Headers.Add("RqUID", Guid.NewGuid().ToString());  // Добавляем уникальный идентификатор запроса (RqUID)

                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["scope"] = "GIGACHAT_API_PERS",  // Тело запроса содержит параметр scope со значением для физических лиц
                });

                
                using var response = _httpClient.Send(request); // Отправляем запрос
                Log.Debug("Ответ от сервера авторизации. StatusCode: {StatusCode}", response.StatusCode);

                response.EnsureSuccessStatusCode();  // Проверяем успешность ответа

                var token = JsonSerializer.Deserialize<TokenResponce>(ReadBody(response), JsonOpts); // Десериализуем тело ответа в объект TokenResponse

                return token.AccessToken; // Возвращаем полученный access_token
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Ошибка при получении токена доступа");
                throw;
            }
            
        }

        static string ReadBody(HttpResponseMessage response)
        {
            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }  // Вспомогательный метод: читает тело ответа как строку

        record TokenResponce(
          [property: JsonPropertyName("access_token")] string AccessToken,
          [property: JsonPropertyName("expires_at")] long ExpiresAt);  // Класс для десериализации ответа токена

        record ChatMessage(string Role, string Content);  // Модель сообщения чата
        record ChatRequest(string Model, List<ChatMessage> Messages);  // Модель запроса к GigaChat
        record ChatResponse(List<Choice> Choices);  // Модель ответа от GigaChat
        record Choice(ChatMessage Message); // Один вариант ответа (выбор)      
        record FunctionDef(string Name, string Description, object Parameters); // Описание функции для модели: имя, что делает, и схема параметров (JSON Schema).
    }
}
