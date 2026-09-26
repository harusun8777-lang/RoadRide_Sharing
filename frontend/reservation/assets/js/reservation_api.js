/**
 * reservation_api.js - 予約 API（/api/reservations 系）の呼び出しと、予約画面で共通の表示処理
 *
 * - API の呼び出しは RoadRideAuth.apiFetch に任せる（認証ヘッダー、401 時のログイン画面への移動、
 *   エラーの日本語化）。失敗時は RoadRideAuth.ApiError がそのまま投げられる
 * - 予約データは API のレスポンス（スネークケース）をそのまま扱う
 * - API の日時は UTC で返るため、表示するときは日本時間（Asia/Tokyo）に変換する
 */
const RoadRideReservationApi = (() => {
  const TIME_ZONE = "Asia/Tokyo";

  const careLabels = {
    wheelchair: "車いす",
    "large-luggage": "大きな荷物",
    other: "その他"
  };

  // docs/ENDPOINT.md の予約状態（status）
  const statusLabels = {
    matching: "マッチング中",
    confirmed: "予約確定",
    in_progress: "乗車中",
    completed: "乗車完了",
    cancelled: "キャンセル済み"
  };

  // 表示の色分け（info / success / danger）
  const statusTones = {
    matching: "info",
    confirmed: "success",
    in_progress: "info",
    completed: "success",
    cancelled: "danger"
  };

  // API でキャンセルできる状態
  const cancellableStatuses = ["matching", "confirmed"];

  // ─────────────────────────────────────────
  // 日時（日本時間）
  // ─────────────────────────────────────────

  const tokyoFormatter = new Intl.DateTimeFormat("en-CA", {
    timeZone: TIME_ZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23"
  });

  /**
   * 日時を日本時間の年・月・日・時・分に分ける。解釈できなければ null
   * @param {string|Date} value API の日時（末尾 Z またはオフセット付き）か Date
   */
  function toTokyoParts(value) {
    if (!value) return null;

    let date = value;
    if (!(value instanceof Date)) {
      // タイムゾーンの指定がない文字列は UTC として扱う（API の日時は UTC のため）
      const text = String(value);
      const hasZone = /(?:[zZ]|[+-]\d{2}:?\d{2})$/.test(text);
      date = new Date(hasZone || !text.includes("T") ? text : `${text}Z`);
    }
    if (Number.isNaN(date.getTime())) return null;

    const parts = {};
    tokyoFormatter.formatToParts(date).forEach(({ type, value: part }) => {
      parts[type] = part;
    });
    const hour = parts.hour === "24" ? "00" : parts.hour;

    return {
      year: parts.year,
      month: parts.month,
      day: parts.day,
      hour,
      minute: parts.minute,
      date: `${parts.year}-${parts.month}-${parts.day}`
    };
  }

  /** "2026/10/01" */
  function formatDate(value) {
    const parts = toTokyoParts(value);
    return parts ? `${parts.year}/${parts.month}/${parts.day}` : "---";
  }

  /** "09:00" */
  function formatTime(value) {
    const parts = toTokyoParts(value);
    return parts ? `${parts.hour}:${parts.minute}` : "---";
  }

  /** "2026/10/01 09:00" */
  function formatDateTime(value) {
    const parts = toTokyoParts(value);
    return parts ? `${parts.year}/${parts.month}/${parts.day} ${parts.hour}:${parts.minute}` : "---";
  }

  /** "10月1日 09:00" */
  function formatShortDateTime(value) {
    const parts = toTokyoParts(value);
    return parts ? `${Number(parts.month)}月${Number(parts.day)}日 ${parts.hour}:${parts.minute}` : "---";
  }

  /** 日本時間の今日から days 日後の日付（YYYY-MM-DD） */
  function tokyoDateAfter(days = 0) {
    return toTokyoParts(new Date(Date.now() + days * 24 * 60 * 60 * 1000)).date;
  }

  /** 入力値（日本時間の日付・時・分）を API に送る形式にする */
  function buildRequestedPickupAt({ date, hour, minute }) {
    return `${date}T${hour}:${minute}:00+09:00`;
  }

  // ─────────────────────────────────────────
  // 表示用の値
  // ─────────────────────────────────────────

  function statusLabel(status) {
    return statusLabels[status] || status || "---";
  }

  function statusTone(status) {
    return statusTones[status] || "info";
  }

  function isCancellable(reservation) {
    return cancellableStatuses.includes(reservation?.status);
  }

  function careLabel(care) {
    return careLabels[care] || "";
  }

  function buildConsiderationNotes(formValue) {
    return [careLabel(formValue.care), (formValue.notes || "").trim()].filter(Boolean).join(" / ");
  }

  // ─────────────────────────────────────────
  // 画面間で入力値を渡す URL パラメーター（予約登録の入力 → 確認）
  // ─────────────────────────────────────────

  const formKeys = ["pickup", "destination", "date", "hour", "minute", "passengers", "care", "notes"];

  function formFromParams(params) {
    const formValue = {};
    formKeys.forEach((key) => {
      formValue[key] = params.get(key) || "";
    });
    return formValue;
  }

  function formToParams(formValue) {
    const params = new URLSearchParams();
    formKeys.forEach((key) => {
      if (formValue[key]) params.set(key, formValue[key]);
    });
    return params;
  }

  /** URL の ?id= から予約IDを取り出す */
  function reservationIdFromUrl() {
    return new URLSearchParams(window.location.search).get("id") || "";
  }

  function pageUrl(pageName, reservationId) {
    return reservationId ? `${pageName}?id=${encodeURIComponent(reservationId)}` : pageName;
  }

  // ─────────────────────────────────────────
  // API
  // ─────────────────────────────────────────

  /**
   * POST /api/reservations。登録した予約（API の Reservation）を返す
   * @param {{pickup: string, destination: string, date: string, hour: string, minute: string,
   *          passengers: string, care?: string, notes?: string}} formValue
   */
  async function createReservation(formValue) {
    const body = {
      pickup_location: formValue.pickup.trim(),
      destination: formValue.destination.trim(),
      requested_pickup_at: buildRequestedPickupAt(formValue),
      passenger_count: Number(formValue.passengers)
    };
    const notes = buildConsiderationNotes(formValue);
    if (notes) body.consideration_notes = notes;

    const fieldErrors = {};
    if (!body.pickup_location || body.pickup_location.length > 200) fieldErrors.pickup_location = "乗車場所は1〜200文字で入力してください。";
    if (!body.destination || body.destination.length > 200) fieldErrors.destination = "目的地は1〜200文字で入力してください。";
    if (!Number.isInteger(body.passenger_count) || body.passenger_count < 1) fieldErrors.passenger_count = "乗車人数は1人以上の整数で入力してください。";
    if (notes.length > 500) fieldErrors.consideration_notes = "配慮事項は選択項目と備考を合わせて500文字以内で入力してください。";
    if (Number.isNaN(Date.parse(body.requested_pickup_at))) fieldErrors.requested_pickup_at = "希望乗車日時を正しく入力してください。";
    if (Object.keys(fieldErrors).length) throw new RoadRideAuth.ApiError(Object.values(fieldErrors)[0], { status: 422, code: "VALIDATION_ERROR", fieldErrors });
    const payload = await RoadRideAuth.apiFetch("/reservations", { method: "POST", body });
    return payload.data;
  }

  /**
   * GET /api/reservations。{ data: 予約の配列, meta: { page, limit, total } } を返す
   * @param {{status?: string, date?: string, page?: number, limit?: number}} query
   */
  async function listReservations({ status, date, page = 1, limit = 20 } = {}) {
    const params = new URLSearchParams({ page: String(page), limit: String(limit) });
    if (status) params.set("status", status);
    if (date) params.set("date", date);

    const payload = await RoadRideAuth.apiFetch(`/reservations?${params}`);
    return {
      data: payload.data || [],
      meta: payload.meta || { page, limit, total: (payload.data || []).length }
    };
  }

  /** GET /api/reservations/{id}。予約（API の Reservation）を返す */
  async function getReservation(reservationId) {
    if (!reservationId) throw new RoadRideAuth.ApiError("予約が指定されていません。予約履歴から選び直してください。");
    const payload = await RoadRideAuth.apiFetch(`/reservations/${encodeURIComponent(reservationId)}`, {
      errorMessages: { 404: "指定された予約が見つかりません。" }
    });
    return payload.data;
  }

  /** POST /api/reservations/{id}/cancel。{ id, status, cancellation_reason, cancelled_at } を返す */
  async function cancelReservation(reservationId, reason = "") {
    if (!reservationId) throw new RoadRideAuth.ApiError("予約が指定されていません。予約履歴から選び直してください。");
    const trimmedReason = reason.trim();
    if (trimmedReason.length > 500) throw new RoadRideAuth.ApiError("キャンセル理由は500文字以内で入力してください。");
    const payload = await RoadRideAuth.apiFetch(`/reservations/${encodeURIComponent(reservationId)}/cancel`, {
      method: "POST",
      body: trimmedReason ? { reason: trimmedReason } : {},
      errorMessages: {
        404: "指定された予約が見つかりません。",
        409: "この予約はキャンセルできない状態です。"
      }
    });
    return payload.data;
  }

  // ─────────────────────────────────────────
  // エラー・読み込み中の表示
  // ─────────────────────────────────────────

  /** 例外を画面に出せる日本語のメッセージにする */
  function errorMessage(error) {
    if (error instanceof RoadRideAuth.ApiError) return error.message;
    console.error(error);
    return "処理に失敗しました。時間をおいて再度お試しください。";
  }

  /** メッセージを要素に表示する（空なら隠す） */
  function showMessage(element, message) {
    if (!element) return;
    element.textContent = message || "";
    element.hidden = !message;
  }

  /**
   * [data-field-error="項目名"] の要素に fieldErrors のメッセージを表示する。表示した件数を返す
   * @param {ParentNode} root
   * @param {Object<string, string>} fieldErrors スネークケースの項目名 → 日本語メッセージ
   */
  function showFieldErrors(root, fieldErrors = {}) {
    let count = 0;
    root.querySelectorAll("[data-field-error]").forEach((element) => {
      const message = fieldErrors[element.dataset.fieldError] || "";
      showMessage(element, message);
      if (message) count += 1;
    });
    return count;
  }

  return {
    careLabels,
    statusLabels,
    toTokyoParts,
    formatDate,
    formatTime,
    formatDateTime,
    formatShortDateTime,
    tokyoDateAfter,
    buildRequestedPickupAt,
    statusLabel,
    statusTone,
    isCancellable,
    careLabel,
    formFromParams,
    formToParams,
    reservationIdFromUrl,
    pageUrl,
    createReservation,
    listReservations,
    getReservation,
    cancelReservation,
    errorMessage,
    showMessage,
    showFieldErrors
  };
})();
