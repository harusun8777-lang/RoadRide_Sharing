using System.Data.SqlTypes;
using Backend.Tests.Infrastructure.Fixtures;
using Backend.Tests.TestDoubles;
using Domain.Reservations;
using Infrastructure.Reservations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Usecase.Reservation;

namespace Backend.Tests.Infrastructure;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Database")]
public class EfReservationRepositoryTests(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    private async Task<Guid> SeedRiderAsync() => (await SaveUserAsync(TestUsers.Rider())).Id;

    private async Task<Reservation> SeedAsync(Guid userId, DateTime pickupAt, ReservationStatus status = ReservationStatus.Matching)
    {
        var reservation = NewReservation(userId, pickupAt);
        Advance(reservation, status);
        return await SaveReservationAsync(reservation);
    }

    // ドメインの公開メソッドで状態を進める
    private static void Advance(Reservation reservation, ReservationStatus status)
    {
        switch (status)
        {
            case ReservationStatus.Confirmed:
                reservation.Confirm();
                break;
            case ReservationStatus.InProgress:
                reservation.Confirm();
                reservation.StartInProgress();
                break;
            case ReservationStatus.Completed:
                reservation.Confirm();
                reservation.StartInProgress();
                reservation.Complete();
                break;
            case ReservationStatus.Cancelled:
                reservation.Cancel("テスト用のキャンセル");
                break;
        }
    }

    // 1 時間ずつずらした予約を count 件保存する
    private async Task<List<Reservation>> SeedManyAsync(Guid userId, int count, DateTime? start = null)
    {
        var list = new List<Reservation>();
        for (var i = 0; i < count; i++)
            list.Add(await SeedAsync(userId, (start ?? Utc(2026, 10, 1)).AddHours(i)));
        return list;
    }

    private async Task<(IReadOnlyList<Reservation> Items, int Total)> ListAsync(ReservationListFilter filter)
    {
        await using var db = CreateContext();
        return await new EfReservationRepository(db).ListAsync(filter);
    }

    private async Task<Reservation?> FindAsync(Guid id)
    {
        await using var db = CreateContext();
        return await new EfReservationRepository(db).FindByIdAsync(id);
    }

    private async Task<bool> HasUnfinishedAsync(Guid userId)
    {
        await using var db = CreateContext();
        return await new EfReservationRepository(db).HasUnfinishedAsync(userId);
    }

    private static Guid[] Ids(IEnumerable<Reservation> reservations) => reservations.Select(r => r.Id).ToArray();

    // ---- AddAsync / FindByIdAsync / UpdateAsync ----

    // I-092
    [Fact]
    public async Task AddAsync_予約を保存して別コンテキストで取得_全プロパティが一致する()
    {
        var userId = await SeedRiderAsync();
        var saved = await SeedAsync(userId, Utc(2026, 10, 1, 9, 30));

        var read = await FindAsync(saved.Id);

        Assert.NotNull(read);
        Assert.Equal(saved.Id, read.Id);
        Assert.Equal(saved.ReservationNumber, read.ReservationNumber);
        Assert.Equal(userId, read.UserId);
        Assert.Equal(saved.PickupLocation, read.PickupLocation);
        Assert.Equal(saved.Destination, read.Destination);
        Assert.Equal(saved.RequestedPickupAt, read.RequestedPickupAt);
        Assert.Equal(saved.PassengerCount, read.PassengerCount);
        Assert.Equal(saved.ConsiderationNotes, read.ConsiderationNotes);
        Assert.Equal(ReservationStatus.Matching, read.Status);
        Assert.Null(read.CancellationReason);
        Assert.Null(read.CancelledAt);
        Assert.Equal(saved.CreatedAt.Ticks, read.CreatedAt.Ticks);
        Assert.Equal(saved.UpdatedAt.Ticks, read.UpdatedAt.Ticks);
        Assert.All([read.RequestedPickupAt, read.CreatedAt, read.UpdatedAt], d => Assert.Equal(DateTimeKind.Utc, d.Kind));
    }

    // I-093
    [Fact]
    public async Task AddAsync_配慮事項なし_読み直してもnull()
    {
        var userId = await SeedRiderAsync();
        var saved = await SaveReservationAsync(NewReservation(userId, Utc(2026, 10, 1), considerationNotes: null));

        Assert.Null((await FindAsync(saved.Id))!.ConsiderationNotes);
    }

    // I-094
    [Fact]
    public async Task AddAsync_予約番号の重複_DbUpdateExceptionで2601()
    {
        var userId = await SeedRiderAsync();
        await SaveReservationAsync(Reservation.Create("RR-20261001-AAAA", userId, "市役所前", "中央病院", Utc(2026, 10, 1), 1));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => SaveReservationAsync(
            Reservation.Create("RR-20261001-AAAA", userId, "駅前", "中央病院", Utc(2026, 10, 2), 1)));

        Assert.Equal(2601, SqlErrorNumber(ex));
    }

    // I-095
    [Fact]
    public async Task AddAsync_Riderプロフィールがないユーザー_DbUpdateExceptionで547()
    {
        var driver = await SaveUserAsync(TestUsers.Driver());

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => SaveReservationAsync(NewReservation(driver.Id, Utc(2026, 10, 1))));

        Assert.Equal(547, SqlErrorNumber(ex));
    }

    // I-096
    [Fact]
    public async Task AddAsync_存在しないユーザー_DbUpdateExceptionで547()
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => SaveReservationAsync(NewReservation(Guid.NewGuid(), Utc(2026, 10, 1))));

        Assert.Equal(547, SqlErrorNumber(ex));
    }

    // I-097
    [Fact]
    public async Task FindByIdAsync_存在しないID_null()
    {
        await SeedAsync(await SeedRiderAsync(), Utc(2026, 10, 1));

        Assert.Null(await FindAsync(Guid.NewGuid()));
    }

    // I-098
    [Fact]
    public async Task FindByIdAsync_取得した予約_追跡されUnchanged()
    {
        var saved = await SeedAsync(await SeedRiderAsync(), Utc(2026, 10, 1));

        await using var db = CreateContext();
        var reservation = (await new EfReservationRepository(db).FindByIdAsync(saved.Id))!;

        Assert.Equal(EntityState.Unchanged, db.Entry(reservation).State);
    }

    private async Task AssertCancelledAsync(Reservation original)
    {
        var read = (await FindAsync(original.Id))!;
        Assert.Equal(ReservationStatus.Cancelled, read.Status);
        Assert.Equal("体調不良", read.CancellationReason);
        Assert.NotNull(read.CancelledAt);
        Assert.Equal(DateTimeKind.Utc, read.CancelledAt.Value.Kind);
        Assert.True(read.UpdatedAt > original.UpdatedAt);
        Assert.Equal(original.CreatedAt.Ticks, read.CreatedAt.Ticks);
        Assert.Equal(original.ReservationNumber, read.ReservationNumber);
    }

    // I-099
    [Fact]
    public async Task UpdateAsync_追跡中の予約をキャンセル_状態と理由と日時が保存される()
    {
        var saved = await SeedAsync(await SeedRiderAsync(), Utc(2026, 10, 1));

        await using (var db = CreateContext())
        {
            var repository = new EfReservationRepository(db);
            var tracked = (await repository.FindByIdAsync(saved.Id))!;
            tracked.Cancel("体調不良");
            await repository.UpdateAsync(tracked);
        }

        await AssertCancelledAsync(saved);
    }

    // I-100
    [Fact]
    public async Task UpdateAsync_切り離された予約をキャンセル_状態と理由と日時が保存される()
    {
        var saved = await SeedAsync(await SeedRiderAsync(), Utc(2026, 10, 1));
        var detached = (await FindAsync(saved.Id))!; // コンテキスト A は破棄済み
        detached.Cancel("体調不良");

        await using (var db = CreateContext())
            await new EfReservationRepository(db).UpdateAsync(detached);

        await AssertCancelledAsync(saved);
    }

    // I-101
    [Fact]
    public async Task UpdateAsync_確定から完了まで_各段階の状態が保存される()
    {
        var saved = await SeedAsync(await SeedRiderAsync(), Utc(2026, 10, 1));
        var steps = new (Action<Reservation> Act, string Expected)[]
        {
            (r => r.Confirm(), "confirmed"),
            (r => r.StartInProgress(), "in_progress"),
            (r => r.Complete(), "completed"),
        };

        foreach (var (act, expected) in steps)
        {
            await using (var db = CreateContext())
            {
                var repository = new EfReservationRepository(db);
                var tracked = (await repository.FindByIdAsync(saved.Id))!;
                act(tracked);
                await repository.UpdateAsync(tracked);
            }

            await using var read = CreateContext();
            Assert.Equal(expected, await read.Database
                .SqlQuery<string>($"SELECT status AS Value FROM reservations WHERE id = {saved.Id}").SingleAsync());
        }
    }

    // I-102
    [Fact]
    public async Task UpdateAsync_保存していない予約_DbUpdateConcurrencyException()
    {
        var reservation = NewReservation(await SeedRiderAsync(), Utc(2026, 10, 1));

        await using var db = CreateContext();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new EfReservationRepository(db).UpdateAsync(reservation));
    }

    // ---- HasUnfinishedAsync ----

    // I-103
    [Theory]
    [InlineData(ReservationStatus.Matching)]
    [InlineData(ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.InProgress)]
    public async Task HasUnfinishedAsync_未完了の予約が1件_true(ReservationStatus status)
    {
        var userId = await SeedRiderAsync();
        await SeedAsync(userId, Utc(2026, 10, 1), status);

        Assert.True(await HasUnfinishedAsync(userId));
    }

    // I-104
    [Fact]
    public async Task HasUnfinishedAsync_完了とキャンセルだけ_false()
    {
        var userId = await SeedRiderAsync();
        await SeedAsync(userId, Utc(2026, 10, 1), ReservationStatus.Completed);
        await SeedAsync(userId, Utc(2026, 10, 2), ReservationStatus.Cancelled);

        Assert.False(await HasUnfinishedAsync(userId));
    }

    // I-105
    [Fact]
    public async Task HasUnfinishedAsync_予約なし_false()
    {
        Assert.False(await HasUnfinishedAsync(await SeedRiderAsync()));
    }

    // I-106
    [Fact]
    public async Task HasUnfinishedAsync_他人の予約だけ未完了_false()
    {
        var userId = await SeedRiderAsync();
        var otherId = await SeedRiderAsync();
        await SeedAsync(otherId, Utc(2026, 10, 1), ReservationStatus.Matching);
        await SeedAsync(userId, Utc(2026, 10, 1), ReservationStatus.Completed);

        Assert.False(await HasUnfinishedAsync(userId));
    }

    // I-107
    [Fact]
    public async Task HasUnfinishedAsync_完了と確定が混在_true()
    {
        var userId = await SeedRiderAsync();
        await SeedAsync(userId, Utc(2026, 10, 1), ReservationStatus.Completed);
        await SeedAsync(userId, Utc(2026, 10, 2), ReservationStatus.Confirmed);

        Assert.True(await HasUnfinishedAsync(userId));
    }

    // ---- ListAsync ----

    // I-108
    [Fact]
    public async Task ListAsync_ユーザーで絞り込み_そのユーザーの予約だけ()
    {
        var a = await SeedRiderAsync();
        var b = await SeedRiderAsync();
        var aReservations = await SeedManyAsync(a, 3);
        await SeedManyAsync(b, 2);

        var (items, total) = await ListAsync(new ReservationListFilter(UserId: a));

        Assert.Equal(3, total);
        Assert.Equal(Ids(aReservations).Order(), Ids(items).Order());
    }

    // I-109
    [Fact]
    public async Task ListAsync_ユーザー指定なし_全ユーザーの予約()
    {
        await SeedManyAsync(await SeedRiderAsync(), 3);
        await SeedManyAsync(await SeedRiderAsync(), 2);

        var (items, total) = await ListAsync(new ReservationListFilter(UserId: null));

        Assert.Equal(5, total);
        Assert.Equal(5, items.Count);
    }

    // I-110
    [Theory]
    [InlineData(ReservationStatus.Matching)]
    [InlineData(ReservationStatus.Confirmed)]
    [InlineData(ReservationStatus.InProgress)]
    [InlineData(ReservationStatus.Completed)]
    [InlineData(ReservationStatus.Cancelled)]
    public async Task ListAsync_状態で絞り込み_その状態の予約だけ(ReservationStatus status)
    {
        var userId = await SeedRiderAsync();
        var seeded = new Dictionary<ReservationStatus, Reservation>();
        foreach (var s in Enum.GetValues<ReservationStatus>())
            seeded[s] = await SeedAsync(userId, Utc(2026, 10, 1), s);

        var (items, total) = await ListAsync(new ReservationListFilter(Status: status));

        Assert.Equal(1, total);
        Assert.Equal(seeded[status].Id, Assert.Single(items).Id);
    }

    // I-111
    [Fact]
    public async Task ListAsync_日本時間の暦日を指定_JSTの0時以上24時未満だけ()
    {
        var userId = await SeedRiderAsync();
        var justBefore = await SeedAsync(userId, Utc(2026, 9, 30, 14, 59, 59).AddTicks(9999999));
        var start = await SeedAsync(userId, Utc(2026, 9, 30, 15));
        var end = await SeedAsync(userId, Utc(2026, 10, 1, 14, 59, 59).AddTicks(9999999));
        var nextDay = await SeedAsync(userId, Utc(2026, 10, 1, 15));

        var (items, total) = await ListAsync(new ReservationListFilter(Date: new DateOnly(2026, 10, 1)));

        Assert.Equal(2, total);
        Assert.Equal([start.Id, end.Id], Ids(items));
    }

    // I-112
    [Fact]
    public async Task ListAsync_UTCでは10月1日だがJSTでは10月2日_10月2日として扱う()
    {
        var userId = await SeedRiderAsync();
        var reservation = await SeedAsync(userId, Utc(2026, 10, 1, 20)); // JST 10/2 5:00

        var (oct1, _) = await ListAsync(new ReservationListFilter(Date: new DateOnly(2026, 10, 1)));
        var (oct2, _) = await ListAsync(new ReservationListFilter(Date: new DateOnly(2026, 10, 2)));

        Assert.Empty(oct1);
        Assert.Equal(reservation.Id, Assert.Single(oct2).Id);
    }

    // I-113
    [Fact]
    public async Task ListAsync_Fromの境界_同時刻は含み1tick前は含まない()
    {
        var userId = await SeedRiderAsync();
        var from = Utc(2026, 10, 1, 9);
        var same = await SeedAsync(userId, from);
        await SeedAsync(userId, from.AddTicks(-1));

        var (items, _) = await ListAsync(new ReservationListFilter(From: from));

        Assert.Equal(same.Id, Assert.Single(items).Id);
    }

    // I-114
    [Fact]
    public async Task ListAsync_Toの境界_同時刻は含み1tick後は含まない()
    {
        var userId = await SeedRiderAsync();
        var to = Utc(2026, 10, 1, 9);
        var same = await SeedAsync(userId, to);
        await SeedAsync(userId, to.AddTicks(1));

        var (items, _) = await ListAsync(new ReservationListFilter(To: to));

        Assert.Equal(same.Id, Assert.Single(items).Id);
    }

    // I-115
    // From は JST の 9:00（= UTC 0:00）を表す Local の値。どのタイムゾーンでも同じ時点を表すので結果は変わらない。
    // ホストが UTC だと変換の有無を区別できない（設計書 5 章 #5）
    [Fact]
    public async Task ListAsync_FromのKindがLocal_UTCに変換して比較される()
    {
        var userId = await SeedRiderAsync();
        var at = await SeedAsync(userId, Utc(2026, 10, 1));
        await SeedAsync(userId, Utc(2026, 10, 1).AddTicks(-1));
        var from = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.FromHours(9)).LocalDateTime;
        Assert.Equal(DateTimeKind.Local, from.Kind);

        var (items, _) = await ListAsync(new ReservationListFilter(From: from));

        Assert.Equal(at.Id, Assert.Single(items).Id);
    }

    // I-116
    [Fact]
    public async Task ListAsync_FromのKindがUnspecified_UTCとして扱われる_現状の挙動()
    {
        var userId = await SeedRiderAsync();
        var at = await SeedAsync(userId, Utc(2026, 10, 1));
        await SeedAsync(userId, Utc(2026, 10, 1).AddTicks(-1));

        var (items, _) = await ListAsync(new ReservationListFilter(From: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified)));

        Assert.Equal(at.Id, Assert.Single(items).Id);
    }

    // I-117
    [Fact]
    public async Task ListAsync_FromがToより後_0件で例外にならない()
    {
        var userId = await SeedRiderAsync();
        await SeedAsync(userId, Utc(2026, 10, 1, 12));

        var (items, total) = await ListAsync(new ReservationListFilter(From: Utc(2026, 10, 2), To: Utc(2026, 10, 1)));

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    // I-118
    // From / To を日付の中に置くと「日付だけ外れる」予約を作れないため、2 通りの条件で確かめる
    [Fact]
    public async Task ListAsync_条件をすべて指定_すべて満たす予約だけ()
    {
        var a = await SeedRiderAsync();
        var b = await SeedRiderAsync();
        var date = new DateOnly(2026, 10, 1); // UTC 9/30 15:00 〜 10/1 15:00
        var target = await SeedAsync(a, Utc(2026, 10, 1, 3));
        await SeedAsync(b, Utc(2026, 10, 1, 3));                                // ユーザーだけ外れる
        await SeedAsync(a, Utc(2026, 10, 1, 3), ReservationStatus.Confirmed);   // 状態だけ外れる
        await SeedAsync(a, Utc(2026, 9, 30, 16));                               // From だけ外れる
        await SeedAsync(a, Utc(2026, 10, 1, 13));                               // To だけ外れる（1 つ目の条件）
        await SeedAsync(a, Utc(2026, 10, 1, 20));                               // 日付だけ外れる（2 つ目の条件）

        var (items1, total1) = await ListAsync(new ReservationListFilter(
            UserId: a, Date: date, Status: ReservationStatus.Matching, From: Utc(2026, 9, 30, 18), To: Utc(2026, 10, 1, 12)));
        var (items2, total2) = await ListAsync(new ReservationListFilter(
            UserId: a, Date: date, Status: ReservationStatus.Matching, From: Utc(2026, 9, 30, 18), To: Utc(2026, 10, 2)));

        Assert.Equal(1, total1);
        Assert.Equal(target.Id, Assert.Single(items1).Id);
        // 2 つ目は To を広げたので「To だけ外れる」予約が入り、「日付だけ外れる」予約は入らない
        Assert.Equal(2, total2);
        Assert.Equal(target.Id, items2[0].Id);
        Assert.Equal(Utc(2026, 10, 1, 13), items2[1].RequestedPickupAt);
    }

    // I-119
    [Fact]
    public async Task ListAsync_条件に合う予約がない_空で0件()
    {
        await SeedManyAsync(await SeedRiderAsync(), 2);

        var (items, total) = await ListAsync(new ReservationListFilter(UserId: Guid.NewGuid()));

        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    // I-120
    [Fact]
    public async Task ListAsync_日時がばらばら_希望乗車日時の昇順()
    {
        var userId = await SeedRiderAsync();
        DateTime[] times = [Utc(2026, 10, 3), Utc(2026, 10, 1, 12), Utc(2026, 10, 2), Utc(2026, 10, 1)];
        foreach (var t in times.OrderDescending())
            await SeedAsync(userId, t);

        var (items, _) = await ListAsync(new ReservationListFilter());

        Assert.Equal(times.Order(), items.Select(r => r.RequestedPickupAt));
    }

    // I-121
    [Fact]
    public async Task ListAsync_同時刻の予約_IdのSQLServer上の昇順で安定している()
    {
        var userId = await SeedRiderAsync();
        var seeded = new List<Reservation>();
        for (var i = 0; i < 5; i++)
            seeded.Add(await SeedAsync(userId, Utc(2026, 10, 1)));
        // uniqueidentifier の大小は .NET の Guid.CompareTo と異なるため SqlGuid で求める
        var expected = seeded.Select(r => r.Id).OrderBy(id => new SqlGuid(id)).ToArray();

        var (first, _) = await ListAsync(new ReservationListFilter());
        var (second, _) = await ListAsync(new ReservationListFilter());

        Assert.Equal(expected, Ids(first));
        Assert.Equal(expected, Ids(second));
    }

    // I-122
    [Fact]
    public async Task ListAsync_5件をlimit2でページング_2件2件1件で重複と欠落がない()
    {
        var seeded = await SeedManyAsync(await SeedRiderAsync(), 5);

        var pages = new List<Reservation>();
        foreach (var (page, count) in new[] { (1, 2), (2, 2), (3, 1) })
        {
            var (items, total) = await ListAsync(new ReservationListFilter(Page: page, Limit: 2));
            Assert.Equal(count, items.Count);
            Assert.Equal(5, total);
            pages.AddRange(items);
        }

        Assert.Equal(Ids(seeded), Ids(pages));
    }

    // I-123
    [Fact]
    public async Task ListAsync_範囲外のページ_空で件数は5()
    {
        await SeedManyAsync(await SeedRiderAsync(), 5);

        var (items, total) = await ListAsync(new ReservationListFilter(Page: 4, Limit: 2));

        Assert.Empty(items);
        Assert.Equal(5, total);
    }

    // I-124
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ListAsync_pageが1未満_1ページ目と同じ(int page)
    {
        var seeded = await SeedManyAsync(await SeedRiderAsync(), 5);

        var (items, _) = await ListAsync(new ReservationListFilter(Page: page, Limit: 2));

        Assert.Equal(Ids(seeded.Take(2)), Ids(items));
    }

    // I-125
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ListAsync_limitが1未満_50件になる(int limit)
    {
        await SeedManyAsync(await SeedRiderAsync(), 51);

        var (items, total) = await ListAsync(new ReservationListFilter(Limit: limit));

        Assert.Equal(50, items.Count);
        Assert.Equal(51, total);
    }

    // I-126
    [Fact]
    public async Task ListAsync_limitが1000_上限なく60件すべて_現状の挙動()
    {
        await SeedManyAsync(await SeedRiderAsync(), 60);

        var (items, total) = await ListAsync(new ReservationListFilter(Limit: 1000));

        Assert.Equal(60, items.Count);
        Assert.Equal(60, total);
    }

    // I-127
    [Fact]
    public async Task ListAsync_既定値のフィルタ_全ユーザーから1ページ目()
    {
        var a = await SeedManyAsync(await SeedRiderAsync(), 2, Utc(2026, 10, 1));
        var b = await SeedManyAsync(await SeedRiderAsync(), 1, Utc(2026, 10, 2));

        var (items, total) = await ListAsync(new ReservationListFilter());

        Assert.Equal(3, total);
        Assert.Equal(Ids(a.Concat(b)), Ids(items));
    }

    // I-128
    [Fact]
    public async Task ListAsync_pageがint最大値_OFFSETが負になりSqlException_現状の挙動()
    {
        await SeedManyAsync(await SeedRiderAsync(), 2);

        // (page - 1) * limit が int のままオーバーフローして負になり、SQL Server が OFFSET を拒否する（設計書 5 章 #7）
        var ex = await Assert.ThrowsAsync<SqlException>(() => ListAsync(new ReservationListFilter(Page: int.MaxValue, Limit: 50)));

        Assert.Equal(10742, ex.Number); // The offset specified in a OFFSET clause may not be negative.
    }

    // I-129
    [Fact]
    public async Task ListAsync_一覧取得後_追跡されていない()
    {
        await SeedManyAsync(await SeedRiderAsync(), 2);

        await using var db = CreateContext();
        var (items, _) = await new EfReservationRepository(db).ListAsync(new ReservationListFilter());

        Assert.Equal(2, items.Count);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
