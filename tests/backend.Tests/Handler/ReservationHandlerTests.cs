using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Tests.TestDoubles;
using Domain.Reservations;
using Domain.Users;
using static Backend.Tests.Handler.ApiAssert;
using DomainUser = Domain.Users.User;

namespace Backend.Tests.Handler
{
    public class ReservationHandlerTests : IClassFixture<ApiFactory>
    {
        private static readonly string[] ReservationKeys =
        [
            "id", "reservation_number", "user_id", "pickup_location", "destination", "requested_pickup_at",
            "passenger_count", "consideration_notes", "status", "cancellation_reason", "cancelled_at", "created_at"
        ];

        private static readonly string[] SummaryKeys =
        [
            "id", "reservation_number", "pickup_location", "destination", "requested_pickup_at", "passenger_count", "status"
        ];

        private const string NotFoundMessage = "指定された予約が見つかりません";

        private readonly ApiFactory _factory;

        public ReservationHandlerTests(ApiFactory factory)
        {
            _factory = factory;
            _factory.ResetFakes();
        }

        // POST /api/reservations の正しいボディ。キーを消したり値を変えたりして使う
        private static Dictionary<string, object?> ValidRegisterBody() => new()
        {
            ["pickup_location"] = "市役所前",
            ["destination"] = "中央病院",
            ["requested_pickup_at"] = "2026-10-01T00:00:00Z",
            ["passenger_count"] = 2,
            ["consideration_notes"] = "車椅子を使用"
        };

        private HttpClient ClientFor(DomainUser user)
        {
            _factory.Users.Seed(user);
            return _factory.CreateClient(TestJwt.CreateToken(user));
        }

        // 稼働 rider のユーザーでログインしたクライアント
        private (DomainUser Me, HttpClient Client) LoginAsRider()
        {
            var me = TestUsers.Rider();
            return (me, ClientFor(me));
        }

        // ---- POST /api/reservations ----

