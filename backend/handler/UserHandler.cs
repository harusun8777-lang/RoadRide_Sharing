using System.Security.Claims;
using Usecase.User;
using DomainUser = Domain.Users.User;
using DomainUserRole = Domain.Users.UserRole;

namespace Handler.Users
{
    public static class UserHandler
    {
        // 登録以外は本人（JWT の sub）に対する操作。他人のユーザー情報は扱わない
        public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/users", RegisterAsync).WithName("RegisterUser").AllowAnonymous();
            group.MapGet("/users/me", GetMeAsync).WithName("GetMe");
            group.MapPost("/users/me/roles", AddRoleAsync).WithName("AddUserRole");
            group.MapPut("/users/me/active-role", SwitchRoleAsync).WithName("SwitchUserRole");
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
                    request.Password,
                    request.LastName,
                    request.FirstName,
                    request.KanaLastName,
                    request.KanaFirstName,
                    role);
                return Results.Created("/api/users/me", new DataResponse<UserResponse> { Data = ToResponse(user) });
            }
            catch (EmailAlreadyRegisteredException)
            {
                return ConflictError("このメールアドレスはすでに登録されています");
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.ParamName ?? "request", ex.Message);
            }
        }

        private static async Task<IResult> GetMeAsync(
            ClaimsPrincipal principal,
            GetCurrentUserUseCase currentUser)
        {
            var user = await currentUser.ExecuteAsync(principal.FindSubject());
            if (user is null)
                return Results.Unauthorized();

            return Results.Ok(new DataResponse<UserResponse> { Data = ToResponse(user) });
        }

        private static async Task<IResult> AddRoleAsync(
            ClaimsPrincipal principal,
            UserRoleRequest request,
            GetCurrentUserUseCase currentUser,
            AddUserRoleUseCase useCase)
        {
            var me = await currentUser.ExecuteAsync(principal.FindSubject());
            if (me is null)
                return Results.Unauthorized();

            if (!TryParseRole(request.Role, out var role))
                return ValidationError("role", "利用者区分の値が不正です");

            try
            {
                var user = await useCase.ExecuteAsync(me.Id, role);
                return Results.Ok(new DataResponse<UserResponse> { Data = ToResponse(user) });
            }
            catch (InvalidOperationException)
            {
                return ConflictError("この利用者区分は登録済みです");
            }
        }

        private static async Task<IResult> SwitchRoleAsync(
            ClaimsPrincipal principal,
            UserRoleRequest request,
            GetCurrentUserUseCase currentUser,
            SwitchUserRoleUseCase useCase)
        {
            var me = await currentUser.ExecuteAsync(principal.FindSubject());
            if (me is null)
                return Results.Unauthorized();

            if (!TryParseRole(request.Role, out var role))
                return ValidationError("role", "利用者区分の値が不正です");

            try
            {
                var user = await useCase.ExecuteAsync(me.Id, role);
                return Results.Ok(new DataResponse<UserResponse> { Data = ToResponse(user) });
            }
            catch (UnfinishedActivityExistsException ex)
            {
                return ConflictError(ex.ActiveRole == DomainUserRole.Driver
                    ? "完了またはキャンセルされていない運行があるため切り替えできません"
                    : "完了またはキャンセルされていない予約があるため切り替えできません");
            }
            catch (InvalidOperationException)
            {
                return ConflictError("この利用者区分は登録されていません");
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
            ActiveRole = ToRoleString(user.ActiveRole),
            Roles = user.Roles.Select(ToRoleString),
            CreatedAt = user.CreatedAt
        };

        private static string ToRoleString(DomainUserRole role) => role switch
        {
            DomainUserRole.Rider => "rider",
            DomainUserRole.Driver => "driver",
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };

        private static bool TryParseRole(string value, out DomainUserRole role)
        {
            switch (value)
            {
                case "rider": role = DomainUserRole.Rider; return true;
                case "driver": role = DomainUserRole.Driver; return true;
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

        private static IResult ConflictError(string message) => Results.Conflict(new ErrorResponse
        {
            Error = new ErrorBody { Code = "CONFLICT", Message = message }
        });
    }
}
