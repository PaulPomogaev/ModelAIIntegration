using ModelAIIntegrationCore.GigaChat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore
{
    public interface ILanguageModel
    {
        Task<float[][]> EmbedAsync(List<string> texts);
        Task<ChatMessage> ChatWithFunctionsAsync(List<ChatMessage> messages, List<FunctionDef> functions);
        Task<string> ChatAsync(List<ChatMessage> messages, double? temperature = null);
    }
}