        // H-054
        [Fact]
        public async Task RegisterAsync_正常_201と予約を返す()
        {
            var (me, client) = LoginAsRider();

            var response = await client.PostAsync("/api/reservations", Json(ValidRegisterBody()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            var data = json.RootElement.GetProperty("data");
            var id = data.GetProperty("id").GetString();
            Assert.Equal($"/api/reservations/{id}", response.Headers.Location?.OriginalString);
            Assert.Equal("matching", data.GetProperty("status").GetString());
            Assert.Equal(me.Id.ToString(), data.GetProperty("user_id").GetString());
            Assert.Matches(new Regex(@"^RR-\d{8}-[0-9A-F]{4}$"), data.GetProperty("reservation_number").GetString());
            Assert.Equal("市役所前", data.GetProperty("pickup_location").GetString());
            Assert.Equal("中央病院", data.GetProperty("destination").GetString());
            Assert.Equal(2, data.GetProperty("passenger_count").GetInt32());
            Assert.Equal("車椅子を使用", data.GetProperty("consideration_notes").GetString());
            Assert.Equal(JsonValueKind.Null, data.GetProperty("cancellation_reason").ValueKind);
            Assert.Equal(JsonValueKind.Null, data.GetProperty("cancelled_at").ValueKind);
            var saved = _factory.Reservations.Get(Guid.Parse(id!));
            Assert.NotNull(saved);
            Assert.Equal(me.Id, saved.UserId);
            Assert.Equal(ReservationStatus.Matching, saved.Status);
        }

        // H-055
        [Fact]
        public async Task RegisterAsync_正常_JSONのキー名がスネークケースでnullの項目もキーがある()
        {
            var (_, client) = LoginAsRider();

            var response = await client.PostAsync("/api/reservations", Json(ValidRegisterBody()));

            using var json = await ReadJsonAsync(response);
            AssertKeys(json.RootElement, "data");
            AssertKeys(json.RootElement.GetProperty("data"), ReservationKeys);
        }

        // H-056
        [Fact]
        public async Task RegisterAsync_UTCの日時_末尾Zで返す()
        {
            var (_, client) = LoginAsRider();

            var response = await client.PostAsync("/api/reservations", Json(ValidRegisterBody()));

            using var json = await ReadJsonAsync(response);
            var data = json.RootElement.GetProperty("data");
            Assert.Equal("2026-10-01T00:00:00Z", data.GetProperty("requested_pickup_at").GetString());
            AssertUtc(data.GetProperty("created_at"));
        }

        // H-057
        [Fact]
        public async Task RegisterAsync_配慮事項なし_201とnullを返す()
        {
            var (_, client) = LoginAsRider();
            var body = ValidRegisterBody();
            body.Remove("consideration_notes");

            var response = await client.PostAsync("/api/reservations", Json(body));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            var data = json.RootElement.GetProperty("data");
            Assert.True(data.TryGetProperty("consideration_notes", out var notes));
            Assert.Equal(JsonValueKind.Null, notes.ValueKind);
        }

        // H-058
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RegisterAsync_稼働区分がriderでない_409とCONFLICTを返す(bool alsoRider)
        {
            var me = alsoRider ? TestUsers.Both(UserRole.Driver) : TestUsers.Driver();
            var client = ClientFor(me);

            var response = await client.PostAsync("/api/reservations", Json(ValidRegisterBody()));

            await AssertErrorAsync(response, HttpStatusCode.Conflict, "CONFLICT", "利用者として稼働していないため予約できません");
            Assert.Empty(_factory.Reservations.Added);
        }

        // H-059
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RegisterAsync_乗車地が空_422を返す(string pickupLocation)
        {
            var (_, client) = LoginAsRider();
            var body = ValidRegisterBody();
            body["pickup_location"] = pickupLocation;

            var response = await client.PostAsync("/api/reservations", Json(body));

            var (field, _) = await AssertValidationErrorAsync(response);
            Assert.Equal("pickupLocation", field);
        }

        // H-060
        [Fact]
        public async Task RegisterAsync_目的地が空_422を返す()
        {
            var (_, client) = LoginAsRider();
            var body = ValidRegisterBody();
            body["destination"] = "";

            var response = await client.PostAsync("/api/reservations", Json(body));

            var (field, _) = await AssertValidationErrorAsync(response);
            Assert.Equal("destination", field);
        }

        // H-061
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(null)]
        public async Task RegisterAsync_乗車人数が1未満_422を返す(int? passengerCount)
        {
            var (_, client) = LoginAsRider();
            var body = ValidRegisterBody();
            if (passengerCount is null)
                body.Remove("passenger_count");
            else
                body["passenger_count"] = passengerCount;

            var response = await client.PostAsync("/api/reservations", Json(body));

            var (field, _) = await AssertValidationErrorAsync(response);
            Assert.Equal("passengerCount", field);
        }

        // H-062
        [Fact]
        public async Task RegisterAsync_キャメルケースのキー_値として受け取らず422を返す()
        {
            var (_, client) = LoginAsRider();
            var body = ValidRegisterBody();
            body.Remove("pickup_location");
            body["pickupLocation"] = "市役所前";

            var response = await client.PostAsync("/api/reservations", Json(body));

            var (field, _) = await AssertValidationErrorAsync(response);
            Assert.Equal("pickupLocation", field);
        }

        // H-063
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("""{"pickup_location":"市役所前","destination":"中央病院","requested_pickup_at":"2026-10-01T00:00:00Z","passenger_count":"abc"}""")]
        public async Task RegisterAsync_ボディが不正_400を返す(string? body)
        {
            var (_, client) = LoginAsRider();

            var response = await client.PostAsync("/api/reservations", JsonOrNone(body));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---- GET /api/reservations ----

        // H-064
        [Fact]
        public async Task ListAsync_クエリなし_200と一覧とmetaを返す()
        {
            var (me, client) = LoginAsRider();
            _factory.Reservations.ListResult = ([TestReservations.Matching(me.Id), TestReservations.Matching(me.Id)], 5);

            var response = await client.GetAsync("/api/reservations");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            Assert.Equal(2, json.RootElement.GetProperty("data").GetArrayLength());
            var meta = json.RootElement.GetProperty("meta");
            Assert.Equal(1, meta.GetProperty("page").GetInt32());
            Assert.Equal(50, meta.GetProperty("limit").GetInt32());
            Assert.Equal(5, meta.GetProperty("total").GetInt32());
        }

        // H-065
        [Fact]
        public async Task ListAsync_正常_JSONのキー名が一覧用の項目だけ()
        {
            var (me, client) = LoginAsRider();
            _factory.Reservations.ListResult = ([TestReservations.Matching(me.Id), TestReservations.Matching(me.Id)], 5);

            var response = await client.GetAsync("/api/reservations");

            using var json = await ReadJsonAsync(response);
            AssertKeys(json.RootElement, "data", "meta");
            foreach (var item in json.RootElement.GetProperty("data").EnumerateArray())
                AssertKeys(item, SummaryKeys);
            AssertKeys(json.RootElement.GetProperty("meta"), "page", "limit", "total");
        }

        // H-066
        [Fact]
        public async Task ListAsync_0件_空の配列を返す()
        {
            var (_, client) = LoginAsRider();

            var response = await client.GetAsync("/api/reservations");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("data").ValueKind);
            Assert.Equal(0, json.RootElement.GetProperty("data").GetArrayLength());
            Assert.Equal(0, json.RootElement.GetProperty("meta").GetProperty("total").GetInt32());
        }

