using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DomainReservation = Domain.Reservations.Reservation;
using DomainReservationStatus = Domain.Reservations.ReservationStatus;
using DomainDriver = Domain.Users.Driver;
using DomainRideGroup = Domain.RideGroups.RideGroup;
using DomainRideGroupStatus = Domain.RideGroups.RideGroupStatus;
using DomainRider = Domain.Users.Rider;
using DomainUser = Domain.Users.User;
using DomainUserRole = Domain.Users.UserRole;

namespace Infrastructure
{
    public class AppDbContext : DbContext
    {
        public DbSet<DomainUser> Users => Set<DomainUser>();
        public DbSet<DomainReservation> Reservations => Set<DomainReservation>();
        public DbSet<DomainRideGroup> RideGroups => Set<DomainRideGroup>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // DBにはUTCで保存し、読み出し時にKindをUtcへ戻す
        private static readonly ValueConverter<DateTime, DateTime> UtcConverter = new(
            v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter = new(
            v => v.HasValue && v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        private static readonly ValueConverter<DomainUserRole, string> UserRoleConverter = new(
            v => v == DomainUserRole.Driver ? "driver" : "rider",
            v => v == "driver" ? DomainUserRole.Driver : DomainUserRole.Rider);

        private static readonly ValueConverter<DomainReservationStatus, string> ReservationStatusConverter = new(
            v => ToStatusString(v),
            v => ParseStatus(v));

        private static readonly ValueConverter<DomainRideGroupStatus, string> RideGroupStatusConverter = new(
            v => ToRideGroupStatusString(v),
            v => ParseRideGroupStatus(v));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DomainUser>(user =>
            {
                user.ToTable("users");
                user.HasKey(u => u.Id);
                user.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
                user.Property(u => u.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
                user.HasIndex(u => u.Email).IsUnique();
                user.Property(u => u.LastName).HasColumnName("last_name").HasMaxLength(50).IsRequired();
                user.Property(u => u.FirstName).HasColumnName("first_name").HasMaxLength(50).IsRequired();
                user.Property(u => u.KanaLastName).HasColumnName("kana_last_name").HasMaxLength(50).IsRequired();
                user.Property(u => u.KanaFirstName).HasColumnName("kana_first_name").HasMaxLength(50).IsRequired();
                user.Ignore(u => u.FullName);
                user.Ignore(u => u.KanaFullName);
                user.Ignore(u => u.Roles);
                user.Property(u => u.ActiveRole).HasColumnName("active_role").HasMaxLength(20).HasConversion(UserRoleConverter).IsRequired();
                user.Property(u => u.CreatedAt).HasColumnName("created_at").HasConversion(UtcConverter);
                user.HasOne(u => u.Rider).WithOne().HasForeignKey<DomainRider>(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
                user.HasOne(u => u.Driver).WithOne().HasForeignKey<DomainDriver>(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DomainRider>(rider =>
            {
                rider.ToTable("riders");
                rider.HasKey(r => r.UserId);
                rider.Property(r => r.UserId).HasColumnName("user_id").ValueGeneratedNever();
                rider.Property(r => r.CreatedAt).HasColumnName("created_at").HasConversion(UtcConverter);
            });

            modelBuilder.Entity<DomainDriver>(driver =>
            {
                driver.ToTable("drivers");
                driver.HasKey(d => d.UserId);
                driver.Property(d => d.UserId).HasColumnName("user_id").ValueGeneratedNever();
                driver.Property(d => d.CreatedAt).HasColumnName("created_at").HasConversion(UtcConverter);
            });

            modelBuilder.Entity<DomainRideGroup>(group =>
            {
                group.ToTable("ride_groups");
                group.HasKey(g => g.Id);
                group.Property(g => g.Id).HasColumnName("id").ValueGeneratedNever();
                group.Property(g => g.GroupNumber).HasColumnName("group_number").HasMaxLength(32).IsRequired();
                group.HasIndex(g => g.GroupNumber).IsUnique();
                group.Property(g => g.DriverId).HasColumnName("driver_id");
                group.HasOne<DomainDriver>().WithMany().HasForeignKey(g => g.DriverId).OnDelete(DeleteBehavior.Restrict);
                group.Property(g => g.Status).HasColumnName("status").HasMaxLength(20).HasConversion(RideGroupStatusConverter).IsRequired();
                group.Property(g => g.CreatedAt).HasColumnName("created_at").HasConversion(UtcConverter);
                group.Property(g => g.UpdatedAt).HasColumnName("updated_at").HasConversion(UtcConverter);
            });

            modelBuilder.Entity<DomainReservation>(reservation =>
            {
                reservation.ToTable("reservations");
                reservation.HasKey(r => r.Id);
                reservation.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
                reservation.Property(r => r.ReservationNumber).HasColumnName("reservation_number").HasMaxLength(32).IsRequired();
                reservation.HasIndex(r => r.ReservationNumber).IsUnique();
                reservation.Property(r => r.UserId).HasColumnName("user_id");
                // 予約できるのは Rider プロフィールを持つ利用者のみ
                reservation.HasOne<DomainRider>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
                reservation.Property(r => r.PickupLocation).HasColumnName("pickup_location").HasMaxLength(200).IsRequired();
                reservation.Property(r => r.Destination).HasColumnName("destination").HasMaxLength(200).IsRequired();
                reservation.Property(r => r.RequestedPickupAt).HasColumnName("requested_pickup_at").HasConversion(UtcConverter);
                reservation.HasIndex(r => r.RequestedPickupAt);
                reservation.Property(r => r.PassengerCount).HasColumnName("passenger_count");
                reservation.ToTable(t => t.HasCheckConstraint("CK_reservations_passenger_count", "[passenger_count] >= 1"));
                reservation.Property(r => r.ConsiderationNotes).HasColumnName("consideration_notes").HasMaxLength(500);
                reservation.Property(r => r.Status).HasColumnName("status").HasMaxLength(20).HasConversion(ReservationStatusConverter).IsRequired();
                reservation.Property(r => r.CancellationReason).HasColumnName("cancellation_reason").HasMaxLength(500);
                reservation.Property(r => r.CancelledAt).HasColumnName("cancelled_at").HasConversion(NullableUtcConverter);
                reservation.Property(r => r.CreatedAt).HasColumnName("created_at").HasConversion(UtcConverter);
                reservation.Property(r => r.UpdatedAt).HasColumnName("updated_at").HasConversion(UtcConverter);
            });
        }

        private static string ToStatusString(DomainReservationStatus status) => status switch
        {
            DomainReservationStatus.Matching => "matching",
            DomainReservationStatus.Confirmed => "confirmed",
            DomainReservationStatus.InProgress => "in_progress",
            DomainReservationStatus.Completed => "completed",
            DomainReservationStatus.Cancelled => "cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        private static DomainReservationStatus ParseStatus(string value) => value switch
        {
            "matching" => DomainReservationStatus.Matching,
            "confirmed" => DomainReservationStatus.Confirmed,
            "in_progress" => DomainReservationStatus.InProgress,
            "completed" => DomainReservationStatus.Completed,
            "cancelled" => DomainReservationStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };

        private static string ToRideGroupStatusString(DomainRideGroupStatus status) => status switch
        {
            DomainRideGroupStatus.Proposed => "proposed",
            DomainRideGroupStatus.Confirmed => "confirmed",
            DomainRideGroupStatus.InProgress => "in_progress",
            DomainRideGroupStatus.Completed => "completed",
            DomainRideGroupStatus.Cancelled => "cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        private static DomainRideGroupStatus ParseRideGroupStatus(string value) => value switch
        {
            "proposed" => DomainRideGroupStatus.Proposed,
            "confirmed" => DomainRideGroupStatus.Confirmed,
            "in_progress" => DomainRideGroupStatus.InProgress,
            "completed" => DomainRideGroupStatus.Completed,
            "cancelled" => DomainRideGroupStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }
}
