# 錯誤模型

出錯的地方有四個：你自己的呼叫、連線、wire 上的位元組、服務內部。例外階層說明是哪一個，因為
答案決定應用程式該怎麼處理。

## 階層

```
Exception
└── ParsecException                 抽象，接這個就接到全部
    ├── ParsecConfigurationException  請求根本沒送出去
    ├── ParsecTransportException      連線失敗
    ├── ParsecProtocolException       回應看不懂
    └── ParsecServiceException        服務說不行
        └── ParsecPsaException        ⋯而且是密碼學上的不行
```

| 例外 | 意義 | 通常 |
|---|---|---|
| `ParsecConfigurationException` | 用戶端拒絕送出。端點不是 `unix:` URI、路徑超過平台允許的長度，或服務沒有任何符合你設定的 provider。 | 部署設定錯了，重試沒有用。 |
| `ParsecTransportException` | Socket 失敗。服務沒在跑、路徑錯了，或連線中途斷掉。InnerException 是平台回報的原始錯誤。 | 值得重試。 |
| `ParsecProtocolException` | 回應不符合協定，或帶了這個用戶端讀不回來的東西。 | 版本不合或有缺陷，重試沒有用。 |
| `ParsecServiceException` | 服務回應了，而回應是拒絕。`Status` 帶著是哪一種。 | 完全看狀態碼。 |
| `ParsecPsaException` | 同上，但屬於密碼學規格定義的那些狀態碼。 | 看狀態碼。 |

每一種都帶著失敗的那個操作，所以訊息會同時說出請求與原因。

## 狀態碼

服務回報兩類狀態碼。服務層狀態碼從 1 到 21，描述請求本身：provider 沒註冊、內容超過服務接受
的大小、驗證沒通過。PSA 狀態碼從 1132 起，描述密碼學：金鑰名稱已存在、簽章驗不過、provider
不跑這個演算法。

這個區分在你判斷「換一個 provider 有沒有用」時很重要。`PsaErrorNotSupported` 來自一個真的收到
這個操作、但不願意執行的 provider，所以換一個可能就行。像 `OpcodeDoesNotExist` 這種服務層狀態
碼則是在任何 provider 被詢問之前就由服務回的，所以這個服務上沒有任何 provider 會執行它。

```csharp
try
{
    await client.Keys.GenerateKeyAsync(name, attributes);
}
catch (ParsecPsaException fault) when (fault.Status is ResponseStatus.PsaErrorAlreadyExists)
{
    // 名稱被佔用了。這是正常結果，不是失敗。
}
```

用戶端不認識的狀態碼仍然會以帶著該數字的 `ParsecServiceException` 抵達。服務隨時可能新增狀態
碼，而因為一個沒聽過的值就拒絕解析回應，會讓用戶端在服務比它先升級的那一刻壞掉。

## 哪些回答而不是丟例外

有三個操作回傳 `bool`，而不是你可能預期的例外：

- `VerifyHashAsync` 與 `VerifyMessageAsync`
- `HashCompareAsync`
- `CanDoCryptoAsync`

簽章對不上，是這個問題的答案，不是請求失敗。如果呼叫端必須靠接例外才能知道，遲早會接到一個
意義不同的例外 —— 連線斷了、金鑰不存在 —— 然後把它讀成「驗證失敗」。那正是會讓錯誤簽章矇混
過關的那種 bug，所以這三個回 `false`，其他所有情況照樣丟例外。

`AeadDecryptAsync` 是反例中的證明。tag 對不上時它丟例外，因為那時候沒有明文可以回傳，而在
`false` 旁邊回傳一個空的東西，等於邀請呼叫端去讀那個空的。

## 用戶端拒絕讀回來的東西

未知的 opcode 或狀態碼會被帶過去，未知的**演算法**或**金鑰型別**不會 —— 那會丟
`ParsecProtocolException`。

差別在於狀態碼是一個呼叫端可以自己檢視的數字，而演算法必須先變成模型裡的一個值，才能拿來做
任何事。「規格在這個用戶端寫完之後新增的演算法」在模型裡沒有位置可以放，而把未知的值折到最
接近的已知值，等於回報一個沒有人設定過的金鑰政策。老實說「我讀不懂」是傷害比較小的那個。

## 引數

引數錯誤丟的是平台定義的例外，不是 Parsec 的：名稱或演算法是 null 丟
`ArgumentNullException`，規格沒有定義的值丟 `ArgumentOutOfRangeException`。那些是呼叫端程式
碼的缺陷，而且它們從來不會抵達服務。
