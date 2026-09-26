const RoadRideReservationApi = (() => {
  const apiBase = "/api";

  const careLabels = {
    wheelchair: "車いす",
    "large-luggage": "大きな荷物",
    other: "その他"
  };

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
      throw new Error("ログインの有効期限が切れました。再ログインしてください。");
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

    const instant = new Date(value);
    if (Number.isNaN(instant.getTime())) {
      return { date: "", hour: "", minute: "" };
    }
    const jst = new Date(instant.getTime() + 9 * 60 * 60 * 1000).toISOString();
    return { date: jst.slice(0, 10), hour: jst.slice(11, 13), minute: jst.slice(14, 16) };
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
      pickup_location: formValue.pickup,
      destination: formValue.destination,
      requested_pickup_at: buildRequestedPickupAt(formValue),
      passenger_count: Number(formValue.passengers),
      consideration_notes: buildConsiderationNotes(formValue)
    };

    if (!payload.pickup_location.trim() || payload.pickup_location.length > 200 ||
        !payload.destination.trim() || payload.destination.length > 200) {
      throw new Error("乗車地と目的地は1〜200文字で入力してください。");
    }
    if (!Number.isInteger(payload.passenger_count) || payload.passenger_count < 1) {
      throw new Error("乗車人数は1人以上の整数で入力してください。");
    }
    if (payload.consideration_notes.length > 500) {
      throw new Error("配慮事項は選択項目と備考を合わせて500文字以内で入力してください。");
    }
    const result = await fetchJson("/reservations", {
      method: "POST",
      body: JSON.stringify(payload)
    });
    const reservation = toUiReservation(result.data);

    reservation.care = formValue.care;
    reservation.notes = formValue.notes;

    return reservation;
  }

  async function listReservations() {
    const reservations = [];
    let page = 1;
    let result;
    do {
      const params = new URLSearchParams({ page: String(page), limit: "50" });
      result = await fetchJson(`/reservations?${params}`);
      reservations.push(...result.data.map(toUiReservation));
      page += 1;
    } while (result.data.length > 0 && reservations.length < result.meta.total);
    return reservations;
  }

  async function getReservation(reservationId) {
    if (!reservationId) throw new Error("予約IDがありません。予約履歴から選び直してください。");
    const result = await fetchJson(`/reservations/${encodeURIComponent(reservationId)}`);
    return toUiReservation(result.data);
  }

  async function cancelReservation(reservation, reason) {
    if (!reservation.reservationId) throw new Error("予約IDがありません。予約履歴から選び直してください。");
    if (reason && reason.length > 500) throw new Error("キャンセル理由は500文字以内で入力してください。");
    const result = await fetchJson(
      `/reservations/${encodeURIComponent(reservation.reservationId)}/cancel`,
      { method: "POST", body: JSON.stringify({ reason }) }
    );
    return {
      ...reservation,
      status: result.data.status,
      cancellationReason: result.data.cancellation_reason,
      cancelledAt: result.data.cancelled_at
    };
  }

  function escapeHtml(value) {
    return String(value).replace(/[&<>"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[char]);
  }

  function showError(error) {
    let element = document.getElementById("reservation-error");
    if (!element) {
      element = document.createElement("p");
      element.id = "reservation-error";
      element.setAttribute("role", "alert");
      (document.querySelector("main") || document.body).prepend(element);
    }
    element.textContent = error.message || "通信に失敗しました。時間をおいて再度お試しください。";
  }

  return {
    createReservation,
    listReservations,
    getReservation,
    cancelReservation,
    fromParams,
    toParams,
    showError,
    escapeHtml
  };
})();
