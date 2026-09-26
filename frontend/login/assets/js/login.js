(() => {
  const params       = new URLSearchParams(window.location.search);
  const isDispatcher = params.get("role") === "dispatcher";
  const wantsHistory = !isDispatcher && params.get("next") === "history";

  // 遷移先は固定の候補から選ぶ。任意のURLや別の役割の画面には転送しない（オープンリダイレクト対策）。
  const destination = isDispatcher
    ? { label: "配車ダッシュボード", path: "/dispatch/pages/dashboard.html" }
    : wantsHistory
      ? { label: "予約履歴", path: "/reservation/pages/reservation_history.html" }
      : { label: "新規予約", path: "/reservation/pages/reservation.html" };

  const roleName = isDispatcher ? "配車担当者" : "利用者";
  const registrationParams = new URLSearchParams({
    role: isDispatcher ? "dispatcher" : "user",
    next: isDispatcher ? "dispatch" : (wantsHistory ? "history" : "reservation")
  });

  document.getElementById("register-link").href    = `register.html?${registrationParams}`;
  document.body.dataset.role                       = isDispatcher ? "dispatcher" : "user";
  document.title                                   = `${roleName}ログイン | RoadRide Sharing`;
  document.getElementById("role-label").textContent = `${roleName}向け`;
  document.getElementById("page-title").textContent = `${roleName}ログイン`;
  document.getElementById("login-description").textContent =
    `ログイン後、${destination.label}へ進みます。`;

  // すでにトークンが有効なら即遷移
  if (RoadRideAuth.isTokenValid()) {
    window.location.assign(destination.path);
    return;
  }

  const form      = document.getElementById("login-form");
  const submitBtn = form.querySelector("button[type=submit]");
  const emailEl   = document.getElementById("email");
  const passEl    = document.getElementById("password");
  const errorEl   = document.getElementById("login-error");

  function setLoading(loading) {
    submitBtn.disabled    = loading;
    submitBtn.textContent = loading ? "ログイン中..." : "ログイン";
    form.setAttribute("aria-busy", String(loading));
  }

  function setInvalid(invalid) {
    [emailEl, passEl].forEach((input) => {
      if (invalid) {
        input.setAttribute("aria-invalid", "true");
        input.setAttribute("aria-describedby", "login-error");
      } else {
        input.removeAttribute("aria-invalid");
        input.removeAttribute("aria-describedby");
      }
    });
  }

  function showError(message) {
    errorEl.textContent = message;
  }

  function clearError() {
    errorEl.textContent = "";
    setInvalid(false);
  }

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;

    clearError();
    setLoading(true);

    try {
      await RoadRideAuth.login(emailEl.value.trim(), passEl.value);
      window.location.assign(destination.path);
    } catch (error) {
      const message = error instanceof RoadRideAuth.ApiError
        ? error.message
        : "ログインに失敗しました。時間をおいて再度お試しください。";
      showError(message);
      if (error?.status === 401 || error?.status === 422) setInvalid(true);
      setLoading(false);
      if (error?.status === 401) passEl.select();
    }
  });
})();
