# ビューの行型をDB生成器で作る書き方への移行

## 背景

ビューの結果列は処理本体の戻りから決める（合意済み）。現行実装はこれを、Managerが利用者のC#ソースへ名前付き行型の列と生成投影を書き込み、`/* Seamless generated view projection */`マーカーで自分の生成物を識別する方式で実現した（[ビューの結果列同期の実装](20261110-view-query-integration.md#ビューの結果列同期の実装)）。

2026年11月11日、利用者は次の方針を示した。

- ビューは`CreateView(()=>{...},"名前")`のようなラッパーではなく、`Query`の本体に問い合わせを直接書く。
- 行型は生成器で作る方式にする。

## 現状の観察

- `Shared/Database.Generation/DatabaseContainerGeneration.cs`の`CollectDeclaredObjects`は、`<ルート>.Views[.スキーマ]`名前空間の最上位structをビューとして集め、`db.X`（スキーマは`db.S.X`）を`ISet<行型> X=>行型.Query(this)`として生成する。行型と`Query`の置き場所は同じstructである。
- 現行の`Query`は`container.CreateViewDefinition<TSource,TResult>(definition,projection,name)`を返し、遅延評価・投影後の重複除去・子実行scope・循環検出は`Seamless.Database.View`側にある。
- FLinqは生成getterから静的`Query`を認識し、`definition`のラムダ本体を呼出元へ展開する（`FLinqIncrementalGenerator.ViewDefinitions.cs`）。
- Managerは`DatabaseDesigner.ViewResults.cs`で匿名型の列を読み、record struct宣言と生成投影を書き換える。IDEでの本文編集では列が更新されない。

## 過去設計から引き継ぐ仕様

- ビューは名前付き問い合わせで、呼出元へ展開してFLinqで最適化する。マテリアライズドビューは範囲外。
- 列挙のたびに定義を評価する遅延評価、子実行scope、循環検出、展開状態と未展開理由の計画への記録、赤太字の未対応通知。
- DB直下と名前付きスキーマの同名ビューの区別（`db.X`と`db.dbo0.X`）。StableId・属性・コメントの保持。
- 直接ラムダだけをインライン化し、Anchorの副作用順を保護する。
- 重複除去（集合）の扱いは現行契約を維持する。変更は未決事項とする。

## 目的

- 利用者が書くのは問い合わせ本文だけにする。行型の列と、匿名型から行型への変換は生成器が`.g.cs`に出す。
- IDEで編集してもManagerで編集しても、ビルドのたびに列が本文に追従する。
- Managerは利用者ソースへ生成投影・マーカーを書き込まない。

## 非目的

- 取引snapshotへの束縛（別の未実装事項として残す）。
- 集合／多重集合の意味変更。
- 関数（TableFunction）の同様の書き方。ビューで成立してから同じ形を適用する。
- 既存DBの保存済みソースの自動変換。

## 提案設計

### 利用者が書く形

```csharp
namespace UserCode.Views;          // スキーマは UserCode.Views.<スキーマ>

public readonly partial record struct Summary{
    public static IEnumerable<object> Query(ManagedDb db)=>
        db.Items.Where(item=>item.Active).Select(item=>new{item.Id,Title=item.Name});
}
```

- 行型`Summary`はpartialとし、列（位置引数）を利用者は書かない。
- `Query`の本体に問い合わせを直接書く。結果が匿名型のため、戻り値は`IEnumerable<object>`と書く。匿名型は参照型なので変換はそのままコンパイルできる。
- 行型を利用者が自分で書く場合は、従来どおり位置引数を宣言し、`IEnumerable<Summary>`（または`ISet<Summary>`）を返す。この場合、生成器は列を作らない。
- 空のビューは`=>[];`と書ける。

### 生成器が出す形

```csharp
namespace UserCode.Views;
public readonly partial record struct Summary(int Id,string Title){
    internal static IEnumerable<Summary> Rows(ManagedDb db)=>
        db.Items.Where(item=>item.Active).Select(item=>new Summary(item.Id,item.Name));
}
partial class ManagedDb{
    public ISet<Views.Summary> Summary=>View.FromRows(this,Views.Summary.Rows,"Summary");
}
```

- 列は`Query`本体の式の型（意味解析で得る実際の要素型）が匿名型のとき、そのプロパティ名・型・順序から作る。
- `Rows`は利用者の本体を写し、結果要素となる匿名型の生成式だけを`new 行型(...)`へ置き換える。生成部分は同じ型のpartial・同じ名前空間に置き、利用者ファイルのusingを写すので、名前解決とprivateアクセスは元の本体と同じになる。中間の匿名型は置き換えない。
- getterは`Rows`を列挙のたびに評価し、現行Viewと同じ遅延評価・重複除去・子scope・循環検出を適用する。`Query`は解析用の定義として残り、実行時には呼ばない。
- FLinqは生成getterから`Rows`を認識し、その本体を呼出元へ展開する。

