(() => {
  const header = document.querySelector(".topbar-inner, .app-header .header-inner");
  if (!header) return;

  // MVPでは user-001 を利用するため、認証APIができるまでは仮の表示名を使う。
  const isDispatcher = header.classList.contains("header-inner");
  const userName = isDispatcher ? "田中 健一" : "仮ユーザー";
  header.classList.add("account-header");
  header.querySelector(".header-user")?.remove();
  const headerUser = document.createElement("div");
  headerUser.className = "account-header-user";
  headerUser.innerHTML = '<span class="account-avatar" aria-hidden="true">仮</span>';
  headerUser.querySelector(".account-avatar").textContent = isDispatcher ? "田" : "仮";
  const accountButton = document.createElement("button");
  accountButton.type = "button";
  accountButton.className = "account-button";
  accountButton.setAttribute("aria-label", "ユーザーメニューを開く");
  accountButton.setAttribute("aria-expanded", "false");
  accountButton.setAttribute("aria-controls", "account-menu");
  accountButton.innerHTML = '<span class="account-hamburger" aria-hidden="true"><span></span><span></span><span></span></span>';
  headerUser.append(accountButton);
  header.append(headerUser);

  const menu = document.createElement("aside");
  menu.id = "account-menu";
  menu.className = "account-drawer";
  menu.inert = true;
  menu.setAttribute("aria-labelledby", "account-title");
  menu.innerHTML = `
    <div class="account-drawer-heading">
      <h2 id="account-title">メニュー</h2>
      <button type="button" class="account-close" data-action="close" aria-label="メニューを閉じる">×</button>
    </div>
    <p class="account-user-name"></p>
    <div class="account-actions">
      <button type="button" class="account-primary" data-action="logout">ログアウト</button>
    </div>`;

  menu.querySelector(".account-user-name").textContent = userName;
  // 配車画面の既存の補助リンクもメニューにまとめ、ヘッダーを一行に保つ。
  const headerLinks = header.querySelectorAll(".header-actions a, :scope > .back-link");
  if (headerLinks.length) {
    const navigation = document.createElement("nav");
    navigation.className = "account-navigation";
    navigation.setAttribute("aria-label", "配車メニュー");
    headerLinks.forEach((link) => navigation.append(link));
    menu.insertBefore(navigation, menu.querySelector(".account-actions"));
    header.querySelector(".header-actions")?.remove();
  }

  const confirmDialog = document.createElement("dialog");
  confirmDialog.className = "account-dialog";
  confirmDialog.setAttribute("aria-labelledby", "logout-title");
  confirmDialog.innerHTML = `
    <h2 id="logout-title">ログアウトしますか？</h2>
    <p>ログアウトするとトップページに戻ります。</p>
    <div class="account-actions">
      <button type="button" data-action="cancel" autofocus>キャンセル</button>
      <button type="button" class="account-primary" data-action="confirm">ログアウト</button>
    </div>`;
  document.body.append(menu, confirmDialog);

  function closeMenu(restoreFocus = true) {
    menu.classList.remove("is-open");
    menu.inert = true;
    accountButton.setAttribute("aria-expanded", "false");
    accountButton.setAttribute("aria-label", "ユーザーメニューを開く");
    if (restoreFocus) accountButton.focus();
  }

  accountButton.addEventListener("click", () => {
    if (menu.classList.contains("is-open")) {
      closeMenu();
      return;
    }
    menu.inert = false;
    menu.classList.add("is-open");
    accountButton.setAttribute("aria-expanded", "true");
    accountButton.setAttribute("aria-label", "ユーザーメニューを閉じる");
    menu.querySelector('[data-action="close"]').focus();
  });
  menu.querySelector('[data-action="close"]').addEventListener("click", () => closeMenu());
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && menu.classList.contains("is-open")) closeMenu();
  });
  document.addEventListener("click", (event) => {
    if (menu.classList.contains("is-open") && !menu.contains(event.target) && !accountButton.contains(event.target)) {
      closeMenu(menu.contains(document.activeElement));
    }
  });
  menu.querySelector('[data-action="logout"]').addEventListener("click", () => {
    closeMenu();
    confirmDialog.showModal();
  });
  confirmDialog.querySelector('[data-action="cancel"]').addEventListener("click", () => confirmDialog.close());
  confirmDialog.addEventListener("close", () => accountButton.focus());
  confirmDialog.querySelector('[data-action="confirm"]').addEventListener("click", () => {
    // 仮ログインでは認証情報を保持していない。認証導入時にセッション破棄を追加する。
    window.location.assign("../../top/index.html");
  });
})();
