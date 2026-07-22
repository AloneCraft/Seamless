# Database 仕様書統合

## UI SQLite（通常Benchmark UIはスキーマ版24、Scalable UI/Parameter Sweep UIはスキーマ版20）

UIの実行履歴は、アプリごとに独立した次のSQLite DBへ保存する。

| アプリ | DB | `UI実行アプリ種別` |
|---|---|---|
| 通常Benchmark UI | `Work/UI/Benchmark.db` | `ベンチマーク` |
| Scalable UI | `Work/UI/Benchmark.Scalable.db` | `スケーラブルベンチマーク` |
| Parameter Sweep UI | `Work/UI/Benchmark.ParameterSweep.db` | `パラメータスイープ` |

- `PRAGMA user_version` は、通常Benchmark UIは `24`、Scalable UI/Parameter Sweep UIは `20` とする。
- DBパスは `UI実行DBパスリゾルバー` で解決する。
- 旧スキーマからの自動移行や旧英語名との互換処理は持たない。既存DBは明示バックアップを作成して保持してから、本体・WAL・SHMを明示的に破棄する。起動時の自動処理に頼らず直接作成し、旧履歴を移行しないため、再作成直後のテーブルは空とする。
- テーブル名、ビュー名、プロジェクト独自の列名は日本語とする。Benchmark引数である `Count`、`Initial`、`Data`、`Index`、`Page`、`Segment`、`Memory`、`Fraction` は英語を維持する。
- `状態` はコード上の `UI実行状態` との明示変換を通し、`Queued`、`Running`、`Paused`、`Stopped`、`Completed`、`Failed` を保存する。通常UIはタブのGrid見出しを接頭辞とする明示列へ保存し、固定のLeft/Center/Right列は持たない。
- 診断カウンター(FLinq/ValueLinqフォールバック計測)を保存するテーブルは持たない。書き込み専用で読み出し側が存在しなかったため版23で廃止した。

### 実行テーブル

- 通常Benchmark UI: `実行ID`、`表示名`、`状態`、`実行プロファイル`、`タブ名`、`更新日時`、`実行ルート`、`要素数一覧`、`計測時間ns`、`論理CPU一覧`
- Scalable UI / Parameter Sweep UI: `実行ID`、`表示名`、`状態`、`実行プロファイル`、`タブ名`、`更新日時`、`実行ルート`、`要素数一覧`、`適応閾値ns`、`論理CPU一覧`、`前回実行ID`(前回実行結果との比較に使用。通常Benchmark UIはこの列を持たない)

### アプリ別のベンチマーク表

- 通常Benchmark UI: 単一の `ベンチマーク` 表は持たず、`書き換え`、`パイプライン融合`、`述語プッシュダウン`、`Array`、`IEnumerable`、`Range`、`List`、`Persisted`、`Tsavorite` の9表を持つ。各表は `実行ID`、`型`、`ベンチマーク`、`CPU番号`、`引数キー`、`引数JSON`、`行チェック` と、当該Grid見出しを接頭辞にした `アセンブリ名`、`型完全名`、`状態`、`失敗理由`、`日時`、`時間ns`、`メモリバイト`、`ループ回数` を持つ。\n- Scalable UI: `実行ID`、`タブ名`、`型`、`ベンチマーク`、`Count`、`Initial`、`Data`、`Index`、`Page`、`Segment`、`Memory`、`Fraction`、`CPU番号`、`行チェック`、`今回状態`、`今回失敗理由`、`今回日時`、`今回時間ns`、`今回メモリバイト`。前回は `実行.前回実行ID` で結ぶ。
- Parameter Sweep UI: `実行ID`、`ケース名`、`コレクション`、`ジェネリック`、`バックエンド`、`スイープ`、`Indexサイズ`、`Pageサイズ`、`Segmentサイズ`、`Memoryサイズ`、`可変Fraction`、`最大成功Count`、`エントリCount`、`失敗種別`、`失敗理由`、`追加時間ms`、`検索時間ns`、`測定日時`

共通索引は `索引_実行_更新日時` とする。通常UIは各タブ表に `実行ID` 索引と、`実行ID,型,ベンチマーク,引数キー` の一意索引を持つ。

### 分析ビューと時間シェア

- 通常Benchmark UIは未使用の分析ビューを作成しない。
- Scalable UIの `スケーラブル履歴` は今回の時間・メモリ元値を返す。`スケーラブル前回シェア` は `実行.前回実行ID` と8個のBenchmark引数を完全一致させ、今回／前回の時間・メモリ元値と各シェアを返す。
- Parameter Sweep UIは分析ビューを持たない。
- シェア対象の測定値は書き込み境界で非有限の実数をNULLへ変換し、ビューではNULLと負数を欠測として扱う。片側ゼロは `1.00:0.00` または `0.00:1.00`、両側ゼロは `0.00:0.00` とする。

# Source: Document\Code\Internal\TsavoriteDBSourceGeneratorInternalDesign.md

---
phase: code-internal-reverse
reverse_document: true
canonical_target: Document/Code/Internal/Seamless.DatabaseSourceGeneratorInternalDesign.md
evidence_sources:
  - Seamless.Database/Seamless.Database.csproj
  - Seamless.Database/SourceGenerator/GenerateDatabase.cs
  - Seamless.SourceGenerator/SourceGenerator/GeneratorHelpers.cs
  - Test/Tsavorite/Database/