### 推論できない形

宣言型に依存する空集合（`IEnumerable<object>`で`[]`を返し列が不明）、異なる匿名形状の分岐、`object`以外の型が混ざる場合、匿名型以外の`object`を返す場合は、生成器が診断を出し、行型の列を作らない。Managerは診断を赤太字で表示する。

### 現行形式との関係

- `CreateViewDefinition`／`CreateView`と生成投影マーカーの既存形式は引き続き読める。Managerの新規ビューは新形式で作る。既存ビューの新形式への変換は、利用者が明示的に操作したときだけ行う。
- 新形式では、Managerの列同期（`SynchronizeViewResult`）は不要になる。構造画面は生成器の結果を読取専用で表示する。

## 影響範囲

- `Shared/Database.Generation`（DB生成器と解析用コンパイル）
- `Seamless.Database`（`View.FromRows`の入口、既存Viewの契約の共有）
- `Seamless.FLinq.Generator`（生成getterと`Rows`の認識、展開）
- `Seamless.Management`（新規テンプレート、本文読み書き、構造表示、計画のfactory認識）
- `Seamless.Management.Components`（構造画面の表示と診断）

## 移行手順

1. 生成器で、partial行型の列と`Rows`、getterを生成する。代表入力でコンパイル・実行値を確認する。
2. ランタイム入口と既存View契約（遅延・重複除去・scope・循環）を接続する。
3. FLinqの展開を`Rows`へ接続し、未展開・展開済みの計画と値・副作用順を照合する。
4. Managerの新規作成・本文編集・構造表示を新形式へ切り替える。旧形式の読み取りは残す。
5. 正式候補の両OS受入と固定URLへの反映。

## テスト計画

- 生成器：DB直下と複数スキーマの同名ビュー、匿名列の名前・型・順序、nullable参照型、中間匿名型の保持、usingエイリアス・private helper、利用者が列を宣言した場合の非生成、推論できない形の診断。
- ランタイム：遅延評価、重複除去、途中Dispose、循環検出、予算返却。
- FLinq：展開の有無と値・副作用順・例外の一致。
- Manager：新規作成、本文編集の反復、IDE側編集後の列追従、旧形式の読み取り。

## リスクと対策

- 本体の写しの名前解決：同じ型のpartial・同名前空間・usingの写しで解決を揃える。写した本体を生成器内で再束縛し、診断が出たら生成を止めて元の本文位置へ報告する。
- 中間匿名型との区別：意味解析で、返す列の要素型と同一の匿名型の生成式だけを置き換える。
- 生成型が利用者の`Query`と同じ型にあること：利用者側に位置引数がある場合は生成しない規則で衝突を避ける。

## 利用者の決定（2026年11月11日）

- `CreateViewDefinition`／`CreateView`は削除し、旧形式との互換は持たない。生成コードが使う実行入口は専用のものへ置き換える。上記「現行形式との関係」の互換方針はこの決定で置き換えた。
- ビューの結果は重複を除いた集合として扱う。ただし呼出元へ展開した中間結果では、意味を変えない範囲で最適化が重複除去を省く場合がある（後段が`Any`・`Distinct`など）。
- 関数（TableFunction・ScalarFunction）にも同じ書き方を適用する。

## 未決事項

- [ ] 取引snapshotへの束縛を今回の範囲に含めるか。案は、生成器が本体の引数型を取引型へ替えた写しも生成し、`read.Summary`で取引の時点に揃えること。表以外のメンバーを使う本体は理由付きで生成しない。

## 実装の進捗（2026年11月11日）

