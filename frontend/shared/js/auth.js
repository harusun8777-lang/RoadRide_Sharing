/**
 * auth.js - 認証と API 呼び出しの共通モジュール（docs/ENDPOINT.md に対応）
 *
 * - JWT を localStorage に保存・取得・削除する
 * - apiFetch で API を呼ぶ。認証ヘッダーの付与、401 時のログイン画面への移動、
 *   エラーの日本語化（ApiError）をまとめて行う
 * - 画面の URL は配信のルート（frontend/）からの絶対パスで扱う
 */

const RoadRideAuth = (() => {
  const TOKEN_KEY   = "roadride_access_token";
  const EXPIRES_KEY = "roadride_token_expires_at";
  const API_BASE    = "/api";

  const LOGIN_PATH = "/login/index.html";
  const TOP_PATH   = "/top/index.html";

  // VALIDATION_ERROR の details[].field（キャメルケースのものがある）→ リクエストのキー（スネークケース）
  const FIELD_KEYS = {
    email: "email",
    password: "password",
    lastName: "last_name",
    firstName: "first_name",
    kanaLastName: "kana_last_name",
    kanaFirstName: "kana_first_name",
    role: "role",
    pickupLocation: "pickup_location",
    destination: "destination",
    passengerCount: "passenger_count",
    requestedPickupAt: "requested_pickup_at",
    status: "status"
  };

  // 項目ごとの日本語メッセージ（API のメッセージは英語のものがあるため、画面ではこちらを使う）
  const FIELD_MESSAGES = {
    email: "メールアドレスを入力してください。",
    password: "パスワードは8〜128文字で入力してください。",
    last_name: "姓を入力してください。",
    first_name: "名を入力してください。",
    kana_last_name: "セイは全角カタカナで入力してください。",
    kana_first_name: "メイは全角カタカナで入力してください。",
    role: "利用者区分の値が正しくありません。",
    pickup_location: "乗車場所を入力してください。",
    destination: "目的地を入力してください。",
    passenger_count: "乗車人数は1人以上で入力してください。",
    requested_pickup_at: "希望乗車日時を正しく入力してください。",
    status: "予約状態の値が正しくありません。"
  };

  const STATUS_MESSAGES = {
    400: "入力内容の形式が正しくありません。",
    401: "ログインの有効期限が切れました。もう一度ログインしてください。",
    404: "指定されたデータが見つかりません。",
    409: "現在の状態ではこの操作を行えません。",
    422: "入力内容を確認してください。"
  };

  /** API のエラーを画面で扱いやすい形にしたもの */
  class ApiError extends Error {
    /**
     * @param {string} message 画面に表示する日本語のメッセージ
     * @param {{status?: number, code?: string, fieldErrors?: Object<string, string>}} info
     */
    constructor(message, { status = 0, code = "", fieldErrors = {} } = {}) {
      super(message);
      this.name = "ApiError";
      this.status = status;          // HTTP ステータス（通信エラーは 0）
      this.code = code;              // error.code（VALIDATION_ERROR など。本文なしのエラーは空）
      this.fieldErrors = fieldErrors; // { スネークケースの項目名: 日本語メッセージ }
    }
  }

  // ─────────────────────────────────────────
  // トークンの保存 / 取得 / 削除
  // ─────────────────────────────────────────

  function saveToken(accessToken, expiresAt) {
    localStorage.setItem(TOKEN_KEY, accessToken);
    localStorage.setItem(EXPIRES_KEY, expiresAt);
  }

  function getToken() {
    return localStorage.getItem(TOKEN_KEY) || null;
  }

  function isTokenValid() {
    const token = getToken();
    const expiresAt = localStorage.getItem(EXPIRES_KEY);
    if (!token || !expiresAt) return false;
    return new Date(expiresAt) > new Date();
  }

  function clearToken() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EXPIRES_KEY);
  }

  // ─────────────────────────────────────────
  // 画面遷移
  // ─────────────────────────────────────────

  /**
   * POST /api/auth/login
   * 成功時にトークンを保存して true を返す
   * 失敗時はエラーをスローする
   */
  async function login(email, password) {
  const response = await fetch(`${API_BASE}/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Accept": "application/json"
    },
    body: JSON.stringify({ email, password })
  });

  const payload = await response.json().catch(() => ({}));

  if (!response.ok) {
    const message =
      response.status === 401
        ? "メールアドレスまたはパスワードが正しくありません。"
        : payload.error?.message || "ログインに失敗しました。";
    throw new Error(message);
  }

  const { access_token, expires_at } = payload.data;
  saveToken(access_token, expires_at);
  return true;
}

  /**
   * POST /api/users
   * 利用者登録。成功後に自動でログインしてトークンを保存する
   */
  async function register({ email, password, lastName, firstName, kanaLastName, kanaFirstName, role = "rider" }) {
    const response = await fetch(`${API_BASE}/users`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Accept": "application/json"
      },
      body: JSON.stringify({
        email,
        password,
        last_name:       lastName,
        first_name:      firstName,
        kana_last_name:  kanaLastName,
        kana_first_name: kanaFirstName,
        role
      })
    });

    const payload = await response.json().catch(() => ({}));

    if (!response.ok) {
      const message =
        response.status === 409
          ? "このメールアドレスはすでに登録されています。"
          : payload.error?.message || "登録に失敗しました。";
      throw new Error(message);
    }
    return `${LOGIN_PATH}?${params}`;
  }

  function redirectToLogin(role) {
    clearToken();
    window.location.assign(loginUrl(role));
  }

  /**
   * 認証が必要なページで最初に呼ぶ。トークンがないか期限切れならログイン画面へ移動して false を返す
   * @param {"user"|"dispatcher"} role
   */
  function requireAuth(role = currentRole()) {
    if (isTokenValid()) return true;
    redirectToLogin(role);
    return false;
  }

  /** トークンを削除してトップページへ戻る */
  function logout() {
    clearToken();
    window.location.assign(TOP_PATH);
  }

  // ─────────────────────────────────────────
  // API 呼び出し
  // ─────────────────────────────────────────

  function toApiError(status, payload, fallbackMessage) {
    const error = payload?.error;
    const fieldErrors = {};

    if (error?.code === "VALIDATION_ERROR" && Array.isArray(error.details)) {
      error.details.forEach((detail) => {
        const key = FIELD_KEYS[detail.field] || detail.field;
        fieldErrors[key] = FIELD_MESSAGES[key] || "入力内容を確認してください。";
      });
    }

    const firstFieldMessage = Object.values(fieldErrors)[0];
    // CONFLICT などのメッセージは日本語で返るものが多いが、英語のもの（例：キャンセル不可）は使わない
    const apiMessage = error?.message && /[぀-ヿ一-鿿]/.test(error.message) ? error.message : "";
    // 優先順位: 項目ごとのメッセージ → 画面が指定した文言（errorMessages） → API の日本語メッセージ → ステータスごとの既定
    const message = firstFieldMessage || fallbackMessage || apiMessage || STATUS_MESSAGES[status]
      || (status >= 500 ? "サーバーでエラーが発生しました。時間をおいて再度お試しください。" : "処理に失敗しました。");

    return new ApiError(message, { status, code: error?.code || "", fieldErrors });
  }

  /**
   * API を呼ぶ。成功時はレスポンスの JSON（{ data, meta }）を返し、失敗時は ApiError を投げる
   * @param {string} path "/reservations" のような /api 以降のパス
   * @param {{method?: string, body?: any, auth?: boolean, errorMessages?: Object<number, string>}} options
   *   auth: false でトークンを付けない。401 でもログイン画面へ移動しない
   *   errorMessages: ステータスごとに表示したいメッセージ（API のメッセージより優先）
   */
  async function apiFetch(path, { method = "GET", body, auth = true, errorMessages = {} } = {}) {
    const headers = { Accept: "application/json" };
    if (body !== undefined) headers["Content-Type"] = "application/json";

    if(!token){
      throw new Error("authFetch function required token in localstrage");
    }

    const response = await fetch(`${API_BASE}${path}`, {
      ...options,
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        "Authorization": `Bearer ${token}`
      }
      headers.Authorization = `Bearer ${token}`;
    }

    let response;
    try {
      response = await fetch(`${API_BASE}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body)
      });
    } catch {
      throw new ApiError("サーバーに接続できません。通信環境を確認してください。", { status: 0 });
    }

    const payload = await response.json().catch(() => null);

    if (response.status === 401 && auth) {
      redirectToLogin();
      throw new ApiError(STATUS_MESSAGES[401], { status: 401 });
    }

    if (!response.ok) {
      throw toApiError(response.status, payload, errorMessages[response.status]);
    }

    return payload;
  }

  // ─────────────────────────────────────────
  // 認証 API
  // ─────────────────────────────────────────

  /** POST /api/auth/login。成功したらトークンを保存する */
  async function login(email, password) {
    const payload = await apiFetch("/auth/login", {
      method: "POST",
      auth: false,
      body: { email, password },
      errorMessages: { 401: "メールアドレスまたはパスワードが正しくありません。" }
    });
    saveToken(payload.data.access_token, payload.data.expires_at);
    return payload.data;
  }

  /**
   * POST /api/users で登録し、続けてログインする。登録したユーザー情報を返す
   * @param {{email: string, password: string, lastName: string, firstName: string,
   *          kanaLastName: string, kanaFirstName: string, role?: "rider"|"driver"}} input
   */
  async function register({ email, password, lastName, firstName, kanaLastName, kanaFirstName, role = "rider" }) {
    const payload = await apiFetch("/users", {
      method: "POST",
      auth: false,
      body: {
        email,
        password,
        last_name: lastName,
        first_name: firstName,
        kana_last_name: kanaLastName,
        kana_first_name: kanaFirstName,
        role
      },
      errorMessages: { 409: "このメールアドレスはすでに登録されています。" }
    });

    try {
      await login(email, password);
    } catch (error) {
      // アカウントは作成済みなので、登録の失敗と区別できるようにする（同じ内容で送り直すと 409 になるため）
      throw new ApiError("登録は完了しましたが、ログインできませんでした。ログイン画面からログインしてください。", {
        status: error.status ?? 0,
        code: "REGISTERED_BUT_LOGIN_FAILED"
      });
    }
    return payload.data;
  }

  /** GET /api/users/me。ログイン中のユーザー情報（ENDPOINT.md の User）を返す */
  async function getMe() {
    const payload = await apiFetch("/users/me");
    return payload.data;
  }

  /** PUT /api/users/me/active-role。稼働中の区分を切り替え、ユーザー情報を返す */
  async function switchActiveRole(role) {
    const payload = await apiFetch("/users/me/active-role", { method: "PUT", body: { role } });
    return payload.data;
  }

  /** POST /api/users/me/roles。区分を追加し、ユーザー情報を返す */
  async function addRole(role) {
    const payload = await apiFetch("/users/me/roles", { method: "POST", body: { role } });
    return payload.data;
  }

  return {
    ApiError,
    apiFetch,
    login,
    register,
    logout,
    getMe,
    switchActiveRole,
    addRole,
    requireAuth,
    getToken,
    isTokenValid,
    saveToken,
    clearToken,
    loginUrl
  };
})();
