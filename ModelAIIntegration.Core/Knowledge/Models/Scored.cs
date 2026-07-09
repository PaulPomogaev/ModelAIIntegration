using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModelAIIntegrationCore.GigaChat.Models;

namespace ModelAIIntegrationCore.Knowledge.Models
{
    // Результат поиска: кусок + близость к вопросу
    public record Scored(Chunk Chunk, double Score);
}
