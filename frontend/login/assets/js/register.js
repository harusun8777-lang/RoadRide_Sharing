(() => {
  const params       = new URLSearchParams(window.location.search);
  const isDispatcher = params.get("role") === "dispatcher";
  if (isDispatcher) {
    window.location.replace("index.html?role=dispatcher&next=dispatch");
    return;
  }
  const role         = isDispatcher ? "dispatcher" : "user";
  const roleName     = isDispatcher ? "配車担当者" : "利用者";
  const loginParams  = new URLSearchParams({
    role,
    next: isDispatcher ? "dispatch" : (params.get("next") === "history" ? "history" : "reservation")
  });

  // 遷移先は固定の候補から選ぶ（任意のURLには転送しない）
  const destination = isDispatcher
    ? "/dispatch/pages/dashboard.html"
    : params.get("next") === "history"
      ? "/reservation/pages/reservation_history.html"
      : "/reservation/pages/reservation.html";

  document.body.dataset.role                        = role;
  document.title                                    = `${roleName}新規登録 | Hitch Tac`;
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
  const formError = document.getElementById("register-error");

  // API の項目名（スネークケース）→ 入力欄とエラー表示欄
  const FIELDS = {
    email:           { input: "email",           error: "email-error" },
    last_name:       { input: "last-name",       error: "last-name-error" },
    first_name:      { input: "first-name",      error: "first-name-error" },
    kana_last_name:  { input: "kana-last-name",  error: "kana-last-name-error" },
    kana_first_name: { input: "kana-first-name", error: "kana-first-name-error" },
    password:        { input: "password",        error: "password-field-error" }
  };

  Object.values(FIELDS).forEach((field) => {
    field.inputEl = document.getElementById(field.input);
    field.errorEl = document.getElementById(field.error);
    // 元の aria-describedby（ヒントなど）を覚えておき、エラー欄を追加・削除する
    field.baseDescribedBy = field.inputEl.getAttribute("aria-describedby") || "";
  });

  function setFieldError(field, message) {
    field.errorEl.textContent = message;
    const ids = field.baseDescribedBy ? [field.baseDescribedBy] : [];
    if (message) {
      field.inputEl.setAttribute("aria-invalid", "true");
      ids.push(field.error);
    } else {
      field.inputEl.removeAttribute("aria-invalid");
    }
    if (ids.length) field.inputEl.setAttribute("aria-describedby", ids.join(" "));
    else field.inputEl.removeAttribute("aria-describedby");
  }

  function clearErrors() {
    formError.textContent = "";
    Object.values(FIELDS).forEach((field) => setFieldError(field, ""));
  }

  function validatePasswords() {
    const mismatch = confirm.value !== "" && password.value !== confirm.value;
    const message  = mismatch ? "パスワードが一致しません。同じパスワードを入力してください。" : "";
    confirm.setCustomValidity(message);
    if (mismatch) confirm.setAttribute("aria-invalid", "true");
    else confirm.removeAttribute("aria-invalid");
    pwError.textContent = message;
  }

  function setLoading(loading) {
    submitBtn.disabled    = loading;
    submitBtn.textContent = loading ? "登録中..." : "新規登録";
    form.setAttribute("aria-busy", String(loading));
  }

  form.addEventListener("input", event => RoadRideAuth.clearInputError(event.target), true);
  form.addEventListener("change", event => RoadRideAuth.clearInputError(event.target), true);

  form.addEventListener("invalid", (event) => {
    const field = Object.values(FIELDS).find(item => item.inputEl === event.target);
    const message = RoadRideAuth.inputErrorMessage(event.target);
    if (field) setFieldError(field, message);
    formError.textContent = "入力内容を確認してください。";
  }, true);

  // 入力し直したら、その欄の API エラー表示を消す
  Object.values(FIELDS).forEach((field) => {
    field.inputEl.addEventListener("input", () => {
      if (field.errorEl.textContent) setFieldError(field, "");
    });
  });

  password.addEventListener("input", validatePasswords);
  confirm.addEventListener("input",  validatePasswords);

  function showApiError(error) {
    if (!(error instanceof RoadRideAuth.ApiError)) {
      formError.textContent = "登録に失敗しました。時間をおいて再度お試しください。";
      return;
    }

    let firstInvalid = null;
    Object.entries(error.fieldErrors || {}).forEach(([key, message]) => {
      const field = FIELDS[key];
      if (!field) return;
      setFieldError(field, message);
      firstInvalid ??= field.inputEl;
    });

    // 409：登録済みのメールアドレス。メール欄にも表示する
    if (error.status === 409) {
      setFieldError(FIELDS.email, "このメールアドレスはすでに登録されています。");
      firstInvalid ??= FIELDS.email.inputEl;
      formError.textContent = `${error.message}ログイン画面からログインしてください。`;
    } else {
      formError.textContent = error.message;
    }

    firstInvalid?.focus();
  }

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    validatePasswords();
    if (!form.reportValidity()) return;

    clearErrors();
    setLoading(true);

    try {
      await RoadRideAuth.register({
        email:         FIELDS.email.inputEl.value.trim(),
        password:      password.value,
        lastName:      FIELDS.last_name.inputEl.value.trim(),
        firstName:     FIELDS.first_name.inputEl.value.trim(),
        kanaLastName:  FIELDS.kana_last_name.inputEl.value.trim(),
        kanaFirstName: FIELDS.kana_first_name.inputEl.value.trim(),
        role:          "rider"
      });
      window.location.assign(destination);
    } catch (error) {
      showApiError(error);
      setLoading(false);
    }
  });
})();
