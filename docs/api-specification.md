# API仕様書

本仕様書は、RoadRide Sharing MVPのフロントエンドとバックエンド間の通信仕様を定義する。

## 1. 基本方針

- 通信方式: REST API
- データ形式: JSON
- 文字コード: UTF-8
- ベースパス: `/api`
- 日時形式: ISO 8601形式。例: `2026-09-21T10:00:00+09:00`
- 認証: メールアドレスとパスワードでログインして取得した JWT を `Authorization: Bearer <token>` ヘッダーで送る。詳細は「1.1 認証」を参照。

### 1.1 認証

- `POST /api/users`（利用者登録）と `POST /api/auth/login`（ログイン）以外の `/api` エンドポイントは JWT が必要。トークンがない、または不正・期限切れの場合は `401 Unauthorized` を返す。
- JWT は本APIがログイン時に発行する。署名は HS256 で、署名・発行者（`iss`）・受け手（`aud`）・有効期限（`exp`）を検証する。有効期限の初期値は60分。
- 利用者は JWT の `sub`（ユーザーID）で識別する。リクエストボディやクエリで `user_id` は受け取らない。
- 予約など利用者のデータは本人のものだけを扱う。他人の予約IDを指定した場合は、存在しない場合と同じく `404 NOT_FOUND` を返す。
- MVPではリフレッシュトークンを扱わない。有効期限が切れたら再ログインする。
- パスワードは8〜128文字とし、ハッシュ化（PBKDF2）して保存する。

## 2. エンドポイント一覧

| メソッド | パス | 用途 | 利用者 |
| --- | --- | --- | --- |
| `POST` | `/api/auth/login` | ログイン（JWTの発行） | 共通 |
| `POST` | `/api/users` | 利用者登録 | 共通 |
| `GET` | `/api/users/me` | 本人のユーザー情報取得 | 共通 |
| `POST` | `/api/users/me/roles` | 区分（利用者・運転手）の追加 | 共通 |
| `PUT` | `/api/users/me/active-role` | 稼働区分の切り替え | 共通 |
| `POST` | `/api/reservations` | 予約登録 | 利用者 |
| `GET` | `/api/reservations` | 予約一覧取得 | 利用者 |
| `GET` | `/api/reservations/{reservation_id}` | 予約詳細取得 | 利用者 |
| `POST` | `/api/reservations/{reservation_id}/cancel` | 予約キャンセル | 利用者 |
| `GET` | `/api/reservations/{reservation_id}/candidates` | 配車候補一覧取得 | 利用者 |
| `POST` | `/api/reservations/{reservation_id}/select` | 配車候補の選択・確定 | 利用者 |
| `POST` | `/api/matching/candidates` | 乗合候補作成（システム内部） | システム |
| `GET` | `/api/drivers/me/ride-groups` | 担当運行一覧取得 | 運転手 |
| `GET` | `/api/ride-groups/{ride_group_id}` | 便（乗合グループ）詳細取得 | 運転手 |
| `POST` | `/api/ride-groups/{ride_group_id}/start` | 運行開始の記録 | 運転手 |
| `POST` | `/api/ride-groups/{ride_group_id}/complete` | 乗車完了の記録 | 運転手 |
| `GET` | `/api/reservations/{reservation_id}/notifications` | 通知一覧取得 | 利用者 |
| `POST` | `/api/notifications/{notification_id}/read` | 通知を既読にする | 共通 |

## 3. 共通レスポンス

### 成功時

```json
{
  "data": {},
  "meta": {}
}
```

