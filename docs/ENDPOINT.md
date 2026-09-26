# エンドポイント一覧（実装済み）

現在のバックエンドに**実装されている** API のリクエスト・レスポンスをまとめたドキュメントです。
今後の予定も含めた設計は [API仕様書](api-specification.md) を参照してください。
OpenAPI（Swagger）形式の定義は [swagger.yaml](swagger.yaml) にあります。

> コード（`backend/handler/`）を変更したら、このファイルと `swagger.yaml` も更新してください。

## 目次

- [共通仕様](#共通仕様)
- [一覧](#一覧)
- [ヘルスチェック](#ヘルスチェック)
  - [GET /health](#get-health)
- [認証](#認証)
  - [POST /api/auth/login](#post-apiauthlogin)
- [ユーザー](#ユーザー)
  - [POST /api/users](#post-apiusers)
  - [GET /api/users/me](#get-apiusersme)
  - [POST /api/users/me/roles](#post-apiusersmeroles)
  - [PUT /api/users/me/active-role](#put-apiusersmeactive-role)
- [予約](#予約)
  - [POST /api/reservations](#post-apireservations)
  - [GET /api/reservations](#get-apireservations)
  - [GET /api/reservations/{reservationId}](#get-apireservationsreservationid)
  - [POST /api/reservations/{reservationId}/cancel](#post-apireservationsreservationidcancel)
- [既知の制約](#既知の制約)

## 共通仕様

### ベース URL

| 環境 | URL |
| --- | --- |
| ローカル | `http://localhost:8080` |
| 本番（Azure Container Apps） | `https://ca-rideshare-backend.delightfulsea-b341bdde.japaneast.azurecontainerapps.io` |

### 形式

- リクエスト・レスポンスとも JSON（`Content-Type: application/json`）、UTF-8
- JSON のキーはスネークケース（例：`pickup_location`）
- 日時は ISO 8601 形式。レスポンスの日時は UTC（末尾が `Z`）で返す
  - リクエストでタイムゾーン付き（例：`2026-10-01T09:00:00+09:00`）で送ると UTC に変換して保存する

### 認証

- `POST /api/users`、`POST /api/auth/login`、`GET /health` 以外は JWT が必要
- `POST /api/auth/login` で受け取った `access_token` を、ヘッダーで送る

  ```
  Authorization: Bearer <access_token>
  ```

- トークンの有効期限は 60 分。署名は HS256
- ユーザーはトークンの `sub`（ユーザーID）で識別する。リクエストでユーザーIDは受け取らない

### レスポンスの形

成功時（1件）

```json
{
  "data": { }
}
```

成功時（一覧）

```json
{
  "data": [ ],
  "meta": { "page": 1, "limit": 50, "total": 3 }
}
```

エラー時

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "入力内容を確認してください",
    "details": [
      { "field": "passengerCount", "message": "PassengerCount must be 1 or more. (Parameter 'passengerCount')" }
    ]
  }
}
```

`details` は `VALIDATION_ERROR` のときだけ付きます。

### エラーコード

| HTTP ステータス | `error.code` | 発生する場面 |
| --- | --- | --- |
| `400 Bad Request` | （本文なし） | JSON の形式が不正、必須のリクエストボディがない、日付などの型が不正 |
| `401 Unauthorized` | （本文なし） | トークンがない・不正・期限切れ、トークンのユーザーが存在しない |
| `401 Unauthorized` | `INVALID_CREDENTIALS` | ログインでメールアドレスかパスワードが違う |
| `404 Not Found` | `NOT_FOUND` | 指定した予約が存在しない、または他人の予約 |
| `409 Conflict` | `CONFLICT` | 状態的に実行できない（登録済みのメールアドレス、キャンセルできない状態など） |
| `422 Unprocessable Entity` | `VALIDATION_ERROR` | 入力値が不正 |

### 列挙値

| 項目 | 値 |
| --- | --- |
| 利用者区分（`role`, `active_role`, `roles`） | `rider`（利用者）、`driver`（運転手） |
| 予約状態（`status`） | `matching`（マッチング中）、`confirmed`（確定）、`in_progress`（乗車中）、`completed`（完了）、`cancelled`（キャンセル） |

## 一覧

| メソッド | パス | 認証 | 概要 |
| --- | --- | --- | --- |
| `GET` | `/health` | 不要 | サーバーと DB の稼働確認 |
| `POST` | `/api/auth/login` | 不要 | ログイン（JWT の発行） |
| `POST` | `/api/users` | 不要 | 利用者登録 |
| `GET` | `/api/users/me` | 必要 | 自分のユーザー情報を取得 |
| `POST` | `/api/users/me/roles` | 必要 | 利用者区分を追加 |
| `PUT` | `/api/users/me/active-role` | 必要 | 稼働中の利用者区分を切り替え |
| `POST` | `/api/reservations` | 必要 | 予約を登録 |
| `GET` | `/api/reservations` | 必要 | 自分の予約一覧を取得 |
| `GET` | `/api/reservations/{reservationId}` | 必要 | 自分の予約の詳細を取得 |
| `POST` | `/api/reservations/{reservationId}/cancel` | 必要 | 自分の予約をキャンセル |

---

## ヘルスチェック

### GET /health

サーバーが稼働しているか、DB に接続できるかを確認します。監視やデプロイ後の確認に使います。
このエンドポイントだけは共通のレスポンス形式ではありません。

- 認証：不要
- レスポンスヘッダー：`Cache-Control: no-store`

#### レスポンス

| ステータス | 意味 |
| --- | --- |
| `200 OK` | すべて正常 |
| `503 Service Unavailable` | DB に接続できないなど、どれかのチェックが異常 |

```json
{
  "status": "Healthy",
  "checks": {
    "database": { "status": "Healthy", "description": null }
  }
}
```

異常時

```json
{
  "status": "Unhealthy",
  "checks": {
    "database": { "status": "Unhealthy", "description": "データベースに接続できません" }
  }
}
```

---

## 認証

### POST /api/auth/login

メールアドレスとパスワードでログインし、JWT を受け取ります。

- 認証：不要

#### リクエストボディ

| 項目 | 型 | 必須 | 説明 |
| --- | --- | --- | --- |
| `email` | string | ○ | 登録したメールアドレス（前後の空白は無視） |
| `password` | string | ○ | パスワード |

```json
{
  "email": "taro.yamada@example.com",
  "password": "password1234"
}
```

#### レスポンス `200 OK`

| 項目 | 型 | 説明 |
| --- | --- | --- |
| `data.access_token` | string | JWT。`Authorization: Bearer` で送る |
| `data.token_type` | string | 常に `Bearer` |
| `data.expires_at` | string (date-time) | 有効期限（UTC） |

```json
{
  "data": {
    "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "token_type": "Bearer",
    "expires_at": "2026-09-26T07:00:00Z"
  }
}
```

#### エラー

| ステータス | code | 条件 |
| --- | --- | --- |
| `401` | `INVALID_CREDENTIALS` | メールアドレスが未登録、パスワードが違う、どちらかが空（どれが原因かは区別しない） |

```json
{
  "error": {
    "code": "INVALID_CREDENTIALS",
    "message": "メールアドレスまたはパスワードが正しくありません"
  }
}
```

---

## ユーザー

### ユーザー情報（`User`）

`/api/users` 系のレスポンスの `data` は、すべて次の形です。

| 項目 | 型 | 説明 |
| --- | --- | --- |
| `id` | string (uuid) | ユーザーID |
| `email` | string | メールアドレス |
| `last_name` | string | 姓 |
| `first_name` | string | 名 |
| `kana_last_name` | string | 姓（全角カタカナ） |
| `kana_first_name` | string | 名（全角カタカナ） |
| `active_role` | string | 稼働中の区分（`rider` / `driver`） |
| `roles` | string[] | 登録済みの区分 |
| `created_at` | string (date-time) | 登録日時（UTC） |

```json
{
  "data": {
    "id": "3f2b8c1e-5d4a-4e7b-9c1f-2a6d8e0b4c71",
    "email": "taro.yamada@example.com",
    "first_name": "太郎",
    "last_name": "山田",
    "kana_first_name": "タロウ",
    "kana_last_name": "ヤマダ",
    "active_role": "rider",
    "roles": ["rider"],
    "created_at": "2026-09-26T05:00:00Z"
  }
}
```

### POST /api/users

利用者を登録します。指定した区分が、登録済みの区分かつ稼働中の区分になります。
登録してもトークンは発行されないので、続けて `POST /api/auth/login` でログインしてください。

- 認証：不要

#### リクエストボディ

| 項目 | 型 | 必須 | 説明 |
| --- | --- | --- | --- |
| `email` | string | ○ | メールアドレス（前後の空白は除去。最大 254 文字） |
| `password` | string | ○ | パスワード（8〜128 文字） |
| `last_name` | string | ○ | 姓（前後の空白は除去。最大 50 文字） |
| `first_name` | string | ○ | 名（前後の空白は除去。最大 50 文字） |
| `kana_last_name` | string | ○ | 姓の読み。全角カタカナと長音符「ー」のみ（最大 50 文字） |
| `kana_first_name` | string | ○ | 名の読み。全角カタカナと長音符「ー」のみ（最大 50 文字） |
| `role` | string | ○ | 最初の区分（`rider` / `driver`） |

```json
{
  "email": "taro.yamada@example.com",
  "password": "password1234",
  "last_name": "山田",
  "first_name": "太郎",
  "kana_last_name": "ヤマダ",
  "kana_first_name": "タロウ",
  "role": "rider"
}
```

#### レスポンス `201 Created`

- `Location` ヘッダー：`/api/users/me`
- ボディ：[ユーザー情報](#ユーザー情報user)

#### エラー

チェックは上から順に行い、最初に見つかったエラーだけを返します。

| ステータス | code | `details[].field` | 条件 |
| --- | --- | --- | --- |
| `422` | `VALIDATION_ERROR` | `role` | `role` が `rider` / `driver` 以外（メッセージ：利用者区分の値が不正です） |
| `422` | `VALIDATION_ERROR` | `password` | パスワードが 8 文字未満または 128 文字超 |
| `409` | `CONFLICT` | － | メールアドレスが登録済み（メッセージ：このメールアドレスはすでに登録されています） |
| `422` | `VALIDATION_ERROR` | `email` / `lastName` / `firstName` | 空、または空白のみ |
| `422` | `VALIDATION_ERROR` | `kanaLastName` / `kanaFirstName` | 全角カタカナ以外を含む、または空 |

`role` 以外の `details[].message` は英語のメッセージです（例：`kanaLastName must be full-width katakana. (Parameter 'kanaLastName')`）。

### GET /api/users/me

ログイン中のユーザー自身の情報を取得します。

- 認証：必要

#### レスポンス `200 OK`

ボディ：[ユーザー情報](#ユーザー情報user)

#### エラー

| ステータス | 条件 |
| --- | --- |
| `401` | トークンが不正、またはトークンのユーザーが存在しない |

### POST /api/users/me/roles

利用者区分を追加します（例：利用者として登録した人が、運転手としても登録する）。
追加するだけで、稼働中の区分（`active_role`）は変わりません。

- 認証：必要

#### リクエストボディ

| 項目 | 型 | 必須 | 説明 |
| --- | --- | --- | --- |
| `role` | string | ○ | 追加する区分（`rider` / `driver`） |

```json
{ "role": "driver" }
```

#### レスポンス `200 OK`

ボディ：[ユーザー情報](#ユーザー情報user)（`roles` に追加した区分が入る）

#### エラー

| ステータス | code | 条件 |
| --- | --- | --- |
| `401` | － | トークンが不正、またはユーザーが存在しない |
| `422` | `VALIDATION_ERROR` | `role` が `rider` / `driver` 以外 |
| `409` | `CONFLICT` | その区分はすでに登録済み（メッセージ：この利用者区分は登録済みです） |

### PUT /api/users/me/active-role

稼働中の利用者区分を切り替えます。同時に稼働できる区分は 1 つだけです。

- 認証：必要
- 今の区分で完了・キャンセルになっていない予約（利用者）や運行（運転手）が残っている場合は、別の区分に切り替えられません
- 今と同じ区分を指定した場合は、何も変えずに成功します

#### リクエストボディ

| 項目 | 型 | 必須 | 説明 |
| --- | --- | --- | --- |
| `role` | string | ○ | 切り替え先の区分（`rider` / `driver`）。登録済みの区分であること |

```json
{ "role": "driver" }
```

#### レスポンス `200 OK`

ボディ：[ユーザー情報](#ユーザー情報user)（`active_role` が切り替え後の区分になる）

#### エラー

| ステータス | code | 条件（メッセージ） |
| --- | --- | --- |
| `401` | － | トークンが不正、またはユーザーが存在しない |
| `422` | `VALIDATION_ERROR` | `role` が `rider` / `driver` 以外 |
| `409` | `CONFLICT` | 未完了の予約がある（完了またはキャンセルされていない予約があるため切り替えできません） |
| `409` | `CONFLICT` | 未完了の運行がある（完了またはキャンセルされていない運行があるため切り替えできません） |
| `409` | `CONFLICT` | 切り替え先の区分が未登録（この利用者区分は登録されていません） |

---

## 予約

予約はすべて**自分の予約だけ**を扱います。他人の予約IDを指定した場合は、存在しない場合と同じく `404` を返します。

### 予約（`Reservation`）

| 項目 | 型 | 説明 |
| --- | --- | --- |
| `id` | string (uuid) | 予約ID |
| `reservation_number` | string | 予約番号（`RR-yyyyMMdd-XXXX`。日付は UTC） |
| `user_id` | string (uuid) | 予約したユーザーのID |
| `pickup_location` | string | 乗車地 |
| `destination` | string | 目的地 |
| `requested_pickup_at` | string (date-time) | 希望乗車日時（UTC） |
| `passenger_count` | integer | 乗車人数 |
| `consideration_notes` | string \| null | 配慮事項 |
| `status` | string | 予約状態 |
| `cancellation_reason` | string \| null | キャンセル理由 |
| `cancelled_at` | string (date-time) \| null | キャンセル日時（UTC） |
| `created_at` | string (date-time) | 登録日時（UTC） |

### POST /api/reservations

予約を登録します。登録直後の状態は `matching` です。

- 認証：必要
- 稼働中の区分が `rider`（利用者）であること

#### リクエストボディ

| 項目 | 型 | 必須 | 説明 |
| --- | --- | --- | --- |
| `pickup_location` | string | ○ | 乗車地（最大 200 文字） |
| `destination` | string | ○ | 目的地（最大 200 文字） |
| `requested_pickup_at` | string (date-time) | ○ | 希望乗車日時。タイムゾーン付きで送ること |
| `passenger_count` | integer | ○ | 乗車人数（1 以上） |
| `consideration_notes` | string | － | 配慮事項（最大 500 文字） |

```json
{
  "pickup_location": "市役所前",
  "destination": "中央病院",
  "requested_pickup_at": "2026-10-01T09:00:00+09:00",
  "passenger_count": 2,
  "consideration_notes": "車椅子を使用"
}
```

#### レスポンス `201 Created`

- `Location` ヘッダー：`/api/reservations/{id}`

```json
{
  "data": {
    "id": "8a1d2f4c-6b3e-4c9a-a7d5-1e2f3a4b5c6d",
    "reservation_number": "RR-20260926-A1B2",
    "user_id": "3f2b8c1e-5d4a-4e7b-9c1f-2a6d8e0b4c71",
    "pickup_location": "市役所前",
    "destination": "中央病院",
    "requested_pickup_at": "2026-10-01T00:00:00Z",
    "passenger_count": 2,
    "consideration_notes": "車椅子を使用",
    "status": "matching",
    "cancellation_reason": null,
    "cancelled_at": null,
    "created_at": "2026-09-26T05:10:00Z"
  }
}
```

#### エラー

| ステータス | code | `details[].field` | 条件 |
| --- | --- | --- | --- |
| `401` | － | － | トークンが不正、またはユーザーが存在しない |
| `409` | `CONFLICT` | － | 稼働中の区分が `rider` ではない（メッセージ：利用者として稼働していないため予約できません） |
| `422` | `VALIDATION_ERROR` | `pickupLocation` | 乗車地が空 |
| `422` | `VALIDATION_ERROR` | `destination` | 目的地が空 |
| `422` | `VALIDATION_ERROR` | `passengerCount` | 乗車人数が 1 未満（省略した場合も 0 扱いでこのエラー） |

### GET /api/reservations

自分の予約の一覧を、希望乗車日時の昇順で取得します。

- 認証：必要

#### クエリパラメーター

| 項目 | 型 | 必須 | 既定値 | 説明 |
| --- | --- | --- | --- | --- |
| `date` | string (date) | － | － | 乗車日で絞り込む（例：`2026-10-01`）。**日本時間**の 0:00〜24:00 として扱う |
| `status` | string | － | － | 予約状態で絞り込む |
| `from` | string (date-time) | － | － | 希望乗車日時がこの日時以降 |
| `to` | string (date-time) | － | － | 希望乗車日時がこの日時以前 |
| `page` | integer | － | `1` | ページ番号（1 未満は 1 として扱う） |
| `limit` | integer | － | `50` | 1 ページの件数（1 未満は 50 として扱う） |

例：`GET /api/reservations?date=2026-10-01&status=matching&page=1&limit=20`

#### レスポンス `200 OK`

一覧の各要素は予約の一部の項目だけを返します。

| 項目 | 型 | 説明 |
| --- | --- | --- |
| `data[].id` | string (uuid) | 予約ID |
| `data[].reservation_number` | string | 予約番号 |
| `data[].pickup_location` | string | 乗車地 |
| `data[].destination` | string | 目的地 |
| `data[].requested_pickup_at` | string (date-time) | 希望乗車日時（UTC） |
| `data[].passenger_count` | integer | 乗車人数 |
| `data[].status` | string | 予約状態 |
| `meta.page` | integer | リクエストした `page` |
| `meta.limit` | integer | リクエストした `limit` |
| `meta.total` | integer | 絞り込み後の全件数 |

```json
{
  "data": [
    {
      "id": "8a1d2f4c-6b3e-4c9a-a7d5-1e2f3a4b5c6d",
      "reservation_number": "RR-20260926-A1B2",
      "pickup_location": "市役所前",
      "destination": "中央病院",
      "requested_pickup_at": "2026-10-01T00:00:00Z",
      "passenger_count": 2,
      "status": "matching"
    }
  ],
  "meta": { "page": 1, "limit": 20, "total": 1 }
}
```

#### エラー

| ステータス | code | 条件 |
| --- | --- | --- |
| `400` | － | `date` / `from` / `to` / `page` / `limit` の形式が不正 |
| `401` | － | トークンが不正、またはユーザーが存在しない |
| `422` | `VALIDATION_ERROR` | `status` が定義外の値（`details[].field` は `status`） |

### GET /api/reservations/{reservationId}

自分の予約の詳細を取得します。

- 認証：必要

#### パスパラメーター

| 項目 | 型 | 説明 |
| --- | --- | --- |
| `reservationId` | string (uuid) | 予約ID。UUID 形式でない場合はルートに一致せず `404`（本文なし） |

#### レスポンス `200 OK`

ボディ：[予約](#予約reservation)

#### エラー

| ステータス | code | 条件 |
| --- | --- | --- |
| `401` | － | トークンが不正、またはユーザーが存在しない |
| `404` | `NOT_FOUND` | 予約が存在しない、または他人の予約（メッセージ：指定された予約が見つかりません） |

### POST /api/reservations/{reservationId}/cancel

自分の予約をキャンセルします。キャンセルできるのは `matching` か `confirmed` の予約だけです。

- 認証：必要

#### パスパラメーター

| 項目 | 型 | 説明 |
| --- | --- | --- |
| `reservationId` | string (uuid) | 予約ID |

#### リクエストボディ

ボディは必須です。理由を書かない場合も `{}` を送ってください（ボディがないと `400`）。

| 項目 | 型 | 必須 | 説明 |
| --- | --- | --- | --- |
| `reason` | string | － | キャンセル理由（最大 500 文字） |

```json
{ "reason": "予定が変わったため" }
```

#### レスポンス `200 OK`

| 項目 | 型 | 説明 |
| --- | --- | --- |
| `data.id` | string (uuid) | 予約ID |
| `data.status` | string | 常に `cancelled` |
| `data.cancellation_reason` | string \| null | キャンセル理由 |
| `data.cancelled_at` | string (date-time) | キャンセル日時（UTC） |

```json
{
  "data": {
    "id": "8a1d2f4c-6b3e-4c9a-a7d5-1e2f3a4b5c6d",
    "status": "cancelled",
    "cancellation_reason": "予定が変わったため",
    "cancelled_at": "2026-09-26T05:20:00Z"
  }
}
```

#### エラー

| ステータス | code | 条件 |
| --- | --- | --- |
| `400` | － | リクエストボディがない |
| `401` | － | トークンが不正、またはユーザーが存在しない |
| `404` | `NOT_FOUND` | 予約が存在しない、または他人の予約 |
| `409` | `CONFLICT` | `in_progress` / `completed` / `cancelled` の予約（メッセージは英語。例：`Cannot cancel a reservation in status Cancelled.`） |

---

## 既知の制約

実装を調べて見つかった、今の API の制約です。フロントエンドを作るときに注意してください。

| 内容 | 影響 |
| --- | --- |
| 文字数の上限（姓名 50、乗車地 200、配慮事項 500 など）は DB の制約だけで、API ではチェックしていない | 上限を超えると `422` ではなく `500` になる |
| `VALIDATION_ERROR` の `details[].field` が、`role` / `password` / `status` 以外はキャメルケース（例：`kanaLastName`） | リクエストのキー（`kana_last_name`）と一致しないので、フォームの項目との対応付けに変換が必要 |
| `role` / `status` 以外のバリデーションメッセージと、キャンセル不可のメッセージが英語 | そのまま画面に出さず、フロント側で文言を用意する |
| 一覧の `meta.page` / `meta.limit` は、補正前のリクエスト値を返す | `page=0` を指定すると、実際は 1 ページ目なのに `meta.page` は `0` になる |
| `limit` に上限がない | 大きな値を指定すると全件を返す |
