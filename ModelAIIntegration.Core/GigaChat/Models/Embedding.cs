using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.GigaChat.Models
{
    public record EmbeddingRequest(string Model, List<string> Input); // Запрос: имя модели эмбеддингов + список текстов (input принимает массив строк).

    public record EmbeddingResponse(List<EmbeddingData> Data); // Ответ: список векторов; у каждого Embedding — числа, Index — позиция текста во входе.

    public record EmbeddingData(float[] Embedding, int Index); // модель информации эмбеддингов, содержащая массив векторов и индекс соотвествующего текста 
}
