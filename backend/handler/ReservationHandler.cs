using Microsoft.AspNetCore.Mvc;
using Usecase.Reservation;
using DomainReservation = Domain.Reservations.Reservation;
using DomainReservationStatus = Domain.Reservations.ReservationStatus;
using UserNotFoundException = Usecase.User.UserNotFoundException;

namespace Handler.Reservations
{
    public static class ReservationHandler
    {
        public static RouteGroupBuilder MapReservationEndpoints(this RouteGroupBuilder group)
        {
            group.MapPost("/reservations", RegisterAsync).WithName("RegisterReservation");
            group.MapGet("/reservations", ListAsync).WithName("ListReservations");
            group.MapGet("/reservations/{reservationId:guid}", GetAsync).WithName("GetReservation");
            group.MapPost("/reservations/{reservationId:guid}/cancel", CancelAsync).WithName("CancelReservation");
            return group;
        }

        private static async Task<IResult> RegisterAsync(
            RegisterReservationRequest request,
            RegisterReservationUseCase useCase)
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                return ValidationError("user_id", "利用者IDの形式が不正です");

            try
            {
                var reservation = await useCase.ExecuteAsync(
                    userId,
                    request.PickupLocation,
                    request.Destination,
                    request.RequestedPickupAt,
                    request.PassengerCount,
                    request.ConsiderationNotes);

                return Results.Created(
                    $"/api/reservations/{reservation.Id}",
                    new DataResponse<ReservationResponse> { Data = ToResponse(reservation) });
            }
            catch (UserNotFoundException)
            {
                return ValidationError("user_id", "指定された利用者が見つかりません");
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.ParamName ?? "request", ex.Message);
            }
        }

        private static async Task<IResult> ListAsync(
            ListReservationsUseCase useCase,
            [FromQuery(Name = "user_id")] Guid? userId,
            DateOnly? date,
            string? status,
            DateTime? from,
            DateTime? to,
            int page = 1,
            int limit = 50)
        {
            DomainReservationStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!TryParseStatus(status, out var s))
                    return ValidationError("status", "予約状態の値が不正です");
                parsedStatus = s;
            }

            var filter = new ReservationListFilter(userId, date, parsedStatus, from, to, page, limit);
            var (items, total) = await useCase.ExecuteAsync(filter);

            return Results.Ok(new ListResponse<ReservationSummaryResponse>
            {
                Data = items.Select(ToSummaryResponse),
                Meta = new PageMeta { Page = page, Limit = limit, Total = total }
            });
        }

        private static async Task<IResult> GetAsync(
            Guid reservationId,
            GetReservationUseCase useCase)
        {
            try
            {
                var reservation = await useCase.ExecuteAsync(reservationId);
                return Results.Ok(new DataResponse<ReservationResponse> { Data = ToResponse(reservation) });
            }
            catch (ReservationNotFoundException)
            {
                return NotFoundError("指定された予約が見つかりません");
            }
        }

        private static async Task<IResult> CancelAsync(
            Guid reservationId,
            CancelReservationRequest request,
            CancelReservationUseCase useCase)
        {
            try
            {
                var reservation = await useCase.ExecuteAsync(reservationId, request.Reason);
                return Results.Ok(new DataResponse<CancelReservationResponse>
                {
                    Data = new CancelReservationResponse
                    {
                        Id = reservation.Id.ToString(),
                        Status = ToStatusString(reservation.Status),
                        CancellationReason = reservation.CancellationReason,
                        CancelledAt = reservation.CancelledAt
                    }
                });
            }
            catch (ReservationNotFoundException)
            {
                return NotFoundError("指定された予約が見つかりません");
            }
            catch (InvalidOperationException ex)
            {
                return ConflictError(ex.Message);
            }
        }

        private static ReservationResponse ToResponse(DomainReservation reservation) => new()
        {
            Id = reservation.Id.ToString(),
            ReservationNumber = reservation.ReservationNumber,
            UserId = reservation.UserId.ToString(),
            PickupLocation = reservation.PickupLocation,
            Destination = reservation.Destination,
            RequestedPickupAt = reservation.RequestedPickupAt,
            PassengerCount = reservation.PassengerCount,
            ConsiderationNotes = reservation.ConsiderationNotes,
            Status = ToStatusString(reservation.Status),
            CancellationReason = reservation.CancellationReason,
            CancelledAt = reservation.CancelledAt,
            CreatedAt = reservation.CreatedAt
        };

        private static ReservationSummaryResponse ToSummaryResponse(DomainReservation reservation) => new()
        {
            Id = reservation.Id.ToString(),
            ReservationNumber = reservation.ReservationNumber,
            PickupLocation = reservation.PickupLocation,
            Destination = reservation.Destination,
            RequestedPickupAt = reservation.RequestedPickupAt,
            PassengerCount = reservation.PassengerCount,
            Status = ToStatusString(reservation.Status)
        };

        private static string ToStatusString(DomainReservationStatus status) => status switch
        {
            DomainReservationStatus.Matching => "matching",
            DomainReservationStatus.Confirmed => "confirmed",
            DomainReservationStatus.InProgress => "in_progress",
            DomainReservationStatus.Completed => "completed",
            DomainReservationStatus.Cancelled => "cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        private static bool TryParseStatus(string value, out DomainReservationStatus status)
        {
            switch (value)
            {
                case "matching": status = DomainReservationStatus.Matching; return true;
                case "confirmed": status = DomainReservationStatus.Confirmed; return true;
                case "in_progress": status = DomainReservationStatus.InProgress; return true;
                case "completed": status = DomainReservationStatus.Completed; return true;
                case "cancelled": status = DomainReservationStatus.Cancelled; return true;
                default: status = default; return false;
            }
        }

        private static IResult ValidationError(string field, string message) => Results.UnprocessableEntity(new ErrorResponse
        {
            Error = new ErrorBody
            {
                Code = "VALIDATION_ERROR",
                Message = "入力内容を確認してください",
                Details = [new ErrorDetail { Field = field, Message = message }]
            }
        });

        private static IResult NotFoundError(string message) => Results.NotFound(new ErrorResponse
        {
            Error = new ErrorBody { Code = "NOT_FOUND", Message = message }
        });

        private static IResult ConflictError(string message) => Results.Conflict(new ErrorResponse
        {
            Error = new ErrorBody { Code = "CONFLICT", Message = message }
        });
    }
}
