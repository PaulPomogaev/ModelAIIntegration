using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleAppAPI_II_GigaChat.GigaChat.Models;

namespace ConsoleAppAPI_II_GigaChat.Knowledge.Models
{
    // Кусок лекции: из какого файла, текст, вектор смысла
    internal record Chunk(string Source, string Text, float[] Vector);
}
