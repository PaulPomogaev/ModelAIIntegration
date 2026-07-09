using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Gemini.Models
{
    internal record GeminiGenerateRequest(
    List<GeminiContent> Contents,
    GeminiContent? SystemInstruction = null,
    List<GeminiTool>? Tools = null,
    GeminiToolConfig? ToolConfig = null,
    object? GenerationConfig = null);
}
