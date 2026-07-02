using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConsoleAppAPI_II_GigaChat.GigaChat.Models
{
    record FunctionCall(string Name, JsonElement Arguments); // Вызов функции от модели: имя + аргументы. У GigaChat arguments — это JSON-ОБЪЕКТ, поэтому читаем его универсально как JsonElement (а не как строку, как в OpenAI).

    record FunctionDef(string Name, string Description, object Parameters); // Описание функции для модели: имя, что делает, и схема параметров (JSON Schema).
}
