# ビュー参照のクエリ統合とキーJoinの確認境界

## 合意済みの方向

通常ビューと関数は名前付きクエリ定義として呼出元へ統合し、FLinqで最適化する。引数の評価回数、副作用と例外の順序、既存の集合契約と読取りsnapshotを保持する。マテリアライズドビューは将来の検討範囲とする。

## ビュー定義・専用型・最適化の確定仕様

2026年11月11日の追加合意として、以降の実装判断と受入は本節を基準にする。後続の実装・検証節は各候補で確認した形状と制約を示す。

### 名前付き定義の呼出元への展開

ビューは問い合わせの定義に名前を付けたものである。C#からは、そのContainerに属するインスタンスプロパティとして`container.ItemList`のように参照する。生成器はビューの元定義を呼出元の問い合わせへ接続し、全体を解析・最適化する。結果の保存・キャッシュ・事前計算は通常ビューの要求に含まれない。

例えば、`ItemList`の定義を次の問い合わせとする。この例の匿名型は論理的な結果の形状を表し、公開行型への対応は後述する。

```csharp
from item in container.Items
select new { item.Id, Twice = item.Value * 2 }
```

呼出元は次の形で利用する。

```csharp
from view in container.ItemList
where view.Id == id
select view.Twice
```

定義展開後の解析対象は、意味上次の形となる。

```csharp
from view in (
    from item in container.Items
    select new { item.Id, Twice = item.Value * 2 }
)
where view.Id == id
select view.Twice
```

外側の`Id`条件と元テーブルの列・キーの対応を解析し、契約を保つ実行方法を選ぶ。ビューから別のビューを参照する場合も、定義と参照先を追跡する。

### ビュー専用型の役割

| 役割 | 責務と最適化上の扱い |
| --- | --- |
| 結果の一行を表す公開型。例は`UserCode.Views.ItemList` | 列名・列型・公開メタデータ・C#利用契約を表す。定義本体の戻りから列構造を決定する。内部の匿名型や公開行型への投影を通して、元の列・キーとの対応を保持する。 |
| 列挙・実行を担う型。現行の`View<T>`や集合インターフェース等 | 非展開経路の実行、集合の意味、遅延評価、資源の寿命を支える。展開経路では必要性を解析し、実呼出し・delegate・中間オブジェクトの生成を削減する。 |

専用型の追加・維持・変更は、公開API、定義識別、非展開時の実行、集合／シーケンス契約、最適化後の実行それぞれの必要性から判断する。公開行型の存在と、結果の物理的な格納方法は別々に決める。最適化の可否は、実際の演算と観測可能な意味に基づいて判断する。

現行の静的`Query(container)`は、生成器が元定義を発見する入口として扱う。利用者向けのアクセスはContainerのインスタンスプロパティへ接続する。最適化後に残す`Query`・factory呼出し、delegateや`View`の生成は必要性を解析して選択する。

### 問い合わせ全体の最適化

ビュー本体と呼出元を合わせ、次を検討する。

- 外側の条件を内側へ移動し、複数の射影を合成する。
- 元テーブルの主キー・索引と列の由来を追跡し、キー検索を選択する。
- 必要な列だけを取得・計算し、中間列挙・オブジェクト・コピーを削減する。
- 複数ビューをまたぐ関係代数とBoolean条件を書き換える。
- 複数Joinの結合順序と物理的な実行方法を選択する。
- 一意性・比較の意味から、物理的な重複除去の必要性を判定する。

`ISet`として扱うときは関係代数の契約、`IEnumerable`として扱うときは通常LINQのシーケンス契約に従う。キャスト後の契約も追跡する。集合の意味は維持し、重複除去の実処理はキー・射影の一意性と比較の等価性の証明に基づいて選択する。条件移動・射影合成・重複除去の省略では、正規化、NULL、ユーザー定義比較、例外、評価回数も確認する。

副作用なしを通常の最適化の前提とする。Anchorがある処理は副作用の発生順を保護する。直接ラムダのインライン展開と、キャスト・変数・メソッド経由のdelegateの既存契約を維持する。各変換では必要な評価回数と例外条件、遅延実行、途中終了とDispose、取消、資源予算、取引と読取時点を保持する。

採用した変換と見送った変換の具体的な根拠を、展開コードのコメントと実行計画へ記録する。未証明の形状は理由と通常実行経路を示し、未実装・未検証の制約は利用者に分かる警告や大きな赤太字の案内へ接続する。

### 本文・結果列・生成投影の接続

Managerのビュー列名・型はC#／LINQ本体の戻りから決定し、結果列は読取専用で表示する。現在の`CreateViewDefinition<TSource,TResult>(definition,projection,name)`は、元本文と公開行型への生成投影を分離する接続である。反復編集でも元本文から型を推論し、公開行宣言・生成投影・対象の識別情報を同期する。

FLinqは両方の定義を解析して呼出元へ接続する。生成投影を越えて元テーブルの列・キーの由来を追跡し、投影合成・条件移動を拡張する。`ResultProjectionPushdownNotProven`等は、その候補での実装・証明範囲を示す。今後の対応範囲と受入は、前節の全体最適化の要求に照らして判断する。

関数も引数付きの名前付き処理定義として扱う。定義解析・型推論・展開・最適化は共通化できる基盤を利用する。C#アルゴリズムと副作用から展開可能性を判断し、引数の評価契約を保つ。Managerでは引数を編集し、本体の戻りから集合の列構造またはスカラーの単一型を決定する。

### 受入対象

元のC#実行とFLinq実行を比較し、対象・版・ソースに対応する生成コードと実行計画を照合する。

| 例 | 主な確認内容 |
| --- | --- |
| 単純なビュー参照 | 元定義が実際の列挙コードへ接続される。 |
| 外側からのキー列の等値検索 | 元テーブルのキー由来と、検索方法を採用・不採用とした根拠を確認する。 |
| 内外の射影と条件 | 射影合成、条件移動、必要列、例外・評価回数を比較する。 |
| 内外のJoinと複数ビューの結合 | 入力の由来、結合順序、物理方式、結果の契約を確認する。 |
| 別ビューの参照と同一定義の複数参照 | 定義の対応、必要な評価回数、再帰の診断を確認する。 |
| 重複が生じる射影と一意性を証明できる射影 | 集合結果、比較と正規化、物理的重複除去の採否を確認する。 |
| Anchor等で順序を保護する処理 | 値と副作用順、例外、途中終了、Disposeと予算返却を比較する。 |
| 展開できない処理 | 未展開の具体的な理由と通常実行の値・評価契約を確認する。 |

展開済みの判定には、実生成コードへの定義接続、適用可能な最適化の選択、実結果と必要な評価の意味の一致を必要とする。計画ノードの有無、コンパイル成功、結果の一致それぞれに対応する証拠を残す。各例の実装・確認状態は後続節と作業ツリーの`W20-VIEW-EXPANSION`で追跡する。

## 現在確認した事実

`Seamless.Database.View<T>`は列挙のたびに定義を評価し、子の実行スコープで`Distinct()`を通す。`DatabaseContainer.CreateView`は同名の初回定義を保持する。これらは現行実装の観測である。外部から観測できる遅延評価・集合結果・寿命を保ち、物理的な重複除去や境界の配置は本節より前の確定仕様に従って最適化する。