confidence: medium
review_status: draft
---

# Seamless.Database Source Generator 内部設計

## 対象
`Seamless.Database` は Roslyn source generator として構成され、`DatabaseGenerator` marker と schema 型から MemoryPack 対応 record を生成する（`Seamless.Database/Seamless.Database.csproj:3`, `Seamless.Database/Seamless.Database.csproj:8`, `Seamless.Database/Seamless.Database.csproj:14`, `Seamless.Database/Seamless.Database.csproj:19`）。

## 主要クラス

| クラス/領域 | 責務 | 実装証拠 |
|---|---|---|
| `DatabaseSchemaGenerator` | schema generator の入口。型宣言を syntax provider で収集し、生成ソースを `AddSource` する。 | `Seamless.Database/SourceGenerator/GenerateDatabase.cs:8`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:9`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:11`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:17`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:21` |
| `型対象生成` | 型宣言から symbol を取り、marker/partial などの生成対象判定を行う。 | `Seamless.Database/SourceGenerator/GenerateDatabase.cs:27`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:37`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:47` |
| `データベースソース構築` | marker namespace を基点に Keys/Records namespace の型を集約し、生成順を安定化する。 | `Seamless.Database/SourceGenerator/GenerateDatabase.cs:52`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:55`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:70`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:79`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:86` |
| `レコードソース本体追記` | partial struct を unmanaged/managed storage に分け、constructor/property/MemoryPack 実装を生成する。 | `Seamless.Database/SourceGenerator/GenerateDatabase.cs:114`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:124`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:130`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:148`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:161` |
| `GeneratorHelpers` | symbol 表示形式、namespace 文字列、indent、空行除去などの共通処理を持つ。 | `Seamless.SourceGenerator/SourceGenerator/GeneratorHelpers.cs:3`, `Seamless.SourceGenerator/SourceGenerator/GeneratorHelpers.cs:4`, `Seamless.SourceGenerator/SourceGenerator/GeneratorHelpers.cs:19`, `Seamless.SourceGenerator/SourceGenerator/GeneratorHelpers.cs:28`, `Seamless.SourceGenerator/SourceGenerator/GeneratorHelpers.cs:44` |

## データ生成規則

- schema record は unmanaged property と managed property に分割される（`Seamless.Database/SourceGenerator/GenerateDatabase.cs:124`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:125`）。
- unmanaged 側は `_U`、managed 側は `_M` に格納される（`Seamless.Database/SourceGenerator/GenerateDatabase.cs:148`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:151`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:297`）。
- managed 側は MemoryPack serializer/deserializer を生成する（`Seamless.Database/SourceGenerator/GenerateDatabase.cs:265`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:268`, `Seamless.Database/SourceGenerator/GenerateDatabase.cs:274`）。

## Review Gate: Code Internal

### Confirmed
- incremental generator で schema record を生成する。
- `DatabaseGenerator` marker と namespace 規約を使用して Keys/Records を集約する。
- unmanaged/managed storage を分離して MemoryPack 対応を生成する。

### Inferred
- confidence: medium。大量データ record のシリアライズ効率と型安全性を両立する意図がある。
- confidence: low。生成対象 namespace の規約はテストや利用例で補完される可能性がある。

### Unknown
- `DatabaseGenerator` marker の正式な利用者向け文書。
- schema 変更時の互換性、migration、versioning の方針。

### Questions
- 通常設計化時には schema namespace 規約をどのレベルで公開契約にするかを確認する。
# Source: Shared\Seamless.Linq.Enumerable\Methods1-generic-span-regression.md

# Methods1 繧ｸ繧ｧ繝阪Μ繝・け蛹悶〒 root 縺・Span 縺ｫ螻暮幕縺輔ｌ縺ｪ縺上↑縺｣縺滉ｻｶ

## 逞・憾

