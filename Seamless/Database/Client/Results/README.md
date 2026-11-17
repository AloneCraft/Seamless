# Seamless.Database.Client.Results

Native クエリの結果を受信順で保持します。正常完了後に接続を閉じても、結果ハンドルの `ReadRows()` で先頭から読み直せます。結果ハンドルを破棄すると、メモリ予約と一時ファイルを解放します。

```csharp
using Seamless.Database.Client;
using Seamless.Database.Client.Results;

var options = new NativeResultOptions(tempRoot, 1 << 20, 16 << 20, 128 << 10);
NativeResult<string> result;
await using (var client = new NativeQueryClient(host, port))
{
    result = await client.QueryBufferedAsync<int, string>(
        moduleId, queryId, database, schemaFingerprint, argument, options);
}
using (result)
{
    foreach (var row in result.ReadRows())
        Consume(row);
}
```

小さい結果は分割メモリに保持し、しきい値を超えると受信済みの行を一時ファイルへ順に移します。複数の結果に共通の容量上限を適用する場合は、同じ `ResourceBudget` を `NativeResultOptions.ParentBudget` に設定します。ファイルの再生時に 64 KiB を超える行を読む場合は、行を復元するための追加領域もメモリ予算から確保します。

切り替え時にファイル用バッファの余裕が足りない場合は、保持済みのチャンクを先にファイルへ書き出してからバッファを確保します。しきい値を `0` にすると最初の行から一時ファイルを使います。

## 生成クライアントの集合結果

生成クライアントの集合メソッドは `NativeResultStream<T>` を返します。`await using` で所有し、一度だけ列挙します。最初の `MoveNextAsync` で要求を開始し、途中の解放は実行中の読取とサーバー側の後始末を待ちます。

```csharp
await using var rows=queries.ListAsync(argument,cancellationToken);
await foreach(var row in rows.ConfigureAwait(false)){
    Consume(row);
}
```

繰り返し読む場合は、`ToResultAsync` で受信順の独立コピーを作ります。受信した行のバイト列を既存の結果ストアへ格納し、復元・再シリアライズを挟まずにメモリ保持とディスク退避を行います。以下の `queries` は生成されたクライアントです。

```csharp
await using var rows=queries.ListAsync(argument,cancellationToken);
using var result=await rows.ToResultAsync(options,cancellationToken);
foreach(var row in result.ReadRows()){
    Consume(row);
}
```

通常列挙と実体化は同じ一回の列挙権を使います。結果コピーは接続を閉じた後も使えます。`QueryRecordsAsync` と既存の `QueryRecordsBufferedAsync` も利用できます。

リトルエンディアン環境では、`QueryBufferedAsync` で `int`、`long`、`string` を再生できます。スキーマ生成器が `INativeReplayDecoder<T>` を付けた行型には `QueryGeneratedBufferedAsync` を使います。生成対象は、対応するスカラー、同じ生成単位の入れ子の行、および対応する要素の一次元配列です。配列の配列、nullable値型の配列、固定長の生成行の配列、循環する形は生成対象外です。

`INativeReplayDecoder<T>` を手書きで実装して呼び出す場合は、検査済みの Native 行を同じ値に復元し、呼び出しの副作用や外部状態への依存を持たせない契約です。
