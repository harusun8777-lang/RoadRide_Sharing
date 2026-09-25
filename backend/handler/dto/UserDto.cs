using System.Text.Json.Serialization;

namespace Handler.Users
{
    public class RegisterUserRequest
    {
        [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
        [JsonPropertyName("first_name")] public string FirstName { get; init; } = string.Empty;
        [JsonPropertyName("last_name")] public string LastName { get; init; } = string.Empty;
        [JsonPropertyName("kana_first_name")] public string KanaFirstName { get; init; } = string.Empty;
        [JsonPropertyName("kana_last_name")] public string KanaLastName { get; init; } = string.Empty;
        [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
    }

    public class UserResponse
    {
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
        [JsonPropertyName("first_name")] public string FirstName { get; init; } = string.Empty;
        [JsonPropertyName("last_name")] public string LastName { get; init; } = string.Empty;
        [JsonPropertyName("kana_first_name")] public string KanaFirstName { get; init; } = string.Empty;
        [JsonPropertyName("kana_last_name")] public string KanaLastName { get; init; } = string.Empty;
        [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
    }
}
