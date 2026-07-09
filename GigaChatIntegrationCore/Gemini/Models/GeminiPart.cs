using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Gemini.Models
{
    internal record GeminiPart(
         string? Text = null,
         GeminiFunctionCall? FunctionCall = null,
         GeminiFunctionResponse? FunctionResponse = null);
}
