using System.Net;
using Backend.Tests.TestDoubles;
using Microsoft.IdentityModel.JsonWebTokens;
using static Backend.Tests.Handler.ApiAssert;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.Handler
{
    public class AuthHandlerTests : IClassFixture<ApiFactory>
    {
        private const string Email = "taro@example.com";
        private const string InvalidCredentialsMessage = "メールアドレスまたはパスワードが正しくありません";

        private readonly ApiFactory _factory;

        public AuthHandlerTests(ApiFactory factory)
        {
            _factory = factory;
            _factory.ResetFakes();
        }

        // 本物のハッシュでパスワードを登録したユーザー
        private DomainUser SeedUser()
        {
            var user = TestUsers.Rider(email: Email, passwordHash: PasswordHash);
            _factory.Users.Seed(user);
            return user;
        }

        private Task<HttpResponseMessage> LoginAsync(string json)
            => _factory.CreateClient().PostAsync("/api/auth/login", Json(json));

        private Task<HttpResponseMessage> LoginAsync(string email, string password)
            => _factory.CreateClient().PostAsync("/api/auth/login", Json(new { email, password }));

        // H-019
        [Fact]
        public async Task LoginAsync_正常_200とトークンを返す()
        {
            SeedUser();
            var before = DateTimeOffset.UtcNow;

            var response = await LoginAsync(Email, TestUsers.Password);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            AssertKeys(json.RootElement, "data");
            var data = json.RootElement.GetProperty("data");
            AssertKeys(data, "access_token", "token_type", "expires_at");
            Assert.Equal("Bearer", data.GetProperty("token_type").GetString());
            Assert.False(string.IsNullOrEmpty(data.GetProperty("access_token").GetString()));
            var expiresAt = AssertUtc(data.GetProperty("expires_at"));
            Assert.InRange(expiresAt, before.AddMinutes(59), DateTimeOffset.UtcNow.AddMinutes(61));
        }

        // H-020
        [Fact]
        public async Task LoginAsync_正常_発行したトークンに設定どおりのクレームが入る()
        {
            var user = SeedUser();

            var response = await LoginAsync(Email, TestUsers.Password);

            using var json = await ReadJsonAsync(response);
            var token = new JsonWebTokenHandler().ReadJsonWebToken(
                json.RootElement.GetProperty("data").GetProperty("access_token").GetString());
            Assert.Equal(user.Id.ToString(), token.Subject);
            Assert.Equal(TestJwt.Issuer, token.Issuer);
            Assert.Equal([TestJwt.Audience], token.Audiences);
            Assert.Equal("HS256", token.Alg);
        }

        // H-021
        [Fact]
        public async Task LoginAsync_正常_発行したトークンで保護APIを呼べる()
        {
            var user = SeedUser();
            var login = await LoginAsync(Email, TestUsers.Password);
            using var loginJson = await ReadJsonAsync(login);
            var token = loginJson.RootElement.GetProperty("data").GetProperty("access_token").GetString();

            var response = await _factory.CreateClient(token).GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            Assert.Equal(user.Id.ToString(), json.RootElement.GetProperty("data").GetProperty("id").GetString());
        }

        // H-022
        [Fact]
        public async Task LoginAsync_メールアドレスの前後に空白_200を返す()
        {
            SeedUser();

            var response = await LoginAsync(" taro@example.com ", TestUsers.Password);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // H-023
        [Fact]
        public async Task LoginAsync_パスワード違い_401とINVALID_CREDENTIALSを返す()
        {
            SeedUser();

            var response = await LoginAsync(Email, "wrong-password");

            await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", InvalidCredentialsMessage);
        }

        // H-024
        [Fact]
        public async Task LoginAsync_未登録のメールアドレス_401とINVALID_CREDENTIALSを返す()
        {
            SeedUser();

            var response = await LoginAsync("unknown@example.com", TestUsers.Password);

            await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", InvalidCredentialsMessage);
        }

        // H-025
        [Theory]
        [InlineData("""{"email":"","password":"password1234"}""")]
        [InlineData("""{"email":"   ","password":"password1234"}""")]
        [InlineData("""{"email":null,"password":"password1234"}""")]
        [InlineData("""{"password":"password1234"}""")]
        [InlineData("""{"email":"taro@example.com","password":""}""")]
        [InlineData("""{"email":"taro@example.com","password":"   "}""")]
        [InlineData("""{"email":"taro@example.com","password":null}""")]
        [InlineData("""{"email":"taro@example.com"}""")]
        public async Task LoginAsync_空の入力_422ではなく401を返す(string body)
        {
            SeedUser();

            var response = await LoginAsync(body);

            await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", InvalidCredentialsMessage);
        }

        // H-026
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("{")]
        public async Task LoginAsync_ボディが不正_400を返す(string? body)
        {
            var response = await _factory.CreateClient().PostAsync("/api/auth/login", JsonOrNone(body));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
