@echo off
rem ローカル環境（SQL Server・バックエンド・Swagger UI）を起動する
rem   start-local.bat        起動
rem   start-local.bat stop   停止（DB のデータは残る）
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set "API_URL=http://localhost:8080"
set "SWAGGER_URL=http://localhost:8081"
set "SWAGGER_CONTAINER=roadride-swagger-ui"

where docker >nul 2>&1
if errorlevel 1 (
    echo [エラー] docker が見つかりません。Docker Desktop をインストールしてください。
    exit /b 1
)
docker info >nul 2>&1
if errorlevel 1 (
    echo [エラー] Docker が起動していません。Docker Desktop を起動してから再実行してください。
    exit /b 1
)

if /i "%~1"=="stop" goto :stop

if not exist ".env" (
    echo .env がないため .env.example からコピーします。必要に応じて値を変更してください。
    copy /y ".env.example" ".env" >nul
)

echo === SQL Server とバックエンドを起動します ===
docker compose up -d --build
if errorlevel 1 (
    echo [エラー] docker compose の起動に失敗しました。
    exit /b 1
)

echo === Swagger UI を起動します（docs/SWAGGER.yaml を表示） ===
docker rm -f %SWAGGER_CONTAINER% >nul 2>&1
docker run -d --name %SWAGGER_CONTAINER% -p 8081:8080 -e SWAGGER_JSON=/spec/SWAGGER.yaml -v "%~dp0docs:/spec:ro" swaggerapi/swagger-ui >nul
if errorlevel 1 (
    echo [エラー] Swagger UI の起動に失敗しました。
    exit /b 1
)

echo === バックエンドの起動を待っています（最大 3 分） ===
set /a RETRY=0
:wait_health
curl -s -o nul -f "%API_URL%/health" >nul 2>&1
if not errorlevel 1 goto :ready
set /a RETRY+=1
if %RETRY% geq 60 (
    echo [エラー] バックエンドが起動しません。docker compose logs backend でログを確認してください。
    exit /b 1
)
timeout /t 3 /nobreak >nul
goto :wait_health

:ready
echo.
echo 起動しました。
echo   API           : %API_URL%
echo   ヘルスチェック : %API_URL%/health
echo   Swagger UI    : %SWAGGER_URL%
echo   停止          : start-local.bat stop
start "" "%SWAGGER_URL%"
exit /b 0

:stop
echo === ローカル環境を停止します ===
docker rm -f %SWAGGER_CONTAINER% >nul 2>&1
docker compose down
exit /b 0
