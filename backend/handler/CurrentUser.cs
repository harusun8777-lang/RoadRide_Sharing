using System.Security.Claims;

namespace Handler
{
    public static class CurrentUser
    {
        // MapInboundClaims を無効にしているため、sub（ユーザーID）はそのままのクレーム名で入る
        public static string? FindSubject(this ClaimsPrincipal principal) => principal.FindFirstValue("sub");
    }
}
