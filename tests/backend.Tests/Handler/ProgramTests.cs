using System.Net;
using System.Net.Http.Headers;
using Backend.Tests.TestDoubles;
using Microsoft.IdentityModel.Tokens;
using static Backend.Tests.Handler.ApiAssert;

namespace Backend.Tests.Handler
{
    // Program.cs の認証・認可の設定を確認する
    public class ProgramTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public ProgramTests(ApiFactory factory)
        {
            _factory = factory;
            _factory.ResetFakes();
        }

        // 保護 API 7 本。ボディを取るものには正しいボディを付ける
        public static TheoryData<string> ProtectedEndpoints => new()
        {
            "GET /api/users/me",
            "POST /api/users/me/roles",
            "PUT /api/users/me/active-role",
            "POST /api/reservations",
            "GET /api/reservations",
            "GET /api/reservations/{id}",
            "POST /api/reservations/{id}/cancel"
        };

        private static HttpRequestMessage BuildProtectedRequest(string endpoint)
        {
            var id = Guid.NewGuid();
            return endpoint switch
            {
                "GET /api/users/me" => new(HttpMethod.Get, "/api/users/me"),
                "POST /api/users/me/roles" => new(HttpMethod.Post, "/api/users/me/roles") { Content = Json("""{"role":"driver"}""") },
                "PUT /api/users/me/active-role" => new(HttpMethod.Put, "/api/users/me/active-role") { Content = Json("""{"role":"driver"}""") },
                "POST /api/reservations" => new(HttpMethod.Post, "/api/reservations")
                {
                    Content = Json("""{"pickup_location":"市役所前","destination":"中央病院","requested_pickup_at":"2026-10-01T00:00:00Z","passenger_count":1}""")
                },
                "GET /api/reservations" => new(HttpMethod.Get, "/api/reservations"),
                "GET /api/reservations/{id}" => new(HttpMethod.Get, $"/api/reservations/{id}"),
                "POST /api/reservations/{id}/cancel" => new(HttpMethod.Post, $"/api/reservations/{id}/cancel") { Content = Json("""{"reason":"予定が変わったため"}""") },
                _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
            };
        }

        // H-001
        [Theory]
        [MemberData(nameof(ProtectedEndpoints))]
        public async Task Authentication_トークンなし_401とWWWAuthenticateを返す(string endpoint)
        {
            var client = _factory.CreateClient();

            var response = await client.SendAsync(BuildProtectedRequest(endpoint));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await AssertNoBodyAsync(response);
            Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        }

        // H-002
        [Fact]
        public async Task Authentication_別の鍵で署名_401を返す()
        {
            var user = TestUsers.Rider();
            _factory.Users.Seed(user);
            var client = _factory.CreateClient(TestJwt.CreateCustomToken(user.Id.ToString(), signingKey: TestJwt.OtherSigningKey));

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await AssertNoBodyAsync(response);
        }

        // H-003
        [Fact]
        public async Task Authentication_期限切れ_401を返す()
        {
            var user = TestUsers.Rider();
            _factory.Users.Seed(user);
            var client = _factory.CreateClient(TestJwt.CreateCustomToken(user.Id.ToString(), expires: DateTime.UtcNow.AddMinutes(-5)));

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await AssertNoBodyAsync(response);
        }

        // H-004
        [Fact]
        public async Task Authentication_期限切れが許容誤差の内側_200を返す()
        {
            var user = TestUsers.Rider();
            _factory.Users.Seed(user);
            var client = _factory.CreateClient(TestJwt.CreateCustomToken(user.Id.ToString(), expires: DateTime.UtcNow.AddSeconds(-10)));

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // H-005
        [Fact]
        public async Task Authentication_発行者が違う_401を返す()
        {
            var user = TestUsers.Rider();
            _factory.Users.Seed(user);
            var client = _factory.CreateClient(TestJwt.CreateCustomToken(user.Id.ToString(), issuer: "other-issuer"));

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // H-006
        [Fact]
        public async Task Authentication_対象者が違う_401を返す()
        {
            var user = TestUsers.Rider();
            _factory.Users.Seed(user);
            var client = _factory.CreateClient(TestJwt.CreateCustomToken(user.Id.ToString(), audience: "other-audience"));

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // H-007
        [Fact]
        public async Task Authentication_HS512で署名_401を返す()
        {
            var user = TestUsers.Rider();
            _factory.Users.Seed(user);
            var client = _factory.CreateClient(TestJwt.CreateCustomToken(user.Id.ToString(), algorithm: SecurityAlgorithms.HmacSha512));

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // H-008
        [Theory]
        [InlineData("Bearer", "abc")]
        [InlineData("Basic", "xxx")]
        public async Task Authentication_形式が不正_401を返す(string scheme, string parameter)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme, parameter);

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // H-009
        [Fact]
        public async Task Authentication_subなし_401を返す()
        {
            var client = _factory.CreateClient(TestJwt.CreateCustomToken(subject: null));

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await AssertNoBodyAsync(response);
        }

        // H-010
        [Fact]
        public async Task Authentication_subがGUIDでない_401を返す()
        {
            var client = _factory.CreateClient(TestJwt.CreateCustomToken("abc"));

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await AssertNoBodyAsync(response);
        }

        // H-011
        [Theory]
        [MemberData(nameof(ProtectedEndpoints))]
        public async Task Authentication_ユーザー不在_401を返す(string endpoint)
        {
            var client = _factory.CreateClient(TestJwt.CreateCustomToken(Guid.NewGuid().ToString()));

            var response = await client.SendAsync(BuildProtectedRequest(endpoint));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await AssertNoBodyAsync(response);
        }

        public static TheoryData<string> AnonymousEndpoints => new()
        {
            "POST /api/auth/login",
            "POST /api/users",
            "GET /health"
        };

        // 匿名で呼べるエンドポイントに正しい入力を送り、期待するステータスを返す
        private async Task<(HttpResponseMessage Response, HttpStatusCode Expected)> SendAnonymousAsync(HttpClient client, string endpoint)
        {
            switch (endpoint)
            {
                case "POST /api/auth/login":
                    var user = TestUsers.Rider(email: "login@example.com", passwordHash: PasswordHash);
                    _factory.Users.Seed(user);
                    return (await client.PostAsync("/api/auth/login",
                        Json(new { email = "login@example.com", password = TestUsers.Password })), HttpStatusCode.OK);
                case "POST /api/users":
                    return (await client.PostAsync("/api/users", Json(UserHandlerTests.ValidRegisterBody())), HttpStatusCode.Created);
                case "GET /health":
                    return (await client.GetAsync("/health"), HttpStatusCode.OK);
                default:
                    throw new ArgumentOutOfRangeException(nameof(endpoint));
            }
        }

        // H-012
        [Theory]
        [MemberData(nameof(AnonymousEndpoints))]
        public async Task Authorization_匿名許可のエンドポイント_トークンなしで成功する(string endpoint)
        {
            var client = _factory.CreateClient();

            var (response, expected) = await SendAnonymousAsync(client, endpoint);

            Assert.Equal(expected, response.StatusCode);
        }

        // H-013
        [Theory]
        [MemberData(nameof(AnonymousEndpoints))]
        public async Task Authorization_匿名許可に不正トークン_401にならない(string endpoint)
        {
            var client = _factory.CreateClient(TestJwt.CreateCustomToken(Guid.NewGuid().ToString(), signingKey: TestJwt.OtherSigningKey));

            var (response, expected) = await SendAnonymousAsync(client, endpoint);

            Assert.Equal(expected, response.StatusCode);
        }
    }
}
