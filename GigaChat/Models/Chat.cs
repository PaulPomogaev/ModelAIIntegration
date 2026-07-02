using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ConsoleAppAPI_II_GigaChat.GigaChat.Models
{
    // Сообщение в переписке. Кроме role/content может нести:
    //   • function_call — когда модель (assistant) решила вызвать функцию;
    //   • functions_state_id — служебный id, который GigaChat просит вернуть обратно;
    //   • name — имя функции, когда мы отправляем РЕЗУЛЬТАТ (role = "function").
    record ChatMessage(
        string Role,
        string? Content,
        [property: JsonPropertyName("function_call")] FunctionCall? FunctionCall = null,
        [property: JsonPropertyName("functions_state_id")] string? FunctionsStateId = null,
        string? Name = null);



    // Тело запроса к чату: модель + история (+ опц. функции, режим их вызова, температура).
    // Temperature шлём только когда нужна предсказуемость (GenerateQuiz); WhenWritingNull
    // в JsonOpts означает, что для обычных запросов поле не сериализуется (модель берёт дефолт).
    record ChatRequest(
        string Model,
        List<ChatMessage> Messages,
        List<FunctionDef>? Functions = null,
        [property: JsonPropertyName("function_call")] string? FunctionCallMode = null,
        [property: JsonPropertyName("temperature")] double? Temperature = null);

    record ChatResponse(List<Choice> Choices);  // Модель ответа от GigaChat

    record Choice(ChatMessage Message); // Один вариант ответа (выбор)     
}
