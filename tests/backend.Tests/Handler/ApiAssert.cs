using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Backend.Tests.TestDoubles;
using Infrastructure.Auth;

namespace Backend.Tests.Handler
{
    // handler 層のテストで使う HTTP・JSON の補助
    public static class ApiAssert
    {
        // 本物の AspNetPasswordHasher のハッシュ（PBKDF2 は遅いので 1 回だけ作る）
        private static readonly Lazy<string> RealPasswordHash =
            new(() => new AspNetPasswordHasher().Hash(TestUsers.Password));

        public static string PasswordHash => RealPasswordHash.Value;

        public static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

        public static StringContent Json(object body) => Json(JsonSerializer.Serialize(body));

        // null ならボディも Content-Type も付けない。"" なら Content-Type だけ JSON の空ボディ
        public static StringContent? JsonOrNone(string? json) => json is null ? null : Json(json);

        public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
            => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        public static async Task AssertNoBodyAsync(HttpResponseMessage response)
            => Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());

        // オブジェクトのキーが過不足なく一致すること
        public static void AssertKeys(JsonElement element, params string[] expected)
        {
            var actual = element.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);
            Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), actual);
        }

        // 末尾が Z の UTC の日時であること
        public static DateTimeOffset AssertUtc(JsonElement element)
        {
            var text = element.GetString();
            Assert.NotNull(text);
            Assert.EndsWith("Z", text);
            return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
        }

        // error のみのボディで、code / message が一致し details がないこと
        public static async Task AssertErrorAsync(
            HttpResponseMessage response, HttpStatusCode status, string code, string message)
        {
            Assert.Equal(status, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            AssertKeys(json.RootElement, "error");
            var error = json.RootElement.GetProperty("error");
            Assert.Equal(code, error.GetProperty("code").GetString());
            Assert.Equal(message, error.GetProperty("message").GetString());
            Assert.False(error.TryGetProperty("details", out _));
        }

        // 422 VALIDATION_ERROR で details が 1 件だけであること。details[0] を返す
        public static async Task<(string Field, string Message)> AssertValidationErrorAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            var error = json.RootElement.GetProperty("error");
            Assert.Equal("VALIDATION_ERROR", error.GetProperty("code").GetString());
            Assert.Equal("入力内容を確認してください", error.GetProperty("message").GetString());
            var details = error.GetProperty("details");
            Assert.Equal(1, details.GetArrayLength());
            var detail = details[0];
            AssertKeys(detail, "field", "message");
            return (detail.GetProperty("field").GetString()!, detail.GetProperty("message").GetString()!);
        }
    }
}
