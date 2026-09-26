(() => {
  const params = new URLSearchParams(window.location.search);
  if (params.get("role") === "dispatcher") {
    window.location.replace("index.html?role=dispatcher&next=dispatch");
    return;
  }
  const role = "user";
  const roleName = "利用者";
  const loginParams = new URLSearchParams({
    role,
    next: params.get("next") === "history" ? "history" : "reservation"
  });
  const destination = params.get("next") === "history"
    ? "../../reservation/pages/reservation_history.html"
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
        lastName:      form.querySelector("#family-name").value.trim(),
        firstName:     form.querySelector("#given-name").value.trim(),
        kanaLastName:  form.querySelector("#family-name-kana")?.value.trim() ?? "",
        kanaFirstName: form.querySelector("#given-name-kana")?.value.trim() ?? "",
        role:          "rider"
      });
      window.location.assign(destination);
    } catch (error) {
      apiError.textContent = error.message;
      setLoading(false);
    }
  });
})();
