const RoadRideReservationApi = (() => {
  const apiBase = "/api";
  const userId = "user-001";
  const historyKey = "roadrideReservationHistory";

  const careLabels = {
    wheelchair: "車いす",
    "large-luggage": "大きな荷物",
    other: "その他"
  };

  function readHistory() {
    try {
      return JSON.parse(localStorage.getItem(historyKey)) || [];
    } catch {
      return [];
    }
  }

  function writeHistory(history) {
    localStorage.setItem(historyKey, JSON.stringify(history));
  }

  function mergeHistory(reservation) {
    if (!reservation.reservationNumber && !reservation.reservationId) {
      return;
    }

    const history = readHistory();
    const nextHistory = [
      reservation,
      ...history.filter((item) => {
        if (reservation.reservationId && item.reservationId === reservation.reservationId) {
          return false;
        }

        return item.reservationNumber !== reservation.reservationNumber;
      })
    ];

    writeHistory(nextHistory);
  }

  function mergeReservation(baseReservation, nextReservation) {
    const mergedReservation = { ...baseReservation };

    Object.entries(nextReservation).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== "") {
        mergedReservation[key] = value;
      }
    });

    return mergedReservation;
  }

  function createIdempotencyKey() {
    if (window.crypto?.randomUUID) {
      return window.crypto.randomUUID();
    }

    return `reservation-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  }

  async function fetchJson(path, options = {}) {
    // RoadRideAuth が読み込まれていればトークンを付与する
    const token = (typeof RoadRideAuth !== "undefined") ? RoadRideAuth.getToken() : null;

    const response = await fetch(`${apiBase}${path}`, {
      ...options,
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...options.headers
      }
    });

    const payload = await response.json().catch(() => ({}));

    // 401 の場合はトークンを削除してログイン画面へ戻す
    if (response.status === 401) {
      if (typeof RoadRideAuth !== "undefined") {
        RoadRideAuth.clearToken();
        window.location.assign("/frontend/login/index.html?role=user");
      }
      return null;
    }

    if (!response.ok) {
      const message =
        payload.error?.message || "APIリクエストの処理に失敗しました。";
      throw new Error(message);
    }

    return payload;
  }

  function buildRequestedPickupAt({ date, hour, minute }) {
    return `${date}T${hour}:${minute}:00+09:00`;
  }

  function splitRequestedPickupAt(value) {
    if (!value) {
      return { date: "", hour: "", minute: "" };
    }

    const match = value.match(/^(\d{4}-\d{2}-\d{2})T(\d{2}):(\d{2})/);

    if (!match) {
      return { date: "", hour: "", minute: "" };
    }

    return {
      date: match[1],
      hour: match[2],
      minute: match[3]
    };
  }

  function buildConsiderationNotes(formValue) {
    const care = careLabels[formValue.care] || "";

    return [care, formValue.notes].filter(Boolean).join(" / ");
  }

  function toUiReservation(apiReservation) {
    const schedule = splitRequestedPickupAt(apiReservation.requested_pickup_at);

    return {
      reservationId: apiReservation.id || "",
      reservationNumber: apiReservation.reservation_number || "",
      pickup: apiReservation.pickup_location || "",
      destination: apiReservation.destination || "",
      date: schedule.date,
      hour: schedule.hour,
      minute: schedule.minute,
      passengers: String(apiReservation.passenger_count || ""),
      care: "",
      notes: apiReservation.consideration_notes || "",
      status: apiReservation.status || "matching",
      savedAt: apiReservation.created_at || new Date().toISOString()
    };
  }

  function fromParams(params) {
    return {
      reservationId: params.get("reservationId") || "",
      reservationNumber: params.get("reservationNumber") || "",
      pickup: params.get("pickup") || "",
      destination: params.get("destination") || "",
      date: params.get("date") || "",
      hour: params.get("hour") || "",
      minute: params.get("minute") || "",
      passengers: params.get("passengers") || "",
      care: params.get("care") || "",
      notes: params.get("notes") || "",
      status: params.get("status") || "matching"
    };
  }

  function toParams(reservation) {
    const params = new URLSearchParams();

    [
      "reservationId",
      "reservationNumber",
      "pickup",
      "destination",
      "date",
      "hour",
      "minute",
      "passengers",
      "care",
      "notes",
      "status"
    ].forEach((key) => {
      if (reservation[key]) {
        params.set(key, reservation[key]);
      }
    });

    return params;
  }

  async function createReservation(formValue) {
    const payload = {
      user_id: userId,
      pickup_location: formValue.pickup,
      destination: formValue.destination,
      requested_pickup_at: buildRequestedPickupAt(formValue),
      passenger_count: Number(formValue.passengers),
      consideration_notes: buildConsiderationNotes(formValue)
    };

    const result = await fetchJson("/reservations", {
      method: "POST",
      headers: {
        "Idempotency-Key": createIdempotencyKey()
      },
      body: JSON.stringify(payload)
    });
    const reservation = toUiReservation(result.data);

    reservation.care = formValue.care;
    reservation.notes = formValue.notes;
    mergeHistory(reservation);

    return reservation;
  }

  async function listReservations() {
    try {
      const params = new URLSearchParams({ user_id: userId, limit: "50" });
      const result = await fetchJson(`/reservations?${params.toString()}`);
      const reservations = (result.data || []).map(toUiReservation);

      reservations.forEach(mergeHistory);
      return reservations;
    } catch {
      return readHistory();
    }
  }

  async function getReservation(reservationId, fallbackReservation) {
    if (!reservationId) {
      return fallbackReservation;
    }

    try {
      const result = await fetchJson(`/reservations/${encodeURIComponent(reservationId)}`);
      const reservation = mergeReservation(
        fallbackReservation,
        toUiReservation(result.data)
      );

      mergeHistory(reservation);
      return reservation;
    } catch {
      return fallbackReservation;
    }
  }

  async function cancelReservation(reservation, reason) {
    if (reservation.reservationId) {
      try {
        const result = await fetchJson(
          `/reservations/${encodeURIComponent(reservation.reservationId)}/cancel`,
          {
            method: "POST",
            body: JSON.stringify({ reason })
          }
        );
        const cancelledReservation = {
          ...reservation,
          status: result.data?.status || "cancelled",
          cancellationReason: result.data?.cancellation_reason || reason
        };

        mergeHistory(cancelledReservation);
        return cancelledReservation;
      } catch {
      }
    }

    const cancelledReservation = {
      ...reservation,
      status: "cancelled",
      cancellationReason: reason,
      cancelledAt: new Date().toISOString()
    };

    mergeHistory(cancelledReservation);
    return cancelledReservation;
  }

  return {
    createReservation,
    listReservations,
    getReservation,
    cancelReservation,
    fromParams,
    toParams,
    mergeHistory,
    readHistory,
    writeHistory
  };
})();
