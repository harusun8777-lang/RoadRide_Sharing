using global::Domain.Reservations;

namespace Backend.Tests.Domain
{
    public class ReservationTests
    {
        private const string ReservationNumber = "R-20260926-0001";
        private const string PickupLocation = "駅前";
        private const string Destination = "病院";
        private const int PassengerCount = 1;
        private const string Reason = "予定が変わったため";
        private static readonly DateTime RequestedPickupAt = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        private static Reservation CreateReservation(
            string? reservationNumber = ReservationNumber,
            Guid? userId = null,
            string? pickupLocation = PickupLocation,
            string? destination = Destination,
            DateTime? requestedPickupAt = null,
            int passengerCount = PassengerCount,
            string? considerationNotes = null)
            => Reservation.Create(
                reservationNumber!,
                userId ?? Guid.NewGuid(),
                pickupLocation!,
                destination!,
                requestedPickupAt ?? RequestedPickupAt,
                passengerCount,
                considerationNotes);

        // 公開メソッドだけで任意の状態を作る
        private static Reservation CreateIn(ReservationStatus status)
        {
            var r = CreateReservation(considerationNotes: "車いす");
            switch (status)
            {
                case ReservationStatus.Confirmed: r.Confirm(); break;
                case ReservationStatus.InProgress: r.Confirm(); r.StartInProgress(); break;
                case ReservationStatus.Completed: r.Confirm(); r.StartInProgress(); r.Complete(); break;
                case ReservationStatus.Cancelled: r.Cancel("最初の理由"); break;
            }
            return r;
        }

        // 遷移操作を名前で呼び出す（Theory で使う）
        private static void Invoke(Reservation r, string operation)
        {
            switch (operation)
            {
                case nameof(Reservation.Confirm): r.Confirm(); break;
                case nameof(Reservation.StartInProgress): r.StartInProgress(); break;
                case nameof(Reservation.Complete): r.Complete(); break;
                case nameof(Reservation.Cancel): r.Cancel(Reason); break;
                default: throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }

        // 許可される遷移（D-051〜D-055）
        public static TheoryData<string, ReservationStatus, ReservationStatus> AllowedTransitions => new()
        {
            { nameof(Reservation.Confirm), ReservationStatus.Matching, ReservationStatus.Confirmed },
            { nameof(Reservation.StartInProgress), ReservationStatus.Confirmed, ReservationStatus.InProgress },
            { nameof(Reservation.Complete), ReservationStatus.InProgress, ReservationStatus.Completed },
            { nameof(Reservation.Cancel), ReservationStatus.Matching, ReservationStatus.Cancelled },
            { nameof(Reservation.Cancel), ReservationStatus.Confirmed, ReservationStatus.Cancelled },
        };

        // 拒否される遷移（D-060〜D-074 の全15通り）
        public static TheoryData<string, ReservationStatus> RejectedTransitions => new()
        {
            { nameof(Reservation.Confirm), ReservationStatus.Confirmed },          // D-060
            { nameof(Reservation.Confirm), ReservationStatus.InProgress },         // D-061
            { nameof(Reservation.Confirm), ReservationStatus.Completed },          // D-062
            { nameof(Reservation.Confirm), ReservationStatus.Cancelled },          // D-063
            { nameof(Reservation.StartInProgress), ReservationStatus.Matching },   // D-064
            { nameof(Reservation.StartInProgress), ReservationStatus.InProgress }, // D-065
            { nameof(Reservation.StartInProgress), ReservationStatus.Completed },  // D-066
            { nameof(Reservation.StartInProgress), ReservationStatus.Cancelled },  // D-067
            { nameof(Reservation.Complete), ReservationStatus.Matching },          // D-068
            { nameof(Reservation.Complete), ReservationStatus.Confirmed },         // D-069
            { nameof(Reservation.Complete), ReservationStatus.Completed },         // D-070
            { nameof(Reservation.Complete), ReservationStatus.Cancelled },         // D-071
            { nameof(Reservation.Cancel), ReservationStatus.InProgress },          // D-072
            { nameof(Reservation.Cancel), ReservationStatus.Completed },           // D-073
            { nameof(Reservation.Cancel), ReservationStatus.Cancelled },           // D-074
        };

        // ---- Create ----

        // D-038
        [Fact]
        public void Create_既定値_入力どおりでMatchingになる()
        {
            var userId = Guid.NewGuid();

            var r = CreateReservation(userId: userId, considerationNotes: "車いす");

            Assert.NotEqual(Guid.Empty, r.Id);
            Assert.Equal(ReservationNumber, r.ReservationNumber);
            Assert.Equal(userId, r.UserId);
            Assert.Equal(PickupLocation, r.PickupLocation);
            Assert.Equal(Destination, r.Destination);
            Assert.Equal(RequestedPickupAt, r.RequestedPickupAt);
            Assert.Equal(PassengerCount, r.PassengerCount);
            Assert.Equal("車いす", r.ConsiderationNotes);
            Assert.Equal(ReservationStatus.Matching, r.Status);
            Assert.Null(r.CancellationReason);
            Assert.Null(r.CancelledAt);
        }

        // D-039
        [Fact]
        public void Create_既定値_CreatedAtとUpdatedAtが同じで呼び出し前後のUtc()
        {
            var before = DateTime.UtcNow;
            var r = CreateReservation();
            var after = DateTime.UtcNow;

            Assert.Equal(r.CreatedAt, r.UpdatedAt);
            Assert.InRange(r.CreatedAt, before, after);
            Assert.Equal(DateTimeKind.Utc, r.CreatedAt.Kind);
        }

        // D-040
        [Fact]
        public void Create_配慮事項を省略_ConsiderationNotesがnull()
        {
            var r = Reservation.Create(ReservationNumber, Guid.NewGuid(), PickupLocation, Destination, RequestedPickupAt, PassengerCount);
            Assert.Null(r.ConsiderationNotes);
        }

        // D-041
        [Fact]
        public void Create_人数が1_例外なし()
        {
            var r = CreateReservation(passengerCount: 1);
            Assert.Equal(1, r.PassengerCount);
        }

        // D-042
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Create_人数が1未満_ArgumentException(int passengerCount)
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateReservation(passengerCount: passengerCount));
            Assert.Equal("passengerCount", ex.ParamName);
        }

