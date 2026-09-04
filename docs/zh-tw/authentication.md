# 驗證與應用程式身分

Parsec 的金鑰屬於建立它的那個應用程式。服務從每個請求的驗證欄位判斷是誰在問，所以用戶端送出
的身分不是形式：那是你的金鑰與其他所有人的金鑰之間的界線。

## 身分是做什麼用的

金鑰名稱位於 per-application 的命名空間。兩個應用程式可以各自擁有一把叫 `signing-key` 的金鑰，
而且都看不到、用不到、也刪不掉對方的。`ListKeys` 只回傳發出該請求的那個身分擁有的金鑰。

這讓身分成為部署的一部分，而不是程式碼的一部分。它必須

- **唯一**，否則兩個應用程式共用命名空間，可以互刪對方的金鑰；
- **重開機後不變**，否則應用程式下次啟動時就失去自己的金鑰；
- **不是機密**，因為 Direct 驗證是明文送出的，服務也不把它當成任何東西的證明。

最後一點最常被誤會。選擇之前先讀[安全性注意事項](security.md)。

## 四種型別

| 型別 | 送出 | 什麼時候用 |
|---|---|---|
| `NoAuthentication` | 什麼都不送 | 只是詢問服務本身。這是預設值。 |
| `DirectAuthentication` | 應用程式名稱的 UTF-8 | 服務設定為 `Direct`，而且 socket 本身已經是可信的 |
| `UnixPeerCredentialsAuthentication` | 有效使用者 ID，little-endian 32 位元整數 | 服務設定為 `UnixPeerCredentials`，而且雙方在同一台機器上 |
| `JwtSvidAuthentication` | 一個 SPIFFE JWT-SVID | 由 SPIFFE workload API 發身分 |

一個服務只跑一種 authenticator。問它是哪一種：

```csharp
var authenticators = await client.ListAuthenticatorsAsync();
```

## 「不驗證」是一個真的選項

`NoAuthentication` 是 `ParsecClientOptions` 的預設值，而它對「只問問題」的用戶端是正確的：
Ping、ListProviders、ListOpcodes、ListAuthenticators 都不需要身分，而且不管你怎麼設定，Ping
一律不帶驗證送出 —— 應用程式呼叫它是為了在還不知道服務跑哪種 authenticator 之前先找到服務。

其他任何事情它都不對。沒有身分，服務就沒有命名空間可以放金鑰，`ListKeys` 會回
`NotAuthenticated`。

> [!NOTE]
> Parsec book 的 API overview 說 core provider 只接受 `None`。服務實際的行為不是這樣：它先
> 驗證身分，再看 provider，所以 core provider 接受任何驗證型別。做選擇的是個別操作，不是
> provider。

## Direct

```csharp
new DirectAuthentication("my-application")
```

名稱以 UTF-8 送出，服務照單全收。任何能連到那個 socket 的東西都可以宣稱任何名稱，所以 Direct
的強度就等於 socket 上的權限。測試映像檔用的是這個，而當 socket 已經只開放給單一可信應用程式
時，它也是正確的選擇。

## Unix peer credentials

```csharp
new UnixPeerCredentialsAuthentication()
```

用戶端送出自己的有效使用者 ID，而核心會告訴服務真正的那個。說謊的呼叫端會被抓到，這讓它成為
本機選項中最強的一個。

有兩件事要知道。身分是使用者 ID，所以同一個使用者下的每個行程共用一個命名空間。而且它只在
用戶端直接連到 socket 時有效：任何轉送連線的東西 —— 中繼、port mapping、容器橋接 —— 都會讓
服務看到轉送者的憑證。這也是 `Parsec.Testcontainers` 在非 Linux 主機上用的橋接無法承載它的
原因。

## JWT-SVID

```csharp
new JwtSvidAuthentication(token)
```

token 來自 SPIFFE workload API，不是本程式庫。它有有效期限，而這個型別不會自動更新：在過期
之前重新取得 token 並建立新的驗證物件，或建立新的用戶端。

## 在執行期選擇

驗證在用戶端建立時就固定了，所以需要換身分的應用程式要另外建一個。用
[`Parsec.Client.DependencyInjection`](getting-started.md) 的應用程式，設定是從容器建出來的，
所以身分可以來自組態：

```csharp
builder.Services.AddParsecClient(provider => new ParsecClientOptions
{
    Authentication = new DirectAuthentication(
        provider.GetRequiredService<IConfiguration>()["Parsec:ApplicationName"]!),
});
```
