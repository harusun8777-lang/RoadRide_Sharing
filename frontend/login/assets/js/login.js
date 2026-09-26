(() => {
  const params = new URLSearchParams(window.location.search);
  const isDispatcher = params.get("role") === "dispatcher";
  // 遷移先は固定の候補から選ぶ。任意のURLや別の役割の画面には転送しない。
  const destination = isDispatcher
    ? { label: "配車ダッシュボード", path: "../dispatch/pages/dashboard.html" }
    : params.get("next") === "history"
      ? { label: "予約履歴", path: "../reservation/pages/reservation_history.html" }
      : { label: "新規予約", path: "../reservation/pages/reservation.html" };
  const roleName = isDispatcher ? "配車担当者" : "利用者";
  const registrationParams = new URLSearchParams({
    role: "user",
    next: params.get("next") === "history" ? "history" : "reservation"
  });
  document.getElementById("register-link").href = `register.html?${registrationParams}`;
  if (isDispatcher) {
    document.getElementById("register-link").parentElement.remove();
    document.getElementById("identifier-label").firstChild.textContent = "ユーザーID ";
    const identifier = document.getElementById("login-identifier");
    identifier.type = "text";
    identifier.inputMode = "text";
    identifier.placeholder = "ユーザーIDを入力";
    document.getElementById("demo-notice").textContent =
      "現在は仮ログインです。ユーザーIDとパスワードの入力のみ確認します。実際に使用しているパスワードは入力しないでください。";
  }
  document.getElementById("registration-status").hidden = isDispatcher || params.get("registration") !== "demo";

  document.body.dataset.role = isDispatcher ? "dispatcher" : "user";
  document.title = `${roleName}ログイン | RoadRide Sharing`;
  document.getElementById("role-label").textContent = `${roleName}向け`;
  document.getElementById("page-title").textContent = `${roleName}ログイン`;
  document.getElementById("login-description").textContent =
    `ログイン後、${destination.label}へ進みます。`;

  const form = document.getElementById("login-form");
  form.addEventListener("submit", (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;

    // API仕様書のMVP方針に合わせた仮ログイン。認証APIの追加時に置き換える。
    // メールアドレス・ユーザーID・パスワードは保存・送信しない。
    form.reset();
    window.location.assign(destination.path);
  });
})();
