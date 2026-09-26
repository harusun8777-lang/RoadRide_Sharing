// /api/* へのリクエストを Azure のバックエンドへ中継する。
// フロントエンドは同じオリジンの /api を呼ぶため、ブラウザから見て CORS が不要になる。
// /api/* 以外は wrangler.jsonc の run_worker_first により静的ファイルとして配信され、ここには来ない

// ブラウザや Cloudflare が付けるヘッダーのうち、バックエンドへ渡さないもの
const DROP_REQUEST_HEADERS = ["host", "cookie", "cf-connecting-ip", "cf-ipcountry", "cf-ray", "cf-visitor", "x-forwarded-proto", "x-real-ip"];

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (!url.pathname.startsWith("/api/")) {
      return env.ASSETS.fetch(request);
    }

    if (!env.BACKEND_ORIGIN) {
      return jsonError(502, "BAD_GATEWAY", "バックエンドの接続先が設定されていません");
    }

    const target = new URL(url.pathname + url.search, env.BACKEND_ORIGIN);

    const headers = new Headers(request.headers);
    for (const name of DROP_REQUEST_HEADERS) {
      headers.delete(name);
    }
    const clientIp = request.headers.get("cf-connecting-ip");
    if (clientIp) {
      headers.set("x-forwarded-for", clientIp);
    }
    headers.set("x-forwarded-host", url.host);

    try {
      const response = await fetch(target, {
        method: request.method,
        headers,
        body: request.method === "GET" || request.method === "HEAD" ? undefined : request.body,
        // バックエンドのリダイレクトはそのままブラウザに返す
        redirect: "manual",
      });

      // 認証が必要な API の応答を共有キャッシュに残さない
      const proxied = new Response(response.body, response);
      proxied.headers.set("cache-control", response.headers.get("cache-control") ?? "no-store");
      return proxied;
    } catch (error) {
      console.error("バックエンドへの中継に失敗しました", error);
      return jsonError(502, "BAD_GATEWAY", "バックエンドに接続できません");
    }
  },
};

// バックエンドと同じエラー形式で返す
function jsonError(status, code, message) {
  return Response.json({ error: { code, message } }, { status, headers: { "cache-control": "no-store" } });
}
