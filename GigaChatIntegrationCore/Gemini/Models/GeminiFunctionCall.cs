using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.Gemini.Models
{
    internal record GeminiFunctionCall(string Name, JsonElement Args);
}
