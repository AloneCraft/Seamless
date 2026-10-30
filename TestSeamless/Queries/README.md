# 手書きの型付きクエリ

`query-definitions.json` に配列で定義します。`prepare` 時に DB 名を生成済み目録と照合し、指紋を解決して入口とクライアントを生成します。空配列は手書き定義なしを表します。ID とクライアントメソッド名は自動生成のものも含め一意にしてください。読み取り専用 DB に書き込み定義は登録できません。型名は名前空間を含む通常の非ジェネリック型名、クラス名とメソッド名は ASCII の C# 識別子です。

```json
[{"Id":"orders/by-id","Database":"Sales","ClassName":"TestSeamless.Queries.OrderQueries","MethodName":"Find","ArgumentsType":"TestSeamless.Queries.OrderArgs","ResultType":"TestSeamless.Queries.OrderRow","ClientMethodName":"FindOrdersAsync","IsReadOnly":true}]
```

対応する `Queries` 配下の通常 C# ソースに、引数・結果の MemoryPack 対応型と、引数なしで構築可能な非 static sealed クラスを用意してください。生成される DB 型名は対象 generation の `GeneratedName` に従います。

```csharp
public sealed class OrderQueries{
    public IEnumerable<OrderRow> Find(global::GeneratedSales.GeneratedSales database,OrderArgs args,CancellationToken cancellationToken){
        // database のテーブルから必要な行を返す。
        throw new NotImplementedException();
    }
}
```

メソッド本体は通常の C# として FLinq ジェネレーターに渡されます。生成クライアントは `IAsyncEnumerable<TResult>` を返し、同じモジュール入口が direct と remote で使われます。定義を変更したら generation の prepare・ビルド・module 配置をやり直してください。実行時に DLL を送信する仕組みではありません。

比較する定義には `ComparisonArgumentsJson`（引数の JSON 表現を収めた文字列）と `ComparisonColumns`（結果オブジェクトのプロパティ名を SQL 列順に並べた配列）を両方指定します。結果自体が単一列の場合は `"$value"` を指定します。生成モジュールの `CreateComparisonArguments` と `ProjectComparisonRow` はそれぞれ MemoryPack の引数と比較用の列配列を返します。

DB 型名が generation ごとに決まる場合は `SourceFile` に Queries 内の `.cs.template` ファイル名を指定し、型名を `{{DatabaseType}}`、生成された型の名前空間を `{{DatabaseNamespace}}` と記述できます。prepare 時に型名を埋めた通常 C# を generation の queries 配下へ保存してコンパイルします。付属の `AggregateJoin.cs.template` と定義は共通 SQL ケースに対応します。
