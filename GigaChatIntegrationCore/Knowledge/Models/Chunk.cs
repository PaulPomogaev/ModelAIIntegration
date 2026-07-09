using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModelAIIntegrationCore.GigaChat.Models;

namespace ModelAIIntegrationCore.Knowledge.Models
{
    // Кусок лекции: из какого файла, текст, вектор смысла
    public record Chunk(string Source, string Text, float[] Vector);
}
