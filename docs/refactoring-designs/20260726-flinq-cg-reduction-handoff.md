# FLinq 生成コード量削減 — 引き継ぎ

作成日: 2026-07-26 / 対象ブランチ: `opusで最適化`

新しいセッションでこの作業を引き継ぐときは、このファイルをそのまま読ませてください。

---

## 0. 最初に必ず読むこと

1. `g:\GitHub\Seamless\CLAUDE.md` → 指示に従い `G:\AI\ai_rules.md` と
   `g:\GitHub\Seamless\AI_SYNTAX_RULES.md` を読む
   (K&R・スペース詰め・`this.` 付与・制御文の `{}` 必須・三項演算子禁止・アクセス修飾は最小)
2. `G:\AI\ai_rules.md` の指示で `g:\GitHub\Seamless\AGENTS.md` も読む
   (日本語応答・質問は1つずつ・Database Change Policy)
3. `G:\AI\Skills\plan-model-gate\SKILL.md` を読み、モデル/思考レベルを明示してから進める

## 1. 現在地

- リポジトリ: `g:\GitHub\Seamless` / ブランチ **`opusで最適化`**
  (`sonnet5` ではない。Phase1/2 のコミット `16ff6901` がこちらにしかない)
- HEAD: `3cdb4f17`
- 未コミット: **1件のみ** — `Seamless.Abstractions/Collections/IRecord.cs` で
  `IRecord<TKey>` → `IRecord<out TKey>`(共変化)。意図と検証状況は要確認。

### 検証済みの状態(2026-07-26 時点、全緑)

| スイート | 結果 |
|---|---|
| `Test` (net10.0) | **1330 / 1330** |
| `Test.System.Linq.Enumerable.FLinq` | 198 / 198 |
| `Test.Persisted.Collections` | 140 / 140 |
| `Test.Tsavorite.Collections` | 363 / 363 |
| `dotnet build Seamless.slnx -c Debug -m:1` | 0 エラー |

### 生成コード量

計測対象は **FLinq ジェネレータが `Test` プロジェクトへ吐いた `.g.cs` 全ファイルの合計行数**。

```bash
cd /g/GitHub/Seamless && find Test/obj/Debug/net10.0/generated/Seamless.FLinq -name '*.g.cs' -exec cat {} + | wc -l
```

| 時点 | 行数 | 対ベースライン |
|---|---:|---:|
| ベースライン(`16ff6901`) | 115,070 | — |
| Phase 3 完了 | 100,951 | -12.3% |
| **現在** | **94,337** | **-18.0%** |

ファイル数 191・生成 struct 数 1,459 は**全期間で不変**。減少はすべて1ファイルあたりの縮小。

## 2. 完了済み(触らない・退行させない)

`git log --oneline 16ff6901..HEAD` が正。要点のみ:

### Phase 1 / 2(`16ff6901`)
ランタイム comparer devirtualize、`ByteRange.cs`、Persisted の O(1) mmap ハッシュ索引
`PersistedHashIndex.cs`(第3随伴ファイル `.hindex`)、`AutoSerializer.cs` の明示特殊化。

### Phase 3(`8a13d8fd`)115,070 → 100,951 行
- 「常に false」の `TryGetNonEnumeratedCount`/`TryGetSpan`/`TryCopyTo` を1行形で出力
- 生成 struct から `IEnumerator<T>` と `object IEnumerator.Current`/`Reset`/`MoveNext` を除去、
  消費側 112 箇所を `TryGetNext(out var v)` へ移行
- ToArray ラダーを `GeneratedCodeHelpers.ToArrayFromEnumerable` へ移譲
- Persisted/Tsavorite の `Contains` を `.hindex` 直接検索へ(O(n)→O(1))
- native scan の中間段に `Cast`/`OfType` を追加

### その後
- `e3b30fbf` 既存4失敗(Benchmark UI)を解消
- `2d703943` **`IValueEnumerator<T>.TryContains` の DIM ボクシングを解消**。
  DIM を撤去し、`FromHashSet`/`FromSortedSet` と同じ型特殊化オーバーロード
  (`PersistedSetEnumerator`/`TsavoriteHashSetEnumerator`/`TsavoriteSetEnumerator`)へ移した
- `eb3afcbf` Phase 4: 100,951 → 94,118 行。生成コードの非決定性も修正
- `e12cccf0` R1 死にコード削除 + `Fragments/Bases` 配置規約適合
  (`GetCurrentExpression` はここで撤去済み)
