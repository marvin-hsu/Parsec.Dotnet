# 對真實服務做測試

`Parsec.Testcontainers` 在容器裡啟動一個真的 Parsec 服務，並給你的測試一個可以連的 socket。
沒有任何東西被假造：你的用戶端送出的位元組，是由跟你的應用程式將來會遇到的同一個服務讀的。

## 安裝

```bash
dotnet add package Parsec.Testcontainers
```

這個模組需要 Docker 端點。它會拉 `ghcr.io/marvin-hsu/parsec-testcontainers`，以 digest 釘住，
從上游服務與 `parsec-tool` 的釘住標籤建置，提供 `linux/amd64` 與 `linux/arm64` 兩種架構。

> [!WARNING]
> 這個映像檔是為測試存在的。它跑軟體的 Mbed Crypto provider、Direct 驗證，背後沒有任何硬體。
> 不適合正式環境。

## 啟動一個

```csharp
await using var container = new ParsecBuilder().Build();

await container.StartAsync();

await using var client = await ParsecClient.CreateAsync(new ParsecClientOptions
{
    Endpoint = container.Endpoint,
    Authentication = new DirectAuthentication("my-tests"),
});
```

`ParsecContainer.Endpoint` 是一個 `unix:` URI，指向執行測試那台機器上的 socket。
`ParsecContainer.SocketPath` 是同一個路徑但沒有 scheme，給需要純路徑的東西用。

## socket 怎麼傳到你這裡

在 Linux 上，模組把一個目錄掛進容器，服務在裡面建立 socket，所以你的測試連的就是服務建立的
那個檔案。

其他平台不行。從 macOS 或 Windows 透過 bind mount 是連不到容器裡的 Unix socket 的，而這是實測
出來的、不是假設：主機端連過去會被 `Connection refused`。所以在那些主機上，模組在容器裡跑
`socat` 把 socket 轉成 TCP、對外映射連接埠，再在主機上跑一個小型中繼，監聽一個 Unix socket
並轉送過去。

有兩個後果值得知道：

- 服務看到的是中繼的憑證，不是你測試行程的憑證，所以
  `UnixPeerCredentialsAuthentication` 沒辦法用這條路徑測。用 Direct，或在 Linux 上跑那個測試。
- `ParsecBuilder.WithSocketDirectory` 只在直掛路徑有效。在橋接的主機上，路徑由模組決定。

## 調整服務

```csharp
var container = new ParsecBuilder()
    .WithAuthType(ParsecAuthType.UnixPeerCredentials)
    .WithLogLevel(ParsecLogLevel.Debug)
    .Build();
```

`WithAuthType` 與 `WithLogLevel` 會改寫服務啟動時的組態。這兩個涵蓋不到的部分，
`WithConfigFile` 直接整份換掉，而且不管你有沒有同時指定自己的映像檔都能用。

## 把需要 Docker 的操作分開跑

這個儲存庫的測試帶了 trait，所以兩條 lane 可以分開跑：

```bash
just test-unit          # 不需要 Docker 的全部
just test-integration   # 需要 Docker 的全部
```

找不到 Docker 端點的整合測試會 skip 而不是 fail，所以沒有 Docker 的貢獻者仍然跑得出全綠。

## 測試映像檔能做與不能做什麼

問，不要假設：

```csharp
var supported = await client.ListOpcodesAsync(client.Provider);
```

軟體 provider 跑十六個操作：金鑰管理、對雜湊簽章與驗章、雜湊、亂數、非對稱加解密、
authenticated encryption、raw key agreement，以及能力查詢。它沒有 cipher 操作、沒有對訊息簽章
的操作，也不能對金鑰做認證。

這兩種「沒有」失敗的方式不一樣，而這個差別在你判斷「換一個 provider 有沒有用」時很重要。
cipher 請求會抵達 provider，然後帶著 `PsaErrorNotSupported` 回來。MAC 請求根本到不了那裡：
服務在任何 provider 被詢問之前就回 `OpcodeDoesNotExist`。
