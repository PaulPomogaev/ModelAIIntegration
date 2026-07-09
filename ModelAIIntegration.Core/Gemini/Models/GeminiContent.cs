using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Gemini.Models
{
    internal record GeminiContent(string? Role, List<GeminiPart> Parts);
}
