using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleAppAPI_II_GigaChat.GigaChat.Models;

namespace ConsoleAppAPI_II_GigaChat.Knowledge.Models
{
    // Результат поиска: кусок + близость к вопросу
    internal record Scored(Chunk Chunk, double Score);
}
