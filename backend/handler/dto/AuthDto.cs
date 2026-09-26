using System.Text.Json.Serialization;

namespace Handler.Auth
{
    public class LoginRequest
    {
        [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
        [JsonPropertyName("password")] public string Password { get; init; } = string.Empty;
    }

    public class LoginResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; init; } = string.Empty;
        [JsonPropertyName("token_type")] public string TokenType { get; init; } = "Bearer";
        [JsonPropertyName("expires_at")] public DateTime ExpiresAt { get; init; }
    }
}