固定候補05では、直接表と`View`を内側へ置くJoinの結果80件・順序・外側キー80回は一致した。両方とも索引照合を生成していなかったため、この比較だけでビュー境界による最適化の差は判定できなかった。

既存の`PrimaryKeyJoinPreservesOrderAndEvaluatesOuterKeyOnceWhenSwitchingToLookup`と同一のソース・DEBUG条件を固定候補05へ与えると、実索引は利用可能でも索引照合の生成は0件で、元の`Run()`は`false`になった。失敗条件は内側が空のJoinだった。結果は0件でも外側キーが65回評価されていた。

現行の`PipelineTerminal`には既存の専用Joinレンダーとprovider補助型を登録する接続がなかった。同一ソースをFへ固定し、次の2箇所だけを接続する実験では既存PK試験が成功した。

- `GetDirectExecutionStatementCore`で既存`TryRenderTransactionalJoin`を使用する。
- `StructMethod`へ既存Outer／Inner sinkの補助メンバーを登録する。

レンダー呼出だけの実験ではprovider状態型が未定義となり、生成コードのコンパイルに失敗した。両方の接続を伴う候補では既存PK試験の索引照合2件、元`Run()==true`、別のTypedRows対照の索引照合1件を確認した。

## 副作用と例外の対照

70行を使い、64回の点照合からLookupへ移る境界を跨いで非展開のSystem.Linqと比較した。比較対象は結果、全イベント順序、例外メッセージ、Where・キー・結果の呼出回数である。

| 場面 | 未接続の候補 | 2箇所を接続した候補 |
| --- | --- | --- |
| 通常70行 | 一致 | 一致 |
| Whereの65回目で例外 | 一致 | 一致 |
| キーの65回目で例外 | 一致 | 一致 |
| 結果処理の65回目で例外 | 一致 | 一致 |
| 内側が空で、キーが呼ばれたら例外 | 不一致。不要なキー評価で例外 | 一致。キーを呼ばない |
| 外側の条件結果が空 | 一致 | 一致 |

Viewを通常メソッド経由で列挙する別の対照では、重複4件が2件へなり、各列挙で定義が1回だけ評価されることも接続前後で一致した。

## Viewとsnapshotの境界

SharedV2の明示読取取引と、rawな`db.Inner`を読むViewを混在させると、既存のsnapshot選択ガードが拒否する。Viewの定義も同じ`read.Inner.Rows`へ明示束縛した対照で次を確認した。

- 直接のTypedRowsとViewの結果80件・順序・外側キー80回が一致した。
- 直接のTypedRowsは索引照合を生成した。
- View側は定義呼出が残り、索引照合を生成しなかった。

この対照は1つの読取取引の寿命内で実行する。取引へ束縛したViewを取引終了後に再利用できることは検証していない。

## 現在の実装・検証範囲

上記2箇所を製品ソースへ反映し、新しい近接試験を`DatabaseViewBoundaryTests.cs`へ追加した。実Gソースからの生成器と試験のReleaseビルドは警告0・エラー0で成功し、新2件と既存PK試験の3件が成功した。

周辺を含む7件では3件成功・4件失敗だった。失敗した4件は通常DB provider、生成取引の2モード、async取引所有者の試験で、生成された補助コードの`Exception`／`InvalidOperationException`解決エラーとasync生成エラーを含む。同じ試験DLLへ未接続Control生成器だけを差し替えた別コピーでも、4件すべての失敗メッセージが完全一致した。これらは今回の接続前から存在する失敗として追跡し、7件全体を合格にはしない。

`Table.GetQueryRows()`の宣言キー対応はTypedRowsと同じ範囲には接続されていない。既存`CreateView`の初回定義キャッシュは維持する。

索引を使う一部の展開本文は取得できるが、実型解決CFGが`GeneratedCode.BindingError=CS0234`で`Unknown`となる例がある。原因の確定とCFGの修正は残る。実行成功からCFG取得成功を推定しない。

## 宣言View本体の展開の第1段階

11月11日に専用API`DatabaseContainer.CreateViewDefinition`と、生成getterから`Views`配下の静的`Query`を認識する限定経路を追加した。専用factoryは呼出しごとにViewを作り、定義の列挙は遅延させる。既存の`CreateView`は従来の名前単位キャッシュを維持する。旧ソースを自動変換しない。

対応形状はDB引数1個の`Query`が専用factoryを返し、その引数のparameterless lambdaが式または単一returnを持つ場合である。この形状判定は副作用が無いことの証明ではない。通常のpublic helperを呼ぶ代表入力で、元の呼出回数とイベント順を比較した。

生成器は内側の実列挙コードを呼出元の静的補助メソッドへ生成し、`ViewDefinitionRoot`から`ExpandDefinition`を通して実行する。元の定義delegateの直接列挙を、計画上の子ノード追加だけで展開済みと扱わない。Distinct・子実行scope・遅延開始はView境界に残す。外側Whereの限定的な条件移動は次節の範囲に実装した。Join条件の移動、点検索への置換、集合境界を除去する融合、関数の呼出元への展開は未実装である。

再帰のruntime guardはownerの参照・名前・元の定義delegateのメソッドで識別する。同名でも別定義メソッドの合法な入れ子を拒否せず、各MoveNextの間だけ再入を検査する。yield前と例外時に解除し、深さ64を上限とする。メソッド情報はViewの生成時に保存する。

実Gの3回帰と、同じ最新生成器を使った`GroupConditions`の1件がReleaseで4件成功した。Database・生成器・試験の各ビルドは警告0・エラー0。値、Distinct、遅延、receiver評価1回、通常helperのイベント順、legacy cache、同名別定義、再帰失敗後の解除、yield中の別列挙、終了後の親メモリ・一時領域予約0を確認した。代表入力の展開本文は`Available`だった。

Managerの新規View作成テンプレートと計画のfactory認識は専用APIへ接続した。CoreとSDKの後続確認は「ManagerテンプレートからSDKへの確認」節へ記録する。後続のWindows隔離library／GUI受入は末尾の確認節へ集約する。正式Linux再publishと固定URLへの配布は残る。Linux隔離実行の後続証拠は末尾へ記録した。生成getterを使う代表入力は生成器driverで確認済み。新しい生成ソースには専用APIを持つSDKとDBランタイムが必要で、今回固定URLの実行物は更新していない。

既存のlegacy factory、追加の文を持つ定義、再帰参照、明示TypedTransaction引数を持つ呼出元などは`OpaqueViewRoot`の未展開理由を記録する実装とした。後続のSDK／GUIとprivate static helperの確認範囲は各節へ集約する。診断の網羅試験、private instance helper／genericの境界は未検証。特に明示取引のsnapshotに束縛する新View入口は未実装である。

## 直接キー列の条件移動

外側の最初のWhereが、投影結果のキー列と同じ整数型のリテラルを組込み等値で比較する形に限り、条件を内側Selectの直前へ移す。元の表宣言のキーselectorと、結果recordのconstructor引数から得た直接列のsymbolを照合する。名前一致によるキー推定は行わない。前段のWhereとそのcallbackの順序は維持し、Distinct・子scope・遅延列挙は元のView境界に残す。

