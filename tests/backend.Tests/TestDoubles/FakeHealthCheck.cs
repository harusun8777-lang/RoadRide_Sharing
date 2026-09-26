using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Backend.Tests.TestDoubles
{
    // /health の database チェックの代わり。返す結果を指定できる（既定は Healthy）
    public class FakeHealthCheck : IHealthCheck
    {
        public HealthCheckResult Result { get; set; } = HealthCheckResult.Healthy();

        public void Reset() => Result = HealthCheckResult.Healthy();

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(Result);
    }
}