- `c7dc63b8` R3 生成器のアクセス修飾最小化(public 174 型 → internal)
- `18d69360`/`d7b71542`/`e0cbaba6` R4 `Pipeline.cs`(4,667行)・`RunnerViewModel.cs`(7,880行)の分割
- `bbb708e9` R5 生成出力の空白スタイル圧縮
- `e767895f`〜`3cdb4f17` FLinq 性能改善群
  (順序パイプラインの System.Linq 委譲撤去、Reverse/TakeLast の ArrayPool 化、
   SequenceEqual devirtualize、Sum の SIMD 化、OrderBy comparer devirtualize、
   RangeRoot の直接 Consume 展開、Chunk の先行確保撤去)

## 3. 検証コマンド(変更したら必ず全部)

生成器を変えたら**インクリメンタル残骸に注意**。必ず生成物を消してフル再生成する。
並列ビルドでキャッシュ衝突(MSB3491)が起きるので `-m:1` を付けること。

```bash
cd /g/GitHub/Seamless && rm -rf Test/obj/Debug/net10.0/generated && dotnet build Test/Test.csproj -c Debug -f net10.0 -t:Rebuild -m:1
```

```bash
cd /g/GitHub/Seamless && dotnet test Test/Test.csproj -c Debug -f net10.0 -m:1
```

```bash
cd /g/GitHub/Seamless && dotnet test Test.System.Linq.Enumerable.FLinq/Test.System.Linq.Enumerable.FLinq.csproj -c Debug -m:1
```

```bash
cd /g/GitHub/Seamless && dotnet test Test.Persisted.Collections/Test.Persisted.Collections.csproj -c Debug -m:1
```

```bash
cd /g/GitHub/Seamless && dotnet test Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj -c Debug -m:1
```

```bash
cd /g/GitHub/Seamless && dotnet build Seamless.slnx -c Debug -m:1
```

## 4. 残課題

### A. 未コミットの `IRecord<out TKey>` 共変化(最優先で判断)
`Seamless.Abstractions/Collections/IRecord.cs` の1行だけが未コミット。
共変化は公開 API の契約変更なので、意図・影響範囲・検証状況を確認してから
コミットするか戻すか決めること。`TKey` が `in` 位置(メソッド引数・`ref`/`out`)で
使われていないかを `IRecord<` の全参照で確認する必要がある。

### B. Benchmark の実測(未実施)
「既存の他メソッド性能が退行していない(Benchmark UI ランキングを大きく悪化させない)」
という完了条件だけ未検証。Debug ビルド確認のみ済み。
Release + Worker 経由の重い工程なので、実行タイミングをユーザーに確認してから回すこと。

Phase 3 時点では、生成コードの列挙ロジック(`TryGetNext`・interceptor 本体・Dispose・分岐)が
前後 diff で**差分ゼロ**であることを確認済み。ただし Phase 4 以降は
SIMD 化・ArrayPool 化・devirtualize など実挙動を変える最適化が入っているので、
実測の価値は Phase 3 当時より高い。

### C. Set の TRecord キー冗長解消(据え置き)
`Seamless.Collections.Tsavorite/Tsavorite/Stores/SetStore.cs:5` の `KEYED-RECORD-CODEC-TODO`。
TRecord が TKey を Tsavorite キーとは別に二重で永続化している。
生成コーデックが非自明なため据え置き中(TODO は維持されている)。

### D. `Any(x => x.Key==k)` の key lookup 透過化(見送り中)
述語が `param.<KeyMember>==<expr>` かつ `<KeyMember>` が `IRecord<TKey>.Key` であることを
構文木で証明する新規パターンマッチが必要。同名の別メンバを誤認すると意味論が壊れるため見送り。
やるなら独立したタスクとして切り出すこと。

## 5. 地雷(先に知らないと必ず踏む)

1. **`IValueEnumerator<T>` に default interface method を追加してはならない。**
   実装型が値型のとき `where TEnumerator:struct` 経由の呼び出しは `constrained. callvirt` になり、
   「実装を持たない」DIM では値型が box される。RyuJIT は DIM を devirtualize せず
   (`CORINFO_DEVIRTUALIZATION_FAILED_DIM`)、.NET 10 の escape analysis でも消えない。
   **実測 24 バイト/呼び出し。** 加えて DIM は具象型レシーバーから呼べないため、
   enumerator を具象 struct のローカルで持つ生成コードは CS1061 になる。
   provider 固有の高速経路が要るときは `Contains.cs` のように**型特殊化オーバーロード**で表現する。
   根拠は `Seamless.Linq/Linq/ValueEnumerable.cs` のコメントにも残してある。**再挑戦しないこと。**