移動する投影はreadonlyな位置引数record structで、引数は整数・bool・charの直接列に限定する。全partial宣言を検査し、instance member、明示constructor／equality、静的初期化、object initializerなどがある形を採用しない。副作用がある通常helperを純粋とみなさず、元の集合境界で実行する。条件の捕捉変数、indexed lambda、非リテラル条件、合成キー、Join、明示snapshotはこの単位の対象外である。

実計画へ`PredicatePushdownStatus`・`PredicatePushdownReason`・`PushedKeyMember`を記録する。採用時の理由`DirectKeyFilterBeforeProjection;PointLookupNotSelected`は、宣言キーを照合したscan中の条件移動までを示す。物理点検索の選択は未実装である。未採用は理由付き`NotApplied`とし、未処理の形を最適化済みと表示しない。

修正前の代表入力では元の値・callback／例外は一致したが、生成TryGetNextに内側のキー条件が無く、新状態propertyの検査が失敗した。修正後は実生成コード内の「元Where callback→キー等値→new DirectRows」の順を確認した。明示constructorの例外`constructor3`と全callback順は元の実行と一致した。別partialのcustom GetHashCodeを持つ形は保守的に拒否し、値・入力callbackは一致した。現行Viewのcanonical符号化によるDistinctではそのcustom GetHashCodeは呼ばれず、hash例外が出るという途中の試験期待は誤りだった。

Windows Releaseの同一生成器で、製品の新1件と既存View3件・GroupConditions1件の計5件が成功した。新1件内で直接投影・明示constructor・別partialのcustom Hashの3形状を検査する。生成器・試験のビルドは警告0・エラー0。証拠は`F:/Seamless/view-boundary-probe-20261110/definition/pushdown`の`baseline-results/checks.trx`、`product-proof-r1`、`integrated-results-r5/checks.trx`と`summary.json`。SDK生成／取込・Linux・実Managerへの今回の条件移動の配布は未検証で、固定URLサービスは更新していない。

### initializer代入先と捕捉値の区別

`new InitializedRows(row.Id,row.Value){Id=2}`のinitializer左辺をinstance propertyの値参照と扱い、`this.`を補っていた。独立回帰を旧生成器SHA`219D1760`で実行し、CS1922・CS0747・CS0103を再現した。同じ名前の右辺を置換する文字列処理にも、左辺を保護する必要があった。

`RenderExpressionForGeneratedMethod`の識別子処理と共通置換で、object／with initializerのメンバー代入先を構文で識別して保護する。`{P=P}`の右辺は従来の意味解析に従い、外側のfield・property・parameterを参照する。巻上げ候補からinitializerの代入先を除き、indexerの`[Field]`全体をintの値として巻き上げる誤生成も除去した。indexer引数のFieldやcollection initializer内の値、通常代入先のinstance fieldはこの保護で除外しない。

新2件と既存View／条件移動／GroupConditionsの5件を同一生成器で実行し、Windows Release7件が成功した。生成器・試験ビルドは警告0・エラー0。Viewの列上書き後の結果、同名field／property／parameter、ネストinitializer、collection・indexer initializer、通常のinstance field代入と元実行の値を比較した。非public setter・Linux・SDK適用・実Manager受入はこの単位では未確認。initializerを含む投影の条件移動は引き続き拒否する。

同じ固定生成器0BE7へ追加のbinding回帰を1件だけ実行し、`new WithResult(row) with { Value=Value }`の代入先を保持し、右辺が外側instance propertyの13を読む結果[13,13]を元実行と照合した。Release1件成功、試験ビルド警告0・エラー0。生成器ソース・DLLは変更せず、実行コピーのSHA一致を確認した。追加証拠は同じinitializer保存先の`with-results-r1/checks.trx`と`with-summary.json`。

修正前の複合入力5件中4成功・1失敗は`definition/pushdown/integrated-results-r3/checks.trx`へ保持する。独立再現と修正後は`F:/Seamless/view-boundary-probe-20261110/definition/initializer`の`baseline-results/checks.trx`、`product-results-r3/checks.trx`、`product-proof-r3/binding-generated.cs.txt`、`summary.json`へ記録した。生成器SHAは`0BE7F79308E0E3C503E87DCC4391D79642CC8F58182E44380CB42A608EB47155`。実行コピーのSHA一致を確認し、SDK担当へ固定パスを渡した。固定Managerは変更していない。

## 宣言Viewからprivate static helperへの転送

宣言Viewのrecord struct内にある非genericなprivate static helperは、本文をインライン化せず既存のUnsafeAccessorで転送する。旧生成器0BE7では対象型を示す第一引数へ`PrivateRows?`を生成し、runtimeが`Nullable<PrivateRows>`からProjectメソッドを探していた。通常経路は値[10,20,30]・callback3回だが、生成経路はcallback0回でMissingMethodExceptionとなることを単独再現した。元helperが例外を投げる場合も、同じMissingMethodExceptionへ変わっていた。

private static呼出式とmethod groupの両経路で、含有型が値型の場合は第一引数を実型・実引数をdefaultとする。参照型の対象は従来のnullable注釈付き実型・nullを保持する。defaultはユーザーのconstructorを呼ばず、静的対象型を指定するためにだけ渡す。

helper宣言型struct／classとlambda／method groupの4形状で、実生成・コンパイル・値・callback全順序・元のInvalidOperationException:helper3を照合した。class対照は結果record structを維持し、helper宣言型だけclassにして明示getterを使う。初期のrecord classへの変更による自動getter不足・codec不足、対照fixtureの重複宣言は途中の失敗として保持し、helper転送の合格へ含めない。

新4件と既存View／initializer／条件移動／GroupConditions7件のWindows Release11件が成功し、生成器・試験ビルドは警告0・エラー0。新生成器は`8BA2DB436ABEC7323B85B069445763C3873666366CFE785F8997A41CB07A1968`、実行コピーのSHA一致を確認した。証拠は`F:/Seamless/view-boundary-probe-20261110/definition/private-helper`の`baseline-results/checks.trx`、`baseline-proof/private-helper-observation.json`、`product-results-r4/checks.trx`、`product-proof-r4`、`summary.json`。旧受入済み0BE7成果物、SDK／GUIのcohort、固定Managerは変更していない。

対象型とdummy引数の設定を、リポジトリの構文規約に従うif／elseへ揃えた最終生成器は`E0DD351AFD096FF0FFECFF7D48B4C94DD691E86B36703390598562A89A4B470B`。同じ11件を、新DBランタイム`FBAA279ABFF72E07F05F0BAAE15F63D61C7749C861CDF1B585D7412762DAB58C`と組み合わせて再実行し、Release11件成功・生成器／試験ビルド警告0・エラー0を確認した。両DLLの実行コピーSHAを照合し、旧候補を上書きせず`private-helper/syntax-final`へ固定した。同配下の`results/checks.trx`と`summary.json`が最終候補の証拠であり、SDK担当へ絶対パスとSHAを共有した。

同じ最終試験一式179ファイルを独立Linux出力へコピーし、全SHAを照合した。固有0700 TMPDIR・CPU0〜7・MemoryMax1600Mのscopeでbuildせず同じRelease DLLを実行し、11件が成功した。値・callback順・元例外と既存View／initializer／条件移動の回帰を確認し、終了後の専用scopeはinactive・残存PID0。証拠は`F:/Seamless/view-boundary-probe-20261110/definition/private-helper/syntax-linux-r1`の`input-sha256.json`、`results/checks.trx`、`execution-proof.json`、`exit-proof.json`。

