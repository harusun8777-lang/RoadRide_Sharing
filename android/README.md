# RoadRide Sharing Android アプリ

## 概要

デプロイ済みの Web フロントエンドを WebView で表示する Android アプリです。  
利用者画面・配車担当者画面を起動時に選択できます。

## セットアップ

### 1. URLを設定する

`app/build.gradle` の以下の行をデプロイ先のURLに変更してください。

```groovy
buildConfigField "String", "RIDER_URL",      "\"https://your-domain.example.com/frontend/reservation/pages/reservation.html\""
buildConfigField "String", "DISPATCHER_URL", "\"https://your-domain.example.com/frontend/dispatch/pages/dashboard.html\""
```

### 2. gradle-wrapper.jar を取得する

`gradle/wrapper/gradle-wrapper.jar` はバイナリファイルのためリポジトリに含まれていません。  
Android Studio で開くと自動的に生成されます。  
または以下のコマンドで取得できます。

```bash
gradle wrapper --gradle-version 8.2
```

### 3. Android Studio で開く

1. Android Studio を起動
2. `android/` フォルダを「Open an Existing Project」で開く
3. Gradle の同期が完了するまで待つ
4. エミュレーターまたは実機で実行

## 動作環境

- 最低 Android バージョン：8.0（API 26）
- ターゲット SDK：34（Android 14）

## 画面構成

```
起動
 └─ SplashActivity（モード選択）
      ├─ 利用者として使う    → MainActivity（予約フォームURL）
      └─ 配車担当者として使う → MainActivity（ダッシュボードURL）
```

## 主な機能

- JavaScript・localStorage 有効
- 戻るボタンで WebView 履歴を戻る
- プルリフレッシュでページ再読み込み
- オフライン時にエラー画面を表示・再試行ボタン付き
- ステータスバーをライト表示（白背景に合わせる）
