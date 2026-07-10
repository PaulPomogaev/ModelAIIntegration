# 🤖 AI Mentor C# — учебный проект ИИ-наставника по C# с поддержкой GigaChat, Gemini и локальной Ollama

<div align="center">
  <img src="https://github.com/user-attachments/assets/70cd45a7-bd06-4eb5-bc82-3e7953172443" alt="AI Mentor C#" width="600">
  <br><sub>Интерактивный наставник по C# с поиском по лекциям, планом обучения и генерацией тестов</sub>
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
Клиент Gemini - настройка прокси, сериализация, обработка ошибок
```csharp
public class GeminiClient : ILanguageModel
{
    public GeminiClient(string apiKey, string proxyUrl = null) { /* HttpClient + WebProxy */ }

    public async Task<string> ChatAsync(List<ChatMessage> messages, double? temperature = null)
    {
        var body = GeminiRequestBuilder.Build(messages, temperature);
        var response = await PostAsync<GeminiResponse>(generateUrl, body);
        return ResponseParser.ExtractText(response);
    }
```
// Фрагмент кода. Полный код: [ссылка на файл в репозитории](https://github.com/PaulPomogaev/ModelAIIntegration/blob/master/ModelAIIntegration.Core/Gemini/GeminiClient.cs)

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
<div align="center">
  <img src="https://github.com/user-attachments/assets/e20933c7-2147-4255-8c6b-8f4073188e7d" alt="Демонстрация AI Mentor C#" width="700">
  <br>
  <sub>Диалог с наставником: вопрос по лекциям, генерация теста, ответ с пояснением</sub>
</div>

---

**📷 Скриншоты интерфейса**

<table>
  <tr>
    <td><img src="https://github.com/user-attachments/assets/70cd45a7-bd06-4eb5-bc82-3e7953172443" alt="Главная страница" width="400"></td>
    <td><img src="https://github.com/user-attachments/assets/5a12e5f2-8e21-4696-85c4-e58f0286a87c" alt="История диалога" width="400"></td>
    <td><img src="https://github.com/user-attachments/assets/a28ba471-252a-445c-8459-2b2f3e078ba3" alt="Тестирование" width="400"></td>
  </tr>
  <tr>
    <td align="center">Главная страница</td>
    <td align="center">История диалога</td>
    <td align="center">Тестирование</td>
  </tr>
</table>

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