genericなprivate helper・genericなView、関数展開、明示snapshot、private instance helperはこの単位で未確認または未実装。最終E0DDを含む正式feedとWindows SDK Release／GUI適用／正常再起動の確認は「修正統合候補のWindows SDK Release適用と再起動」へ集約する。特にScalarFunctionは呼出し時評価済みのValue、TableFunctionは遅延IEnumerableであり、ViewのDistinct・子scopeを流用せず各契約を別途設計する。現在の生成getterはowner DBだけをQueryへ渡し、明示TypedTransactionの束縛経路は持たない。

## 文再構成とasyncの追加修正

throw文を元テキストのまま移していたため、生成名前空間では`Exception`などが解決できなかった。throw文を既存の意味解析付きレンダラーへ接続し、型名を完全修飾する。asyncな元メソッドを再構成する成功経路では、生成メソッドにもasync修飾子を付ける。

修正後は通常DB providerの既存試験が実行まで成功した。await後のLINQ、結果、await後の例外型とメッセージを確認する`AsyncQueryKeepsAwaitAndQualifiesThrownTypes`を追加し、既存PK・View境界・評価順と合わせたRelease近接5件が成功した。生成器・試験ビルドは警告0・エラー0。証拠は同じ専用保存先の`results-product-r10/checks.trx`と`run-product-r10/receipt.json`にある。

広域のクラス11件では4件成功・7件失敗を観測した（新試験の入力クラス修正前）。残る取引試験はコンパイルエラーを越えてRunの展開失敗stubと`Reader.skip`誤変換へ進んだ。`Reader.skip`は次節の修正でコンパイルを通した。コメント出力2件、Conditionなどの未展開と別の境界解析の失敗は残る。新試験は傍受対象の通常クラスへ修正後、上記近接で成功した。全クラスの合格とは扱わない。

## GroupBy集約の捕捉変数と条件評価

GroupBy集約の結果式は、生成enumeratorのフィールドと、静的終端のローカルを区別する。静的終端では巻き上げ値と捕捉変数の両方について、計画の`ReceiverNames`に記録したreceiverだけを除去する。従来の`StaticProjectedResult`は巻き上げ値だけを処理し、`skip`などの捕捉引数に残った`this.`が元メソッドの`Reader` receiverへ変換されていた。既存の`ProjectedResultExpressionWithoutReceiver`を使用して修正した。

集約の融合では、元の選択子が集約呼出しを評価する条件も保持する。`skip ? 0 : group.Sum(...)`を要素走査へ無条件に移すと、戻り値が0でも、本来評価されないcallback・例外が実行される。条件演算子の選択枝、論理AND／OR・null合体の右辺、条件付きアクセス内にある集約は融合候補にせず、既存のGroupBy＋Selectの評価位置を維持する。通常の無条件集約にはこの制限を適用しない。

Windows Releaseの同じ既存テストDLLと生成器`F8CC03F1520BD378B0C4E2B41983B3F6647C5E71991EA4C7FD02E5B68055E186`で、AsyncOwner単独のCS1061を再現した。receiver修正だけの生成器ではEmitに成功したが、縮小した入力でskip=trueのcallbackが128回となる不一致を確認した。局所ガードを加えた同一入力では、true側は0回、false側は総和8128・callback128回となった。

製品回帰`GroupedAggregatesKeepCapturedValuesAndConditionalCallbackOrder`は、?:／&&／||／??の両分岐、無条件の捕捉引数、GroupBy結果選択子を11通り照合する。callback順の期待値は同一取引のRows列挙から取得し、表の物理順を固定しない。同じ新テストDLLで旧生成器はCS1061、新生成器`E8C9280F0C9D0771FEF2BEC6E931A6CCEA8049F170A955F3CEE650ED751785D2`は実Emit・実DB実行で1件成功した。生成器と試験ビルドはいずれも警告0・エラー0。

既存`JoinAndGroupedTerminalsKeepLinqValuesCallbacksAndEarlyExit`の2モードは、新旧とも同じ`GroupCount did not use the transaction terminal`で失敗した。元のAsyncOwner単独はCS1061解消後に既存コメントassertで停止し、同じEntry.Runの独立診断も先行するCondition未展開で停止したため、これらを全体合格には含めない。Linux・Debug・新View展開との統合は今回未確認。証拠は`F:/Seamless/async-owner-skip-20261110`の`single-results/single.trx`、`diagnostic-result.json`、`conditional-result.json`、`conditional-guard-result.json`、`before-new-results/before.trx`、`new-results/new.trx`、`near-results/near.trx`、`before-near-results/before-near.trx`。

## if条件内の終端と文の寿命

2026年11月11日に、通常の取引クエリ内のSum／Countがif条件にある場合、条件を文字列で出力するだけでFragment置換数が増えず、置換0件として展開失敗コードを出すことを再現した。同じ試験DLLと観測用生成器で、ゼロ置換の分岐へ到達することを確認した。

各外側のスカラー終端を、既存の終端生成処理によるローカル関数へ接続し、元条件の同じ位置を呼出しへ置換する。宣言を前置きし、値の計算は元の位置で行う。構文ノード位置で別呼出しを区別し、同じ文字列の式が2回ある場合も元の回数だけ評価する。条件の残りの部分は解析用の別構文木で再束縛し、実出力へは実際のFragment本体を生成する。

捕捉・結果型・評価文脈を先に検査し、終端生成の試行は独立したGenerationStateと名前scopeで行う。失敗した試行の置換数・Fragment・名前・traceを本体へ持ち越さない。証明できない条件は元の条件と評価位置を保持し、展開しない根拠をコードコメントへ出す。別の文が既に展開されていたメソッドも、従来の成功経路を維持する。元々置換0件のメソッドでは、理由付きの未対応コードを維持する。

明示checked／unchecked文脈、ref／out／in・ref-like捕捉、allows ref struct型パラメーター捕捉、struct this、条件内の宣言・await・条件付きアクセス等は保守的に元形を保持する。NullableAnnotationだけを根拠に捕捉を拒否しない。通常C#14の小実験で、null判定後のローカル関数呼出しがnull-flowを保持して警告0でコンパイルすることを確認した。一方、checked外の関数へ式を移すとOverflowExceptionが消える実例を再現し、評価文脈の保護に使った。

条件を展開して元のRunへ到達した後、await using文のawait欠落、catch型の未修飾、using宣言のusing／await欠落を順に分離・修正した。宣言のブロック位置を移さず、同期／非同期の解放順と寿命を保持する。catch宣言の型は元semantic modelで完全修飾し、識別子とfilterを保持する。ジェネリック署名は元symbolのAllowsRefLikeTypeがtrueの場合に、allows ref structを制約列の末尾へ伝搬する。

最終生成器6AC862BFはWindows Releaseビルド警告0・エラー0。同じ試験DLLで、新しい条件評価73比較、既存の混在条件6比較、checkedとref-like genericの理由・Emit、元取引2モードの70／100・競合80とScopeProbe解放を含む6件が成功した。さらに同一DLL・同一生成器で既存の非同期／例外型、PK Join、通常DB providerの3件が成功した。旧生成器では条件専用の失敗と混在条件の成功を確認し、anti-constraint欠落は旧新の同入力で同じCS9244となることを分けて記録した。最終generic試験は署名のEmitと未対応理由を検査する。ref-like捕捉を実行可能へ変更する契約は残る。