`Methods1` 繝吶Φ繝√・繝ｼ繧ｯ・・this.srcCount(N).AsX<T>().<Operator>(...)` 繧・LINQ 貍皮ｮ怜ｭ舌＃縺ｨ縺ｫ貂ｬ繧句渕蠎輔け繝ｩ繧ｹ・峨ｒ
**髱槭ず繧ｧ繝阪Μ繝・け `Methods1 : Base`・郁ｦ∫ｴ蝙・`int` 蝗ｺ螳夲ｼ俄・ 繧ｸ繧ｧ繝阪Μ繝・け `Methods1<T> : Base<T> where T:notnull`** 縺ｫ螟峨∴縺・
繧ｳ繝溘ャ繝・`985430c2` 莉･髯阪∝､壹￥縺ｮ貍皮ｮ怜ｭ舌・繝ｳ繝√′驕・￥縺ｪ縺｣縺溘・

菴捺─荳翫・蜴溷屏縺ｯ縲罫oot・磯・蛻・`T[]` / 繝ｪ繧ｹ繝・`List<T>`・峨′ `Span` 逶ｴ襍ｰ譟ｻ縺ｫ螻暮幕縺輔ｌ縺ｪ縺上↑縺｣縺溘阪％縺ｨ縲ゅ□縺・
**縺昴・譬ｹ譛ｬ縺ｯ隕∫ｴ蝙九′ `T` 縺ｫ縺ｪ縺｣縺溘％縺ｨ閾ｪ菴薙〒縺ｯ縺ｪ縺上：Linq 繧ｽ繝ｼ繧ｹ繧ｸ繧ｧ繝阪Ξ繝ｼ繧ｿ縺・`Methods1<T>` 縺ｮ繝｡繧ｽ繝・ラ繧・
蛯榊女蟇ｾ雎｡縺九ｉ荳ｸ縺斐→髯､螟悶＠縺ｦ縺・ｋ縺薙→**縺ｫ縺ゅｋ縲・

---

## 譬ｹ譛ｬ蜴溷屏

FLinq 縺ｯ `[LinqIntercept(true)]` 縺ｮ莉倥＞縺・`_Impl` 繝｡繧ｽ繝・ラ蜀・・ LINQ 繧偵，# 繧､繝ｳ繧ｿ繝ｼ繧ｻ繝励ち縺ｨ縺励※逕滓・縺励◆
鬮倬溽沿縺ｫ蟾ｮ縺玲崛縺医ｋ縲Ｓoot 縺碁・蛻・繝ｪ繧ｹ繝医↑繧・`ArrayRoot`/`ListRoot` 縺ｫ關ｽ縺｡縲～Span` 繧堤峩謗･襍ｰ譟ｻ縺吶ｋ繧ｳ繝ｼ繝峨↓縺ｪ繧九・

縺ｨ縺薙ｍ縺後√◎繧ゅ◎繧ゅ後◎縺ｮ繝｡繧ｽ繝・ラ繧貞ｍ蜿怜ｯｾ雎｡縺ｫ縺吶ｋ縺九阪ｒ豎ｺ繧√ｋ蜈･蜿｣縺ｮ繧ｲ繝ｼ繝医〒蠑ｾ縺九ｌ縺ｦ縺・ｋ縲・
[`LinqTargetDiscovery.cs`](../../Seamless.FLinq/SourceGenerator/LinqTargetDiscovery.cs) 縺ｮ驕ｸ蛻･譚｡莉ｶ:

```csharp
// LinqTargetDiscovery.cs
|| method.ContainingType.IsGenericType     // 竊・Methods1<T> 縺ｯ縺薙％縺ｧ true 竊・return false
// 繧ｸ繧ｧ繝阪Μ繝・け蝙九Γ繝ｳ繝舌・縺ｯ逕滓・繝｡繧ｽ繝・ラ蛛ｴ縺ｸ蝙九ヱ繝ｩ繝｡繝ｼ繧ｿ繧呈ｼ上ｉ縺励ｄ縺吶＞縺ｮ縺ｧ髯､螟悶・
```

- 螟画鋤蜑・`Methods1`・磯撼繧ｸ繧ｧ繝阪Μ繝・け・・ `ContainingType.IsGenericType == false` 竊・騾夐℃ 竊・蜷・`_Impl` 縺ｫ鬮倬溘う繝ｳ繧ｿ繝ｼ繧ｻ繝励ち縺檎函謌舌＆繧後ｋ縲・
- 螟画鋤蠕・`Methods1<T>`: `IsGenericType == true` 竊・**蜈ｨ `_Impl` 縺碁撼蛯榊女** 竊・逕滓・繧ｳ繝ｼ繝峨′ 1 縺､繧ょ・縺ｪ縺・竊・邏縺ｮ `System.Linq` 縺悟ｮ溯｡後＆繧後ｋ縲・

### 縺ｪ縺懊％縺ｮ繧ｲ繝ｼ繝医′蟄伜惠縺吶ｋ縺ｮ縺・

