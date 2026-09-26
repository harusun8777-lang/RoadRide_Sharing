using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.Health
{
    // DB に接続できるかを確認する。接続できなければ Unhealthy（/health は 503 を返す）
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _db;
        private readonly ILogger<DatabaseHealthCheck> _logger;

        public DatabaseHealthCheck(AppDbContext db, ILogger<DatabaseHealthCheck> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _db.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("データベースに接続できません");
            }
            catch (Exception ex)
            {
                // 接続文字列などが漏れないよう、例外の詳細はレスポンスに含めずログにだけ出す
                _logger.LogError(ex, "データベースのヘルスチェックに失敗しました");
                return HealthCheckResult.Unhealthy("データベースに接続できません");
            }
        }
    }
}
