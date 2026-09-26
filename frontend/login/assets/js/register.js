(() => {
  const params = new URLSearchParams(window.location.search);
  const isDispatcher = params.get("role") === "dispatcher";
  const role = isDispatcher ? "dispatcher" : "user";
  const roleName = isDispatcher ? "配車担当者" : "利用者";
  const loginParams = new URLSearchParams({
    role,
    next: isDispatcher ? "dispatch" : params.get("next") === "history" ? "history" : "reservation"
  });

  document.body.dataset.role = role;
  document.title = `${roleName}新規登録 | RoadRide Sharing`;
  document.getElementById("role-label").textContent = `${roleName}向け`;
  document.getElementById("page-title").textContent = `${roleName}新規登録`;
  document.getElementById("login-link").href = `index.html?${loginParams}`;

  const form = document.getElementById("register-form");
  const password = document.getElementById("password");
  const confirmation = document.getElementById("password-confirm");
  const error = document.getElementById("password-error");
  function validatePasswords() {
    const mismatch = confirmation.value !== "" && password.value !== confirmation.value;
    const message = mismatch ? "パスワードが一致しません。同じパスワードを入力してください。" : "";
    confirmation.setCustomValidity(message);
    confirmation.setAttribute("aria-invalid", String(mismatch));
    error.textContent = message;
  }
  password.addEventListener("input", validatePasswords);
  confirmation.addEventListener("input", validatePasswords);
  form.addEventListener("submit", (event) => {
    event.preventDefault();
    validatePasswords();
    if (!form.reportValidity()) return;

    // 登録API未定義のため入力確認のみ。入力内容は保存・送信しない。
    form.reset();
    loginParams.set("registration", "demo");
    window.location.assign(`index.html?${loginParams}`);
  });
})();
