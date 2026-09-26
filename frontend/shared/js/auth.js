/**
 * auth.js - 認証・トークン管理の共通モジュール
 *
 * - JWT を localStorage に保存・取得・削除する
 * - 全 API 呼び出しに Authorization ヘッダーを自動付与する
 * - 未認証ページへのアクセスをログイン画面にリダイレクトする
 */

const RoadRideAuth = (() => {
  const TOKEN_KEY    = "roadride_access_token";
  const EXPIRES_KEY  = "roadride_token_expires_at";
  const API_BASE     = "/api";

  // ─────────────────────────────────────────
  // トークンの保存 / 取得 / 削除
  // ─────────────────────────────────────────

  /** トークンを localStorage に保存する */
  function saveToken(accessToken, expiresAt) {
    localStorage.setItem(TOKEN_KEY,   accessToken);
    localStorage.setItem(EXPIRES_KEY, expiresAt);
  }

  /** 保存済みのトークンを返す。なければ null */
  function getToken() {
    return localStorage.getItem(TOKEN_KEY) || null;
  }

  /** トークンの有効期限が切れていないか確認する */
  function isTokenValid() {
    const token     = getToken();
    const expiresAt = localStorage.getItem(EXPIRES_KEY);
    if (!token || !expiresAt) return false;
    return new Date(expiresAt) > new Date();
  }

  /** トークンと有効期限を削除する（ログアウト） */
  function clearToken() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(EXPIRES_KEY);
  }

  // ─────────────────────────────────────────
  // ログイン / 登録 / ログアウト
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

    // 登録完了後に自動でログインしてトークンを取得する
    await login(email, password);
    return payload.data;
  }

  /** トークンを削除してログイン画面へ戻る */
  function logout(role = "user") {
    clearToken();
    const roleParam = role === "dispatcher" ? "dispatcher" : "user";
    window.location.assign(`/frontend/login/index.html?role=${roleParam}`);
  }

  // ─────────────────────────────────────────
  // 認証ガード
  // ─────────────────────────────────────────

  /**
   * 認証が必要なページで呼び出す。
   * トークンがないか期限切れの場合はログイン画面へリダイレクトする。
   * @param {string} role - "user" | "dispatcher"
   */
  function requireAuth(role = "user") {
    if (!isTokenValid()) {
      clearToken();
      const params = new URLSearchParams({ role });
      window.location.assign(`/frontend/login/index.html?${params}`);
      return false;
    }
    return true;
  }

  // ─────────────────────────────────────────
  // 認証付き fetch
  // ─────────────────────────────────────────

  /**
   * Authorization ヘッダー付きの fetch。
   * 401 を受け取った場合はトークンを削除してログイン画面へ戻る。
   */
  async function authFetch(path, options = {}) {
    const token = getToken();

    const response = await fetch(`${API_BASE}${path}`, {
      ...options,
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...options.headers
      }
    });

    // トークン期限切れ・不正の場合は再ログインを促す
    if (response.status === 401) {
      clearToken();
      const role = document.body.dataset.role === "dispatcher" ? "dispatcher" : "user";
      window.location.assign(`/frontend/login/index.html?role=${role}`);
      return null;
    }

    const payload = await response.json().catch(() => ({}));

    if (!response.ok) {
      const message = payload.error?.message || "APIリクエストの処理に失敗しました。";
      throw new Error(message);
    }

    return payload;
  }

  // ─────────────────────────────────────────
  // 公開API
  // ─────────────────────────────────────────

  return {
    login,
    register,
    logout,
    requireAuth,
    authFetch,
    getToken,
    isTokenValid,
    saveToken,
    clearToken
  };
})();
