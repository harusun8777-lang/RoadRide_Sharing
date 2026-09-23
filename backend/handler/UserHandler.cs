using System.Text.Json.Serialization;
using Usecase.User;
using DomainUser = Domain.Users.User;
using DomainUserRole = Domain.Users.UserRole;

namespace Handler.Users
{
    public record RegisterUserRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("role")] string Role);

    public class UserResponse
    {
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
    }

    public static class UserHandler
    {
        public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/users", RegisterAsync).WithName("RegisterUser");
            group.MapGet("/users/{userId:guid}", GetAsync).WithName("GetUser");
            return group;
        }

        private static async Task<IResult> RegisterAsync(
            RegisterUserRequest request,
            RegisterUserUseCase useCase)
        {
            if (!TryParseRole(request.Role, out var role))
                return ValidationError("role", "利用者区分の値が不正です");

            try
            {
                var user = await useCase.ExecuteAsync(request.Name, role);
                return Results.Created($"/api/users/{user.Id}", new { data = ToResponse(user) });
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.ParamName ?? "request", ex.Message);
            }
        }

        private static async Task<IResult> GetAsync(
            Guid userId,
            GetUserUseCase useCase)
        {
            try
            {
                var user = await useCase.ExecuteAsync(userId);
                return Results.Ok(new { data = ToResponse(user) });
            }
            catch (UserNotFoundException)
            {
                return NotFoundError("指定された利用者が見つかりません");
            }
        }

        private static UserResponse ToResponse(DomainUser user) => new()
        {
            Id = user.Id.ToString(),
            Name = user.Name,
            Role = ToRoleString(user.Role),
            CreatedAt = user.CreatedAt
        };

        private static string ToRoleString(DomainUserRole role) => role switch
        {
            DomainUserRole.Rider => "rider",
            DomainUserRole.Dispatcher => "dispatcher",
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };

        private static bool TryParseRole(string value, out DomainUserRole role)
        {
            switch (value)
            {
                case "rider": role = DomainUserRole.Rider; return true;
                case "dispatcher": role = DomainUserRole.Dispatcher; return true;
                default: role = default; return false;
            }
        }

        private static IResult ValidationError(string field, string message) => Results.UnprocessableEntity(new
        {
            error = new
            {
                code = "VALIDATION_ERROR",
                message = "入力内容を確認してください",
                details = new[] { new { field, message } }
            }
        });

        private static IResult NotFoundError(string message) => Results.NotFound(new
        {
            error = new { code = "NOT_FOUND", message }
        });
    }
}