        // D-043
        [Fact]
        public void Create_人数がintMaxValue_上限チェックがなく例外なし()
        {
            var r = CreateReservation(passengerCount: int.MaxValue);
            Assert.Equal(int.MaxValue, r.PassengerCount);
        }

        // D-044
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_予約番号が空_ArgumentException(string? reservationNumber)
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateReservation(reservationNumber: reservationNumber));
            Assert.Equal("reservationNumber", ex.ParamName);
        }

        // D-045
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_乗車地が空_ArgumentException(string? pickupLocation)
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateReservation(pickupLocation: pickupLocation));
            Assert.Equal("pickupLocation", ex.ParamName);
        }

        // D-046
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_目的地が空_ArgumentException(string? destination)
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateReservation(destination: destination));
            Assert.Equal("destination", ex.ParamName);
        }

        // D-047
        [Fact]
        public void Create_文字列の前後に空白_Trimされずに保持される()
        {
            var r = CreateReservation(
                reservationNumber: " R-20260926-0001 ",
                pickupLocation: " 駅前 ",
                destination: " 病院 ",
                considerationNotes: " 車いす ");

            Assert.Equal(" R-20260926-0001 ", r.ReservationNumber);
            Assert.Equal(" 駅前 ", r.PickupLocation);
            Assert.Equal(" 病院 ", r.Destination);
            Assert.Equal(" 車いす ", r.ConsiderationNotes);
        }

        // D-048
        [Fact]
        public void Create_未検証の値_例外なくそのまま保持される()
        {
            var past = new DateTime(2000, 1, 1, 9, 0, 0, DateTimeKind.Unspecified);

            var r = CreateReservation(userId: Guid.Empty, requestedPickupAt: past, considerationNotes: "");

            Assert.Equal(Guid.Empty, r.UserId);
            Assert.Equal(past, r.RequestedPickupAt);
            Assert.Equal(DateTimeKind.Unspecified, r.RequestedPickupAt.Kind);
            Assert.Equal("", r.ConsiderationNotes);
        }

        // D-049
        [Fact]
        public void Create_予約番号と人数が両方不正_ParamNameはreservationNumber()
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateReservation(reservationNumber: "", passengerCount: 0));
            Assert.Equal("reservationNumber", ex.ParamName);
        }

        // D-050
        [Fact]
        public void Create_2回生成_Idが異なる()
        {
            Assert.NotEqual(CreateReservation().Id, CreateReservation().Id);
        }

        // ---- 状態遷移（許可） ----

        // D-051
        [Fact]
        public void Confirm_Matching_ConfirmedになりUpdatedAtが更新される()
        {
            var r = CreateIn(ReservationStatus.Matching);

            var before = DateTime.UtcNow;
            r.Confirm();
            var after = DateTime.UtcNow;

            Assert.Equal(ReservationStatus.Confirmed, r.Status);
            Assert.InRange(r.UpdatedAt, before, after);
        }

        // D-052
        [Fact]
        public void StartInProgress_Confirmed_InProgressになりUpdatedAtが更新される()
        {
            var r = CreateIn(ReservationStatus.Confirmed);

            var before = DateTime.UtcNow;
            r.StartInProgress();
            var after = DateTime.UtcNow;

            Assert.Equal(ReservationStatus.InProgress, r.Status);
            Assert.InRange(r.UpdatedAt, before, after);
        }

        // D-053
        [Fact]
        public void Complete_InProgress_CompletedになりUpdatedAtが更新される()
        {
            var r = CreateIn(ReservationStatus.InProgress);

            var before = DateTime.UtcNow;
            r.Complete();
            var after = DateTime.UtcNow;

            Assert.Equal(ReservationStatus.Completed, r.Status);
            Assert.InRange(r.UpdatedAt, before, after);
        }

        // D-054, D-055
        [Theory]
        [InlineData(ReservationStatus.Matching)]  // D-054
        [InlineData(ReservationStatus.Confirmed)] // D-055
        public void Cancel_キャンセル可能な状態_Cancelledになり理由と日時が設定される(ReservationStatus status)
        {
            var r = CreateIn(status);

            var before = DateTime.UtcNow;
            r.Cancel(Reason);
            var after = DateTime.UtcNow;

            Assert.Equal(ReservationStatus.Cancelled, r.Status);
            Assert.Equal(Reason, r.CancellationReason);
            Assert.NotNull(r.CancelledAt);
            Assert.InRange(r.CancelledAt.Value, before, after);
            Assert.InRange(r.UpdatedAt, before, after);
        }

        // D-056
        [Fact]
        public void Cancel_理由がnull_CancelledでCancelledAtは設定される()
        {
            var r = CreateIn(ReservationStatus.Matching);

            r.Cancel(null);

            Assert.Equal(ReservationStatus.Cancelled, r.Status);
            Assert.Null(r.CancellationReason);
            Assert.NotNull(r.CancelledAt);
        }

        // D-057
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void Cancel_理由が空文字や空白_そのまま保持される(string reason)
        {
            var r = CreateIn(ReservationStatus.Matching);

            r.Cancel(reason);

            Assert.Equal(ReservationStatus.Cancelled, r.Status);
            Assert.Equal(reason, r.CancellationReason);
        }

        // D-058
        [Theory]
        [MemberData(nameof(AllowedTransitions))]
        public void 遷移メソッド_許可される遷移_不変項目が変わらない(string operation, ReservationStatus from, ReservationStatus to)
        {
            var r = CreateIn(from);
            var (id, number, userId, pickup, dest, at, count, notes, createdAt) =
                (r.Id, r.ReservationNumber, r.UserId, r.PickupLocation, r.Destination, r.RequestedPickupAt, r.PassengerCount, r.ConsiderationNotes, r.CreatedAt);

            Invoke(r, operation);

            Assert.Equal(to, r.Status);
            Assert.Equal(id, r.Id);
            Assert.Equal(number, r.ReservationNumber);
            Assert.Equal(userId, r.UserId);
            Assert.Equal(pickup, r.PickupLocation);
            Assert.Equal(dest, r.Destination);
            Assert.Equal(at, r.RequestedPickupAt);
            Assert.Equal(count, r.PassengerCount);
            Assert.Equal(notes, r.ConsiderationNotes);
            Assert.Equal(createdAt, r.CreatedAt);
            Assert.True(r.UpdatedAt >= r.CreatedAt);
        }

        // D-059
        [Fact]
        public void Confirm_StartInProgress_Complete_Matchingから順に呼ぶ_Completedになる()
        {
            var r = CreateIn(ReservationStatus.Matching);
            var updatedAt = r.UpdatedAt;

            r.Confirm();
            Assert.True(r.UpdatedAt >= updatedAt);
            updatedAt = r.UpdatedAt;

            r.StartInProgress();
            Assert.True(r.UpdatedAt >= updatedAt);
            updatedAt = r.UpdatedAt;

            r.Complete();
            Assert.True(r.UpdatedAt >= updatedAt);
            Assert.Equal(ReservationStatus.Completed, r.Status);
        }

        // ---- 状態遷移（拒否） ----

        // D-060, D-061, D-062, D-063
        [Theory]
        [InlineData(ReservationStatus.Confirmed)]  // D-060
        [InlineData(ReservationStatus.InProgress)] // D-061
        [InlineData(ReservationStatus.Completed)]  // D-062
        [InlineData(ReservationStatus.Cancelled)]  // D-063
        public void Confirm_Matching以外_InvalidOperationException(ReservationStatus status)
        {
            var r = CreateIn(status);
            Assert.Throws<InvalidOperationException>(() => r.Confirm());
        }

        // D-064, D-065, D-066, D-067
        [Theory]
        [InlineData(ReservationStatus.Matching)]   // D-064
        [InlineData(ReservationStatus.InProgress)] // D-065
        [InlineData(ReservationStatus.Completed)]  // D-066
        [InlineData(ReservationStatus.Cancelled)]  // D-067
        public void StartInProgress_Confirmed以外_InvalidOperationException(ReservationStatus status)
        {
            var r = CreateIn(status);
            Assert.Throws<InvalidOperationException>(() => r.StartInProgress());
        }

        // D-068, D-069, D-070, D-071
        [Theory]
        [InlineData(ReservationStatus.Matching)]  // D-068
        [InlineData(ReservationStatus.Confirmed)] // D-069
        [InlineData(ReservationStatus.Completed)] // D-070
        [InlineData(ReservationStatus.Cancelled)] // D-071
        public void Complete_InProgress以外_InvalidOperationException(ReservationStatus status)
        {
            var r = CreateIn(status);
            Assert.Throws<InvalidOperationException>(() => r.Complete());
        }

        // D-072, D-073
        [Theory]
        [InlineData(ReservationStatus.InProgress)] // D-072
        [InlineData(ReservationStatus.Completed)]  // D-073
        public void Cancel_キャンセル不可の状態_InvalidOperationException(ReservationStatus status)
        {
            var r = CreateIn(status);
            Assert.Throws<InvalidOperationException>(() => r.Cancel(Reason));
        }

        // D-074
        [Fact]
        public void Cancel_二重キャンセル_InvalidOperationExceptionで1回目の理由と日時が残る()
        {
            var r = CreateIn(ReservationStatus.Matching);
            r.Cancel("最初の理由");
            var cancelledAt = r.CancelledAt;

            Assert.Throws<InvalidOperationException>(() => r.Cancel("2回目の理由"));

            Assert.Equal("最初の理由", r.CancellationReason);
            Assert.Equal(cancelledAt, r.CancelledAt);
        }

        // D-075
        [Theory]
        [MemberData(nameof(RejectedTransitions))]
        public void 遷移メソッド_拒否される遷移_状態と日時が変わらない(string operation, ReservationStatus status)
        {
            var r = CreateIn(status);
            var (updatedAt, reason, cancelledAt) = (r.UpdatedAt, r.CancellationReason, r.CancelledAt);

            Assert.Throws<InvalidOperationException>(() => Invoke(r, operation));

            Assert.Equal(status, r.Status);
            Assert.Equal(updatedAt, r.UpdatedAt);
            Assert.Equal(reason, r.CancellationReason);
            Assert.Equal(cancelledAt, r.CancelledAt);
        }

        // D-076
        [Theory]
        [MemberData(nameof(RejectedTransitions))]
        public void 遷移メソッド_拒否される遷移_メッセージに現在の状態名が含まれる(string operation, ReservationStatus status)
        {
            var r = CreateIn(status);

            var ex = Assert.Throws<InvalidOperationException>(() => Invoke(r, operation));

            Assert.Contains(status.ToString(), ex.Message);
        }
    }
}