2. **`IEnumerator<T>` には2種類ある。混同すると壊れる。**
   - **(A) ユーザー由来 — 変更しない**: `Fragments/Root.cs` の
     `where TEnum : struct, IEnumerator<T>`(`List<T>.Enumerator` 等が入る)と、
     その中の `MoveNext()`/`.Current` 呼び出し。`SelectMany` の `innerEnumerator` と
     `Pipeline` の `InnerEnumerator` フィールドも同じくユーザーの `IEnumerable<T>` 由来。
   - **(B) 生成 struct — `IValueEnumerator<T>` のみ**: それ以外。
   生成コードに残る `IEnumerator<`・`MoveNext()` はすべて (A) で、**正常**。

3. **回帰スイートは `Test/Test.csproj`。**
   `Test.Tsavorite.Collections` は Seamless.FLinq を Analyzer 参照していないので
   生成コードを一切含まない。Intercept / FragmentCoverage / Chain1 / Chain2 /
   SourceGenerator shape テストはすべて `Test/System/Linq/Intercept/` と
   `Test/SourceGenerator/` 配下にある。

4. **`GeneratedInterceptorAssert.SourceFor<T>(name)` は `.g.cs` ファイル全体を返す。**
   「この経路では出ないこと」を検証したいときはファイル全体だと他メソッド分が混ざって誤判定する。
   `Test/Seamless/Persisted/Linq/Enumerable/FLinqNativeIterateTests.cs` の
   `InterceptorBody(methodName)` ヘルパーで対象インターセプター本体だけを切り出すこと。

5. **`persistedSet.Contains(x)` は LINQ 介入が起きない。**
   `Set<T>` 自身のインスタンスメソッドに束縛されるため。
   FLinq が介入するのは `.AsEnumerable().Contains(x)` の形。

6. **`OfType<T>` で入出力が同型だと `is not T` が CS8518 になる。**
   `Bases/ProviderIterateTerminalBase.cs` の `InvokeMethodSource` は
   `stage.InputType==stage.OutputType` で早期 continue して回避している。

7. **`PipelineStageCapabilityTable.SupportsProviderNativeIterate` は中間段の許可表。**
   Count/Any/First/Sum は `PipelineTerminalKind` 側で、既に
   `Bases/ProviderIterateTerminalBase.cs` の `CanCreateFor` が許可済み。
   ここに段を足すときは `InvokeMethodSource` の描画側も必ず同時に足すこと
   (片方だけだと `NotSupportedException`)。順序依存段(Take/Skip/TakeLast/OrderBy/Reverse)は
   provider scan が `SourceCapability.Unordered` なので追加してはいけない。

8. **生成コードの決定性**に注意(`eb3afcbf` で一度修正済み)。
   ディクショナリ列挙順などが混ざると差分ノイズになる。

## 6. 絶対条件

- ユーザー側の使い方・公開API・foreach 挙動・戻り型は**ゼロ変化**。
  生成 struct は private 実装詳細。壊れたらそれは仕様変更ではなく不具合。
- 性能は中立以上。生成コードにアロケーションを増やさない。
- Phase 1/2 の確定した設計判断は変更しない:
  - 値のバイト比較は VALUE 側では非適用(`EqualityComparer<TValue>.Default` と不一致になるため)。
    健全な KEY 側のみ実装済み。
  - 未対応 storage 操作は fallback で隠さず検出可能に保つ。

## 7. 参考

- Pipeline/Fragment 仕様:
  `docs/refactoring-designs/20260624-1806-flinq-intercept-optimization-pipeline-fragment-spec.md`
- Phase 3 の実装計画と実施結果:
  `C:\Users\kazuh\.claude\plans\g-github-seamless-sonnet5-phase-3-twinkly-stonebraker.md`

---

まず現状確認(§3 の4スイートが §1 の期待値どおりであること)をしてから、
plan-model-gate に従って作業計画を出してください。

## 2026-08-09: FLinq SourceGenerator Registry中心リファクタリング

- `Seamless.FLinq` Generatorを`netstandard2.0`へ移行し、Roslynを5.6.0へ更新した。
- 演算子・Variantの探索、型付きFactory、Root、ProviderNative、Pipeline能力を`SourceGenerator/Linq/Registry/`に集約した。
- ReflectionによるFragment探索、コンストラクタ反射、型名Variant分解、汎用Constructor Binderを通常生成経路から除去した。
- 公開`System.Linq.Enumerable` APIとのCoverage契約、Priority/Factory/ProviderNative整合性を`FLINQ006`とRegistry単体テストで検証する。
- FLinqパッケージは`Seamless.Linq`を同一バージョン依存として宣言し、ランタイムDLLを同梱しない。
- 設計の正本は`docs/FLinq.md`と`Document/Code/Internal/FLinqRegistryInternalDesign.md`、UMLは`Document/Code/Internal`のComponent/Class/Interaction diagramsを参照する。