using System.Net;
using System.Text.Json;
using Backend.Tests.TestDoubles;
using Domain.Users;
using static Backend.Tests.Handler.ApiAssert;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.Handler
{
    public class UserHandlerTests : IClassFixture<ApiFactory>
    {
        private static readonly string[] UserKeys =
        [
            "id", "email", "first_name", "last_name", "kana_first_name", "kana_last_name", "active_role", "roles", "created_at"
        ];

        private const string InvalidRoleMessage = "利用者区分の値が不正です";

        private readonly ApiFactory _factory;

        public UserHandlerTests(ApiFactory factory)
        {
            _factory = factory;
            _factory.ResetFakes();
        }

        // POST /api/users の正しいボディ。キーを消したり値を変えたりして使う
        public static Dictionary<string, object?> ValidRegisterBody() => new()
        {
            ["email"] = "hanako@example.com",
            ["password"] = "password1234",
            ["last_name"] = "山田",
            ["first_name"] = "花子",
            ["kana_last_name"] = "ヤマダ",
            ["kana_first_name"] = "ハナコ",
            ["role"] = "rider"
        };

        private Task<HttpResponseMessage> RegisterAsync(object body)
            => _factory.CreateClient().PostAsync("/api/users", Json(body));

        private HttpClient ClientFor(DomainUser user)
        {
            _factory.Users.Seed(user);
            return _factory.CreateClient(TestJwt.CreateToken(user));
        }

        private static string[] Roles(JsonElement data)
            => data.GetProperty("roles").EnumerateArray().Select(r => r.GetString()!).ToArray();

        // ---- POST /api/users ----

        // H-027
        [Fact]
        public async Task RegisterAsync_riderで登録_201とユーザーを返す()
        {
            var response = await RegisterAsync(ValidRegisterBody());

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal("/api/users/me", response.Headers.Location?.OriginalString);
            using var json = await ReadJsonAsync(response);
            var data = json.RootElement.GetProperty("data");
            Assert.True(Guid.TryParse(data.GetProperty("id").GetString(), out var id));
            Assert.Equal("rider", data.GetProperty("active_role").GetString());
            Assert.Equal(["rider"], Roles(data));
            AssertUtc(data.GetProperty("created_at"));
            var saved = _factory.Users.Get(id);
            Assert.NotNull(saved);
            Assert.Equal("hanako@example.com", saved.Email);
            Assert.Equal(UserRole.Rider, saved.ActiveRole);
        }

        // H-028
        [Fact]
        public async Task RegisterAsync_driverで登録_201とdriverを返す()
        {
            var body = ValidRegisterBody();
            body["role"] = "driver";

            var response = await RegisterAsync(body);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            var data = json.RootElement.GetProperty("data");
            Assert.Equal("driver", data.GetProperty("active_role").GetString());
            Assert.Equal(["driver"], Roles(data));
        }

        // H-029
        [Fact]
        public async Task RegisterAsync_正常_JSONのキー名がスネークケースでパスワードを含まない()
        {
            var response = await RegisterAsync(ValidRegisterBody());

            using var json = await ReadJsonAsync(response);
            AssertKeys(json.RootElement, "data");
            AssertKeys(json.RootElement.GetProperty("data"), UserKeys);
        }

        // H-030
        [Theory]
        [InlineData("admin")]
        [InlineData("Rider")]
        [InlineData("")]
        [InlineData(null)]
        public async Task RegisterAsync_区分が不正_422を返す(string? role)
        {
            var body = ValidRegisterBody();
            if (role is null)
                body.Remove("role");
            else
                body["role"] = role;

            var response = await RegisterAsync(body);

            var (field, message) = await AssertValidationErrorAsync(response);
            Assert.Equal("role", field);
            Assert.Equal(InvalidRoleMessage, message);
        }

        // H-031
        [Fact]
        public async Task RegisterAsync_パスワードが7文字_422を返す()
        {
            var body = ValidRegisterBody();
            body["password"] = "1234567";

            var response = await RegisterAsync(body);

            var (field, _) = await AssertValidationErrorAsync(response);
            Assert.Equal("password", field);
        }

        // H-032
        [Fact]
        public async Task RegisterAsync_メールアドレス重複_409とCONFLICTを返す()
        {
            _factory.Users.Seed(TestUsers.Rider(email: "hanako@example.com"));

            var response = await RegisterAsync(ValidRegisterBody());

            await AssertErrorAsync(response, HttpStatusCode.Conflict, "CONFLICT", "このメールアドレスはすでに登録されています");
        }

        // H-033
        [Theory]
        [InlineData("email", "email")]
        [InlineData("last_name", "lastName")]
        [InlineData("first_name", "firstName")]
        public async Task RegisterAsync_必須項目が空白のみ_422とキャメルケースのfieldを返す(string key, string expectedField)
        {
            var body = ValidRegisterBody();
            body[key] = "   ";

            var response = await RegisterAsync(body);

            var (field, _) = await AssertValidationErrorAsync(response);
            Assert.Equal(expectedField, field);
        }

        // H-034
        [Theory]
        [InlineData("kana_last_name", "kanaLastName")]
        [InlineData("kana_first_name", "kanaFirstName")]
        public async Task RegisterAsync_読み仮名がひらがな_422と英語のメッセージを返す(string key, string expectedField)
        {
            var body = ValidRegisterBody();
            body[key] = "やまだ";

            var response = await RegisterAsync(body);

            var (field, message) = await AssertValidationErrorAsync(response);
            Assert.Equal(expectedField, field);
            Assert.Equal($"{expectedField} must be full-width katakana. (Parameter '{expectedField}')", message);
        }

        // H-035
        [Fact]
        public async Task RegisterAsync_区分とパスワードが両方不正_roleの1件だけを返す()
        {
            var body = ValidRegisterBody();
            body["role"] = "admin";
            body["password"] = "1234567";

            var response = await RegisterAsync(body);

            var (field, _) = await AssertValidationErrorAsync(response);
            Assert.Equal("role", field);
        }

        // H-036
        [Fact]
        public async Task RegisterAsync_パスワード不正かつメールアドレス重複_409ではなく422を返す()
        {
            _factory.Users.Seed(TestUsers.Rider(email: "hanako@example.com"));
            var body = ValidRegisterBody();
            body["password"] = "1234567";

            var response = await RegisterAsync(body);

            var (field, _) = await AssertValidationErrorAsync(response);
            Assert.Equal("password", field);
        }

        // H-037
        [Fact]
        public async Task RegisterAsync_姓の前後に空白_空白を除いて登録する()
        {
            var body = ValidRegisterBody();
            body["last_name"] = " 山田 ";

            var response = await RegisterAsync(body);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            Assert.Equal("山田", json.RootElement.GetProperty("data").GetProperty("last_name").GetString());
        }

        // H-038
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("{")]
        public async Task RegisterAsync_ボディが不正_400を返す(string? body)
        {
            var response = await _factory.CreateClient().PostAsync("/api/users", JsonOrNone(body));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---- GET /api/users/me ----

        // H-039
        [Fact]
        public async Task GetMeAsync_riderのユーザー_200とユーザー情報を返す()
        {
            var user = TestUsers.Rider(email: "me@example.com");
            var client = ClientFor(user);

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            AssertKeys(json.RootElement, "data");
            var data = json.RootElement.GetProperty("data");
            AssertKeys(data, UserKeys);
            Assert.Equal(user.Id.ToString(), data.GetProperty("id").GetString());
            Assert.Equal("me@example.com", data.GetProperty("email").GetString());
            Assert.Equal(user.LastName, data.GetProperty("last_name").GetString());
            Assert.Equal(user.FirstName, data.GetProperty("first_name").GetString());
            Assert.Equal(user.KanaLastName, data.GetProperty("kana_last_name").GetString());
            Assert.Equal(user.KanaFirstName, data.GetProperty("kana_first_name").GetString());
            Assert.Equal("rider", data.GetProperty("active_role").GetString());
            Assert.Equal(["rider"], Roles(data));
            Assert.Equal(new DateTimeOffset(user.CreatedAt), AssertUtc(data.GetProperty("created_at")));
        }

        // H-040
        [Fact]
        public async Task GetMeAsync_区分を両方持ちdriverで稼働_両方の区分とdriverを返す()
        {
            var client = ClientFor(TestUsers.Both(UserRole.Driver));

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            var data = json.RootElement.GetProperty("data");
            Assert.Equal(["rider", "driver"], Roles(data));
            Assert.Equal("driver", data.GetProperty("active_role").GetString());
        }

        // H-041
        [Fact]
        public async Task GetMeAsync_複数ユーザーがいる_トークンの本人を返す()
        {
            var a = TestUsers.Rider();
            var b = TestUsers.Rider();
            _factory.Users.Seed(b);
            var client = ClientFor(a);

            var response = await client.GetAsync("/api/users/me");

            using var json = await ReadJsonAsync(response);
            Assert.Equal(a.Id.ToString(), json.RootElement.GetProperty("data").GetProperty("id").GetString());
        }

        // ---- POST /api/users/me/roles ----

        // H-042
        [Fact]
        public async Task AddRoleAsync_riderにdriverを追加_200と両方の区分を返す()
        {
            var user = TestUsers.Rider();
            var client = ClientFor(user);

            var response = await client.PostAsync("/api/users/me/roles", Json("""{"role":"driver"}"""));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            var data = json.RootElement.GetProperty("data");
            AssertKeys(data, UserKeys);
            Assert.Equal(["rider", "driver"], Roles(data));
            Assert.Equal("rider", data.GetProperty("active_role").GetString());
            var saved = _factory.Users.Get(user.Id)!;
            Assert.True(saved.HasRole(UserRole.Driver));
            Assert.Contains(saved, _factory.Users.Updated);
        }

        // H-043
        [Fact]
        public async Task AddRoleAsync_登録済みの区分_409とCONFLICTを返す()
        {
            var client = ClientFor(TestUsers.Rider());

            var response = await client.PostAsync("/api/users/me/roles", Json("""{"role":"rider"}"""));

            await AssertErrorAsync(response, HttpStatusCode.Conflict, "CONFLICT", "この利用者区分は登録済みです");
        }

        // H-044
        [Fact]
        public async Task AddRoleAsync_区分が不正_422を返す()
        {
            var client = ClientFor(TestUsers.Rider());

            var response = await client.PostAsync("/api/users/me/roles", Json("""{"role":"admin"}"""));

            var (field, message) = await AssertValidationErrorAsync(response);
            Assert.Equal("role", field);
            Assert.Equal(InvalidRoleMessage, message);
        }

        // H-045
        [Fact]
        public async Task AddRoleAsync_ユーザー不在かつ区分が不正_422ではなく401を返す()
        {
            var client = _factory.CreateClient(TestJwt.CreateCustomToken(Guid.NewGuid().ToString()));

            var response = await client.PostAsync("/api/users/me/roles", Json("""{"role":"admin"}"""));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // H-046
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddRoleAsync_ボディなし_400を返す(string? body)
        {
            var client = ClientFor(TestUsers.Rider());

            var response = await client.PostAsync("/api/users/me/roles", JsonOrNone(body));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---- PUT /api/users/me/active-role ----

        // H-047
        [Fact]
        public async Task SwitchRoleAsync_未完了の予約なし_200と切り替え後の区分を返す()
        {
            var user = TestUsers.Both(UserRole.Rider);
            var client = ClientFor(user);

            var response = await client.PutAsync("/api/users/me/active-role", Json("""{"role":"driver"}"""));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            var data = json.RootElement.GetProperty("data");
            AssertKeys(data, UserKeys);
            Assert.Equal("driver", data.GetProperty("active_role").GetString());
            Assert.Equal(UserRole.Driver, _factory.Users.Get(user.Id)!.ActiveRole);
            Assert.Contains(user, _factory.Users.Updated);
        }

        // H-048
        [Fact]
        public async Task SwitchRoleAsync_同じ区分で未完了の予約あり_200を返す()
        {
            var user = TestUsers.Both(UserRole.Rider);
            _factory.Reservations.UsersWithUnfinished.Add(user.Id);
            var client = ClientFor(user);

            var response = await client.PutAsync("/api/users/me/active-role", Json("""{"role":"rider"}"""));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            Assert.Equal("rider", json.RootElement.GetProperty("data").GetProperty("active_role").GetString());
        }

        // H-049
        [Fact]
        public async Task SwitchRoleAsync_未完了の予約あり_409とCONFLICTを返す()
        {
            var user = TestUsers.Both(UserRole.Rider);
            _factory.Reservations.UsersWithUnfinished.Add(user.Id);
            var client = ClientFor(user);

            var response = await client.PutAsync("/api/users/me/active-role", Json("""{"role":"driver"}"""));

            await AssertErrorAsync(response, HttpStatusCode.Conflict, "CONFLICT",
                "完了またはキャンセルされていない予約があるため切り替えできません");
            Assert.Equal(UserRole.Rider, _factory.Users.Get(user.Id)!.ActiveRole);
        }

        // H-050
        [Fact]
        public async Task SwitchRoleAsync_未完了の運行あり_409とCONFLICTを返す()
        {
            var user = TestUsers.Both(UserRole.Driver);
            _factory.RideGroups.DriversWithUnfinished.Add(user.Id);
            var client = ClientFor(user);

            var response = await client.PutAsync("/api/users/me/active-role", Json("""{"role":"rider"}"""));

            await AssertErrorAsync(response, HttpStatusCode.Conflict, "CONFLICT",
                "完了またはキャンセルされていない運行があるため切り替えできません");
            Assert.Equal(UserRole.Driver, _factory.Users.Get(user.Id)!.ActiveRole);
        }

        // H-051
        [Fact]
        public async Task SwitchRoleAsync_未登録の区分_409とCONFLICTを返す()
        {
            var client = ClientFor(TestUsers.Rider());

            var response = await client.PutAsync("/api/users/me/active-role", Json("""{"role":"driver"}"""));

            await AssertErrorAsync(response, HttpStatusCode.Conflict, "CONFLICT", "この利用者区分は登録されていません");
        }

        // H-052
        [Fact]
        public async Task SwitchRoleAsync_区分が不正_422を返す()
        {
            var client = ClientFor(TestUsers.Both(UserRole.Rider));

            var response = await client.PutAsync("/api/users/me/active-role", Json("""{"role":"Driver"}"""));

            var (field, _) = await AssertValidationErrorAsync(response);
            Assert.Equal("role", field);
        }

        // H-053
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task SwitchRoleAsync_ボディなし_400を返す(string? body)
        {
            var client = ClientFor(TestUsers.Both(UserRole.Rider));

            var response = await client.PutAsync("/api/users/me/active-role", JsonOrNone(body));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