### エラー時

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "入力内容を確認してください",
    "details": [
      {
        "field": "passenger_count",
        "message": "乗車人数は1以上で入力してください"
      }
    ]
  }
}
```

## 4. 認証API

### 4.1 ログイン

`POST /api/auth/login`

JWT は不要。

#### リクエスト

```json
{
  "email": "taro.yamada@example.com",
  "password": "password1234"
}
```

#### 成功レスポンス: `200 OK`

```json
{
  "data": {
    "access_token": "eyJhbGciOiJIUzI1NiIs...",
    "token_type": "Bearer",
    "expires_at": "2026-09-20T13:00:00Z"
  }
}
```

メールアドレスが未登録、またはパスワードが違う場合は、どちらかを区別せず `401 INVALID_CREDENTIALS` を返す。

## 5. ユーザーAPI

1つのアカウントで利用者（`rider`）と運転手（`driver`）の両方を登録できる。同時に稼働できるのは `active_role` の1つだけとする。

### 5.1 利用者登録

`POST /api/users`

JWT は不要。指定した `role` が最初に登録される区分となり、そのまま稼働区分になる。同じメールアドレスで登録済みの場合は `409 CONFLICT` を返す。登録後は `POST /api/auth/login` でログインする。

#### リクエスト

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

#### 成功レスポンス: `201 Created`

```json
{
  "data": {
    "id": "cd0b97db-60dc-40c3-9523-86633cf4389f",
    "email": "taro.yamada@example.com",
    "last_name": "山田",
    "first_name": "太郎",
    "kana_last_name": "ヤマダ",
    "kana_first_name": "タロウ",
    "active_role": "rider",
    "roles": ["rider"],
    "created_at": "2026-09-20T12:00:00Z"
  }
}
```

### 5.2 本人のユーザー情報取得

`GET /api/users/me`

レスポンスは 5.1 と同じ形式。

### 5.3 区分の追加

`POST /api/users/me/roles`

#### リクエスト

```json
{
  "role": "driver"
}
```

成功時は `200 OK` でユーザー情報を返す。すでに登録済みの区分の場合は `409 CONFLICT` を返す。

### 5.4 稼働区分の切り替え

`PUT /api/users/me/active-role`

#### リクエスト

```json
{
  "role": "driver"
}
```

成功時は `200 OK` でユーザー情報を返す。以下の場合は `409 CONFLICT` を返す。

| 条件 | メッセージ |
| --- | --- |
| 指定した区分が未登録 | この利用者区分は登録されていません |
| 利用者として未完了の予約がある | 完了またはキャンセルされていない予約があるため切り替えできません |
| 運転手として未完了の運行がある | 完了またはキャンセルされていない運行があるため切り替えできません |

未完了の予約とは `matching`、`confirmed`、`in_progress` の予約を指す。未完了の運行とは `confirmed`、`in_progress` の便を指す。運転手が切り替えた場合、その運転手の `proposed` の候補は取り消す。

## 6. 予約API

### 6.1 予約登録

`POST /api/reservations`

#### リクエスト

```json
{
  "pickup_location": "電波学園前",
  "destination": "市役所",
  "requested_pickup_at": "2026-09-21T10:00:00+09:00",
  "passenger_count": 1,
  "consideration_notes": "車いすなし"
}
```

予約者は JWT の本人となる。本人が利用者（`rider`）として稼働中でない場合は `409 CONFLICT` を返す。

#### 成功レスポンス: `201 Created`

```json
{
  "data": {
    "id": "reservation-001",
    "reservation_number": "RR-20260921-0001",
    "user_id": "user-001",
    "pickup_location": "電波学園前",
    "destination": "市役所",
    "requested_pickup_at": "2026-09-21T10:00:00+09:00",
    "passenger_count": 1,
    "consideration_notes": "車いすなし",
    "status": "matching",
    "created_at": "2026-09-20T12:00:00+09:00"
  }
}
```

### 6.2 予約一覧取得

`GET /api/reservations`

本人の予約だけを返す。

#### クエリパラメータ

| パラメータ | 必須 | 説明 |
| --- | --- | --- |
| `date` | 任意 | 乗車日。例: `2026-09-21` |
| `status` | 任意 | `matching`、`confirmed` など |
| `from` | 任意 | 希望乗車日時の開始 |
| `to` | 任意 | 希望乗車日時の終了 |
| `page` | 任意 | ページ番号。初期値 `1` |
| `limit` | 任意 | 取得件数。初期値 `50` |

#### 成功レスポンス: `200 OK`

```json
{
  "data": [
    {
      "id": "reservation-001",
      "reservation_number": "RR-20260921-0001",
      "pickup_location": "電波学園前",
      "destination": "市役所",
      "requested_pickup_at": "2026-09-21T10:00:00+09:00",
      "passenger_count": 1,
      "status": "matching"
    }
  ],
  "meta": {
    "page": 1,
    "limit": 50,
    "total": 1
  }
}
```

### 6.3 予約詳細取得

`GET /api/reservations/{reservation_id}`

#### 成功レスポンス: `200 OK`

予約情報に加えて、所属する便、運転手、通知を返す。

```json
{
  "data": {
    "id": "reservation-001",
    "reservation_number": "RR-20260921-0001",
    "status": "confirmed",
    "pickup_location": "電波学園前",
    "destination": "市役所",
    "requested_pickup_at": "2026-09-21T10:00:00+09:00",
    "confirmed_pickup_at": "2026-09-21T09:55:00+09:00",
    "estimated_fare": 800,
    "estimated_duration_minutes": 25,
    "ride_group_id": "group-001",
    "driver_name": "田中 健一",
    "notifications": []
  }
}
```

### 6.4 予約キャンセル

`POST /api/reservations/{reservation_id}/cancel`

#### リクエスト

```json
{
  "reason": "予定が変更になったため"
}
```

#### 成功レスポンス: `200 OK`

```json
{
  "data": {
    "id": "reservation-001",
    "status": "cancelled",
    "cancellation_reason": "予定が変更になったため",
    "cancelled_at": "2026-09-20T13:00:00+09:00"
  }
}
```

`completed` またはすでに `cancelled` の予約はキャンセルできない。確定済みの便に所属していた場合は、その便から外して運転手に通知する。便の利用者がいなくなった場合は、便も `cancelled` にする。

### 6.5 配車候補一覧取得

`GET /api/reservations/{reservation_id}/candidates`

予約に対して作成された配車候補を返す。候補は `proposed` の便と、空きのある `confirmed` の便である。利用者はこの中から1つを選ぶ。

#### 成功レスポンス: `200 OK`

```json
{
  "data": [
    {
      "ride_group_id": "group-001",
      "driver_name": "田中 健一",
      "planned_departure_at": "2026-09-21T09:50:00+09:00",
      "confirmed_pickup_at": "2026-09-21T09:55:00+09:00",
      "estimated_fare": 800,
      "estimated_duration_minutes": 25,
      "estimated_wait_minutes": 5,
      "passenger_count_in_group": 2,
      "capacity": 4,
      "matching_reason": "希望時刻の差が10分で、乗車場所と目的地が近いため"
    }
  ]
}
```

予約が `matching` 以外の場合は空の配列を返す。

### 6.6 配車候補の選択・確定

`POST /api/reservations/{reservation_id}/select`

#### リクエスト

```json
{
  "ride_group_id": "group-001"
}
```

確定時に、以下を検証する。

- 予約が `matching` 状態である
- 便が `proposed` または `confirmed` 状態である
- 便の運転手が運転手として稼働中である
- 車両定員を超えない
- 配慮事項と車両条件が一致している
- 利用者ごとの待ち時間が許容範囲内である

同じ便の最後の空席を複数の利用者が同時に選んだ場合、後から処理されたほうには `409 CONFLICT` を返す。

#### 成功レスポンス: `200 OK`

```json
{
  "data": {
    "reservation_id": "reservation-001",
    "status": "confirmed",
    "ride_group_id": "group-001",
    "ride_group_status": "confirmed",
    "confirmed_at": "2026-09-20T14:00:00+09:00"
  }
}
```

便が `proposed` だった場合は `confirmed` に更新し、運転手に通知する。

## 7. マッチングAPI

### 7.1 乗合候補作成

`POST /api/matching/candidates`

予約登録時にシステムが内部で呼び出す。画面からは直接呼び出さない。

#### リクエスト

```json
{
  "reservation_ids": [
    "reservation-001",
    "reservation-002"
  ]
}
```

`reservation_ids` を省略した場合は、条件に合う `matching` 状態の予約を自動抽出する。候補の運転手は、運転手として稼働中で、時間帯が重なる確定済みの運行がないユーザーから選ぶ。候補を作成できなかった場合は、`422 Unprocessable Entity` と理由を返す。

#### 成功レスポンス: `201 Created`

```json
{
  "data": {
    "ride_group_id": "group-001",
    "group_number": "RG-20260921-0001",
    "driver_id": "driver-user-001",
    "status": "proposed",
    "matching_reason": "希望時刻の差が10分で、乗車場所と目的地が近いため",
    "matching_score": 0.86,
    "planned_departure_at": "2026-09-21T09:50:00+09:00",
    "members": [
      {
        "reservation_id": "reservation-001",
        "pickup_order": 1,
        "dropoff_order": 2,
        "estimated_wait_minutes": 5,
        "estimated_extra_minutes": 8
      }
    ],
    "warnings": []
  }
}
```

## 8. 運行API（運転手向け）

### 8.1 担当運行一覧取得

`GET /api/drivers/me/ride-groups`

#### クエリパラメータ

| パラメータ | 必須 | 説明 |
| --- | --- | --- |
| `status` | 任意 | 初期値は `confirmed,in_progress` |
| `date` | 任意 | 運行日 |

`proposed` の便は運転手には表示しない。

### 8.2 便詳細取得

`GET /api/ride-groups/{ride_group_id}`

利用者一覧（氏名、人数、配慮事項）、車両、乗車順、降車順、乗降場所、出発時刻を返す。運転手には自分が担当する便だけを返す。

### 8.3 運行開始

`POST /api/ride-groups/{ride_group_id}/start`

便を `confirmed` から `in_progress` にし、所属する予約も `in_progress` にする。

### 8.4 乗車完了

`POST /api/ride-groups/{ride_group_id}/complete`

便を `in_progress` から `completed` にし、所属する予約も `completed` にする。

#### 許可される状態遷移（便）

| 現在の状態 | 遷移先 | 操作者 |
| --- | --- | --- |
| `proposed` | `confirmed` | 利用者（候補の選択） |
| `proposed` | `cancelled` | システム（運転手の区分切り替えなど） |
| `confirmed` | `in_progress` | 運転手 |
| `confirmed` | `cancelled` | システム（所属する予約がすべてキャンセル） |
| `in_progress` | `completed` | 運転手 |
| `completed` | なし | - |
| `cancelled` | なし | - |

#### 許可される状態遷移（予約）

| 現在の状態 | 遷移先 | 操作者 |
| --- | --- | --- |
| `matching` | `confirmed` | 利用者（候補の選択） |
| `matching` | `cancelled` | 利用者 |
| `confirmed` | `in_progress` | 運転手（運行開始） |
| `confirmed` | `cancelled` | 利用者 |
| `in_progress` | `completed` | 運転手（乗車完了） |
| `completed` | なし | - |
| `cancelled` | なし | - |

## 9. 通知API

### 通知一覧取得

`GET /api/reservations/{reservation_id}/notifications`

### 通知を既読にする

`POST /api/notifications/{notification_id}/read`

MVPでは画面内通知のみを対象とし、メールやSMSは対象外とする。運転手への通知（新しい運行の確定、予約のキャンセル）も画面内通知とする。

## 10. HTTPステータスとエラーコード

| HTTPステータス | エラーコード | 用途 |
| --- | --- | --- |
| `400` | `BAD_REQUEST` | リクエスト形式が不正 |
| `401` | - | JWT がない、または不正・期限切れ |
| `401` | `INVALID_CREDENTIALS` | ログイン時のメールアドレスまたはパスワードが違う |
| `404` | `NOT_FOUND` | 指定データが存在しない |
| `409` | `CONFLICT` | 状態競合、二重登録、定員超過、稼働区分の切り替え不可 |
| `422` | `VALIDATION_ERROR` | 入力値が要件を満たさない |
| `500` | `INTERNAL_ERROR` | サーバー内部エラー |

## 11. 二重予約防止

- 予約登録リクエストには `Idempotency-Key` ヘッダーを付ける。
- 同じキーを受け取った場合、最初の登録結果を返す。
- サーバー側で `reservation_number` を一意制約にする。
- 状態変更時は現在の状態を再確認し、古い状態からの上書きを拒否する。
- 候補の選択時は便の定員を再確認し、同時選択による定員超過を防ぐ。

## 12. 実装優先順位

1. ログインとユーザーAPI（登録・取得・区分追加・稼働区分切り替え）
2. `POST /api/reservations`
3. `GET /api/reservations`
4. `GET /api/reservations/{reservation_id}`
5. `POST /api/reservations/{reservation_id}/cancel`
6. `POST /api/matching/candidates`
7. `GET /api/reservations/{reservation_id}/candidates`
8. `POST /api/reservations/{reservation_id}/select`
9. `GET /api/drivers/me/ride-groups`・`GET /api/ride-groups/{ride_group_id}`
10. `POST /api/ride-groups/{ride_group_id}/start`・`/complete`
11. 通知API
