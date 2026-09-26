using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Handler.Health
{
    public static class HealthHandler
    {
        // 認証なしで呼べるヘルスチェック。正常なら 200、異常があれば 503 を返す
        public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = WriteResponseAsync
            }).AllowAnonymous();

            return app;
        }

        private static Task WriteResponseAsync(HttpContext context, HealthReport report)
        {
            context.Response.Headers.CacheControl = "no-store";

            return context.Response.WriteAsJsonAsync(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new
                    {
                        status = entry.Value.Status.ToString(),
                        description = entry.Value.Description
                    })
            });
        }
    }
}
