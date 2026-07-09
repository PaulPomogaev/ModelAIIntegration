using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ModelAIIntegrationCore.GigaChat.Models
{
    public record TokenResponse(
          [property: JsonPropertyName("access_token")] string AccessToken,
          [property: JsonPropertyName("expires_at")] long ExpiresAt);  // Класс для десериализации ответа токена
}