現在の確認はif条件と今回の文レンダリングの限定範囲である。同ソースを正式r3へ統合し、Windows生成器70EA／Linux生成器9434と各55DLLで、同じ管理87＋条件／取引9件を各OSで確認した。各SDK Releaseの通常library／C#14でも、ConditionValues(long)内のSum／Countが2個の実local functionへ展開され、元条件位置と埋込PlanのMethodKeyが一致する。GUIの編集・保管・検査・取り込み・明示適用と正常再起動後の4／5・型・本文保持を両OSで確認した。先行6AC8・正式r2の1454／E7F9とは成果物を区別する。他の制御文・catch filter内LINQ、既存GroupCount・コメント／境界解析の広域失敗と固定URL配布は継続する。正式r3の証拠は作業ツリーW05-FORMAL-WEB-PUBLISH-R3／W05-FORMAL-SDK-R3へ集約する。証拠は`F:/Seamless/flinq-remaining-statements-20261111/final-summary.json`、`final-results-r2/checks.trx`、`final-regression-results-r1/checks.trx`、`final-source-sha256.json`、`final-runtime-sha256.json`へ集約する。

## ManagerテンプレートからSDKへの確認

Managerの`DatabaseDesigner.ApplyQuery`を通し、DB直下・dbo0・dbo1に同名のSharedビューを新規作成した。各定義は`CreateViewDefinition`を使い、固有のnamespaceと名前を持つ。ReadQuery→ApplyQueryで列型と処理本文を編集し、宣言／本文コメント・属性・別宣言と、対象ごとに呼ぶhelperの対応を保持した。新しいCore近接1件とパス照合13件、既存フォーム保持6件がWindows Releaseで成功した。

専用.NET SDKプロジェクトは、既に検証済みのDatabase／FLinq生成器とcandidate05の依存をSHA照合し、Reference／Analyzerの固定パスで再利用した。Debug／Releaseとも警告0・エラー0でビルドし、3所属の生成getter、Query、明示Mainからの実呼出しで11／22／33を確認した。入力には各値が重複しており、結果は各1件である。各DLLの計画6件から、所属ごとのhelper計画だけが選択され、呼出元のViewDefinitionRootも確認できた。NuGetの正式feed再packとLinuxは今回の範囲に含めない。

実SDKの計画は絶対CallSite.Path、ManagerのSourceSnapshotは相対Pathで、元の完全一致比較では選択が空になった。同じMethodKey／SourceHashと呼出元・位置を維持したまま、相対パス全体の一意suffixで対応する共通`ResolvePlanSourceFile`を追加した。separatorを正規化し、Windows drive／UNCのcaseとUnixのcaseを区別する。basenameだけの代用、曖昧suffix、他所属、異なるcaller・範囲・hashは受理しない。返すSourceFileの元path・本文は変更せず、UIも同じ入口を使用する。

最初のSDKは検証Programを含む実行プロジェクトで、PDBのlanguage-versionはpreviewであった。この旧出力はGUI取り込みの証拠に含めない。後続の`library-gui-r1`では正式no-build packの12パッケージ・13 DLLをruntimeとSHA照合し、通常のDB由来library／C#14と、選択構成の暗黙Definesを保管下書きへ同期した。SDK Debug／Releaseは各警告0・エラー0。通常検査APIでDLL／PDB識別、元ソースchecksum、Defines、依存DLL、版・所有者を照合し、同じDebug DLLをGUIで検査・取り込み・変更確認・明示適用した。公開v2のShared／dbo0.Shared／dbo1.Sharedは11／22／33を各1行取得し、SDK絶対pathのtooltipと選択本文も一致した。正式feedの既存解析器が検出したhelperの順序なし戻り値には既存のreturn属性を明示し、診断抑制やポリシー緩和は行っていない。

展開通知の確認には通常フォームから既存ViewCalls.Rootを呼ぶFirstShared関数1個を追加し、専用SDK保存・Defines同期・両構成build・GUI適用を公開v3まで実施した。実行値11、実ViewDefinitionRootの赤太字通知、対応コードを同一画像に保存した。通知CSSが未読込のisolated bundleへ置かれていた問題は、既存manager.cssへ統合してUI ACE059で確認した。v2バックアップ125ファイルは全SHA一致、v3ではDatabase.csだけが変わり、他6ファイルの本文・コメント・属性・別宣言はbyte一致を保持した。所有host・headless profileは正常終了。Debug DLLの適用を確認した範囲であり、Release DLLの取り込み、v3再起動後と固定15337への切替は未確認。Linuxは次の独立受入へ分ける。最終cohortはCore98AF／DB3BF2／生成器0BE7／UI ACE059、全SHA・失敗したハーネス条件・終了証拠は`F:/Seamless/manager-view-definition-sdk-20261111/library-gui-r1/summary.json`へ記録した。

証拠は`F:/Seamless/manager-view-definition-sdk-20261111`の`path-results/template.trx`、`preservation-results/preservation.trx`、`sdk-r3-run/receipt.json`、`sdk-debug-proof.json`、`sdk-release-proof.json`、`pdb-compilation-settings.json`。

### 同一cohortのLinux隔離受入

Windowsで受入済みのportable managed DLLを同一SHAで再利用し、Linux nativeを配置した。全面のLinux再publishは実施していない。Core98AF／UI ACE059／Server7A66／DB3BF2／生成器0BE7は、実稼働中の`/proc/<pid>/maps`に現れた各1パスとファイルSHAでも一致した。正式feedの12パッケージ・13 DLLはWindowsの同一入力を再利用した。

新規Linux DB・固有0700 TMPDIRと別profileのheadless Chromeを使用し、Windows最終v3とbyte一致する7ソースを通常library／C#14へ出力した。Linux SDK10.0.403のDebug／Releaseは警告0・エラー0、通常検査APIとGUI検査・取り込み・変更確認・明示適用が公開v2まで成功した。これは別DBの版であり、Windows v3と同一履歴の版番号とは扱わない。Shared／dbo0.Shared／dbo1.Sharedは11／22／33を各1行、FirstSharedは11を通常GUIで取得。赤太字通知とLinux絶対pathの対応コードtooltip／選択本文を`view-plan-linux.png`に保存し目視確認した。

全計算をCPU0〜7へ制限し、hostはMemoryHigh850M／Max1200M、Chromeは650M／900M、SDKは1400M／1700Mの独立scopeを使用した。host172244と専用Chromeは正常終了・残存0、停止後125ファイルの全SHAバックアップを保護した。固定15337のPID170114／開始tick14559705は前後一致し、新規要求や設定変更は行っていない。Release DLLの取込、適用後再起動、後続Contains／private helper修正、正式再publish・固定配布は今回の証拠へ含めない。最初の受入shellはCRLFによりbuild前に失敗し、同じshellだけLFへ直した後の両構成成功と区別した。全証拠は`F:/Seamless/manager-view-definition-sdk-20261111/library-linux-r1/summary.json`へ集約した。

### 修正統合候補のWindows SDK Release適用と再起動

