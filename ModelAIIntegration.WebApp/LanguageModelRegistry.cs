using ModelAIIntegrationCore.Gemini;
using ModelAIIntegrationCore.Knowledge;
using ModelAIIntegrationCore.Ollama;
using ModelAIIntegrationCore;
using ModelAIIntegrationCore.GigaChat;


namespace ModelAIIntegrationWebApp
{
    public class LanguageModelRegistry
    {
        private readonly string _docsDir;
        private readonly string _indexDir;
        private readonly IConfiguration _configuration;
        private GigaChatClient? _gigaChat;
        private readonly Dictionary<string, KnowledgeBase> _knowledgeBases = new();

        public LanguageModelRegistry(IWebHostEnvironment env, IConfiguration configuration)
        {
            _docsDir = Path.Combine(env.ContentRootPath, "docs");
            _indexDir = env.ContentRootPath;
            _configuration = configuration;
        }

        public async Task<ILanguageModel> GetModelAsync(string provider, string? ollamaModel)
        {
            if (string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                string chatModel = ollamaModel ?? "qwen2.5:1.5b";
                return new OllamaClient(chatModel, "nomic-embed-text");
            }
            if (string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase))
            {
                var section = _configuration.GetSection("Gemini");
                string apiKey = section["ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                    throw new InvalidOperationException("Не задан Gemini:ApiKey в appsettings.json");
                return new GeminiClient(apiKey);
            }

            if (_gigaChat == null)
            {
                var section = _configuration.GetSection("GigaChat");
                string authKey = section["AuthKey"];
                string chatUrl = section["ChatUrl"];
                string authUrl = section["AuthUrl"];
                string embeddingsUrl = section["EmbeddingsUrl"];
                _gigaChat = await GigaChatClient.CreateAsync(authKey, chatUrl, authUrl, embeddingsUrl);
            }
            return _gigaChat;
        }

        public async Task<KnowledgeBase> GetKnowledgeBaseAsync(ILanguageModel model, string provider)
        {
            if (_knowledgeBases.TryGetValue(provider, out var kb))
                return kb;

            kb = new KnowledgeBase();
            string indexFile = Path.Combine(_indexDir, $"index.{provider}.json");
            if (File.Exists(indexFile))
            {
                kb.Load(indexFile);
            }
            else
            {
                await kb.BuildFromFolderAsync(_docsDir, model);
                kb.Save(indexFile);
            }
            _knowledgeBases[provider] = kb;
            return kb;
        }

        public async Task<(ILanguageModel Model, KnowledgeBase Kb)> GetAsync(string provider, string? ollamaModel)
        {
            var model = await GetModelAsync(provider, ollamaModel);
            var kb = await GetKnowledgeBaseAsync(model, provider);
            return (model, kb);
        }
    }
}
