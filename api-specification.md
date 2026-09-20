# API仕様書

本仕様書は、RoadRide Sharing MVPのフロントエンドとバックエンド間の通信仕様を定義する。

## 1. 基本方針

- 通信方式: REST API
- データ形式: JSON
- 文字コード: UTF-8
- ベースパス: `/api`
- 日時形式: ISO 8601形式。例: `2026-09-21T10:00:00+09:00`
- MVPでは認証を省略し、利用者ID・担当者IDはリクエストまたは仮固定値で扱う。これは開発用であり、本番運用では認証と権限確認を追加する。

## 2. エンドポイント一覧

| メソッド | パス | 用途 |
| --- | --- | --- |
| `POST` | `/api/reservations` | 予約登録 |
| `GET` | `/api/reservations` | 予約一覧取得 |
| `GET` | `/api/reservations/{reservation_id}` | 予約詳細取得 |
| `POST` | `/api/reservations/{reservation_id}/cancel` | 予約キャンセル |
| `PATCH` | `/api/reservations/{reservation_id}/status` | 予約状態変更 |
| `POST` | `/api/matching/candidates` | 乗合候補作成 |
| `GET` | `/api/matching/candidates` | 乗合候補一覧取得 |
| `POST` | `/api/matching/validate` | 候補の確定前チェック |
| `GET` | `/api/ride-groups/{ride_group_id}` | 乗合グループ詳細取得 |
| `PATCH` | `/api/ride-groups/{ride_group_id}` | 乗合グループ編集 |
| `POST` | `/api/ride-groups/{ride_group_id}/confirm` | 配車確定 |
| `GET` | `/api/reservations/{reservation_id}/notifications` | 通知一覧取得 |
| `POST` | `/api/notifications/{notification_id}/read` | 通知を既読にする |

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

## 4. 予約API

### 4.1 予約登録

`POST /api/reservations`

#### リクエスト

```json
{
  "user_id": "user-001",
  "pickup_location": "電波学園前",
  "destination": "市役所",
  "requested_pickup_at": "2026-09-21T10:00:00+09:00",
  "passenger_count": 1,
  "consideration_notes": "車いすなし"
}
```

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

### 4.2 予約一覧取得

`GET /api/reservations`

#### クエリパラメータ

| パラメータ | 必須 | 説明 |
| --- | --- | --- |
| `user_id` | 任意 | 利用者本人の予約だけを取得 |
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

### 4.3 予約詳細取得

`GET /api/reservations/{reservation_id}`

#### 成功レスポンス: `200 OK`

予約情報に加えて、所属する乗合グループと通知を返す。

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
    "notifications": []
  }
}
```

### 4.4 予約キャンセル

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

`completed` またはすでに `cancelled` の予約はキャンセルできない。

### 4.5 予約状態変更

`PATCH /api/reservations/{reservation_id}/status`

配車担当者が運行開始や乗車完了を記録するために使用する。状態遷移は「許可される状態遷移」に従う。

#### リクエスト

```json
{
  "status": "in_progress",
  "changed_by": "dispatcher-001",
  "reason": "運行開始"
}
```

#### 許可される状態遷移

| 現在の状態 | 遷移先 |
| --- | --- |
| `matching` | `confirmed`、`cancelled` |
| `confirmed` | `in_progress`、`cancelled` |
| `in_progress` | `completed` |
| `completed` | なし |
| `cancelled` | なし |

## 5. マッチングAPI

### 5.1 乗合候補作成

`POST /api/matching/candidates`

#### リクエスト

```json
{
  "reservation_ids": [
    "reservation-001",
    "reservation-002"
  ],
  "vehicle_id": "vehicle-001"
}
```

`reservation_ids` を省略した場合は、条件に合う `matching` 状態の予約を自動抽出する。候補作成に失敗した場合は、`422 Unprocessable Entity` と理由を返す。

#### 成功レスポンス: `201 Created`

```json
{
  "data": {
    "ride_group_id": "group-001",
    "group_number": "RG-20260921-0001",
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

### 5.2 乗合候補一覧取得

`GET /api/matching/candidates`

#### クエリパラメータ

| パラメータ | 必須 | 説明 |
| --- | --- | --- |
| `date` | 任意 | 乗車日 |
| `status` | 任意 | 初期値は `proposed` |
| `min_score` | 任意 | 最低マッチングスコア |

### 5.3 候補チェック

`POST /api/matching/validate`

配車確定前に、定員、時刻、配慮事項を再確認する。

#### リクエスト

```json
{
  "ride_group_id": "group-001"
}
```

#### 成功レスポンス: `200 OK`

```json
{
  "data": {
    "valid": true,
    "warnings": [],
    "errors": []
  }
}
```

## 6. 乗合グループAPI

### 6.1 乗合グループ詳細取得

`GET /api/ride-groups/{ride_group_id}`

利用者一覧、車両、乗車順、降車順、出発時刻、マッチング理由を返す。

### 6.2 乗合グループ編集

`PATCH /api/ride-groups/{ride_group_id}`

#### リクエスト

```json
{
  "vehicle_id": "vehicle-001",
  "planned_departure_at": "2026-09-21T09:50:00+09:00",
  "members": [
    {
      "reservation_id": "reservation-001",
      "pickup_order": 1,
      "dropoff_order": 2
    }
  ],
  "changed_by": "dispatcher-001",
  "reason": "車両の都合で乗車順を変更"
}
```

### 6.3 配車確定

`POST /api/ride-groups/{ride_group_id}/confirm`

配車確定時に、以下を検証する。

- 車両定員を超えていない
- 配慮事項と車両条件が一致している
- 予約が `matching` 状態である
- 利用者ごとの待ち時間が許容範囲内である

#### 成功レスポンス: `200 OK`

```json
{
  "data": {
    "ride_group_id": "group-001",
    "status": "confirmed",
    "confirmed_at": "2026-09-20T14:00:00+09:00",
    "updated_reservation_ids": [
      "reservation-001",
      "reservation-002"
    ]
  }
}
```

## 7. 通知API

### 通知一覧取得

`GET /api/reservations/{reservation_id}/notifications`

### 通知を既読にする

`POST /api/notifications/{notification_id}/read`

MVPでは画面内通知のみを対象とし、メールやSMSは対象外とする。

## 8. HTTPステータスとエラーコード

| HTTPステータス | エラーコード | 用途 |
| --- | --- | --- |
| `400` | `BAD_REQUEST` | リクエスト形式が不正 |
| `404` | `NOT_FOUND` | 指定データが存在しない |
| `409` | `CONFLICT` | 状態競合、二重登録、定員超過 |
| `422` | `VALIDATION_ERROR` | 入力値が要件を満たさない |
| `500` | `INTERNAL_ERROR` | サーバー内部エラー |

## 9. 二重予約防止

- 予約登録リクエストには `Idempotency-Key` ヘッダーを付ける。
- 同じキーを受け取った場合、最初の登録結果を返す。
- サーバー側で `reservation_number` を一意制約にする。
- 状態変更時は現在の状態を再確認し、古い状態からの上書きを拒否する。

## 10. 実装優先順位

1. `POST /api/reservations`
2. `GET /api/reservations`
3. `GET /api/reservations/{reservation_id}`
4. `POST /api/reservations/{reservation_id}/cancel`
5. `POST /api/matching/candidates`
6. `GET /api/matching/candidates`
7. `POST /api/ride-groups/{ride_group_id}/confirm`
8. `PATCH /api/reservations/{reservation_id}/status`
9. グループ編集API
10. 通知API
