(() => {
  const params       = new URLSearchParams(window.location.search);
  const isDispatcher = params.get("role") === "dispatcher";

  // 遷移先は固定の候補から選ぶ。任意のURLや別の役割の画面には転送しない。
  const destination = isDispatcher
    ? { label: "配車ダッシュボード", path: "../../dispatch/pages/dashboard.html" }
    : params.get("next") === "history"
      ? { label: "予約履歴", path: "../../reservation/pages/reservation_history.html" }
      : { label: "新規予約",  path: "../../reservation/pages/reservation.html" };

  const roleName = isDispatcher ? "配車担当者" : "利用者";
  const registrationParams = new URLSearchParams({
    role: "user",
    next: params.get("next") === "history" ? "history" : "reservation"
  });

  document.getElementById("register-link").href      = `register.html?${registrationParams}`;
  if (isDispatcher) {
    document.getElementById("register-link").parentElement.remove();
  }
  document.body.dataset.role                          = isDispatcher ? "dispatcher" : "user";
  document.title                                      = `${roleName}ログイン | RoadRide Sharing`;
  document.getElementById("role-label").textContent   = `${roleName}向け`;
  document.getElementById("page-title").textContent   = `${roleName}ログイン`;
  document.getElementById("login-description").textContent =
    `ログイン後、${destination.label}へ進みます。`;

  // すでにトークンが有効なら即遷移
  if (RoadRideAuth.isTokenValid()) {
    window.location.assign(destination.path);
    return;
  }

  const form        = document.getElementById("login-form");
  const submitBtn   = form.querySelector("button[type=submit]");
  const errorEl     = document.getElementById("login-error") ?? createErrorEl(form);

  function createErrorEl(parentForm) {
    const el = document.createElement("p");
    el.id = "login-error";
    el.setAttribute("role", "alert");
    el.setAttribute("aria-live", "polite");
    el.style.cssText = "margin:12px 0 0;color:#b91c1c;font-size:14px;font-weight:700;";
    parentForm.appendChild(el);
    return el;
  }

  function setLoading(loading) {
    submitBtn.disabled    = loading;
    submitBtn.textContent = loading ? "ログイン中..." : "ログイン";
  }

  function showError(message) {
    errorEl.textContent = message;
  }

  function clearError() {
    errorEl.textContent = "";
  }

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;

    clearError();
    setLoading(true);

    const email    = form.querySelector("#login-identifier").value.trim();
    const password = form.querySelector("#password").value;

    try {
      await RoadRideAuth.login(email, password);
      window.location.assign(destination.path);
    } catch (error) {
      showError(error.message);
      setLoading(false);
    }
  });
})();
