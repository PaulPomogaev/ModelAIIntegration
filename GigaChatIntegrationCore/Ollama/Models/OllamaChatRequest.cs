using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Ollama.Models
{
    internal record OllamaChatRequest(
        string Model,
        List<OllamaMessage> Messages,
        List<OllamaTool>? Tools = null,
        bool Stream = false);
}