逕滓・縺輔ｌ縺溷ｍ蜿励Γ繧ｽ繝・ラ縺ｨ繝倥Ν繝代・ struct 縺ｯ縲∝・繧ｯ繝ｩ繧ｹ縺ｮ荳ｭ縺ｧ縺ｯ縺ｪ縺・*螟門・縺ｮ蛻･縺ｮ髱槭ロ繧ｹ繝磯撕逧・け繝ｩ繧ｹ**縺ｸ蜃ｺ蜉帙＆繧後ｋ
・・`LinqInterceptorSourceWriter.cs`](../../Seamless.FLinq/SourceGenerator/LinqInterceptorSourceWriter.cs) 縺ｮ
`繧､繝ｳ繧ｿ繝ｼ繧ｻ繝励ち繝ｼ蝙九た繝ｼ繧ｹ讒狗ｯ荏・峨ゅ％縺ｮ髱咏噪繧ｯ繝ｩ繧ｹ縺ｯ蜈・け繝ｩ繧ｹ縺ｮ蝙九ヱ繝ｩ繝｡繝ｼ繧ｿ `T` 繧貞ｮ｣險縺励※縺・↑縺・◆繧√・
繝ｬ繧ｷ繝ｼ繝仙梛 `Methods1<T>` 繧・ｦ∫ｴ蝙・`T` 繧貞錐謖・＠縺ｧ縺阪★縲√◎縺ｮ縺ｾ縺ｾ蜃ｺ蜉帙☆繧九→繧ｳ繝ｳ繝代う繝ｫ荳崎・縺ｫ縺ｪ繧九・
縺昴ｌ繧帝∩縺代ｋ縺溘ａ縺ｮ菫晏ｮ育噪縺ｪ蜃ｦ鄂ｮ縺後後ず繧ｧ繝阪Μ繝・け蜷ｫ譛牙梛縺ｯ荳ｸ縺斐→髯､螟悶阪〒縺ゅｋ縲・

### 隱､隗｣縺励ｄ縺吶＞轤ｹ

縲瑚ｦ∫ｴ縺・`T` 縺ｫ縺ｪ縺｣縺溘°繧・Span 縺ｫ螻暮幕縺ｧ縺阪↑縺上↑縺｣縺溘阪・**隱､繧・*縲ゅヵ繝ｩ繧ｰ繝｡繝ｳ繝域ｮｵ
・・`LinqFragmentBuilder.cs`](../../Seamless.FLinq/SourceGenerator/LinqFragmentBuilder.cs) 縺ｮ root 驕ｸ謚橸ｼ峨・縲・
繧ゅ＠蛯榊女縺ｾ縺ｧ蛻ｰ驕斐〒縺阪ｌ縺ｰ `T[]` 繧・`List<T>` 繧ょ撫鬘後↑縺・Span 蛹悶〒縺阪ｋ:

- `T[]` 縺ｯ `IArrayTypeSymbol` 縺ｫ荳閾ｴ 竊・`ArrayRoot`
- `List<T>` 縺ｯ `IsListType`・・OriginalDefinition` 豈碑ｼ・↑縺ｮ縺ｧ `List<int>` 繧・`List<T>` 繧ゆｸ閾ｴ・俄・ `ListRoot`
- 隕∫ｴ蝙句錐縺ｯ `"T"` 縺ｨ縺励※縺昴・縺ｾ縺ｾ繝ｬ繝ｳ繝繝ｪ繝ｳ繧ｰ縺輔ｌ繧・

縺､縺ｾ繧顔悄蝗縺ｯ**繝輔Λ繧ｰ繝｡繝ｳ繝域ｮｵ縺ｫ蛻ｰ驕斐☆繧句燕縺ｮ discovery 髯､螟・*縲ＡT` 縺昴・繧ゅ・縺ｧ縺ｯ縺ｪ縺・・
・井ｾ句､悶→縺励※縲〉oot 縺ｮ髱咏噪蝙九′縲碁・蛻・繝ｪ繧ｹ繝医阪〒縺ｯ縺ｪ縺・*陬ｸ縺ｮ蝙九ヱ繝ｩ繝｡繝ｼ繧ｿ `T`**・・ITypeParameterSymbol`・峨□縺｣縺溷ｴ蜷医・
隕∫ｴ蝙玖ｧ｣豎ｺ縺ｫ螟ｱ謨励＠縺ｦ蛻･邨瑚ｷｯ縺ｧ Span 荳榊庄縺ｫ縺ｪ繧九′縲｀ethods1 縺ｮ root 縺ｯ `T[]`/`List<T>` 縺ｪ縺ｮ縺ｧ縺薙ｌ縺ｫ縺ｯ隧ｲ蠖薙＠縺ｪ縺・ゑｼ・

---

## 螻暮幕繧ｳ繝ｼ繝峨〒隕九ｋ縲碁撼繧ｸ繧ｧ繝阪Μ繝・け=騾溘＞ / 繧ｸ繧ｧ繝阪Μ繝・け=驕・＞縲・

萓九→縺励※譌｢螳壹・驟榊・繝吶Φ繝・ｼ・AsX<T>(this T[]) => T[]`縲〉oot 縺ｯ `T[]`・峨・ `Aggregate_func` 繧剃ｽｿ縺・・
蜈・さ繝ｼ繝会ｼ・`Methods1.cs`](Methods1.cs)・・

```csharp
[LinqIntercept(true)]
private void Aggregate_func_Impl() {
    var result = this.srcCount(ImmediateFullScanCount).AsX<T>().Aggregate((acc,x) => Add(acc,x));
    ...
}
```

### 髱槭ず繧ｧ繝阪Μ繝・け譎ゑｼ磯溘＞・・ 逕滓・縺輔ｌ繧矩ｫ倬溘う繝ｳ繧ｿ繝ｼ繧ｻ繝励ち

`Methods1`・・int` 蝗ｺ螳夲ｼ峨〒縺ｯ `_Impl` 縺悟ｍ蜿励＆繧後〉oot 驟榊・縺・**struct 繧ｨ繝九Η繝｡繝ｬ繝ｼ繧ｿ・義Span` 逶ｴ襍ｰ譟ｻ**縺ｫ關ｽ縺｡繧九・
`obj/Release` 縺ｫ谿九ｋ譌ｧ荳紋ｻ｣縺ｮ繧ｹ繝翫ャ繝励す繝ｧ繝・ヨ `*Methods1_cs.g.cs` 縺ｯ縺ｾ縺輔↓縺薙・蠖｢・・int[] source`・・

