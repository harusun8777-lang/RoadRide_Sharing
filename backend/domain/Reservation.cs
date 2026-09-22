using Domain.Users;

namespace Domain.Reservations
{
    public enum ReservationStatus
    {
        Matching,
        Confirmed,
        InProgress,
        Completed,
        Cancelled
    }

    public class Reservation
    {
        public Guid Id { get; }
        public string ReservationNumber { get; }
        public Guid UserId { get; }
        public string PickupLocation { get; private set; }
        public string Destination { get; private set; }
        public DateTime RequestedPickupAt { get; private set; }
        public int PassengerCount { get; private set; }
        public string? ConsiderationNotes { get; private set; }
        public ReservationStatus Status { get; private set; }
        public string? CancellationReason { get; private set; }
        public DateTime? CancelledAt { get; private set; }
        public DateTime CreatedAt { get; }
        public DateTime UpdatedAt { get; private set; }

        private Reservation(
            Guid id,
            string reservationNumber,
            Guid userId,
            string pickupLocation,
            string destination,
            DateTime requestedPickupAt,
            int passengerCount,
            string? considerationNotes,
            DateTime createdAt)
        {
            Id = id;
            ReservationNumber = reservationNumber;
            UserId = userId;
            PickupLocation = pickupLocation;
            Destination = destination;
            RequestedPickupAt = requestedPickupAt;
            PassengerCount = passengerCount;
            ConsiderationNotes = considerationNotes;
            Status = ReservationStatus.Matching;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }

        public static Reservation Create(
            string reservationNumber,
            Guid userId,
            string pickupLocation,
            string destination,
            DateTime requestedPickupAt,
            int passengerCount,
            string? considerationNotes = null)
        {
            if (string.IsNullOrWhiteSpace(reservationNumber))
                throw new ArgumentException("ReservationNumber must not be empty.", nameof(reservationNumber));
            if (string.IsNullOrWhiteSpace(pickupLocation))
                throw new ArgumentException("PickupLocation must not be empty.", nameof(pickupLocation));
            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentException("Destination must not be empty.", nameof(destination));
            if (passengerCount < 1)
                throw new ArgumentException("PassengerCount must be 1 or more.", nameof(passengerCount));

            return new Reservation(
                Guid.NewGuid(),
                reservationNumber,
                userId,
                pickupLocation,
                destination,
                requestedPickupAt,
                passengerCount,
                considerationNotes,
                DateTime.UtcNow);
        }

        public void Confirm()
        {
            if (Status != ReservationStatus.Matching)
                throw new InvalidOperationException($"Cannot confirm a reservation in status {Status}.");

            Status = ReservationStatus.Confirmed;
            UpdatedAt = DateTime.UtcNow;
        }

        public void StartInProgress()
        {
            if (Status != ReservationStatus.Confirmed)
                throw new InvalidOperationException($"Cannot start a reservation in status {Status}.");

            Status = ReservationStatus.InProgress;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Complete()
        {
            if (Status != ReservationStatus.InProgress)
                throw new InvalidOperationException($"Cannot complete a reservation in status {Status}.");

            Status = ReservationStatus.Completed;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Cancel(string? reason)
        {
            if (Status is not (ReservationStatus.Matching or ReservationStatus.Confirmed))
                throw new InvalidOperationException($"Cannot cancel a reservation in status {Status}.");

            Status = ReservationStatus.Cancelled;
            CancellationReason = reason;
            CancelledAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
