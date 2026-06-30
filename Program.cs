using Serilog;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.SystemConsole.Themes;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Numerics;
using System;

namespace ConsoleAppAPI_II_GigaChat
{
    internal class Program
    {
        static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web); // Настройки сериализации JSON с поведением по умолчанию для веб-API

        private static readonly HttpClient _httpClient = new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        }); // Единственный экземпляр HttpClient на всё приложение

        private static readonly List<StudyTopic> plan = new();

        // ── ТЕСТОВАЯ БАЗА ЗНАНИЙ ──────────────────────────────────────────────────────────
        //  Документы представляют собой короткие заметки по C# для проверки процесса эмбендинга. 
        
        private static readonly KnowledgeDoc[] Knowledge =
        {
            new("Значимые и ссылочные типы",
                "struct — значимый (value) тип: при присваивании копируется целиком, у каждой переменной свой экземпляр. class — ссылочный: копируется только ссылка на один объект в куче. Поэтому правка копии struct не трогает оригинал, а копии class — трогает."),
            new("Списки и массивы",
                "List<T> — динамическая коллекция: хранит элементы по порядку, умеет Add, Remove, индексатор [i], Count, растёт сама. Массив T[] — фиксированной длины. Когда число элементов заранее неизвестно — берут List<T>."),
            new("Словарь Dictionary",
                "Dictionary<TKey,TValue> хранит пары «ключ → значение» и ищет по ключу почти мгновенно (хеш-таблица). ContainsKey проверяет наличие ключа, TryGetValue безопасно достаёт значение без исключения, если ключа нет."),
            new("Обработка ошибок",
                "Чтобы программа не падала при сбое, опасный код оборачивают в try/catch: в try — действие, которое может бросить исключение, в catch — что делать при ошибке. Блок finally выполняется всегда. Своё исключение бросают через throw new Exception(\"текст\")."),
            new("Асинхронность async/await",
                "async/await не блокирует поток, пока программа чего-то ждёт (ответ из сети, чтение файла). Метод помечают async, «ожидающие» вызовы — await. Такие методы возвращают Task или Task<T>. Это про отзывчивость во время ожидания, а не про скорость вычислений."),
            new("LINQ — запросы к коллекциям",
                "LINQ фильтрует и преобразует коллекции цепочкой методов: Where — отбор по условию, Select — преобразование каждого элемента, OrderBy — сортировка, First/Any/Count — выборка и подсчёт. Работает над любым IEnumerable<T>."),
            new("Проверка на null",
                "Оператор ?? (null-coalescing) возвращает левый операнд, если он не null, иначе правый: name ?? \"гость\". Оператор ?. безопасно обращается к члену: user?.Name вернёт null, если user равен null, вместо падения программы."),
            new("Интерфейсы и полиморфизм",
                "Интерфейс — это контракт: список методов и свойств без реализации. Класс, реализующий интерфейс, обязан их предоставить. Благодаря этому код работает с интерфейсом, не зная конкретный класс, — это и есть полиморфизм."),
            new("Строки и интерполяция",
                "Строки в C# неизменяемы. Удобная склейка — интерполяция: $\"Привет, {name}!\". Полезное: string.IsNullOrWhiteSpace, Trim, Split, Contains, ToLower. Когда собираешь строку из многих кусков в цикле — бери StringBuilder."),
            new("Целочисленное деление",
                "Деление двух int даёт int — дробная часть отбрасывается: 7 / 2 == 3. Чтобы получить 3.5, хотя бы один операнд должен быть double: 7 / 2.0. Остаток от деления даёт оператор %: 7 % 2 == 1."),
        };

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
            var embeddingsUrl = configuration["GigaChat:EmbeddingsUrl"];

            var accessToken = GetAccessToken(authKey, authUrl); // Получаем временный токен доступа (Bearer) по ключу


            // (1) ИНДЕКСАЦИЯ. Прогоняем 1 раз все заметки в тестовой базе знаний через эмбеддинги и запоминаем
            //     пары {заметка, вектор}. Это хранилище для поиска. Делаем ОДНИМ
            //     батч-запросом — список текстов разом (один поход в сеть на всю базу).
            Console.WriteLine($"Индексирую базу знаний ({Knowledge.Length} заметок)...");
            float[][] vectors = Embed(Knowledge.Select(d => $"{d.Title}. {d.Text}").ToList(), accessToken, embeddingsUrl);

            var knowledgeIndexes = new List<Indexed>();
            for (int i = 0; i < Knowledge.Length; i++)
                knowledgeIndexes.Add(new Indexed(Knowledge[i], vectors[i]));
            Console.WriteLine($"Готово: {knowledgeIndexes.Count} заметок, размерность вектора смысла — {vectors[0].Length}.\n");

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
                    "- Если в результате выполнения quiz_me пришло поле milestone_reached равное true, ты должен немедленно вызвать mark_studied для указанной темы, не спрашивая ученика. " +
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
                    "Проводит мини-тест по заданной теме C# (количество вариантов зависит от сложности: easy – 3, medium – 4, hard – 5). " +
                    "Ученик видит вопрос, варианты и вводит номер ответа. Программа возвращает результат и пояснение.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            topic = new { type = "string", description = "Тема теста, напр. «разница между struct и class»" },
                            difficulty = new { type = "string", @enum = new [] { "easy", "medium", "hard" },
                                               description = "Желаемая сложность теста (по-умолчанию medium)"}
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
                    "- Если в результате выполнения quiz_me пришло поле milestone_reached равное true, ты должен немедленно вызвать mark_studied для указанной темы, не спрашивая ученика. " +
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
                    var reply = AskGigaChat(history, accessToken, chatUrl, functions); // Отправляем всю историю в GigaChat и получаем ответ

                    string answer;
                    if (reply.FunctionCall is not null)
                    {
                        // Модель решила вызвать функцию. Сохраняем её «ход» (вместе с
                        // functions_state_id — GigaChat ждёт его обратно) и выполняем функцию.
                        history.Add(reply with { Content = reply.Content ?? "" });

                        string result = ExecuteFunction(reply.FunctionCall, accessToken, chatUrl);

                        history.Add(new ChatMessage("function", result, Name: reply.FunctionCall.Name));

                        // Финальный ответ просим УЖЕ БЕЗ функций: модель обязана ответить текстом
                        // и не зациклится на повторных вызовах одной и той же функции (Function
                        // Calling у GigaChat в бете это любит — звал бы list_topics по кругу).
                        answer = AskRaw(history, accessToken, chatUrl);
                    }
                    else
                    {
                        // Функция не нужна — это обычный текстовый ответ.
                        answer = reply.Content ?? "";
                    }

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

        // ─────────────────────────────────────────────────────────────────────────
        //  Метод эмбеддинга превращает тексты в векторы чисел (координаты смысла).
        //  Один запрос принимает СПИСОК строк (input) и возвращает по вектору на каждую.
        // ─────────────────────────────────────────────────────────────────────────
        private static float[][] Embed(List<string> texts, string accessToken, string embeddingsUrl)
        {
            var body = new EmbeddingRequest("Embeddings", texts);
            string json = JsonSerializer.Serialize(body, JsonOpts);
                        
            using var request = new HttpRequestMessage(HttpMethod.Post, embeddingsUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = _httpClient.Send(request);
            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<EmbeddingResponse>(ReadBody(response), JsonOpts)!;

            // У каждого вектора index = позиция текста во входном списке. Сортируем по index,
            // чтобы порядок векторов ТОЧНО совпал с порядком входных текстов.
            return result.Data
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding)
                .ToArray();
        }

        // Простой запрос без функций — возвращает текст. Используется и в GenerateQuiz
        // (с низкой температурой — нужен предсказуемый JSON), и для финального ответа
        // после вызова функции (без функций модель не зациклится).
        private static string AskRaw(List<ChatMessage> messages, string accessToken, string chatUrl, double? temperature = null)
        {
            var body = new ChatRequest("GigaChat", messages, Temperature: temperature);
            ChatResponse result = PostChat(body, accessToken, _httpClient, chatUrl);
            return result.Choices[0].Message.Content ?? "";
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  ВЫПОЛНЕНИЕ ФУНКЦИЙ, которые захотела вызвать модель.
        //  Возвращаем результат JSON-строкой — её увидит модель.
        // ─────────────────────────────────────────────────────────────────────────
        private static string ExecuteFunction(FunctionCall call, string accessToken, string chatUrl)
        {
            switch (call.Name)
            {
                case "add_topic":
                    {
                        // Аргументы у GigaChat приходят ОБЪЕКТОМ — читаем поля из JsonElement.
                        JsonElement a = call.Arguments;
                        string title = GetStr(a, "title") ?? "(без названия)";
                        string priority = GetStr(a, "priority") ?? "средний";
                        string? note = GetStr(a, "note");

                        plan.Add(new StudyTopic(title, priority, note));
                        Console.WriteLine($"  [добавил в план: {title}]");
                        return JsonSerializer.Serialize(new { status = "ok", added = title, total = plan.Count }, JsonOpts);
                    }

                case "list_topics":
                    Console.WriteLine("  [показываю план]");
                    return JsonSerializer.Serialize(
                    new { topics = plan, total = plan.Count, studied = plan.Count(t => t.Studied) }, JsonOpts);

                case "mark_studied":
                    {
                        string title = GetStr(call.Arguments, "title") ?? "";
                        // Модель могла слегка переформулировать тему — ищем по вхождению без учёта регистра.
                        int idx = plan.FindIndex(t => t.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0)
                        {
                            plan[idx] = plan[idx] with { Studied = true, CorrectAnswers = 0 };   // record + with: новый экземпляр, Studied=true
                            Console.WriteLine($"  [отметил изученным: {plan[idx].Title}]");
                        }
                        return JsonSerializer.Serialize(
                            new { status = idx >= 0 ? "ok" : "not_found", title, studied = idx >= 0 }, JsonOpts);
                    }

                // ── ТОЧКА СОЕДИНЕНИЯ ДВУХ ТЕХНИК ──────────────────────────────────
                //  Function Calling решил ЗАПУСТИТЬ тест (quiz_me), а форму вопроса
                //  гарантирует СТРУКТУРИРОВАННЫЙ ВЫВОД: внутри зовём GenerateQuiz.
                case "quiz_me":
                    {
                        string topic = GetStr(call.Arguments, "topic") ?? "C#";

                        string difficulty = GetStr(call.Arguments, "difficulty");

                        if (difficulty is null)
                        {
                            Console.WriteLine("Выбери сложность теста:");
                            Console.WriteLine("  1. easy   (3 варианта)");
                            Console.WriteLine("  2. medium (4 варианта)");
                            Console.WriteLine("  3. hard   (5 вариантов)");
                            Console.Write("Твой выбор (1-3): ");
                            string choice = Console.ReadLine();
                            difficulty = choice switch
                            {
                                "1" => "easy",
                                "3" => "hard",
                                _ => "medium"
                            };
                        }
                        else
                        {
                            difficulty = difficulty switch
                            {
                                "easy" => "easy",
                                "hard" => "hard",
                                _ => "medium"
                            };
                        }
                        
                        Console.WriteLine($"\n  [запускаю тест по теме: {topic}, сложность: {difficulty}]");

                        // (1) Структурированный вывод как ДВИЖОК инструмента: отдельный запрос
                        //     к модели за строгим JSON по схеме QuizQuestion (+ StripJsonFences).
                        //     Модель иногда присылает кривой JSON (хвостовая запятая, лишний текст) —
                        //     оборачиваем в try/catch, чтобы один тест не уронил весь чат.
                        QuizQuestion quiz;
                        try
                        {
                            quiz = GenerateQuiz(topic, accessToken, chatUrl, difficulty);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  [не удалось собрать тест: {ex.Message}]\n");
                            return JsonSerializer.Serialize(
                                new { topic, error = "Не удалось сгенерировать корректный тест. Предложи попробовать ещё раз." }, JsonOpts);
                        }

                        // Подстраховка от кривого ответа: нет вариантов / индекс вне диапазона.
                        if (quiz.Options is not { Length: > 0 })
                        {
                            Console.WriteLine("  [тест пришёл без вариантов ответа]\n");
                            return JsonSerializer.Serialize(
                                new { topic, error = "Тест без вариантов. Предложи попробовать ещё раз." }, JsonOpts);
                        }
                        int correctIndex = quiz.CorrectIndex >= 0 && quiz.CorrectIndex < quiz.Options.Length
                            ? quiz.CorrectIndex : 0;

                        // Источник вопроса — живая генерация (структурированный вывод по схеме).
                        Console.WriteLine("  [вопрос сгенерирован вживую — структурированный вывод по схеме]");

                        // (2) Показываем тест ученику.
                        Console.WriteLine($"❓ {quiz.Question}");
                        for (int i = 0; i < quiz.Options.Length; i++)
                            Console.WriteLine($"    {i + 1}. {quiz.Options[i]}");

                        // (3) Читаем ответ ученика (блокирующий ReadLine прямо в обработчике —
                        //     учебное упрощение: формально мы всё ещё «выполняем функцию»).
                        Console.Write($"Твой ответ (1-{quiz.Options.Length}): ");
                        bool parsed = int.TryParse(Console.ReadLine(), out int num);
                        bool correct = parsed && num >= 1 && num <= quiz.Options.Length && num - 1 == quiz.CorrectIndex;
                                                
                        bool milestoneReached = false; // Обновляем статистику, если ответ правильный
                        string canonicalTopic = topic; // будем хранить точное название темы из плана, если найдём

                        if (correct)
                        {
                            // Ищем тему в плане по вхождению (как в mark_studied)
                            var matched = plan.FirstOrDefault(t =>
                                t.Title.Contains(topic, StringComparison.OrdinalIgnoreCase));
                            if (matched != null)
                            {
                                canonicalTopic = matched.Title; // точное название из плана
                                int newCount = matched.CorrectAnswers + 1;
                                int idx = plan.FindIndex(t => t.Title == matched.Title);
                                if (idx >= 0)
                                {
                                    plan[idx] = plan[idx] with { CorrectAnswers = newCount };
                                    if (newCount >= 5 && !plan[idx].Studied)
                                        milestoneReached = true;
                                }
                            }
                        }

                        var resultObj = new
                        {
                            topic = canonicalTopic, // отдаём точное название, чтобы модель могла вызвать mark_studied
                            question = quiz.Question,
                            userAnswer = parsed ? num : (int?)null,
                            correct,
                            correctOption = quiz.Options[quiz.CorrectIndex], // здесь используем исходный correctIndex
                            explanation = quiz.Explanation,
                            milestone_reached = milestoneReached,
                            message = milestoneReached
                            ? $"🎉 Вы правильно ответили на 5 тестов по теме «{canonicalTopic}». Тему можно отметить как изученную."
                            : null
                        };

                        // (4) Мгновенный фидбэк ученику.
                        Console.WriteLine(correct ? "✅ Верно!" : "❌ Неверно.");
                        Console.WriteLine($"   Разбор: {quiz.Explanation}\n");

                        // (5) Возвращаем модели структурный вердикт — она прокомментирует
                        //     и сможет предложить mark_studied / add_topic.
                        return JsonSerializer.Serialize(resultObj, JsonOpts);
                    }

                default:
                    // Модель попросила функцию, которой у нас нет — честно говорим об этом.
                    return JsonSerializer.Serialize(new { error = $"Неизвестная функция: {call.Name}" }, JsonOpts);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  СТРУКТУРИРОВАННЫЙ ВЫВОД: просим модель вернуть строгий JSON (тест-вопрос)
        //  и разбираем его в объект C#. Это ДВИЖОК инструмента quiz_me.
        //  Живая генерация — единственный источник вопроса. Структурированный вывод
        //  гарантирует ФОРМУ (4 варианта + индекс верного), но НЕ правильность фактов:
        //  по слабым для модели темам вопрос может выйти с шероховатостями — для демо ок.
        // ─────────────────────────────────────────────────────────────────────────
        private static QuizQuestion GenerateQuiz(string topic, string accessToken, string chatUrl, string difficulty = "medium")
        {

            // Системный промпт генератора теста: держим модель в её зоне — факты языка C#
            // (ключевые слова, типы, синтаксис, операторы, коллекции, строки, ООП) — и
            // задаём строгую JSON-схему ответа. Это и есть «управление моделью».
            string systemPrompt = difficulty switch
            {
                "easy" =>
                    "Ты — генератор простых тест-вопросов для начинающих C#. " +
                    "Задавай только элементарные вопросы про ключевые слова, базовый синтаксис и простые типы. " +
                    "Варианты ответов должны быть очевидными, объяснения краткими. " +
                    "Верни ТОЛЬКО JSON-объект по схеме:\n" +
                    "{ \"question\": строка, \"options\": [ровно 3 строки], \"correctIndex\": число 0..2, \"explanation\": строка }\n" +
                    "correctIndex — позиция верного варианта, нумерация с НУЛЯ. Без markdown, без лишнего текста.",

                "hard" =>
                    "Ты — генератор сложных тест-вопросов по C#. " +
                    "Задавай углублённые вопросы про внутреннее устройство языка, нюансы (ref/in/out, Span<T>, аллокации, boxing, замыкания, работа GC, многопоточность). " +
                    "Варианты ответов должны быть коварными, но строго один верный. " +
                    "Верни ТОЛЬКО JSON-объект по схеме:\n" +
                    "{ \"question\": строка, \"options\": [ровно 5 строк], \"correctIndex\": число 0..4, \"explanation\": строка }\n" +
                    "correctIndex — позиция верного варианта, нумерация с НУЛЯ. explanation подробно разъясняет верный ответ.",

                _ => // medium по-умолчанию
                    "Ты — генератор тест-вопросов про язык C# для начинающих. " +
                    "Спрашивай про факты самого языка и базовой библиотеки .NET: ключевые слова (var, const, readonly, static, ref, out), " +
                    "типы и их различия (struct и class, значимые и ссылочные, nullable), синтаксис, поведение операторов, " +
                    "базовые коллекции (List<T>, Dictionary<TKey,TValue>), строки, исключения, ООП в C#. " +
                    "Сначала ВЫБЕРИ один факт, в котором уверен, сделай его верным ответом, затем придумай 3 правдоподобных, но неверных. " +
                    "Вопрос должен быть КОНКРЕТНЫМ (про конкретное ключевое слово, тип или метод), а не общим рассуждением. " +
                    "Верни ТОЛЬКО JSON-объект по схеме:\n" +
                    "{ \"question\": строка, \"options\": [ровно 4 строки], \"correctIndex\": число 0..3, \"explanation\": строка }\n" +
                    "correctIndex — позиция верного варианта в options, нумерация с НУЛЯ. explanation объясняет именно options[correctIndex]. " +
                    "Ровно один верный вариант, остальные три неверны. Без markdown, без текста до или после JSON."
            };

            double temp = difficulty switch
            {
                "easy" => 0.3,
                "hard" => 0.5,
                _ => 0.2
            };

            var messages = new List<ChatMessage>
            {
                new("system", systemPrompt),
                // FEW-SHOT: один образец задаёт И форму JSON, И стиль (конкретный факт языка).
                //new("user", "Тема для теста (про язык C#): ключевое слово var"),
                //new("assistant",
                //    "{\"question\":\"Что делает ключевое слово var при объявлении локальной переменной в C#?\"," +
                //    "\"options\":[\"Тип выводится компилятором из выражения справа\"," +
                //    "\"Создаёт переменную без типа, как в JavaScript\"," +
                //    "\"Объявляет переменную, которую нельзя переприсвоить\"," +
                //    "\"Делает переменную видимой во всех методах класса\"]," +
                //    "\"correctIndex\":0,\"explanation\":\"var — это неявная типизация: компилятор сам выводит конкретный тип из инициализатора, переменная остаётся строго типизированной.\"}"),
                new("user", $"Тема для теста (про язык C#): {topic}"),
            };

            // Низкая температура: строгому вопросу нужна предсказуемость, а не фантазия.
            // Иногда модель оборачивает JSON в ```json ... ``` — срежем «забор».
            //string clean = StripJsonFences(AskRaw(messages, temperature: 0.2));
            string clean = AskRaw(messages, accessToken, chatUrl, temperature: temp);

            // Кривой JSON бросит из Deserialize, пустой ("null") — поймаем через ??.
            // Любую ошибку перехватит обработчик quiz_me и попросит модель повторить.
            return JsonSerializer.Deserialize<QuizQuestion>(clean, JsonOpts)
                   ?? throw new InvalidOperationException("Модель вернула пустой JSON вместо тест-вопроса.");
        }

        private static string? GetStr(JsonElement obj, string field) // берёт JSON элемент и достаёт из него значение свойства, если его тип sting иначе возвращает null
        {
            return obj.ValueKind == JsonValueKind.Object
                   && obj.TryGetProperty(field, out var v)
                   && v.ValueKind == JsonValueKind.String
                    ? v.GetString()
                    : null;
        }

        static ChatMessage AskGigaChat(List<ChatMessage> history, string accessToken, string chatUrl, List<FunctionDef> functions)
        {
            var body = new ChatRequest("GigaChat", history, functions, FunctionCallMode: "auto");

            ChatResponse result = PostChat(body, accessToken, _httpClient, chatUrl);

            return result!.Choices[0].Message;
        }

        private static ChatResponse PostChat(ChatRequest body, string accessToken, HttpClient httpClient, string chatUrl) // Сериализует тело запроса в JSON, выполняет POST-запрос к указанному URL с авторизацией, проверяет успешность и десериализует ответ в ChatResponse
        {
            string json = JsonSerializer.Serialize(body, JsonOpts);

            using var request = new HttpRequestMessage(HttpMethod.Post, chatUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = httpClient.Send(request);
            response.EnsureSuccessStatusCode();

            return JsonSerializer.Deserialize<ChatResponse>(ReadBody(response), JsonOpts)!;
        }

        static string AskGigaChat(List<ChatMessage> history, string accessToken, string chatUrl) // Метод отправки запроса к чат-модели GigaChat
        {
            Log.Debug("Отправка запроса к GigaChat. История содержит {Count} сообщений", history.Count);

            try
            {
                var body = new ChatRequest("GigaChat", history); // Создаём тело запроса без функций (для обычного разговора)

                ChatResponse response = PostChat(body, accessToken, _httpClient, chatUrl); // Вызываем общий метод отправки 

                if (response?.Choices is { Count: > 0 } && response.Choices[0].Message is not null) // Безопасно извлекаем контент
                {
                    string content = response.Choices[0].Message.Content;
                    Log.Debug("Ответ успешно десериализован");
                    return content;
                }
                else
                {
                    throw new InvalidOperationException("GigaChat вернул пустой ответ или без choices");
                }       
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

        // Сообщение в переписке. Кроме role/content может нести:
        //   • function_call — когда модель (assistant) решила вызвать функцию;
        //   • functions_state_id — служебный id, который GigaChat просит вернуть обратно;
        //   • name — имя функции, когда мы отправляем РЕЗУЛЬТАТ (role = "function").
        record ChatMessage(
            string Role,
            string? Content,
            [property: JsonPropertyName("function_call")] FunctionCall? FunctionCall = null,
            [property: JsonPropertyName("functions_state_id")] string? FunctionsStateId = null,
            string? Name = null);

        // Вызов функции от модели: имя + аргументы. У GigaChat arguments — это JSON-ОБЪЕКТ,
        // поэтому читаем его универсально как JsonElement (а не как строку, как в OpenAI).
        record FunctionCall(string Name, JsonElement Arguments);

        // Тело запроса к чату: модель + история (+ опц. функции, режим их вызова, температура).
        // Temperature шлём только когда нужна предсказуемость (GenerateQuiz); WhenWritingNull
        // в JsonOpts означает, что для обычных запросов поле не сериализуется (модель берёт дефолт).
        record ChatRequest(
            string Model,
            List<ChatMessage> Messages,
            List<FunctionDef>? Functions = null,
            [property: JsonPropertyName("function_call")] string? FunctionCallMode = null,
            [property: JsonPropertyName("temperature")] double? Temperature = null);

        record ChatResponse(List<Choice> Choices);  // Модель ответа от GigaChat
        record Choice(ChatMessage Message); // Один вариант ответа (выбор)      
        record FunctionDef(string Name, string Description, object Parameters); // Описание функции для модели: имя, что делает, и схема параметров (JSON Schema).
                
        record StudyTopic(string Title, string Priority, string? Note, bool Studied = false, int CorrectAnswers = 0); // Тема в плане изучения. Studied ставит функция mark_studied; изученные темы — поиск по смыслу «повтори пройденное».

        record QuizQuestion(string Question, string[] Options, int CorrectIndex, string Explanation); // Тест-вопрос, который достаём структурированным выводом в GenerateQuiz (движок инструмента quiz_me).

        // ── Эмбеддинги ───────────────────────────────────────────────────────────────
        
        record EmbeddingRequest(string Model, List<string> Input); // Запрос: имя модели эмбеддингов + список текстов (input принимает массив строк).
        
        record EmbeddingResponse(List<EmbeddingData> Data); // Ответ: список векторов; у каждого Embedding — числа, Index — позиция текста во входе.
        record EmbeddingData(float[] Embedding, int Index); // модель информации эмбеддингов, содержащая массив векторов и индекс соотвествующего текста
        record KnowledgeDoc(string Title, string Text); // модель документа тестовой базы знаний

        
        record Indexed(KnowledgeDoc Doc, float[] Vector); // Модель проиндексированной заметки: сам документ + его вектор смысла.

        record Scored(KnowledgeDoc Doc, double Score);  // Модель оценки проиндексированной заметки в отношении близости по смыслу с запросоом
    }
}