Core98AF／UI ACE059／Server7A66を受入済みSHAで維持し、Contains修正DB FBAA279Aと最終private helper生成器E0DD351Aを新しい`release-cohort-r1`へ固定した。旧cohort・DB・feedは保持し、正式no-build packの12パッケージ・13 DLLを新runtimeと照合した。feed版は`0.0.0-build.hefaab3922b552d14ba3a311a1f1a6577`。

通常library／C#14をGUIでReleaseへ評価・下書きr4へ同期し、SDK Releaseは警告0・エラー0。通常検査APIでDLL／PDB／元ソース／共有依存／RELEASEあり・DEBUGなしを照合し、同じRelease DLLのGUI検査・取り込み・変更確認・明示適用が公開v2で成功した。SharedとFirstSharedは各11、実計画の赤太字通知とSDK絶対pathのコードtooltip／対応本文を確認。正常停止後157ファイルを全SHA一致でバックアップし、同runtimeで正常再開して公開版・ArtifactHash・定義全体の完全一致、同じ実行値と計画コードを再確認した。ArtifactHashは`62EE482FDAEF4677F697B3EE181EA820AB43E3B6858EC889F0380E3A896592C0`、入力の7ソースも保持した。

初回csprojは旧SDK下書きの暗黙DEBUG／NET符号をDefineConstantsへ明示転記し、ReleaseでもDEBUGとRELEASEを含んでいた。7ソースに#if／#elif／#define／#undefがなく、転記値が旧SourceSnapshot.Definesと一致する根拠を保存し、このfixtureのDefineConstants要素だけを除去して通常SDK評価へ戻した。旧成果物と検査成功も保持した。今回のfixture判断であり、凍結コンパイル契約と構成切替時の明示／SDK由来定数の扱いはW04-CONFIG-PROVENANCEへ残す。

初回headless Edgeの自動拡張による別pageをpages[0]が選び、F5とready待ちがManagerを対象にしていなかった。Manager自体はHTTP200・本文50,119文字・ready=trueであり、誤対象と区別した。拡張無効clean profileとorigin一致一件の選択で最終受入を実施した。browser終了後にEmbedded capacity1の旧sessionが残り、通常復元1回が401だったため、保存r3と70ファイル全SHAバックアップを保持して専用hostを正常再開した。host再起動なしowner再開は独立残件で、適用後の再起動確認とは分ける。

所有host71880／63264／72676と専用browserは正常終了・残存0。固定15337は操作していない。新cohortのLinux GUIは次の独立受入へ記録し、正式再publish・固定配布・物理入力は別工程。全SHA、定数由来、失敗した試験条件、Release適用、再起動・終了証拠は`F:/Seamless/manager-view-definition-sdk-20261111/release-cohort-r1/summary.json`、同一画面は`restart-view-plan.png`へ保存した。

### 修正統合cohortのLinux SDK Release適用と再起動

Windows最終`release-cohort-r1`のCore98AF／UI ACE059／Server7A66／DB FBAA279A／生成器E0DD351Aを、Linux native資産とともに新規`release-linux-r1`へ固定した。正式feed hefaab392の12パッケージ・13 DLLのSHAを再照合し、実ロード5 DLLは再起動前後のmaps各1パスとSHAで一致した。新たな全面Linux publishは行っていない。

fixtureは旧SDK由来定数と条件指令なしを記録し、明示定数を空から通常SDKのRelease評価へ整え、初回SDK build前に保管した。通常GUIでRelease構成へ同期した後も同じr3で、RELEASEあり・DEBUGなしを維持した。Linux SDK Releaseは警告0・エラー0、通常検査APIでDLL／PDB／ソース／共有依存を照合し、同じRelease DLLのGUI検査・取り込み・変更確認・明示適用が公開v2で成功した。Shared／FirstSharedは各11、実計画の赤太字通知とLinux SDK絶対pathのコードtooltip／選択本文を確認した。

正常停止後125ファイルの全SHAバックアップを検証し、同じruntime／DBを正常再開して同じ結果・計画・コードを再確認した。公開版・ArtifactHash・定義全体は再起動前後で完全一致した。7ソースと定義はWindows最終公開定義とも完全一致し、SDK DLLとPDBを含むArtifactHashはOS別の成果物として区別する。Linux側ArtifactHashは`3CEC01B2B5B93363525606E46E7F8282FE8275C98EAAB500D757E1DF1630BA1D`。

固有0700 TMPDIR、CPU0〜7、host850M／1200M・Chrome650M／900M・SDK1400M／1700MのMemoryHigh／Maxを使った。所有host173765／174696と専用Chromeは正常終了・残存0、最終156ファイルも全SHA一致でバックアップした。固定15337のPID170114／開始tick14559705は前後同一で、新規要求や設定変更は行っていない。正式Linux再publish・固定配布・物理入力、製品の定数由来契約とEmbedded owner再開は別工程として残す。全証拠は`F:/Seamless/manager-view-definition-sdk-20261111/release-linux-r1/summary.json`、画像は`restart-view-plan.png`へ集約した。

## ビュー展開状態の通知

計画ノードへ`ExpansionStatus`と未展開時の`ExpansionReason`を明示的に記録する。元の計画作成処理はfragmentのフィールドだけを収集していたため、計算プロパティのこれらの値は自動収集されなかった。Managerの実行計画画面へ共通通知部品を接続し、未対応状態を赤い枠・大きな太字で表示する。旧形式、明示snapshot、再帰、定義形状、未対応本体の理由を日本語・英語で案内し、未知の理由コードもエスケープして表示する。

条件移動が実際に`Applied`と記録された場合は採用段階を表示する。`PointLookupNotSelected`が記録された段階では条件移動済みと点検索への置換未対応を併記し、それ以外の採用済み条件には未対応通知を出さない。`NotApplied`では記録された理由を表示する。本体展開だけの段階には内部キー条件への最適化が未完であることを表示する。通常ノードにはこの通知を付けない。

HtmlRendererのRelease近接7件が成功し、画面全体へ部品を接続した隔離UIも警告0・エラー0でコンパイルした。後続の条件移動表示は部品試験で確認した。最新Coreへ共通のSDKソースパス解決を接続したフルUIもコンパイルし、通知8件と対応コードの実レンダリング2件がReleaseで10件成功した。続いて既知の条件移動拒否理由を日本語・英語の説明へ対応付け、同じフルUIの12件が成功した。初回UI DLL SHAはF3A85E16A5940F595B12A99D8017C2D43655DD18032FA02B9F1165F38ABE3321だった。実GUI確認で既存ホストが新しいscoped CSS bundleを読み込まないことを発見し、通知スタイルを既存wwwroot/manager.cssへ統合した。最新UI DLL SHAはACE059EE6DE444BE7202904E4EEC8EF40BBD0BDB8BD61D6165ECCC43297FECA4で、フルUIとRelease12件が再成功した。SDK絶対パスから一意なソースだけを表示し、曖昧なsuffixでは表示を拒否する。実Managerへの配布とブラウザーでの見た目の確認は残る。最終証拠は`F:/Seamless/view-expansion-warning-20261111/full-test-results-r4/checks.trx`と`full-test-run-r4`、先行証拠は`F:/Seamless/view-expansion-warning-20261111/results-r2/checks.trx`、`run-r3`、`full-build-r2`。隣接フルUIコピーのresxを試験projectが自動収集した重複エラーは、隔離projectのdefault item収集を止めて解消した。

