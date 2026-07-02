using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleAppAPI_II_GigaChat.GigaChat.Models
{
    record EmbeddingRequest(string Model, List<string> Input); // Запрос: имя модели эмбеддингов + список текстов (input принимает массив строк).

    record EmbeddingResponse(List<EmbeddingData> Data); // Ответ: список векторов; у каждого Embedding — числа, Index — позиция текста во входе.

    record EmbeddingData(float[] Embedding, int Index); // модель информации эмбеддингов, содержащая массив векторов и индекс соотвествующего текста 
}
