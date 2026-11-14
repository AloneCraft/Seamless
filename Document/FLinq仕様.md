# FLinq Source Generator

[`Seamless.FLinq.Generator`](../Seamless.FLinq.Generator/) は `System.Linq.Enumerable` 呼び出しを解析し、入力ファイル単位の interceptor ソースを出力する Roslyn incremental generator です。生成コードは [`Seamless.Linq`](../Seamless.Linq/) の実行時補助を使います。

## 評価の契約

- 結果・順序・例外を `System.Linq` に合わせ、`System.Linq` が評価しない要素に利用者のラムダを適用しません。
- `.Anchor` を含むラムダの評価回数を維持します。
- 呼び出し時の中身が確定している関数は、本体をその場へ展開します:直接渡したラムダとdelegate構文(キャストで包んだものを含む)、代入が宣言時の1回だけの変数、メソッドグループ(本体が式1つで同じ型にあり、仮想・総称でないもの)、初期化式だけで決まる`readonly`のフィールド。再代入のある変数、`readonly`でないフィールド、匿名型を返すもの、外側の可変ローカルへ書くもの、外側のlocal functionを呼ぶものは、呼び出し時に選ばれたデリゲートを保持します。デリゲートのまま保持する場合でも、そのラムダ・delegate構文の本体の中にあるLINQは展開します。LINQの引数ではないラムダ(変数へ入れるラムダ、ほかのメソッドへ渡すラムダ)の本体のLINQも展開します。
- 展開を止めたいラムダ・メソッド・フィールドには`[LinqIntercept(false)]`を付けます。フィールドの属性は、フィールド・型・アセンブリの順に探します。展開するかどうかを書き方(キャストなど)では決めません。
- 名前を付けて書いたメンバー(拡張ブロックのプロパティ・メソッド、従来の拡張メソッド、同じ型のメンバー)で、本体が式1つで中にLINQがあるものは、本体を呼び出し側の式へ取り込み、呼び出し側のLINQと1つのループにします。受け手と引数が、定数・`this`・書き換えられない変数や引数・`readonly`のフィールドのときだけ取り込みます(評価の回数と順序が変わらない)。取り込んだ結果がコンパイルできない、または呼び出し先が変わる(usingの違いなど)ときは取り込みません。DBのビュー・関数(生成される`IReader`への拡張メンバー)を取り込めないときは、実行時へ回さずFLINQ007のコンパイルエラーにします。本体は呼び出し側に置くので、本体が使う補助は呼び出し側から見える(`internal`以上の)ものにします(`private`ならFLINQ007で知らせます)。static クラスのメンバーは書き換えの対象外なので、LINQを使う補助を static クラスに置くと、その補助は展開されません。
- `Seamless.Linq.Set`の呼び出し(`ISet`を受けて`ISet`を返す`Where`・`Select`など)は、`IEnumerable<T>`への変換を挟んで`System.Linq`の同じ演算へ下げてから展開します。`Select`・`SelectMany`・`Join`は結果の重複を除くので、下げた後に`Distinct`を足します(`Set.SelectMany`の集合を返すラムダの中では、外側が除くので足しません)。結果を`ISet`のまま使う箇所(`Count`プロパティ、`ISet`の変数への代入など)は下げません。
- デリゲートを保持する理由は、採用した実行計画から生成コードのコメントへ出力します。外側の可変ローカルを変更する直接ラムダは、元のクロージャセルを共有する既存経路を使います。
- 並べ替え、`Reverse`、`Distinct` を省略できるかは、下流が必要とする順序・重複と、途中停止までに評価するラムダを確認して判断します。

下流が必要とする範囲の解析と `GroupBy` 融合の設計、検証結果は [Argon統合仕様書](20261114Argon.md) を参照してください。

## ジェネリック含有型における `[UnsafeAccessor]` の制限

- `.NET 10` ランタイムでは、ジェネリック含有型（例: `Base<T>` や `Methods1<T>`）の非 public メンバーに対する `[UnsafeAccessor]` 解決がランタイム制限により失敗します。
- そのため `Seamless.FLinq.Generator`（`FLinqIncrementalGenerator.UnsafeAccessor.cs`）では、アクセサ署名に型パラメータが含まれる非 public メンバーへの `UnsafeAccessor` 生成を拒否します。
- ジェネリック型内のメンバーや補助メソッドを FLinq のインターセプト・インライン展開対象にする場合は、対象メンバーの可視性を `internal` 以上にして `UnsafeAccessor` を介さず直接参照させてください。

## Registry中心設計

演算子の選択・適用・能力判定は `OperatorRegistry` に集約しています。登録内容、生成サービス、Render 契約の詳細は [FLinq レジストリ内部設計](architecture/FLinqレジストリ内部設計.md) を参照してください。

## Coverageと診断

公開 `System.Linq.Enumerable` API 名は Registry の Coverage 一覧と照合します。実装対象外は `OperatorCoverageKind.Excluded` で明示します。重複 Key、Priority 競合、Factory 型、Coverage、ProviderNative プロファイルの整合性は Generator 初期化時に検証し、不整合時は `FLINQ006` を報告します。

## 配布互換性

Generator アセンブリは `netstandard2.0`、Roslyn 依存は `Microsoft.CodeAnalysis.CSharp` と `Microsoft.CodeAnalysis.Analyzers` の 5.6.0 です。生成コードのランタイムは `Seamless.Linq` の `net10.0` を前提とします。FLinq NuGet は同じバージョンの `Seamless.Linq` パッケージに依存し、`Seamless.Linq.dll` を同梱しません。

## 検証

- [LinqFragmentCoverageTests](../Benchmark.Dynamic.Tests/Migration/SourceGenerator/LinqFragmentCoverageTests.cs) で公開 API の Coverage、Key、Priority、Factory 型、Capability を確認します。
- [IncrementalCacheTests](../Benchmark.Dynamic.Tests/Migration/SourceGenerator/IncrementalCacheTests.cs) で入力ファイル単位のキャッシュ分離を確認します。
- [Benchmark.System.Linq.Enumerable.FLinq](../Benchmark/Benchmark.System.Linq.Enumerable.FLinq/) で Release の Benchmark-as-test を実行します。Dynamic の再現手順と過去の不具合修正の教訓は [テスト責務とDynamic回帰.md](テスト責務とDynamic回帰.md) および [Argon統合仕様書](20261114Argon.md) を参照してください。
- `dotnet pack Seamless.FLinq.Generator/Seamless.FLinq.Generator.csproj -c Release` で作成したパッケージの analyzer TFM、依存、ランタイム DLL の重複を確認します。
