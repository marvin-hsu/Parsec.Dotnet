# Parsec.Dotnet

Parsec.Dotnet 是 Parsec 的 .NET 用戶端程式庫。

Parsec 全稱 Platform AbstRaction for SECurity，是雲端原生運算基金會（Cloud Native
Computing Foundation, CNCF）的專案。Parsec 讓應用程式用單一 API 取得硬體支援的安全服務，
包含 TPM、HSM、PKCS#11 與 Trusted Applications。

本程式庫透過行程間通訊（Inter-Process Communication, IPC）與執行中的 Parsec 服務通訊。
應用程式不需要連結任何硬體驅動程式。

## 套件

| 套件 | 內容 | 依賴 |
|---|---|---|
| `Parsec.Client` | 用戶端本體。`ParsecClient` 建立，`IParsecClient` 是你拿在手上的。 | 只有 protobuf 執行期 |
| `Parsec.Client.DependencyInjection` | 給 `Microsoft.Extensions` 應用程式用的 `AddParsecClient` | 用戶端，加上 DI abstractions |
| `Parsec.Testcontainers` | Testcontainers 模組，在測試中啟動真的服務 | Testcontainers |

兩個選用套件之所以分開，是為了讓兩個都不需要的應用程式不必帶著它們。

wire protocol 的訊息定義來自上游的 parsec-operations 儲存庫。建置時會從那些 `.proto`
檔案產生 C# 程式碼。產生的型別為 internal，公開 API 全部手寫。

目前尚未發佈任何版本。協定定義的操作在公開面上都已齊備，而公開面仍然可以自由變動。

## 從哪裡開始

- [開始使用](getting-started.md) —— 從空專案到一份簽章
- [驗證與應用程式身分](authentication.md) —— 服務認為你是誰
- [錯誤模型](error-model.md) —— 每個例外的意義，以及哪些回答而不丟例外
- [wire protocol](wire-protocol.md) —— socket 上流動的是什麼
- [對真實服務做測試](testing.md) —— 用容器而不是假物件
- [安全性注意事項](security.md) —— 用戶端能承諾的界限

## 支援的框架

| 目標框架 | 說明 |
|---|---|
| `net8.0` | LTS |
| `net10.0` | LTS |

三個套件都支援上述框架。`Parsec.Client` 與 `Parsec.Client.DependencyInjection` 相容 AOT：
兩者一起做原生發佈實測為零 trim 與 AOT 警告，產出的執行檔也真的跑得起來。
`Parsec.Testcontainers` 不相容 AOT，因為它的 Docker 用戶端以反射進行序列化；那是測試期的依賴，
不影響以原生方式發佈的應用程式。

## 文件架構

本站有兩個語言版本。英文是根語言，臺灣正體中文放在 `zh-tw/` 之下。概念文章兩個語言都有。

API 參考由原始碼的 XML 文件註解產生，涵蓋三個套件，只有英文版。

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