- 生成器：`Shared/Database.Generation/DatabaseViewRowGeneration.cs`。`Query`が`IEnumerable<object>`なら匿名型から列とRowsを生成し、`IEnumerable<行型>`なら手書きの列のままRowsを生成する。式本体・ブロック本体（入れ子の関数を除く全return）・`throw`式に対応。推論できない形は診断`SDB004`。`db.X`（スキーマは`db.S.X`）はRowsを呼び、FLinqはRowsを展開対象として認識する。Rowsの実装は現時点では`CreateViewDefinition`を呼ぶ。
- Manager：新規ビューは`partial record struct`に`Query`を直接書くテンプレートで作る。本文の変更後、結果が匿名型なら列の宣言を外して戻り値を`IEnumerable<object>`へ、行型か空なら手書きの列の形へ揃える。生成投影とマーカーの処理は削除した。構造画面は推論した列を読取専用で表示する。
- 近接試験：リポジトリのManagement.Tests（出力は`F:/Seamless/view-generated-row-20261111/artifacts`）で102件中101件成功（`results-r6`）、試験期待値の修正後に残り1件も成功（`results-r7`）。失敗1件`ViewsAndFunctionsCompileAndExecuteAcrossMixedSchemas`は関数`Amount`・`Find`のFLINQ002で、製品DLLを正式r5（本変更前）へ差し替えた同じ試験DLLでも同一に失敗するため既存の失敗（`run-ab`、`results-ab`）。
- 旧形式（`CreateView`／`ISet`を返すQuery／生成投影マーカー）を入力にしていた試験は、互換を持たない決定に従い新しい形へ書き換えた。ビューのラムダ属性・名前引数の評価順を守る試験2件は前提がなくなったため外した（関数の同試験は維持）。
- ランタイム：`CreateViewDefinition`（2オーバーロード）と名前単位キャッシュの`CreateView`を削除し、生成コード専用の`GeneratedView<T>(rows,name)`（EditorBrowsable Never）へ置き換えた。生成器のRowsとFLinqの認識（`GeneratedView`・Arity1だけ）、Managerの計画の認識もこれへ揃え、投影オーバーロード専用のFLinq経路（`TryBuildProjectedViewBody`ほか）を削除した。生成器は手書きの列をプロパティ・コンストラクターで書いた行型も受け付ける。
- コンテナのメソッドで宣言する`View<T> X()=>this.View(()=>...)`の形はサンプル・既存試験が使っているため残し、中身を名前キャッシュなしの`GeneratedView`へ差し替えた（同名で最初の定義を共有する旧挙動はなくなった）。この形を今後も残すかは利用者に確認する。
- DB取引試験：宣言を新しい形へ移し、旧キャッシュ・投影オーバーロード・classに置いたQueryの各観点を外した。投影オーバーロード専用の`DatabaseViewProjectionTests.cs`と`DatabaseViewProjectionRuntimeTests.cs`（未追跡）は`F:/Seamless/view-generated-row-20261111/removed-tests`へ退避した。F側出力でビルドし、ビュー関連25件（`results-tx2`）と宣言カタログ・initializer・Join・private helper6件（`results-tx3`）が成功。
- MemoryPack：`Seamless.Database.Query`、`Seamless.TestKit`、DB取引試験の`1.*`も1.21.4へ固定した。共有キャッシュの1.22.0が試験出力に入り、検証済み1.21.4を要求するNativeワイヤ検査が失敗していた（PublishedMetadataOperations）。固定後に成功（`results-r10`）。
- Manager近接は107件中105件成功（`results-r8`）。残る失敗は既存の関数FLINQ002と、上記MemoryPackの1件（固定後に成功）。
- 利用者の決定（同日）：コンテナのメソッドで書く`View<T> X()=>this.View(()=>...)`の形も廃止し、ビューの書き方を一つにする。ビューはプロパティ（`db.X`）、関数はメソッド（`db.F(引数)`）で実装する。保護メソッド`View()`を削除し、DB生成器・Manager設計器・Server目録は、コンテナのメンバーとして書いた`View<T>`（メソッド・プロパティ）と利用者が書く`ISet<T>`プロパティをビューとして数えない（生成getterの`ISet<T>`と`MaterializedView`は従来どおり）。サンプル（Sample.NetShopの`在庫切れ商品`と章のコード例）、AdventureWorks・TPC-H・SpecDatabase・生成器・Manager目録の試験を新しい形へ移し、旧キャッシュによる同一インスタンスの確認は外した。取引を引数に取る境界試験のViewは関数の形にあたるため、ランタイムの`GeneratedView`を直接使って集合境界だけを確かめる。5プロジェクトのRelease buildは警告0・エラー0、関連147件中146件成功（`F:/Seamless/view-generated-row-20261111/results-viewform2`）。失敗1件は既存の関数FLINQ002。
- 未了：関数への同じ書き方の適用（生成するのはメソッド）、取引snapshotへの束縛（未決）、正式候補・固定URLへの反映。

## 置き換え（2026年11月12日）

利用者はビュー・関数で匿名型を返すことを禁止し、結果は名前付きの行型（`Views`・`Functions`名前空間）を利用者が宣言し、Managerがツリーで列を編集する形に決めた。本体は入口の共通インターフェースへのC#14拡張メンバーで書く。本書の「生成器が匿名型から行型を作る」方式は終了し、後継は[20261112-0017-database-access-model.md](20261112-0017-database-access-model.md)。実装済みの生成器・Managerの該当部分は後継設計の移行手順で削除する。

## 合意状態

利用者は「ビューの行型を生成器で作る方式」と「Queryに直接書く」方向を指示した。上記の具体形（partial行型・`IEnumerable<object>`・生成`Rows`）は本書の提案であり、試作で成立を確認してから確定する。
