using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using static Backend.Tests.Handler.ApiAssert;

namespace Backend.Tests.Handler
{
    public class HealthHandlerTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public HealthHandlerTests(ApiFactory factory)
        {
            _factory = factory;
            _factory.ResetFakes();
        }

        // H-014
        [Fact]
        public async Task WriteResponseAsync_正常_200とHealthyを返す()
        {
            var response = await _factory.CreateClient().GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            var root = json.RootElement;
            AssertKeys(root, "status", "checks");
            Assert.Equal("Healthy", root.GetProperty("status").GetString());
            var checks = root.GetProperty("checks");
            AssertKeys(checks, "database");
            var database = checks.GetProperty("database");
            AssertKeys(database, "status", "description");
            Assert.Equal("Healthy", database.GetProperty("status").GetString());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, database.GetProperty("description").ValueKind);
        }

        // H-015
        [Fact]
        public async Task WriteResponseAsync_異常_503とUnhealthyを返す()
        {
            _factory.Health.Result = HealthCheckResult.Unhealthy("データベースに接続できません");

            var response = await _factory.CreateClient().GetAsync("/health");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            Assert.Equal("Unhealthy", json.RootElement.GetProperty("status").GetString());
            var database = json.RootElement.GetProperty("checks").GetProperty("database");
            Assert.Equal("Unhealthy", database.GetProperty("status").GetString());
            Assert.Equal("データベースに接続できません", database.GetProperty("description").GetString());
        }

        // H-016
        [Theory]
        [InlineData(HealthStatus.Healthy, HttpStatusCode.OK)]
        [InlineData(HealthStatus.Unhealthy, HttpStatusCode.ServiceUnavailable)]
        public async Task WriteResponseAsync_正常でも異常でも_キャッシュを禁止する(HealthStatus status, HttpStatusCode expected)
        {
            _factory.Health.Result = new HealthCheckResult(status);

            var response = await _factory.CreateClient().GetAsync("/health");

            Assert.Equal(expected, response.StatusCode);
            Assert.NotNull(response.Headers.CacheControl);
            Assert.True(response.Headers.CacheControl!.NoStore);
        }

        // H-017
        [Fact]
        public async Task WriteResponseAsync_正常_ContentTypeがJSON()
        {
            var response = await _factory.CreateClient().GetAsync("/health");

            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        }

        // H-018
        [Fact]
        public async Task MapHealthEndpoints_Degraded_200とDegradedを返す()
        {
            _factory.Health.Result = HealthCheckResult.Degraded();

            var response = await _factory.CreateClient().GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            Assert.Equal("Degraded", json.RootElement.GetProperty("status").GetString());
        }
    }
}