        // H-067
        [Fact]
        public async Task ListAsync_他人のuser_idを指定_本人に限定する()
        {
            var (me, client) = LoginAsRider();

            await client.GetAsync($"/api/reservations?user_id={Guid.NewGuid()}");

            Assert.Equal(me.Id, _factory.Reservations.LastListFilter?.UserId);
        }

        // H-068
        [Fact]
        public async Task ListAsync_クエリを指定_フィルターにそのまま渡す()
        {
            var (me, client) = LoginAsRider();

            var response = await client.GetAsync(
                "/api/reservations?date=2026-10-01&status=confirmed&from=2026-10-01T00:00:00Z&to=2026-10-02T00:00:00Z&page=2&limit=20");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var filter = _factory.Reservations.LastListFilter;
            Assert.NotNull(filter);
            Assert.Equal(me.Id, filter.UserId);
            Assert.Equal(new DateOnly(2026, 10, 1), filter.Date);
            Assert.Equal(ReservationStatus.Confirmed, filter.Status);
            Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), filter.From!.Value.ToUniversalTime());
            Assert.Equal(new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), filter.To!.Value.ToUniversalTime());
            Assert.Equal(2, filter.Page);
            Assert.Equal(20, filter.Limit);
            using var json = await ReadJsonAsync(response);
            var meta = json.RootElement.GetProperty("meta");
            Assert.Equal(2, meta.GetProperty("page").GetInt32());
            Assert.Equal(20, meta.GetProperty("limit").GetInt32());
        }

        public static TheoryData<string, ReservationStatus> StatusPairs => new()
        {
            { "matching", ReservationStatus.Matching },
            { "confirmed", ReservationStatus.Confirmed },
            { "in_progress", ReservationStatus.InProgress },
            { "completed", ReservationStatus.Completed },
            { "cancelled", ReservationStatus.Cancelled }
        };

        // H-069
        [Theory]
        [MemberData(nameof(StatusPairs))]
        public async Task ListAsync_状態を指定_対応する列挙値に変換する(string query, ReservationStatus expected)
        {
            var (_, client) = LoginAsRider();

            var response = await client.GetAsync($"/api/reservations?status={query}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expected, _factory.Reservations.LastListFilter?.Status);
        }

        // H-070
        [Theory]
        [MemberData(nameof(StatusPairs))]
        public async Task ListAsync_各状態の予約_状態を文字列に変換する(string expected, ReservationStatus status)
        {
            var (me, client) = LoginAsRider();
            _factory.Reservations.ListResult = ([TestReservations.InStatus(status, me.Id)], 1);

            var response = await client.GetAsync("/api/reservations");

            using var json = await ReadJsonAsync(response);
            Assert.Equal(expected, json.RootElement.GetProperty("data")[0].GetProperty("status").GetString());
        }

        // H-071
        [Theory]
        [InlineData("MATCHING")]
        [InlineData("InProgress")]
        [InlineData("unknown")]
        public async Task ListAsync_状態が不正_422を返す(string status)
        {
            var (_, client) = LoginAsRider();

            var response = await client.GetAsync($"/api/reservations?status={status}");

            var (field, message) = await AssertValidationErrorAsync(response);
            Assert.Equal("status", field);
            Assert.Equal("予約状態の値が不正です", message);
            Assert.Empty(_factory.Reservations.ListCalls);
        }

        // H-072
        [Theory]
        [InlineData("status=")]
        [InlineData("status=%20")]
        public async Task ListAsync_状態が空_絞り込まない(string query)
        {
            var (_, client) = LoginAsRider();

            var response = await client.GetAsync($"/api/reservations?{query}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var filter = _factory.Reservations.LastListFilter;
            Assert.NotNull(filter);
            Assert.Null(filter.Status);
        }

        // H-073
        [Fact]
        public async Task ListAsync_pageとlimitが0_補正せずに渡す()
        {
            var (_, client) = LoginAsRider();

            var response = await client.GetAsync("/api/reservations?page=0&limit=0");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            var meta = json.RootElement.GetProperty("meta");
            Assert.Equal(0, meta.GetProperty("page").GetInt32());
            Assert.Equal(0, meta.GetProperty("limit").GetInt32());
            var filter = _factory.Reservations.LastListFilter;
            Assert.NotNull(filter);
            Assert.Equal(0, filter.Page);
            Assert.Equal(0, filter.Limit);
        }

        // H-074
        [Theory]
        [InlineData("date=abc")]
        [InlineData("from=abc")]
        [InlineData("to=abc")]
        [InlineData("page=abc")]
        [InlineData("limit=abc")]
        public async Task ListAsync_クエリの形式が不正_400を返す(string query)
        {
            var (_, client) = LoginAsRider();

            var response = await client.GetAsync($"/api/reservations?{query}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ---- GET /api/reservations/{reservationId} ----

        // H-075
        [Fact]
        public async Task GetAsync_本人の予約_200と予約を返す()
        {
            var (me, client) = LoginAsRider();
            var reservation = TestReservations.Matching(me.Id);
            _factory.Reservations.Seed(reservation);

            var response = await client.GetAsync($"/api/reservations/{reservation.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            AssertKeys(json.RootElement, "data");
            var data = json.RootElement.GetProperty("data");
            AssertKeys(data, ReservationKeys);
            Assert.Equal(reservation.Id.ToString(), data.GetProperty("id").GetString());
            Assert.Equal(reservation.ReservationNumber, data.GetProperty("reservation_number").GetString());
            Assert.Equal(me.Id.ToString(), data.GetProperty("user_id").GetString());
            Assert.Equal(reservation.PickupLocation, data.GetProperty("pickup_location").GetString());
            Assert.Equal(reservation.Destination, data.GetProperty("destination").GetString());
            Assert.Equal(new DateTimeOffset(reservation.RequestedPickupAt), AssertUtc(data.GetProperty("requested_pickup_at")));
            Assert.Equal(reservation.PassengerCount, data.GetProperty("passenger_count").GetInt32());
            Assert.Equal(reservation.ConsiderationNotes, data.GetProperty("consideration_notes").GetString());
            Assert.Equal("matching", data.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, data.GetProperty("cancellation_reason").ValueKind);
            Assert.Equal(JsonValueKind.Null, data.GetProperty("cancelled_at").ValueKind);
            Assert.Equal(new DateTimeOffset(reservation.CreatedAt), AssertUtc(data.GetProperty("created_at")));
        }

        // H-076
        [Fact]
        public async Task GetAsync_存在しない予約_404とNOT_FOUNDを返す()
        {
            var (_, client) = LoginAsRider();

            var response = await client.GetAsync($"/api/reservations/{Guid.NewGuid()}");

            await AssertErrorAsync(response, HttpStatusCode.NotFound, "NOT_FOUND", NotFoundMessage);
        }

        // H-077
        [Fact]
        public async Task GetAsync_他人の予約_404とNOT_FOUNDを返す()
        {
            var (_, client) = LoginAsRider();
            var other = TestUsers.Rider();
            _factory.Users.Seed(other);
            var reservation = TestReservations.Matching(other.Id);
            _factory.Reservations.Seed(reservation);

            var response = await client.GetAsync($"/api/reservations/{reservation.Id}");

            await AssertErrorAsync(response, HttpStatusCode.NotFound, "NOT_FOUND", NotFoundMessage);
        }

        // H-078
        [Fact]
        public async Task GetAsync_IDがGUIDでない_404と本文なしを返す()
        {
            var (_, client) = LoginAsRider();

            var response = await client.GetAsync("/api/reservations/abc");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            await AssertNoBodyAsync(response);
        }

        // H-079
        [Fact]
        public async Task GetAsync_キャンセル済みの予約_理由と日時を返す()
        {
            var (me, client) = LoginAsRider();
            var reservation = TestReservations.InStatus(ReservationStatus.Cancelled, me.Id);
            _factory.Reservations.Seed(reservation);

            var response = await client.GetAsync($"/api/reservations/{reservation.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            var data = json.RootElement.GetProperty("data");
            Assert.Equal("cancelled", data.GetProperty("status").GetString());
            Assert.Equal(reservation.CancellationReason, data.GetProperty("cancellation_reason").GetString());
            Assert.Equal(new DateTimeOffset(reservation.CancelledAt!.Value), AssertUtc(data.GetProperty("cancelled_at")));
        }

        // ---- POST /api/reservations/{reservationId}/cancel ----

        // H-080
        [Fact]
        public async Task CancelAsync_matchingの予約_200とキャンセル結果を返す()
        {
            var (me, client) = LoginAsRider();
            var reservation = TestReservations.Matching(me.Id);
            _factory.Reservations.Seed(reservation);

            var response = await client.PostAsync($"/api/reservations/{reservation.Id}/cancel", Json("""{"reason":"予定が変わったため"}"""));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            AssertKeys(json.RootElement, "data");
            var data = json.RootElement.GetProperty("data");
            AssertKeys(data, "id", "status", "cancellation_reason", "cancelled_at");
            Assert.Equal(reservation.Id.ToString(), data.GetProperty("id").GetString());
            Assert.Equal("cancelled", data.GetProperty("status").GetString());
            Assert.Equal("予定が変わったため", data.GetProperty("cancellation_reason").GetString());
            AssertUtc(data.GetProperty("cancelled_at"));
            var saved = _factory.Reservations.Get(reservation.Id)!;
            Assert.Equal(ReservationStatus.Cancelled, saved.Status);
            Assert.Equal("予定が変わったため", saved.CancellationReason);
            Assert.Contains(saved, _factory.Reservations.Updated);
        }

        // H-081
        [Fact]
        public async Task CancelAsync_confirmedの予約_200とcancelledを返す()
        {
            var (me, client) = LoginAsRider();
            var reservation = TestReservations.InStatus(ReservationStatus.Confirmed, me.Id);
            _factory.Reservations.Seed(reservation);

            var response = await client.PostAsync($"/api/reservations/{reservation.Id}/cancel", Json("""{"reason":"予定が変わったため"}"""));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            Assert.Equal("cancelled", json.RootElement.GetProperty("data").GetProperty("status").GetString());
        }

        // H-082
        [Fact]
        public async Task CancelAsync_理由なし_200とnullの理由を返す()
        {
            var (me, client) = LoginAsRider();
            var reservation = TestReservations.Matching(me.Id);
            _factory.Reservations.Seed(reservation);

            var response = await client.PostAsync($"/api/reservations/{reservation.Id}/cancel", Json("{}"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = await ReadJsonAsync(response);
            var data = json.RootElement.GetProperty("data");
            Assert.True(data.TryGetProperty("cancellation_reason", out var reason));
            Assert.Equal(JsonValueKind.Null, reason.ValueKind);
        }

        // H-083
        [Theory]
        [InlineData(ReservationStatus.InProgress)]
        [InlineData(ReservationStatus.Completed)]
        [InlineData(ReservationStatus.Cancelled)]
        public async Task CancelAsync_キャンセルできない状態_409とdomainのメッセージを返す(ReservationStatus status)
        {
            var (me, client) = LoginAsRider();
            var reservation = TestReservations.InStatus(status, me.Id);
            _factory.Reservations.Seed(reservation);

            var response = await client.PostAsync($"/api/reservations/{reservation.Id}/cancel", Json("""{"reason":"予定が変わったため"}"""));

            await AssertErrorAsync(response, HttpStatusCode.Conflict, "CONFLICT", $"Cannot cancel a reservation in status {status}.");
        }

        // H-084
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CancelAsync_存在しないか他人の予約_404を返し変更しない(bool othersReservation)
        {
            var (_, client) = LoginAsRider();
            var other = TestUsers.Rider();
            _factory.Users.Seed(other);
            var reservation = TestReservations.Matching(other.Id);
            _factory.Reservations.Seed(reservation);
            var targetId = othersReservation ? reservation.Id : Guid.NewGuid();

            var response = await client.PostAsync($"/api/reservations/{targetId}/cancel", Json("""{"reason":"予定が変わったため"}"""));

            await AssertErrorAsync(response, HttpStatusCode.NotFound, "NOT_FOUND", NotFoundMessage);
            Assert.Equal(ReservationStatus.Matching, _factory.Reservations.Get(reservation.Id)!.Status);
            Assert.Empty(_factory.Reservations.Updated);
        }

        // H-085
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task CancelAsync_ボディなし_400を返す(string? body)
        {
            var (me, client) = LoginAsRider();
            var reservation = TestReservations.Matching(me.Id);
            _factory.Reservations.Seed(reservation);

            var response = await client.PostAsync($"/api/reservations/{reservation.Id}/cancel", JsonOrNone(body));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ReservationStatus.Matching, _factory.Reservations.Get(reservation.Id)!.Status);
        }

        // H-086
        [Fact]
        public async Task CancelAsync_IDがGUIDでない_404と本文なしを返す()
        {
            var (_, client) = LoginAsRider();

            var response = await client.PostAsync("/api/reservations/abc/cancel", Json("""{"reason":"予定が変わったため"}"""));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            await AssertNoBodyAsync(response);
        }
    }
}
