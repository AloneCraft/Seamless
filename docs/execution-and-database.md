# Execution、LINQ、Databaseの現在仕様

## ExecutionScope

`Seamless.Execution` はUIや特定のストレージ実装に依存しない低位契約です。スコープ内の演算子、Codec用バッファ、索引、一時ファイル、DB変更集合は、同じ `ResourceBudget` から予約します。

```csharp
using Seamless.Execution;
using Seamless.Linq;

await using var scope = new ExecutionScope(new ExecutionOptions {
    MemoryLimitBytes = 256L * 1024 * 1024,
    TemporaryStorageLimitBytes = 16L * 1024 * 1024 * 1024,
    TemporaryDirectory = @"D:\SeamlessWork",
    CancellationToken = cancellationToken,
});

var query =
    from row in scope.Query(source)
    where row.Enabled
    orderby row.Category, row.Sequence
    select row;

using var stored = query.ToStorage();
foreach(var row in stored) {
    Consume(row);
}
```

既定値は管理対象メモリ256 MiB、一時領域16 GiBです。この値はCLR全体や呼び出し元が保持する入力を含むプロセス上限ではありません。検証基盤はプロセスのpeak private bytesも別に記録します。

一つのスコープで同時に複数の列挙は行えません。並行実行には別のスコープを使います。遅延クエリ、`ToStorage()` の結果、`GroupBy` が返すグループはスコープ所有です。スコープ破棄後の列挙は `ObjectDisposedException` になります。

`ToList()` と `ToArray()` は全件を管理対象メモリへ実体化する操作です。結果が上限を超える場合は失敗します。大きな結果は `ToStorage()` でスコープ所有の逐次ストレージへ保存します。

失敗は次のように区別します。

- `CodecUnavailableException`: 決定的な保存表現を作れない
- `ResourceLimitException` / `Memory`: 管理対象メモリの合計上限を超えた
- `ResourceLimitException` / `TemporaryStorage`: 一時領域の合計上限を超えた
- `ResourceLimitException` / `SingleElement`: 単一の符号化値だけでメモリ上限を超えた
- `OperationCanceledException`: 指定されたキャンセルが成立した

一時ディレクトリには所有メタデータと排他ロックを置きます。`TemporaryResourceRecovery.RecoverAbandoned` は正しい形式・IDを持ち、ロックを取得できる残骸だけを回収します。壊れた、または所有確認できないディレクトリは削除しません。

## 等値性とCodec

`scope.Query(...)` の `GroupBy`、`Join`、`Distinct`、`Union`、`Intersect`、`Except` は、比較子を省略した場合に決定的な符号化バイトの完全一致を使用します。ハッシュは索引候補の絞り込みにだけ使い、衝突時はバイト列を照合します。比較子を明示した場合はその `IEqualityComparer<T>` が優先されます。

Codecの表現には形式版と型スキーマのSHA-256を含めます。整数はlittle endian、`float`／`double` はビット表現、`decimal` は4整数、文字列はUTF-16コード単位で決定的に符号化します。構造体はフィールド単位で処理し、未定義パディングを含めません。匿名型はプロパティ構造からCodecを作ります。一般クラスは明示Codecが必要です。

```csharp
scope.Codecs.Register(new Seamless.Execution.Codecs.Adapter<MyRecord, Seamless.Execution.Codecs.Serializer<MyRecord, MySerializer>>(new(
    "my-record-v2")));
```

`Codecs.Serializer<T,TSerializer>` は既存の `Seamless.Collections.Serializers.IStrategy<T>` を適合させます。`FormatId` は読み書き互換性が変わるたびに更新してください。スコープが最初のCodecを解決した時点でレジストリ全体が固定され、それ以後は登録内容を変更できません。必要なCodecはクエリ実行前に登録します。

スコープ外でFLinqが `System.Linq` を最適化する場合は、元の.NET等値性を維持します。たとえば.NETでは `+0.0f` と `-0.0f` は既定比較で等しい一方、スコープ付きクエリではビット表現が異なるため別キーです。この2種類の意味論は検証結果の `Semantics` に別の値で記録します。

順序比較は等値性と分離し、`IComparer<T>` を使います。外部ソートは元位置を保持して安定性を維持します。グループはキーの初出順と要素順、Joinは外側の順序と一致する内側要素の順序を維持します。

## 大規模LINQ

