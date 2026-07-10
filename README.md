# 🤖 AI Mentor C# — учебный проект ИИ-наставника по C# с поддержкой GigaChat, Gemini и локальной Ollama

<div align="center">
  <img src="screenshots/1.jpg" alt="AI Mentor C#" width="80%">
  <br>
  <sub>Интерактивный наставник по C# с поиском по лекциям, планом обучения и генерацией тестов</sub>
</div>

---

## 📂 Содержание
- [О проекте](#️-о-проекте)
- [Архитектура репозитория](#-архитектура-репозитория)
- [Ключевые возможности](#-ключевые-возможности)
- [Технологический стек](#-технологический-стек)
- [Архитектурные решения и примеры кода](#-архитектурные-решения-и-примеры-кода)
- [Демонстрация](#-демонстрация)
- [Запуск проекта](#-запуск-проекта)
- [Контакты](#-контакты)

---

## 🏛️ О проекте

<div align="center">
  <sub><i>Для HR: кратко и по делу</i></sub>
</div>

**AI Mentor C#** - учебный проект, емонстрирующий интеграцию больших языковых моделей (LLM) в образовательное приложение. Он представляет собой интеллектуального наставника, который отвечает на вопросы по лекциям, составляет план изучения C#, генерирует тесты и проверяет знания. Проект поддерживает  три бэкенда: облачный GigaChat (API Сбера), Gemini (генрация API key и доступ только чрез прокси или VPN) и локальный Ollama с возможностью выбора модели в интерфейсе. Это показывает навыки работы с асинхронными API, эмбеддингами, RAG (Retrieval-Augmented Generation), Function Calling и построением гибкой архитектуры с интерфейсами и внедрением зависимостей.

<div align="center">
  <sub><i>Для техлидов и разработчиков: технические детали</i></sub>
</div>

Проект построен на многослойной архитектуре с чётким разделением ответственности:
- **ModelAIIntegration.Core** – ядро: модели данных (ChatMessage, FunctionDef, Chunk, Scored, QuizQuestion, StudyTopic), интерфейс ILanguageModel, реализация клиентов для GigaChat, Gemini и Ollama, логика RAG-поиска, управление планом и тестами.
- **ModelAIIntegration.Console**  – консольное приложение для диалога с наставником (выбор модели, ввод вопросов, прохождение тестов).
- **ModelAIIntegration.WebApp** – веб-приложение на ASP.NET Core MVC с выбором модели в интерфейсе, хранением истории в сессии, отображением источников и обработкой тестов.

Основные архитектурные решения:
- **Паттерн Strategy** – интерфейс `ILanguageModel` с методами `EmbedAsync`, `ChatWithFunctionsAsync`, `ChatAsync` позволяет легко подключать любую языковую модель (GigaChat, Gemini, Ollama, OpenAI и т.д.).
- **Function Calling** – модель может вызывать функции (`search_documents`, `quiz_me`, `add_topic`, `list_topics`, `mark_studied`), что делает диалог интерактивным и позволяет управлять планом и тестами.
- **RAG (Retrieval-Augmented Generation)** – поиск по документам по смыслу через эмбеддинги (векторное представление). Индексы кешируются отдельно для каждого провайдера (`index.GigaChat.json`, `index.Gemini.json`, `index.Ollama.json`).
- **Состояние диалога** – история сообщений и ожидающий вызов функции (`pendingFunctionCall`) хранятся в сессии для веба и в полях экземпляра для консоли.
- **Асинхронность** – все сетевые вызовы асинхронны с повторными попытками при ошибках 429.

---

## 📂 Архитектура репозитория

```text
📁 ModelAIIntegration
├── 📁 ModelAIIntegration.Core
│   ├── 📁 GigaChat              # Клиент GigaChat (реализует ILanguageModel)
│   │   ├── 📁 Models            # ChatMessage, ChatRequest, FunctionCall и др.
│   │   └── GigaChatClient.cs
│   ├── 📁 Gemini                # Клиент Gemini (реализует ILanguageModel)
│   │   ├── 📁 Models            # Модели для API Gemini
│   │   └── GeminiClient.cs
│   ├── 📁 Knowledge             # RAG-компоненты
│   │   ├── 📁 Models            # Chunk, Scored
│   │   └── KnowledgeBase.cs     # Индексация и поиск по смыслу
│   ├── 📁 Ollama                # Клиент Ollama
│   │   ├── 📁 Models            # Модели для API Ollama
│   │   └── OllamaClient.cs
│   ├── 📁 Tutor                 # Основная логика наставника
│   │   ├── 📁 Models            # QuizQuestion, StudyTopic
│   │   ├── CSharpTutor.cs       # Главный класс: история, функции, диалог, тесты
│   │   ├── FunctionExecutor.cs  # Выполнение функций (add_topic, list_topics, mark_studied)
│   │   ├── QuizEngine.cs        # Генерация тестов через LLM
│   │   └── StudyPlan.cs         # Управление планом изучения
│   └── ILanguageModel.cs        # Интерфейс для LLM
├── 📁 ModelAIIntegration.Console
│   ├── Program.cs               # Консольная точка входа (выбор модели, цикл диалога)
│   └── appsettings.json         # Конфигурация GigaChat и Gemini
├── 📁 ModelAIIntegration.WebApp
│   ├── 📁 Controllers           # ChatController (обработка вопросов и ответов на тесты)
│   ├── 📁 Models                # ChatViewModel, ChatTurn для представления
│   ├── 📁 Views                 # Razor-представления (форма вопроса, выбор модели, лента диалога)
│   ├── 📁 docs                  # Лекции в формате .md (база знаний для RAG)
│   ├── LanguageModelRegistry.cs # Реестр для получения LLM и KnowledgeBase по провайдеру
│   ├── Program.cs               # Настройка сервисов, сессий, маршрутов
│   └── appsettings.json         # Конфигурация GigaChat, Gemini и настройки
└── ModelAIIntegration.sln       # Файл решения
```

## 💡 Ключевые возможности

### 🤖 Три модели ИИ на выбор
- GigaChat – облачная модель от Сбера (требуется интернет и ключ API).
- Gemini – модель от Google (требуется API-ключ, поддержка прокси для обхода гео-блокировок).
- Ollama – локальная модель (поддерживаются любые модели, например, qwen2.5:1.5b). Выбор модели в веб-интерфейсе через выпадающий список (список подгружается динамически).
- В консоли выбор осуществляется при старте.

### 📚 Поиск по лекциям (RAG)
- Задавайте вопросы по теории C# – наставник ищет ответ в ваших лекциях по смыслу (с помощью эмбеддингов).
- Показывает фрагменты документов с указанием источника (имя файла) и оценкой близости (score).
- Индексы строятся один раз и кешируются на диске отдельно для каждого провайдера, экономя время и ресурсы.

### 📝 План изучения
- Наставник может добавлять темы в личный план (`add_topic`).
- Просмотр текущего плана (`list_topics`).
- Отметка темы как изученной (`mark_studied`).
- Прогресс по темам сохраняется между сессиями (в памяти для консоли, в сессии для веба).

### 🧪 Тестирование знаний
- Генерация тестов по любой теме с выбором сложности (easy, medium, hard).
- Тесты создаются на лету с использованием LLM (структурированный вывод в JSON с повторными попытками при ошибках).
- После ввода ответа наставник проверяет его, даёт пояснение и при правильных ответах (5 подряд) автоматически отмечает тему как изученную.

### 💬 Диалог с историей и лентой сообщений
- Полноценный диалог с сохранением всей истории сообщений.
- В вебе история хранится в сессии пользователя (таймаут 50 минут).
- В консоли история живёт в памяти до завершения программы.
- Лента диалога в веб-интерфейсе отображает все вопросы и ответы, показывая, что наставник помнит контекст.

### 🔌 Function Calling
- Модель самостоятельно решает, какую функцию вызвать (поиск, тест, план и т.д.).
- Реализованы функции: `search_documents`, `quiz_me`, `add_topic`, `list_topics`, `mark_studied`.
- Интерактивное взаимодействие: при генерации теста наставник выдаёт вопрос, а после ответа пользователя обрабатывает его и даёт фидбэк.

### 🛡️ Обход блокировок (прокси)
- Клиент Gemini поддерживает настройку HTTP-прокси через `appsettings.json`, что позволяет использовать сервис из регионов с ограничениями.


### 📦 План обучения (StudyPlan)
- Добавление тем с приоритетом и заметками.
- Отметка темы как изученной, обнуление счётчика правильных ответов.
- Автоматическая отметка после 5 успешных тестов по одной теме.

**Пример кода (отметка изученной темы):**
```csharp
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
```

### 🧪 Генерация и проверка тестов
- Модель получает тему и сложность, генерирует вопрос с вариантами ответов.
- Пользователь вводит номер ответа – результат проверяется мгновенно.
- При 5 правильных ответах тема автоматически помечается как изученная.

**Пример кода (генерация вопроса):**
```csharp
public async Task<QuizQuestion> GenerateAsync(string topic, string difficulty = "medium")
{
    string systemPrompt = difficulty switch
    {
        "easy" => "Ты — генератор простых тест-вопросов...",
        "hard" => "Ты — генератор сложных тест-вопросов...",
        _ => "Ты — генератор тест-вопросов про язык C#..."
    };
    var messages = new List<ChatMessage>
    {
        new("system", systemPrompt),
        new("user", $"Тема для теста (про язык C#): {topic}"),
    };
    string raw = await _llm.ChatAsync(messages, temperature: temp);
    return JsonSerializer.Deserialize<QuizQuestion>(raw, JsonOpts)!;
}
```

### 🔍 Поиск по лекциям (RAG)
- Документы разбиваются на чанки, векторизуются, сохраняются в индекс.
- Поиск по смыслу с помощью косинусного сходства.
- Найденные фрагменты передаются модели как контекст для ответа.

**Пример кода (поиск и генерация ответа):**
```csharp
List<Scored> top = await _kb.SearchAsync(query, _llm, topK: 3);
string answer = await _llm.ChatAsync(history);
history.Add(new ChatMessage("assistant", answer));
```

### 🤖 Динамический выбор LLM (провайдера)
- Интерфейс `ILanguageModel` позволяет переключаться между GigaChat, Ollama, Gemini.
- Провайдер выбирается пользователем в интерфейсе или консоли.
- Клиент для Gemini поддерживает прокси для обхода региональных блокировок.

**Пример кода (создание клиента Gemini с прокси):**
```csharp
HttpClientHandler handler = new();
if (!string.IsNullOrWhiteSpace(proxyUrl))
{
    handler.Proxy = new WebProxy(proxyUrl);
    handler.UseProxy = true;
}
http = new HttpClient(handler);
http.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
```  

### 💬 Веб-интерфейс с историей диалога
- Лента сообщений (`ChatTurn`) сохраняется в сессии. .
- Кнопка «Новый диалог» очищает историю.
- Отображается, какая модель отвечала на каждом шаге.

**Пример кода (сохранение хода диалога):**
```csharp
var newTurn = new ChatTurn
{
    Question = question,
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
```

## 🔧 Технологический стек
- **.NET 9 (C# 12)** 
- **ASP.NET Core MVC** (для веб-приложения)
- **Serilog** (логирование)
- **Microsoft.Extensions.Configuration** (работа с JSON-конфигами)
- **HttpClient** (для вызовов API GigaChat, Gemini и Ollama)
- **System.Text.Json** (сериализация/десериализация JSON)
- **GigaChat API** (облачная модель Сбера)
- **Gemini API** (облачная модель Google)
- **Ollama** (локальный запуск моделей)
- **Git / GitHub**


## 🧠 Архитектурные решения и примеры кода

### 1. Паттерн Strategy для LLM
Интерфейс `ILanguageModel` определяет контракт для чата, вызова функций и эмбеддингов. Каждый провайдер реализует его по-своему, а тьютор работает с абстракцией.

**Интерфейс:**
```csharp
public interface ILanguageModel
{
    Task<float[][]> EmbedAsync(List<string> texts);
    Task<ChatMessage> ChatWithFunctionsAsync(List<ChatMessage> messages, List<FunctionDef> functions);
    Task<string> ChatAsync(List<ChatMessage> messages, double? temperature = null);
}
```

**Провайдет ИИ:**

```csharp
public class GeminiClient : ILanguageModel
{
    private readonly string chatModel;
    private readonly string embedModel;
    private readonly string generateUrl;
    private readonly string batchEmbedUrl;

    // camelCase (Web) сам переводит systemInstruction/functionCall/functionResponse/toolConfig;
    // WhenWritingNull — чтобы пустые варианты part (text/functionCall/functionResponse) не улетали
    // как null и не ломали запрос.
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient http;

    public GeminiClient(string apiKey, string chatModel = "gemini-2.5-flash", string embedModel = "gemini-embedding-001", string baseUrl = "https://generativelanguage.googleapis.com", string? proxyUrl = null)
    {
        this.chatModel = chatModel;
        this.embedModel = embedModel;
        string root = baseUrl.TrimEnd('/');
        generateUrl = $"{root}/v1beta/models/{chatModel}:generateContent";
        batchEmbedUrl = $"{root}/v1beta/models/{embedModel}:batchEmbedContents";

        // Настройка прокси, если задан
        HttpClientHandler handler = new();
        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            handler.Proxy = new WebProxy(proxyUrl);
            handler.UseProxy = true;
        }

        http = new HttpClient(handler);

        // Ключ — заголовком на всё соединение (в конструкторе сети НЕТ, только настройка).
        http.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
    }

    public async Task<string> ChatAsync(List<ChatMessage> messages, double? temperature = null)
    {
        var (system, contents) = ToGemini(messages);
        object? generationConfig = null;
        if (temperature.HasValue)
            generationConfig = new { temperature = temperature.Value };

        var body = new GeminiGenerateRequest(contents, system, GenerationConfig: generationConfig);
        var reply = await PostAsync<GeminiGenerateResponse>(generateUrl, body);
        return FromGemini(reply).Content ?? "";
    }

    public async Task<ChatMessage> ChatWithFunctionsAsync(List<ChatMessage> messages, List<FunctionDef> functions)
    {
        var (system, contents) = ToGemini(messages);
        var tools = new List<GeminiTool>
        {
            new(functions.Select(f => new GeminiFunctionDeclaration(f.Name, f.Description, f.Parameters)).ToList()),
        };

        var body = new GeminiGenerateRequest(contents, system, tools, new GeminiToolConfig(new GeminiFunctionCallingConfig("AUTO")));
        var reply = await PostAsync<GeminiGenerateResponse>(generateUrl, body);
        return FromGemini(reply);
    }

    public async Task<float[][]> EmbedAsync(List<string> texts)
    {
        var requests = texts.Select(t => new GeminiEmbedRequestItem($"models/{embedModel}", new GeminiContent(null, new List<GeminiPart> { new(Text: t) }))).ToList();
        var result = await PostAsync<GeminiBatchEmbedResponse>(batchEmbedUrl, new GeminiBatchEmbedRequest(requests));
        return result.Embeddings.Select(e => e.Values).ToArray();
    }

    // Наш общий ChatMessage[] → формат Gemini: системный промпт уходит в systemInstruction,
    // остальное — в contents (role user/model, результат функции — role:"user" + functionResponse).
    private static (GeminiContent? System, List<GeminiContent> Contents) ToGemini(List<ChatMessage> messages)
    {
        GeminiContent? system = null;
        var contents = new List<GeminiContent>();

        foreach (var m in messages)
        {
            if (m.Role == "system")
            {
                system = new GeminiContent(null, new List<GeminiPart> { new(Text: m.Content ?? "") });
            }
            else if (m.Role == "function")
            {
                // Результат нашей функции. Gemini ждёт его как part functionResponse c role:"user".
                // Тело результата у нас — JSON-строка ({"results":[...]}), Gemini хочет объект.
                contents.Add(new GeminiContent("user", new List<GeminiPart>
                {
                    new(FunctionResponse: new GeminiFunctionResponse(m.Name ?? "", ParseResponse(m.Content))),
                }));
            }
            else
            {
                string role = m.Role == "assistant" ? "model" : "user";
                GeminiPart part = m.FunctionCall is { } fc
                    ? new GeminiPart(FunctionCall: new GeminiFunctionCall(fc.Name, fc.Arguments))
                    : new GeminiPart(Text: m.Content ?? "");
                contents.Add(new GeminiContent(role, new List<GeminiPart> { part }));
            }
        }

        return (system, contents);
    }

    // Ответ Gemini → наш общий ChatMessage. Если среди parts есть functionCall — берём его
    // (просим одну функцию), иначе склеиваем текстовые parts.
    private static ChatMessage FromGemini(GeminiGenerateResponse reply)
    {
        var parts = reply.Candidates?.FirstOrDefault()?.Content?.Parts ?? new List<GeminiPart>();

        var call = parts.FirstOrDefault(p => p.FunctionCall is not null)?.FunctionCall;
        if (call is not null)
            return new ChatMessage("assistant", null, new FunctionCall(call.Name, call.Args));

        string text = string.Concat(parts.Where(p => p.Text is not null).Select(p => p.Text));
        return new ChatMessage("assistant", text);
    }

    // Тело результата функции: строка → JSON-объект (Gemini ждёт объект в response).
    // Не распарсилось (вдруг не JSON) — заворачиваем в { result: "..." }.
    private static object ParseResponse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new { };
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch (JsonException)
        {
            return new { result = content };
        }
    }

    private async Task<T> PostAsync<T>(string url, object body)
    {
        string json = JsonSerializer.Serialize(body, JsonOpts);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        using HttpResponseMessage response = await SendOrThrowAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            // Тело ошибки Gemini содержательное (в т.ч. «User location is not supported» из РФ) —
            // показываем его, а не голый статус.
            string error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Gemini ответил {(int)response.StatusCode}. {Shorten(error)}");
        }

        string respBody = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(respBody, JsonOpts)!;
    }

    // Нет сети / DNS / гео-блок на уровне соединения → понятная подсказка (частый случай из РФ).
    private async Task<HttpResponseMessage> SendOrThrowAsync(HttpRequestMessage request)
    {
        try
        {
            return await http.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "Не достучался до Gemini API (generativelanguage.googleapis.com). Проверь интернет; " +
                "из России API заблокирован по IP — нужен VPN/прокси в поддерживаемой стране.",
                ex);
        }
    }

    private static string Shorten(string s)
    {
        s = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length <= 300 ? s : s[..300] + "…";
    }
}
```

### 2. RAG: индексация и поиск
 `KnowledgeBase`читает `.md` файлы, разбивает на чанки, получает векторы и сохраняет их в индекс. Поиск вычисляет косинусное сходство вопроса с каждым чанком.

**Поиск ответа по лекциям:**
```csharp
public async Task<List<Scored>> SearchAsync(string query, ILanguageModel llm, int topK)
{
    float[] q = (await llm.EmbedAsync(new List<string> { query }))[0];
    return _chunks
        .Select(c => new Scored(c, Cosine(q, c.Vector)))
        .OrderByDescending(s => s.Score)
        .Take(topK)
        .ToList();
}
```

### 3. Function Calling в тьюторе 
Модель получает список доступных функций и решает, какую вызвать. Тьютор обрабатывает вызов, выполняет действие (добавить тему, создать тест, найти документы) и возвращает результат модели для финального ответа.

**Обработчик ответа от модели (Function Calling):**
```csharp
// фрагмент из CSharpTutor.AskAsync
var reply = await _llm.ChatWithFunctionsAsync(history, Functions);
if (reply.FunctionCall is { Name: "search_documents" })
{
    // выполняем поиск и передаём результат
    ...
}
else if (reply.FunctionCall is { Name: "quiz_me" })
{
    // генерируем вопрос и ждём ответ от пользователя
    ...
}
```

### 4. Хранение истории в веб-сессии
Контроллер разделяет внутреннюю историю (для модели) и «ленту» (для отображения). Оба списка хранятся в `HttpContext.Session` в виде сериализованного JSON.

**Загрузка истории диалога:**
```csharp
private List<ChatTurn> LoadTurns() =>
    HttpContext.Session.GetString(TurnsKey) is string json
    ? JsonSerializer.Deserialize<List<ChatTurn>>(json) ?? new()
    : new();
```

## 📸 Демонстрация

| Главная страница веб-интерфейса | Тестирование по теме | История диалога |
|---------------------------|---------------------|--------------------------|
| ![Catalog](screenshots/main.jpg) | ![Cart](screenshots/cart.jpg) | ![Admin](screenshots/admin-panel.jpg) |

## 🚀 Запуск проекта

**Клонировать репозиторий:**
```bash
git clone https://github.com/PaulPomogaev/ModelAIIntegration.git
cd ModelAIIntegration
```
**Настройка API-ключей**:
В файлах `appsettings.json` (в папках ModelAIIntegration и ModelAIIntegrationWebApp) замените плейсхолдеры на свои ключи:
```json
{
  "GigaChat": {
    "AuthKey": "ВАШ_КЛЮЧ_АВТОРИЗАЦИИ_GIGACHAT",
    "ChatUrl": "https://gigachat.devices.sberbank.ru/api/v1/chat/completions",
    "AuthUrl": "https://ngw.devices.sberbank.ru:9443/api/v2/oauth",
    "EmbeddingsUrl": "https://gigachat.devices.sberbank.ru/api/v1/embeddings"
  },
  "Gemini": {
    "ApiKey": "ВАШ_API_KEY_GEMINI",
    "ProxyUrl": "http://user:pass@proxy:8080"  // опционально, если нужен прокси
  }
}
```
**Подготовка лекций:**
Поместите ваши `.md` конспекты в папку docs внутри запускаемого проекта (консольного или веб).

**(Опционально) Установка и запуск Ollama:**
Если планируете использовать локальные модели.
```bash
ollama pull qwen2.5:1.5b
ollama pull nomic-embed-text
ollama serve
```
**Запуск консольной версии:**
```bash
cd ModelAIIntegration
dotnet run
```
Выберите провайдера и общайтесь с наставником в терминале.

**Запуск веб-приложения:**
```bash
cd ../ModelAIIntegrationWebApp
dotnet run
```
Откройте браузер по адресу https://localhost:7078. Выберите нейросеть, задавайте вопросы и проходите тесты.

## 📬 Контакты
- **Автор:** Paul Pomogaev
- **Email:** paulslock1@gmail.com
- **GitHub:** [@PaulPomogaev](https://github.com/PaulPomogaev)

## 🔑 Ключевые слова
ASP.NET Core | MVC | C# | Искусственный интеллект | LLM | GigaChat | Ollama | Gemini | RAG | Эмбеддинги | Function Calling | Паттерн Strategy | Сессии | Прокси | Обучение | Тестирование | Clean Architecture | Serilog