```csharp
private struct OptimizedLinq_Aggregate_func_..._Struct1 : IValueEnumerator<int>, IEnumerator<int> {
    private int[] source;
    private int __e1 = -1;
    public bool TryGetSpan(out ReadOnlySpan<int> span) {
        span = MemoryExtensions.AsSpan(source, __e1 + 1);   // 笘・・蛻励ｒ Span 縺ｧ逶ｴ襍ｰ譟ｻ
        return true;
    }
    public bool TryGetNext(out int current) {
        if(++__e1 < source.Length) { current = source[__e1]; return true; }
        current = default!; return false;
    }
}

[global::System.Runtime.CompilerServices.InterceptsLocation(1, "...Methods1.cs...")]
public static void OptimizedLinq_Aggregate_func_Impl_...(this global::...Methods1 @this) {
    using var __s0 = new ...Struct1(/* 驟榊・ source */);
    if(__s0.TryGetSpan(out var __span)) {
        // 笘・｢・阜繝√ぉ繝・け譛蟆上・莉ｮ諠ｳ蜻ｼ縺ｳ蜃ｺ縺励ぞ繝ｭ繝ｻ繝懊ャ繧ｯ繧ｹ蛹悶ぞ繝ｭ縺ｮ Span 繝ｫ繝ｼ繝・
        var acc = __span[0];
        for(int i = 1; i < __span.Length; i++) { acc = acc + __span[i]; }
        ...
    }
}
```

`List<T>` root 縺ｮ蝣ｴ蜷医・ `ListRoot` 縺・
[`Root.cs`](../../Seamless.FLinq/SourceGenerator/Linq/Fragments/Root.cs) 縺ｧ
`CollectionsMarshal.AsSpan(source)` 繧剃ｽｿ縺・酔蝙九・ Span 繧ｳ繝ｼ繝峨ｒ蜃ｺ縺呻ｼ井ｻｮ諠ｳ蜻ｼ縺ｳ蜃ｺ縺励・繝懊ャ繧ｯ繧ｹ蛹悶↑縺励√う繝ｳ繝・ャ繧ｯ繧ｹ逶ｴ襍ｰ譟ｻ・峨・

### 繧ｸ繧ｧ繝阪Μ繝・け譎ゑｼ磯≦縺・ｼ・ 逕滓・繧､繝ｳ繧ｿ繝ｼ繧ｻ繝励ち縺後檎┌縺・・

`Methods1<T>` 縺ｧ縺ｯ discovery 縺ｧ髯､螟悶＆繧後ｋ縺溘ａ縲・*`.g.cs` 縺ｫ隧ｲ蠖薙Γ繧ｽ繝・ラ縺・1 縺､繧ら函謌舌＆繧後↑縺・*縲・
蜻ｼ縺ｳ蜃ｺ縺礼ｮ・園縺ｯ `_Impl` 縺ｮ譛ｬ菴薙ｒ縺昴・縺ｾ縺ｾ螳溯｡後☆繧・

```csharp
// Methods1<T>.Aggregate_func_Impl 縺ｯ蛯榊女縺輔ｌ縺壹∫ｴ縺ｮ LINQ 縺後◎縺ｮ縺ｾ縺ｾ襍ｰ繧・
var result = this.srcCount(N).AsX<T>().Aggregate((acc,x) => Add(acc,x));

// 螳滄圀縺ｫ襍ｰ繧九・縺ｯ:
//   System.Linq.Enumerable.Aggregate<T>(IEnumerable<T> source, Func<T,T,T> func)
//     竊・source.GetEnumerator() 縺瑚ｿ斐☆ IEnumerator<T> 繧・foreach・医う繝ｳ繧ｿ繝ｼ繝輔ぉ繝ｼ繧ｹ邨檎罰縺ｮ莉ｮ諠ｳ MoveNext/Current・・
//     竊・隕∫ｴ縺斐→縺ｫ Func<T,T,T> 繝・Μ繧ｲ繝ｼ繝亥他縺ｳ蜃ｺ縺・
//     竊・縺昴・荳ｭ縺ｧ豎守畑繝倥Ν繝代・ Add(=Value(Number(l)+Number(r))) 繧堤ｵ檎罰
//        ・亥､蝙・T 縺ｧ縺ｯ ToInt/FromInt 螟画鋤繧りｦ∫ｴ縺斐→縺ｫ逋ｺ逕滂ｼ・
//     竊・Span 逶ｴ襍ｰ譟ｻ縺ｯ荳蛻・┌縺・
```

