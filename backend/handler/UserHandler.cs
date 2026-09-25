using Usecase.User;
using DomainUser = Domain.Users.User;
using DomainUserRole = Domain.Users.UserRole;

namespace Handler.Users
{
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
                var user = await useCase.ExecuteAsync(
                    request.Email,
                    request.LastName,
                    request.FirstName,
                    request.KanaLastName,
                    request.KanaFirstName,
                    role);
                return Results.Created($"/api/users/{user.Id}", new DataResponse<UserResponse> { Data = ToResponse(user) });
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
                return Results.Ok(new DataResponse<UserResponse> { Data = ToResponse(user) });
            }
            catch (UserNotFoundException)
            {
                return NotFoundError("指定された利用者が見つかりません");
            }
        }

        private static UserResponse ToResponse(DomainUser user) => new()
        {
            Id = user.Id.ToString(),
            Email = user.Email,
            LastName = user.LastName,
            FirstName = user.FirstName,
            KanaLastName = user.KanaLastName,
            KanaFirstName = user.KanaFirstName,
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

        private static IResult ValidationError(string field, string message) => Results.UnprocessableEntity(new ErrorResponse
        {
            Error = new ErrorBody
            {
                Code = "VALIDATION_ERROR",
                Message = "入力内容を確認してください",
                Details = [new ErrorDetail { Field = field, Message = message }]
            }
        });

        private static IResult NotFoundError(string message) => Results.NotFound(new ErrorResponse
        {
            Error = new ErrorBody { Code = "NOT_FOUND", Message = message }
        });
    }
}