ソートは予算内のrunを作成してペア単位で外部マージするため、同時に開くrun数を制限します。GroupByと集合演算は退避可能なバイト索引と要素列を使います。Joinは内側を退避可能な索引へ格納し、外側を逐次列挙します。一つの巨大グループ、キー数の多い入力、同一キーが大量にあるJoinも全件を一つの`List<T>`へ入れません。

Codecを作れない値でも、`Where`や`Select`など退避不要の逐次演算は利用できます。退避が必要になった時点で暗黙に通常LINQへ切り替えず、理由付きで失敗します。

## Database

`DatabaseContainer` はDB全体で一つのExecution予算を所有します。各テーブルのTsavoriteメモリ予約、トランザクション変更集合、WAL符号化、一時集合演算を同じ予算へ計上します。SQL Server取り込みでは関係ごとの件数から `DatabaseContainerOptions.TableStorage` を作るため、最大テーブルの固定メモリを全テーブルへ重複配分しません。

`Table<T>`、`Table<TKey,T>`、キー列挙、トランザクション内の読み取りはストリーミングです。正本の件数は `long LongCount`／`GetLongCount()` です。`int Count` は範囲を超えると `OverflowException` になります。

読み取りセッション中は同じDBへのwriterが待機します。読み取りセッションを保持した実行コンテキスト自身がwriterへ昇格しようとした場合は、デッドロックさせず `InvalidOperationException` にします。writerと更新トランザクションは直列化します。

トランザクションの変更集合はメモリ閾値を超えると一時Tsavoriteストアへ退避します。キーなしテーブルの `Clear`、`IntersectWith`、`SymmetricExceptWith` も全件スナップショットを作らず、共有予算の一時ストレージを使います。内側Rollbackが外側トランザクション全体を中止する既存契約は維持しています。

ストレージDBのcommitは次の順です。

1. WALへbegin、逐次operation、commitを書き、ディスクへflushする
2. テーブルへ変更を反映する
3. Dispose時に変更済みテーブルをcheckpointする
4. WAL内の全対象テーブルがcheckpoint済みの場合だけ、WALをSHA-256名のarchiveへ保持してactive WALを空にする

再起動時はcommit記録のある操作だけを再生します。未commit末尾と途中で切れたJSON行は公開状態へ反映しません。WAL archiveに同じSHA名で異なる内容があればactive WALを保持したまま失敗します。まだ開かれていないテーブル向けのWALは、そのテーブルを復旧・checkpointするまでarchiveしません。

## SQL Server取り込み

`seamless database sqlserver import` は宛先へ直接書かず、`<destination>.staging.<guid>` に構築します。全生成関係を再度開き、SQL Serverから読んだ行数と `LongCount` が一致することを検証してから公開します。公開時に旧宛先があれば `<destination>.backup.<timestamp>.<guid>` へ移し、削除しません。

テスト用の生成コードは `Test/Database.Generated/<DB名>` に置きます。テストが型として使うのは今は `AdventureWorks2017` だけなので、ほかのサンプルDBの生成コードはコミットしていません。新しいサンプルDBをテストで使うときは、`seamless database sqlserver prepare --project Test/Test.csproj --connection <接続名> --sample <DB名> --data <データ置き場>` でSQL Serverへ用意し、`seamless database sqlserver generate --project Test/Test.csproj --connection <接続名> --database <DB名>` で生成します。DB名の一覧は `seamless database sqlserver samples` で確認できます。

新しい宛先の `seamless-import.json` にはソースDB、生成コンテナ、スキーマ指紋、UTC時刻、テーブル別行数、テーブル別ストレージ設定を保存します。未対応SQL構文・型は `NotSupportedException` と対象の構文名／SQL型名をCLI診断へ出し、別の意味へ暗黙変換しません。

## 旧データからの移行

保存形式と検証DBは新規開始です。旧DBを新形式へ自動変換しません。

1. 旧アプリとwriterを停止する
2. 旧ストレージ、旧SQLite、関連成果物を読み取り専用バックアップへコピーする
3. `seamless-verify init` またはSQL Server importを明示的に実行する
4. マニフェスト、行数、代表クエリ、再起動後の読み取りを確認する
5. 問題があれば新しい宛先を破棄し、保管した旧データへ戻る

アプリ起動、スキーマ版の増加、自動削除を移行手段にしません。