`Add` 遲峨・隕∫ｴ蝙九ｒ豎守畑蛹悶☆繧九◆繧√・繝ｩ繝・ヱ・・`Methods1.cs`](Methods1.cs) 縺ｮ `Number`/`Value`/`Add` 窶ｦ・峨・
髱槭ず繧ｧ繝阪Μ繝・け譎ゅ・逕滓・繧ｳ繝ｼ繝牙・縺ｧ `acc + item`・・int` 逶ｴ蜉邂暦ｼ峨↓貎ｰ繧後※縺・◆縺後√ず繧ｧ繝阪Μ繝・け・矩撼蛯榊女縺ｧ縺ｯ
繝・Μ繧ｲ繝ｼ繝茨ｼ九・繝ｫ繝代・蜻ｼ縺ｳ蜃ｺ縺励′隕∫ｴ縺斐→縺ｫ谿九ｋ縲・

### 縺ｾ縺ｨ繧・ｼ医↑縺懷ｷｮ縺後▽縺上°・・

| | root 縺ｮ謇ｱ縺・| 蛻玲嫌 | 貍皮ｮ・|
|---|---|---|---|
| 髱槭ず繧ｧ繝阪Μ繝・け・亥ｍ蜿励≠繧奇ｼ・| `T[]`竊蛋ArrayRoot` 縺ｮ `Span` 逶ｴ襍ｰ譟ｻ | 繧､繝ｳ繝・ャ繧ｯ繧ｹ逶ｴ蜿ら・繝ｻ莉ｮ諠ｳ蜻ｼ縺ｳ蜃ｺ縺励ぞ繝ｭ | 逕滓・繧ｳ繝ｼ繝峨↓繧､繝ｳ繝ｩ繧､繝ｳ螻暮幕 |
| 繧ｸ繧ｧ繝阪Μ繝・け・亥ｍ蜿励↑縺暦ｼ・| `IEnumerable<T>` 縺ｨ縺励※邏縺ｮ LINQ | `IEnumerator<T>` 縺ｮ莉ｮ諠ｳ蜻ｼ縺ｳ蜃ｺ縺・| 繝・Μ繧ｲ繝ｼ繝茨ｼ区ｱ守畑繝倥Ν繝代・蜻ｼ縺ｳ蜃ｺ縺・|

---

## 蟇ｾ遲厄ｼ亥ｮ溯｣・ｸ医∩縺ｮ逕滓・蝎ｨ諡｡蠑ｵ・・

**逶ｮ逧・*: 繧ｸ繧ｧ繝阪Μ繝・け蜷ｫ譛牙梛 `Methods1<T>` 縺ｮ繝｡繝ｳ繝舌・縺ｧ繧ょｍ蜿励ｒ蠕ｩ豢ｻ縺輔○縲～ArrayRoot`/`ListRoot` 縺ｮ `Span` 鬮倬溯ｷｯ繧貞叙繧頑綾縺吶・

C# 繧､繝ｳ繧ｿ繝ｼ繧ｻ繝励ち縺ｮ蛻ｶ邏・ｸ翫∝ｍ蜿励Γ繧ｽ繝・ラ繧貞庶繧√ｋ髱咏噪繝帙Ν繝繝ｼ繧ｯ繝ｩ繧ｹ閾ｪ菴薙・繧ｸ繧ｧ繝阪Μ繝・け縺ｫ縺ｧ縺阪↑縺・・
縺昴％縺ｧ**蜷ｫ譛牙梛縺ｮ蝙九ヱ繝ｩ繝｡繝ｼ繧ｿ繧貞ｍ蜿励Γ繧ｽ繝・ラ蛛ｴ縺ｸ遘ｻ縺・*譁ｹ驥昴〒縲：Linq 逕滓・蝎ｨ繧呈ｬ｡縺ｮ縺ｨ縺翫ｊ諡｡蠑ｵ縺励◆・亥ｮ溯｣・ｸ医∩繝ｻ繝薙Ν繝臥｢ｺ隱肴ｸ医∩・・

1. **discovery 繧ｲ繝ｼ繝医・邱ｩ蜥・* 窶・[`LinqTargetDiscovery.cs`](../../Seamless.FLinq/SourceGenerator/LinqTargetDiscovery.cs) 縺ｮ
   `ContainingType.IsGenericType` 荳蠕矩勁螟悶ｒ繧・ａ縲√悟梛繝代Λ繝｡繝ｼ繧ｿ縺ｨ蛻ｶ邏・ｒ螳牙・縺ｫ蜀咲樟縺ｧ縺阪ｋ蜊倡ｴ斐ず繧ｧ繝阪Μ繝・け蜷ｫ譛牙梛縲阪°縺､
   縲梧悽菴・LINQ 縺ｫ繝ｩ繝繝繧貞性縺ｾ縺ｪ縺・榊ｴ蜷医↓髯舌ｊ險ｱ蜿ｯ・・蜀咲樟蜿ｯ閭ｽ縺ｪ繧ｸ繧ｧ繝阪Μ繝・け蜷ｫ譛牙梛縺義・峨・
2. **蜷ｫ譛牙梛縺ｮ蝙九ヱ繝ｩ繝｡繝ｼ繧ｿ・句宛邏・ｒ謐墓拷** 窶・`method.ContainingType.TypeParameters`・亥錐蜑阪→ `notnull`/`class`/`struct`/`new()` 遲峨・蛻ｶ邏・ｼ峨ｒ
   謐墓拷縺・`蝙九ヱ繝ｩ繝｡繝ｼ繧ｿ繝ｼ蛻ｶ邏・ｯ逕滓・` 縺ｧ `where` 遽繧堤函謌舌・
3. **蛯榊女鄂ｲ蜷阪∈蜿肴丐** 窶・逕滓・繝｡繧ｽ繝・ラ繧・`...<T>(this Methods1<T> @this) where T:notnull` 縺ｮ**繧ｸ繧ｧ繝阪Μ繝・け繝｡繧ｽ繝・ラ**縺ｨ縺励※螳｣險
   ・医・繝ｫ繝繝ｼ繧ｯ繝ｩ繧ｹ縺ｯ髱槭ず繧ｧ繝阪Μ繝・け縺ｮ縺ｾ縺ｾ縲・`LinqInterceptorSourceWriter.cs`](../../Seamless.FLinq/SourceGenerator/LinqInterceptorSourceWriter.cs)・峨・
4. **繝倥Ν繝代・ struct 縺ｮ `<T>` 蛹・* 窶・`OuterTypeParameters` 繧呈妙迚・ヤ繝ｪ繝ｼ縺ｸ莨晄眺縺暦ｼ・PropagateContainingTypeName`/`PropagateOuterTypeParameters`・峨・
   Root/Scalar/Set/Concat/Zip/ToArray 蜷・妙迚・・ struct 螳｣險縺ｨ `EnumeratorTypeName` 縺ｫ螟門・蝙九ヱ繝ｩ繝｡繝ｼ繧ｿ繧貞燕鄂ｮ縲・