## 証拠一覧

専用の保存先は`F:/Seamless/view-boundary-probe-20261110`である。

- 元の固定05対照は`results-r5/checks.trx`、空入力の診断は`results-r7/checks.trx`と`baseline-summary.json`。
- 同じ生成器ソースの比較は`results-connection-control-r2`、`results-connection-connected-r3`と`connection-experiment.patch`。実行前の生成器SHA一致を検査している。
- snapshotへ束縛したViewの比較は`results-typed-view-r10/checks.trx`と`typed-view-r10/typed-view-proof.json`。
- 評価順・例外の比較は`results-evaluation-control`、`results-evaluation-connected`と各`join-evaluation-proof.json`。
- 実Gソースの最終近接3件は`results-product-r4/checks.trx`と`run-product-r4/receipt.json`。生成器SHAは`160ACC372418ABC9A30727A91D567D38FE2AB2C09CBFDC42BB25DEAEE7EBAC78`で、実行コピーとの一致を`product-identity-r4.json`へ保存している。
- 周辺4件の修正前後共通失敗は`results-product-r2`と`results-product-control-r3`、試験DLLの同一SHAと失敗メッセージ比較は`product-summary.json`へ保存している。
- 宣言View本体の展開は同保存先の`definition`配下。未展開の元候補は`baseline-results`、最終4件は`integration-results-r2/checks.trx`、実行物一致とソースmanifest・未検証は`summary.json`。DB runtimeと生成器の実行コピーSHAを照合している。

利用者の固定URLサービスとDBは変更していない。今回の試験はホストやIPCを起動せず、隔離したInMemory DBを使用した。

## 集合所属判定と参照グラフ等価性

RecordGraphV1では、値の正規化と参照共有・循環構造を含む型固定比較を使う。`View`の重複除去はこの保存表現を使っていたが、`Contains`は`EqualityComparer<TResult>.Default`を使っていた。キー付きTableのレコード指定`Contains/Remove`にも同じ標準比較が残っていた。

DB3BF2を固定した対照で、-0.0と+0.0、12.00mと12mを含む同値の循環配列を別々に構築した。Viewの列挙は1行へ重複除去する一方、検索は失敗した。Tableでも保存後に復元したレコードと、新たに構築した同値の引数の検索・削除が失敗した。Viewの旧factory／宣言factoryと履歴設定の4組、Tableの検索・削除の2組の合計6件を旧DLLで再現し、既定profileのスカラー対照1件は成功した。

この3箇所を既存の`ExecutionResources.Codecs.GetEqualityComparer<T>()`へ接続した。RecordGraphV1は同じ正規化値と参照構造を比較し、既定profileは従来の標準比較を継続する。Viewは各検索で定義を1回評価し、共有構造の分裂・キー差・値差を不一致とする。Tableのレコード削除は全列が一致する場合だけ成功し、確定後の次の取引でも件数0を確認した。

新7件と既存の保存・取消・退避・形式拒否・チェックポイント復旧15件が、Windows Debug・Release各22件成功した。runtimeと試験のbuildは両構成とも警告0・エラー0。Release runtimeのSHA256は`FBAA279ABFF72E07F05F0BAAE15F63D61C7749C861CDF1B585D7412762DAB58C`。同DLLを含む186ファイルを独立Linux出力へコピーし、全SHAを照合した。同じRelease試験DLLをbuildせずvstestで実行し、22件が成功した。固有0700 TMPDIR、CPU0〜7、MemoryMax1600Mのscopeで実行した。同じFBAAを含む正式feedとWindows SDK Release／GUI適用／正常再起動の確認は「修正統合候補のWindows SDK Release適用と再起動」へ集約する。新cohortのLinux GUIと固定Managerへの配布は後続工程として扱う。

証拠は`F:/Seamless/view-contains-equality-20261111`の`baseline-final-results/checks.trx`、`release-integrated-results/checks.trx`、`debug-integrated-results/checks.trx`と各runの`receipt.json`。Linuxは`linux-integrated-r1/results/checks.trx`、`run/stdout.log`、`execution-proof.json`を使う。初回の隔離runtimeプロジェクトはHost DLLを含めた参照一式から自己依存が解決され、deps生成で重複キーとなった。製品変更を追加せず、runtimeの参照を既存の確認済み依存セットへ揃えて試験を成立させた。固定ManagerとSDK受入済みDB3BF2は維持している。


## ビュー本体の結果行型同期の着手観測

合意済みの「ビューの列型は処理本体の戻りから決める」接続の着手時には赤太字の未対応通知が表示されていた。通常ビューの宣言型はQueryメソッドの所有型・カタログ対象名・ISetの要素型を兼ねるため、factoryの型引数だけを別型へ変えると対象の同一性と行構造が一致しなくなる。既存の通常struct／複数partial／手書きメンバーを保持したまま行宣言を変更する工程が必要である。

正式Windows r3 DLLを参照する隔離診断で、ユーザー本体を実行せず7形状のRoslyn束縛を観測した。静的Queryから既存ScalarFunctionを直接呼ぶ解析置換はprotectedアクセスのCS0122となった。解析専用generic helperを一時構文木にだけ追加すると、既存の名前付き行配列、匿名行配列、Selectの匿名射影、同じ匿名形状を返す複数returnについて要素型と列名／型を取得できた。`return []`は宣言先の型に依存し、異なる匿名形状の分岐は推論できず、各CS0411となる。単一intは推論できるがViewの集合契約には適合しない。外側の旧ISet戻り宣言とのCS0029／CS0266は残り、この観測だけで保存ソースをコンパイル可能にしたとは判定しない。

次の実装では、本体から得た行構造、保持する名前付きView宣言、結果の射影変換を同じ編集候補へ反映し、再束縛とEmitで検証する。中間匿名型まで一括変換すると別の演算子や等価比較を変えるため、結果境界を区別する。 保存本文へ名前付き行へのSelectを挿入する場合、次回の推論でその投影が元の匿名型を隠す。初回だけの成功で完了にせず、生成した投影を識別する所有情報と解析時の取外し、または元定義と投影を分離する明示境界を設け、追加→保管→再度型変更まで検証する。任意のユーザーSelectを形だけで除去しない。宣言依存の空集合・カスタム行実装・曖昧な分岐は既存入力と診断を保持し、未証明の変換を適用しない。診断実験の証拠は`F:/Seamless/view-result-inference-probe-20261111/observations.json`と`run-r2/receipt.json`。これは7例の静的観測であり、7機能の合格試験ではない。

### 次の実装単位の責務分離案

反復編集で元の型が隠れない構成として、`CreateViewDefinition<TSource,TResult>(Func<IEnumerable<TSource>> definition,Func<TSource,TResult> projection,string name)` のoverload追加を実装候補とする。`definition`へ利用者の元本文、`projection`へ公開行型への生成変換を置き、解析は前者だけを使用する。型引数は通常C#の推論で匿名型を扱い、既存の同名View行型とQuery／ISet契約を維持する。ランタイムと管理画面でこのoverloadを採用した。

