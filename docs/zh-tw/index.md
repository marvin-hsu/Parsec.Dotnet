# Parsec.Dotnet

Parsec.Dotnet 是 Parsec 的 .NET 用戶端程式庫。

Parsec 全稱 Platform AbstRaction for SECurity，是雲端原生運算基金會（Cloud Native
Computing Foundation, CNCF）的專案。Parsec 讓應用程式用單一 API 取得硬體支援的安全服務，
包含 TPM、HSM、PKCS#11 與 Trusted Applications。

本程式庫透過行程間通訊（Inter-Process Communication, IPC）與執行中的 Parsec 服務通訊。
應用程式不需要連結任何硬體驅動程式。

## 現況

公開 API 仍在開發中。本儲存庫發佈兩個套件。`Parsec.Client` 提供用戶端 API，目前發佈
`Parsec.Client.IParsecClient` 型別。`Parsec.Testcontainers` 提供 Testcontainers 模組，
在測試中啟動 Parsec 服務，目前發佈 `Parsec.Testcontainers.ParsecImage` 型別。

wire protocol 的訊息定義來自上游的 parsec-operations 儲存庫。建置時會從那些 `.proto`
檔案產生 C# 程式碼。產生的型別為 internal，公開 API 全部手寫。

## 支援的框架

| 目標框架 | 說明 |
|---|---|
| `net8.0` | LTS |
| `net10.0` | LTS |

兩個套件都支援上述框架。`Parsec.Client` 相容 AOT；`Parsec.Testcontainers` 不相容 AOT，
因為它的 Docker 用戶端以反射進行序列化。

## 文件架構

本站有兩個語言版本。英文是根語言，臺灣正體中文放在 `zh-tw/` 之下。概念文章兩個語言都有。

API 參考由原始碼的 XML 文件註解產生，只有英文版。

## 從原始碼建置

`.proto` 檔案來自 git submodule。複製儲存庫時要一併取得 submodule。

```bash
git clone --recurse-submodules https://github.com/marvin-hsu/Parsec.Dotnet.git
```

若已經複製過儲存庫，初始化 submodule。

```bash
git submodule update --init
```

> [!NOTE]
> 若 submodule 不存在，建置會中止並顯示這道指令。

建置並測試方案。

```bash
dotnet build
dotnet test
```

## 建置本站

兩個語言版本都從 `docs` 資料夾建置。

```bash
dotnet docfx docs/docfx.json
dotnet docfx docs/docfx.zh-tw.json
```

輸出位於 `artifacts/docs`。
