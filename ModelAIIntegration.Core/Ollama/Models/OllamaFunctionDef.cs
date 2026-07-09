using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Ollama.Models
{
    internal record OllamaFunctionDef(string Name, string Description, object Parameters);
}