Runtimeは`definition()`とprojectionを、既存View<TResult>の列挙開始時に評価する。投影後のTResultへ集合境界を置き、先にView<TSource>を作って重複除去しない。所有者・名前・定義MethodInfoによる循環検出へadapter delegateの識別も照合する。Managerは元definitionから列構造を取得し、既存宣言の差分編集で行型と生成projectionを同期する。任意の利用者projectionを自動で置き換えないよう、実メソッドsymbolと生成用引数の所有を確認する。

FLinqは実メソッドparameterに対応してdefinition／projectionを取得し、内側pipelineへ既存Select fragmentを接続してからViewDefinitionRoot<TResult>へ渡す。匿名中間型は既存のregistryを使う。追加Selectの融合は実生成コードとPlanで確認する。既存の条件移動は単一Selectに限定されるため、2段の投影合成が証明できるまで理由付きNotAppliedを保持する。条件移動の未採用理由は展開コードコメントと計画へ残す。

最低限の受入には、初回匿名列追加→保管→再度型変更、DB直下／複数schemaの同名View、別partial・属性・コメント・StableId保持、利用者コード非実行、scalar／空集合／異形状分岐の入力保持、未展開runtimeとFLinqの値・副作用順・例外・Dispose・親予算返却の一致を含める。既存手書きの特殊factoryと行実装は、変換可能性を証明した形から対応し、未証明部分の赤い案内を維持する。

## ビューの結果列同期の実装

ビュー作成と既存ビューの本文・C#編集で、定義ラムダだけをRoslynで束縛し、匿名型の要素から列名・型・順序を取得する。元本文を保持し、名前付きreadonly positional record structの列宣言と投影を更新する。生成投影はマーカーと元の列に対応する構文で識別する。既存の列名に対応するパラメーターの属性・コメント・表示用IDを引き継ぎ、別partialにある列宣言も元のファイルで更新する。保存候補を新しいfactoryに再束縛して診断を返す。

構造画面ではビューの結果列を読取専用で表示する。手書き投影、特殊な行宣言、宣言型に依存する本体には赤太字の案内を出す。新しい結果列を確定できない編集では診断を表示し、利用者が入力を直せる状態を保つ。

ランタイムの新overloadは、列挙時に元の定義を評価し、投影後の行へ集合の重複除去を適用する。循環検出には元の定義のMethodInfoを使用する。Windows Release近接27件で遅延・投影順・途中Dispose・同名別定義・旧新overload間の循環を確認した。管理近接77件でDB直下とスキーマ内の反復更新、属性と別partial保持、ソースEmit、構造の読取専用表示を確認した。Windows隔離ブラウザーでは匿名列からの作成と2回のC#再編集、最終ソースのコメント・投影・下書き保管まで確認した。

証跡はランタイムが `F:/Seamless/view-projection-runtime-20261111/summary.json`、管理近接が `F:/Seamless/view-result-sync-20261111/results-r4/checks.trx`、ブラウザーの編集が `F:/Seamless/view-result-sync-20261111/gui-windows-r1/gui-proof.json`。

### 生成getterでの展開と傍受対象の形

編集後のビューを生成getterから読む管理近接`EditedAnonymousViewCompilesAndExpandsAtItsGeneratedGetter`は、新生成器399C6Aを含めても計画0件で失敗した（79件中78件成功）。確認用クラスが`static class`だったため、FLinqの傍受対象判定が意図どおり除外していた（`FLinqIncrementalGenerator.TargetDiscovery.cs`の`ContainingType.IsStatic`）。製品の不具合ではなく試験入力の誤りであり、期待値は弱めずに`sealed class`へ直した。同じCore 5501／UI 3E4E／DB 0FE1／生成器399Cで79件が成功し、`ViewDefinitionRoot`の`ExpandedDefinitionWithSetBoundary`と実行値[7,8]を確認した（`results-r5/checks.trx`、`run-r5/receipt.json`）。static classのメソッドに書いた問い合わせは計画を持たない既存の制約であり、管理画面で案内するかは別途扱う。

復元したランタイム3件（`DatabaseViewProjectionRuntimeTests`）とFLinq 5形状・既存View回帰9件は、通常リポジトリのプロジェクトからRelease buildして17件成功した（警告4件は無関係な既存試験ファイルのxUnit解析器警告）。証拠は`F:/Seamless/view-projection-normal-20261111/results-r1/checks.trx`と`run-r1/receipt.json`。

### Windows隔離GUIの適用と再起動

匿名列で作成し、C#で2回再編集（Id int/Caption string→Id long/Label string→Amount decimal/Title string）、SDK Release（警告0・エラー0、DLL SHA`6F1416242FDF903E8D482087FF72FD9C65A4FE2D8F900E505DF84FD7890C9C92`）、GUIの検査・取り込み・変更確認・明示適用で公開v2へ進んだ。`gui-apply-proof.json`のFailedは、結果見出しを`Amount/Title`と期待したハーネスの誤りで、実際の見出しは`amount/title`だった。適用自体の成功とは区別する。公開v2を再適用せず読んだ`gui-readback-proof.json`はPassedで、入力の重複3は除去された。

その後、正常停止済みのDB157ファイルを全SHA一致でバックアップし、同じruntime（Core 5501／Server A0E3／UI 3E4E／DB 0FE1／FLinq生成器399C／DB生成器EDF7、feed h4cb3eb28）で正常再起動した。新しい所有者起動セッションで、結果2行・列名と型の読取専用表示・元コメントと生成投影を確認し、停止時の公開定義（v2）が再起動前と完全一致した。hostは正常終了、残存0。証拠は`gui-windows-r1/restart-summary.json`、`gui-restart-proof.json`、`published-view-after-restart.png`、`host-restart-run/receipt.json`。

結果表の見出しは管理JSONの命名規則（`RuntimeValueJson.Name`）で先頭小文字になり、構造画面の`Amount/Title`と食い違っていた。利用者の判断で、値のJSONキーは公開契約として維持し、画面の見出しと読み上げ名だけを宣言名へ揃えた。`CatalogField.DisplayName`はキーと宣言名が異なる場合だけ送る任意項目で、旧サーバーの列情報ではキーを見出しに使う。近接37件の証拠は`F:/Seamless/result-header-names-20261111/results-r4/checks.trx`。上記の再起動確認はこの変更前のcohort（UI 3E4E）であり、見出しは`amount/title`のまま。

### Linux隔離GUIと改行の差

同じcohortを同一SHAのportable DLLとLinux native資産で配置し、新規Linux DBで作成から再起動後の保持まで合格した（`F:/Seamless/view-result-sync-20261111/gui-linux-r1/summary.json`）。WindowsとLinuxで公開本文とArtifactHashが一致しなかった（Windows 01B7E2DF…、Linux 30B95161…）。原因は新規ビューのソースを書き出す`DatabaseDesigner.WriteQueryRecord`が`StringBuilder.AppendLine`を使い、Windowsだけ末尾の閉じ括弧にCRLFが混ざることだった。他の設計器の出力は`\n`固定である。LF固定へ直し、新規作成ソースにCRが含まれないことを近接試験へ加えた。さらにManagerが受け取る本文（処理本体とC#部分の置換）を`SourceLineEndings.Normalize`でLFへ揃える。行をまたぐ文字列リテラルの中の改行は値の一部なので保持する。IDEが保存したファイルはSDKのPDBチェックサムと照合するため変換しない。既存DBの保存済み本文も変換しない。正式再publish、両OSの成果物一致の再確認、固定URL反映は未了。
