using Usecase.Auth;

namespace Handler.Auth
{
    public static class AuthHandler
    {
        public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/auth/login", LoginAsync).WithName("Login").AllowAnonymous();
            return group;
        }

        private static async Task<IResult> LoginAsync(
            LoginRequest request,
            LoginUseCase useCase)
        {
            try
            {
                var token = await useCase.ExecuteAsync(request.Email, request.Password);
                return Results.Ok(new DataResponse<LoginResponse>
                {
                    Data = new LoginResponse { AccessToken = token.Token, ExpiresAt = token.ExpiresAt }
                });
            }
            catch (InvalidCredentialsException)
            {
                return Results.Json(new ErrorResponse
                {
                    Error = new ErrorBody { Code = "INVALID_CREDENTIALS", Message = "メールアドレスまたはパスワードが正しくありません" }
                }, statusCode: StatusCodes.Status401Unauthorized);
            }
        }
    }
}
