using System.Text.Json.Serialization;

namespace Handler.Reservations
{
    public class RegisterReservationRequest
    {
        [JsonPropertyName("pickup_location")] public string PickupLocation { get; init; } = string.Empty;
        [JsonPropertyName("destination")] public string Destination { get; init; } = string.Empty;
        [JsonPropertyName("requested_pickup_at")] public DateTime RequestedPickupAt { get; init; }
        [JsonPropertyName("passenger_count")] public int PassengerCount { get; init; }
        [JsonPropertyName("consideration_notes")] public string? ConsiderationNotes { get; init; }
    }

    public class CancelReservationRequest
    {
        [JsonPropertyName("reason")] public string? Reason { get; init; }
    }

    public class ReservationResponse
    {
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("reservation_number")] public string ReservationNumber { get; init; } = string.Empty;
        [JsonPropertyName("user_id")] public string UserId { get; init; } = string.Empty;
        [JsonPropertyName("pickup_location")] public string PickupLocation { get; init; } = string.Empty;
        [JsonPropertyName("destination")] public string Destination { get; init; } = string.Empty;
        [JsonPropertyName("requested_pickup_at")] public DateTime RequestedPickupAt { get; init; }
        [JsonPropertyName("passenger_count")] public int PassengerCount { get; init; }
        [JsonPropertyName("consideration_notes")] public string? ConsiderationNotes { get; init; }
        [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
        [JsonPropertyName("cancellation_reason")] public string? CancellationReason { get; init; }
        [JsonPropertyName("cancelled_at")] public DateTime? CancelledAt { get; init; }
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
    }

    public class ReservationSummaryResponse
    {
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("reservation_number")] public string ReservationNumber { get; init; } = string.Empty;
        [JsonPropertyName("pickup_location")] public string PickupLocation { get; init; } = string.Empty;
        [JsonPropertyName("destination")] public string Destination { get; init; } = string.Empty;
        [JsonPropertyName("requested_pickup_at")] public DateTime RequestedPickupAt { get; init; }
        [JsonPropertyName("passenger_count")] public int PassengerCount { get; init; }
        [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    }

    public class CancelReservationResponse
    {
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
        [JsonPropertyName("cancellation_reason")] public string? CancellationReason { get; init; }
        [JsonPropertyName("cancelled_at")] public DateTime? CancelledAt { get; init; }
    }
}