縺薙・ 1縲・ 縺ｫ繧医ｊ縲・・蛻励・繝ｳ繝・ｼ・Benchmark/Benchmark.System.Linq.Enumerable.FLinq`・峨〒縺ｯ `Methods1<T>` 蜷代￠縺ｫ **38 蛟九・繧ｸ繧ｧ繝阪Μ繝・け蛯榊女繝｡繧ｽ繝・ラ**縺・
逕滓・縺輔ｌ・井ｿｮ豁｣蜑阪・ 0・峨～MemoryExtensions.AsSpan` 繧堤畑縺・◆ root竊担pan 逶ｴ襍ｰ譟ｻ繧ｳ繝ｼ繝峨′蜃ｺ蜉帙＆繧後ｋ・茨ｼ晄ｧ矩縺ｨ縺励※縺ｯ鬮倬溯ｷｯ縺悟ｾｩ豢ｻ縺吶ｋ・峨・

## 縺溘□縺・.NET 10 縺ｮ UnsafeAccessor 蛻ｶ髯舌〒螳溯｡梧凾縺ｫ關ｽ縺｡繧具ｼ磯㍾隕・ｼ・

荳願ｨ倥〒繧ｳ繝ｳ繝代う繝ｫ縺ｯ騾壹ｋ縺後・*螳溯｡梧凾縺ｫ `System.MissingMethodException` / `MissingFieldException`** 縺檎匱逕溘☆繧九・
蜴溷屏縺ｯ **.NET 10 縺ｮ `[UnsafeAccessor]` 縺後後ず繧ｧ繝阪Μ繝・け蝙九・髱槫・髢九Γ繝ｳ繝舌・縲阪ｒ隗｣豎ｺ縺ｧ縺阪↑縺・*縺薙→・・ethod/static/field 縺・★繧後ｂ荳榊庄縲・
髱槭ず繧ｧ繝阪Μ繝・け蝙九↑繧牙庄・峨よ怙蟆丞・迴ｾ縺ｧ遒ｺ隱肴ｸ医∩:

```csharp
public class Base<T> where T:notnull { protected int M(int n) => n; }

[UnsafeAccessor(UnsafeAccessorKind.Method, Name="M")]
static extern int Acc<T>(Base<T> @this, int n) where T:notnull;

Acc<int>(new Base<int>(), 1);   // 竊・MissingMethodException: 'Base`1.M'
```

`Methods1<T>` 縺ｮ蜷・ｼ皮ｮ怜ｭ舌・ `srcCount`/`consumer`/`Result`/`MidpointValue` 窶ｦ 縺ｨ縺・▲縺・**`Base<T>`/`Methods1<T>` 縺ｮ
protected/private 繝｡繝ｳ繝舌・**繧貞ｿ・★隗ｦ繧九◆繧√∫函謌舌＆繧後◆蛯榊女繝｡繧ｽ繝・ラ縺ｯ蜈ｨ貊・☆繧九・

### 迴ｾ迥ｶ縺ｮ螳牙・遲厄ｼ亥ｮ溯｣・ｸ医∩・・

逕滓・蝎ｨ縺ｯ縲後ず繧ｧ繝阪Μ繝・け蝙九・髱槫・髢九Γ繝ｳ繝舌・逕ｨ繧｢繧ｯ繧ｻ繧ｵ・茨ｼ昴ず繧ｧ繝阪Μ繝・け UnsafeAccessor・峨阪ｒ蜃ｺ縺吝ｿ・ｦ√′逕溘§縺滓凾轤ｹ縺ｧ萓句､悶ｒ謚輔￡縲・
縺昴・貍皮ｮ怜ｭ舌・蛯榊女繧定ｫｦ繧√※**蜈・ｮ溯｣・ｼ育ｴ縺ｮ `System.Linq`縲・≦縺・′豁｣縺励＞・峨∈繝輔か繝ｼ繝ｫ繝舌ャ繧ｯ**縺吶ｋ・・繧｢繧ｯ繧ｻ繧ｵ蝙九ヱ繝ｩ繝｡繝ｼ繧ｿ` 縺ｮ繧ｬ繝ｼ繝会ｼ峨・
縺薙ｌ縺ｫ繧医ｊ縲後さ繝ｳ繝代う繝ｫ縺ｯ騾壹ｋ縺悟ｮ溯｡梧凾 throw縲阪→縺・≧謔ｪ蛹悶ｒ驕ｿ縺代※縺・ｋ縲ょ､豈碑ｼ・ユ繧ｹ繝・`Methods1_results_match` 縺ｯ蜷域ｼ縺吶ｋ縲・

