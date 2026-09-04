# 開始使用

這一頁帶你從空專案走到「用一把讀不到的金鑰做出一份簽章」。

## 安裝

```bash
dotnet add package Parsec.Client
```

`Parsec.Client` 的執行階段依賴只有 protobuf 執行期。旁邊有兩個選用套件：給
`Microsoft.Extensions` 應用程式用的 `Parsec.Client.DependencyInjection`，以及給整合測試用的
`Parsec.Testcontainers`。兩個都不需要的應用程式不會被迫帶著它們。

## 找到服務

用戶端需要一個執行中的 Parsec 服務。機器上已經有的話，預設端點是 Unix domain socket
`/run/parsec/parsec.sock`；管理者可以設定 `PARSEC_SERVICE_ENDPOINT` 換位置，用戶端會自己讀
這個環境變數。

手邊沒有服務的話，用容器起一個，見[對真實服務做測試](testing.md)。

## 建立用戶端

```csharp
using Parsec.Client;
using Parsec.Client.Authentication;

await using var client = await ParsecClient.CreateAsync(new ParsecClientOptions
{
    Authentication = new DirectAuthentication("my-application"),
});
```

`ParsecClient.CreateAsync` 在回傳之前會做兩次往返。第一次是 Ping，用來協商協定版本並確認服務
真的會回應；第二次是 ListProviders，用來決定要綁哪一個 provider：`ParsecClientOptions.Provider`
指定的那個，或第一個不是 core 的那個。

這是刻意的。服務不存在、連不上、或只跑了 core provider，都會在這裡失敗 —— 在你的應用程式還
有辦法處理的地方，而不是在你剛好第一個呼叫到的那個操作裡面。

> [!IMPORTANT]
> 預設的驗證不代表任何身分。它足以詢問服務能做什麼，但不足以擁有金鑰。任何碰到金鑰的操作都
> 需要身分，見[驗證與應用程式身分](authentication.md)。

## 建立金鑰

```csharp
using Parsec.Client.Algorithms;
using Parsec.Client.Keys;

var algorithm = SignatureAlgorithm.RsaPkcs1v15Sign(Hash.Sha256);

await client.Keys.GenerateKeyAsync("my-key", KeyAttributes.RsaSigningKey(algorithm: algorithm));
```

`KeyAttributes` 描述金鑰裝什麼、多大、可以拿來做什麼。上面四個工廠方法涵蓋常見形狀；這裡的
`RsaSigningKey` 是一把 2048 位元的 RSA 金鑰對，可以簽章與驗章雜湊，而且不得離開服務。

私鑰那一半從來不會進到你的行程。之後你用名稱來指涉這把金鑰，而它屬於建立它的那個應用程式：
另一個用不同身分驗證的應用程式看不到也用不到。

> [!NOTE]
> 一把金鑰只綁定一個演算法。服務會拒絕指名其他演算法的請求，這就是簽章金鑰不會被拐去解密的
> 原因。

## 簽章與驗章

```csharp
var digest = await client.Crypto.HashComputeAsync(Hash.Sha256, "sign me"u8.ToArray());
var signature = await client.Crypto.SignHashAsync("my-key", algorithm, digest);

var ok = await client.Crypto.VerifyHashAsync("my-key", algorithm, digest, signature);
```

驗章回傳 `bool`。簽章對不上是這個問題的答案，不是請求失敗。其他所有情況都會丟例外 —— 如果
呼叫端必須靠接例外才能知道簽章不對，遲早會接到一個意義完全不同的例外，見[錯誤模型](error-model.md)。

## 在服務外面驗證公鑰那一半

簽章本身跟 Parsec 沒有任何綁定。把公鑰匯出，平台自己就能驗證，不需要服務。

```csharp
using System.Security.Cryptography;

var publicKey = await client.Keys.ExportPublicKeyAsync("my-key");

using var rsa = RSA.Create();
rsa.ImportRSAPublicKey(publicKey, out _);

var verified = rsa.VerifyHash(digest, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
```

匯出公鑰不需要任何權限。匯出私鑰那一半需要金鑰政策帶有 `KeyUsages.Export`，而那個權限預設
是關的，要自己開。

## 清理

```csharp
await client.Keys.DestroyKeyAsync("my-key");
```

刪除一把不存在的金鑰是錯誤，不是安靜成功 —— 對一個正在清理自己東西的應用程式來說，這兩件事
意義不同。

## 什麼跑在哪裡

不是每個 provider 都跑每個操作。先問，不要假設：

```csharp
var supported = await client.ListOpcodesAsync(client.Provider);
```

測試映像檔裡的軟體 provider 跑十六個操作。它沒有 cipher、沒有對訊息簽章的操作，也不能對金鑰
做認證 —— 認證需要一個能替金鑰背書的裝置。

## 接下來

- [驗證與應用程式身分](authentication.md)
- [錯誤模型](error-model.md)
- [wire protocol](wire-protocol.md)
- [對真實服務做測試](testing.md)
- [安全性注意事項](security.md)
