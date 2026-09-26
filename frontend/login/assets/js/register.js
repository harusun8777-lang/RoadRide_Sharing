(() => {
  const params       = new URLSearchParams(window.location.search);
  const isDispatcher = params.get("role") === "dispatcher";
  const role         = isDispatcher ? "dispatcher" : "user";
  const roleName     = isDispatcher ? "配車担当者" : "利用者";
  const loginParams  = new URLSearchParams({
    role,
    next: isDispatcher ? "dispatch" : (params.get("next") === "history" ? "history" : "reservation")
  });

  // 遷移先
  const destination = isDispatcher
    ? "../../dispatch/pages/dashboard.html"
    : "../../reservation/pages/reservation.html";

  document.body.dataset.role                        = role;
  document.title                                    = `${roleName}新規登録 | RoadRide Sharing`;
  document.getElementById("role-label").textContent  = `${roleName}向け`;
  document.getElementById("page-title").textContent  = `${roleName}新規登録`;
  document.getElementById("login-link").href         = `index.html?${loginParams}`;

  // すでにトークンが有効なら即遷移
  if (RoadRideAuth.isTokenValid()) {
    window.location.assign(destination);
    return;
  }

  const form      = document.getElementById("register-form");
  const submitBtn = form.querySelector("button[type=submit]");
  const password  = document.getElementById("password");
  const confirm   = document.getElementById("password-confirm");
  const pwError   = document.getElementById("password-error");
  const apiError  = document.getElementById("register-error") ?? createApiErrorEl(form);

  function createApiErrorEl(parentForm) {
    const el = document.createElement("p");
    el.id = "register-error";
    el.setAttribute("role", "alert");
    el.setAttribute("aria-live", "polite");
    el.style.cssText = "margin:12px 0 0;color:#b91c1c;font-size:14px;font-weight:700;";
    parentForm.appendChild(el);
    return el;
  }

  function validatePasswords() {
    const mismatch = confirm.value !== "" && password.value !== confirm.value;
    const message  = mismatch ? "パスワードが一致しません。同じパスワードを入力してください。" : "";
    confirm.setCustomValidity(message);
    confirm.setAttribute("aria-invalid", String(mismatch));
    pwError.textContent = message;
  }

  function setLoading(loading) {
    submitBtn.disabled    = loading;
    submitBtn.textContent = loading ? "登録中..." : "新規登録";
  }

  password.addEventListener("input", validatePasswords);
  confirm.addEventListener("input",  validatePasswords);

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    validatePasswords();
    if (!form.reportValidity()) return;

    apiError.textContent = "";
    setLoading(true);

    try {
      await RoadRideAuth.register({
        email:         form.querySelector("#email").value.trim(),
        password:      password.value,
        lastName:      form.querySelector("#last-name").value.trim(),
        firstName:     form.querySelector("#first-name").value.trim(),
        kanaLastName:  form.querySelector("#kana-last-name")?.value.trim() ?? "",
        kanaFirstName: form.querySelector("#kana-first-name")?.value.trim() ?? "",
        role:          isDispatcher ? "driver" : "rider"
      });
      window.location.assign(destination);
    } catch (error) {
      apiError.textContent = error.message;
      setLoading(false);
    }
  });
})();