邨先棡縺ｨ縺励※縲・*縺薙・ .NET 蛻ｶ髯舌′隗｣縺代ｋ縺九・繝ｫ繝代・繧貞・髢九☆繧九∪縺ｧ縲～Methods1<T>` 縺ｮ騾溷ｺｦ縺ｯ謌ｻ繧峨↑縺・*・亥ｮ牙・縺ｫ驕・＞縺ｾ縺ｾ・峨・

## 騾溷ｺｦ繧貞ｮ滄圀縺ｫ謌ｻ縺咎∈謚櫁い・郁ｦ∝愛譁ｭ・・

- **(A) 繝倥Ν繝代・繝｡繝ｳ繝舌・繧・internal 莉･荳翫↓蜈ｬ髢・*: 蛯榊女繧ｳ繝ｼ繝峨・蜷御ｸ繧｢繧ｻ繝ｳ繝悶Μ縺ｫ逕滓・縺輔ｌ繧九・縺ｧ縲～srcCount`/`GetOrCreateByLength`/
  `CreateSourceArray`/`consumer`/`Result`/`MidpointValue`/`src2Count`/`CountForComparison`/`Value` 窶ｦ 貍皮ｮ怜ｭ舌′隗ｦ繧・*謗ｨ遘ｻ逧・哩蛹・☆縺ｹ縺ｦ**繧・
  internal・医° public・峨↓縺吶ｌ縺ｰ UnsafeAccessor 繧剃ｻ九＆縺夂峩謗･蜻ｼ縺ｹ縲∝ｍ蜿暦ｼ鬼pan 縺梧怏蜉ｹ蛹悶☆繧九ゅ・繝ｳ繝∝渕逶､縺ｮ蜿ｯ隕匁ｧ繧貞ｺ・￥螟峨∴繧九・
- **(B) `Methods1` 繧帝撼繧ｸ繧ｧ繝阪Μ繝・け縺ｸ謌ｻ縺・*・医さ繝溘ャ繝・`985430c2` 縺ｮ騾・ｼ・ 蜈・・鬮倬溯ｷｯ縺悟叉蠕ｩ豢ｻ縲ゅず繧ｧ繝阪Μ繝・け蜈ｱ譛峨・螟ｱ縺・・
- **(C) 迴ｾ迥ｶ邯ｭ謖・*: `Methods1<T>` 縺ｯ驕・＞縺梧ｭ｣縺励＞縺ｾ縺ｾ縲・NET 蛛ｴ縺ｮ UnsafeAccessor 繧ｸ繧ｧ繝阪Μ繝・け蟇ｾ蠢懊ｒ蠕・▽縲・

### 蜊ｴ荳九＠縺滉ｻ｣譖ｿ譯・

- **蝙九ヱ繝ｩ繝｡繝ｼ繧ｿ繧偵け繝ｩ繧ｹ竊偵Γ繧ｽ繝・ラ縺ｸ遘ｻ蜍・*: `_Impl` 縺ｯ蠑墓焚繧貞叙繧峨★ `T` 縺ｯ繧ｯ繝ｩ繧ｹ迥ｶ諷具ｼ・T[]` 驟榊・縺ｪ縺ｩ・峨↓螻槭☆繧九◆繧∵・遶九＠縺ｪ縺・・
  縺輔ｉ縺ｫ蠑墓焚縺ｫ蝙九ヱ繝ｩ繝｡繝ｼ繧ｿ縺梧ｮ九ｋ縺ｨ蛻･縺ｮ髯､螟匁擅莉ｶ・・ContainsTypeParameter`・峨〒蜀榊ｺｦ蠑ｾ縺九ｌ繧九・

### 繧ｨ繝薙ョ繝ｳ繧ｹ

`Benchmark/Benchmark.System.Linq.Enumerable.FLinq/obj/Release` 縺ｫ谿九ｋ譌ｧ `*Methods1_cs.g.cs` 繧ｹ繝翫ャ繝励す繝ｧ繝・ヨ縺ｯ縺・★繧後ｂ髱槭ず繧ｧ繝阪Μ繝・け迚・
・・int[] source` 縺ｮ Span 迚茨ｼ峨〒縲∫樟陦・`Methods1<T>` 蠖｢繧呈拷縺医◆繧ゅ・縺ｯ辟｡縺・ｼ挌enericization 蠕後・蛯榊女繧ｳ繝ｼ繝峨′蜃ｺ縺ｦ縺・↑縺九▲縺溘％縺ｨ縺ｮ陬丈ｻ倥￠縲・
