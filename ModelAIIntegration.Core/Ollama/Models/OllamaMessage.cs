using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Ollama.Models
{
    internal record OllamaMessage(
         string Role,
         string? Content,
         [property: JsonPropertyName("tool_calls")] List<OllamaToolCall>? ToolCalls = null);
}
