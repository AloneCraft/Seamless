# Tsavorite.Linq.Enumerable 遘ｻ陦瑚ｨｭ險・
## 閭梧勹

`System.Linq.Enumerable` 縺ｯ `System.Collections.Generic.IEnumerable<T>` 繧剃ｸｭ蠢・↓縺励◆ LINQ API 縺ｧ縺ゅｋ縲ゆｸ譁ｹ縲～Tsavorite.Collections` 縺ｫ縺ｯ Tsavorite backed 縺ｪ `IHashSet<T>`/`IDictionary<TKey,TValue>`/memory-mapped `List<T>` 縺後≠繧翫√％繧後ｉ縺ｫ縺ｯ Tsavorite 縺ｮ storage/session/scan 繧貞燕謠舌↓縺励◆ LINQ API 縺悟ｿ・ｦ√↓縺ｪ繧九・
迴ｾ迥ｶ縺ｯ `Tsavorite.Linq.HashSet` 縺・`IHashSet<T>` 蜷代￠ LINQ API 縺ｫ逶ｸ蠖薙＠縺ｦ縺・ｋ縺後∝錐蜑阪′蟇ｾ雎｡蝙九↓蟇・ｊ縺吶℃縺ｦ縺翫ｊ縲～System.Linq.Enumerable` 縺ｫ蟇ｾ蠢懊☆繧・API 縺ｨ縺励※縺ｯ `Tsavorite.Linq.Enumerable` 縺碁←蛻・〒縺ゅｋ縲・
## 迴ｾ迥ｶ縺ｮ隕ｳ蟇・
- `src/Tsavorite.Collections/Linq/HashSet.cs` 縺ｯ `namespace Tsavorite.Linq; public static partial class HashSet` 縺ｨ縺励※蠎・＞ LINQ API 繧呈署萓帙＠縺ｦ縺・ｋ縲・- `HashSet.cs` 縺ｮ螟壹￥縺ｮ iterator 縺ｯ `global::System.Collections.Generic.List<T>` / `HashSet<T>` / `Dictionary<TKey,TValue>` 縺ｫ譚先侭蛹悶＠縺ｦ縺九ｉ霑斐＠縺ｦ縺翫ｊ縲∝､ｧ螳ｹ驥丈ｸｭ髢薙ヰ繝・ヵ繧｡遖∵ｭ｢譁ｹ驥昴→陦晉ｪ√☆繧九・- `src/Tsavorite.Collections/Collections/IHashSet.cs` 縺ｮ譌｢螳・`IntersectWith` / `SymmetricExceptWith` 繧・`System.Collections.Generic.List<T>` 繧剃ｸｭ髢薙↓菴ｿ縺｣縺ｦ縺・ｋ縲・- `Collection.Default.cs` 縺ｮ `Enumerator` 縺ｯ `IterateLookup` 邨先棡繧・`InMemoryList<T>` 縺ｫ蜈ｨ莉ｶ譬ｼ邏阪＠縺ｦ縺九ｉ蛻玲嫌縺励※縺翫ｊ縲ゝsavorite 縺ｮ荳ｦ蛻玲ｧ縺ｨ螟ｧ螳ｹ驥丞・謖吶・隕ｳ轤ｹ縺ｧ隕狗峩縺怜ｯｾ雎｡縲・- `src/FLinq/SourceGenerator` 縺ｯ `System.Linq.Enumerable` 蝗ｺ螳壼愛螳壹′螟壹＞縲ゆｻ｣陦ｨ萓九・ `LinqTargetDiscovery.cs`, `LinqMethodBodyRewriter.cs`, `LinqCounterOnlyMethodBodyRewriter.cs`, `LinqFragmentBuilder.cs`, `LinqGeneratorModels.cs`, 蜷・`Fragments`縲・- `tests/Test.Tsavorite.Collections/Intercept/System/Linq` 縺ｫ縺ｯ Chain1/Chain2/FragmentCoverage/Optimization 縺ｮ雎雁ｯ後↑繝・せ繝医′縺ゅｋ縲・- `tests/Test.Tsavorite.Collections/Intercept/Tsavorite/Linq` 縺ｫ縺ｯ譌｢蟄・`Tsavorite.Linq.HashSet` 蜷代￠繝・せ繝医′縺ゅｋ縲・- `sandbox/Benchmark.Shared` 縺ｯ蜈ｱ騾壹た繝ｼ繧ｹ蛹悶・荳句慍縺ｨ縺励※菴ｿ縺医ｋ縲・- `src/Tsavorite.Collections/Tsavorite.Collections.csproj` 縺ｯ `Microsoft.Garnet 2.0.1-beta.2`縲～src/Seamless.Data/Seamless.Data.csproj` 縺ｯ `2.0.0-beta.3` 繧貞盾辣ｧ縺励※縺・◆縲よ婿驥晉｢ｺ隱阪↓繧医ｊ縲√←縺｡繧峨ｂ NuGet 荳翫・譛譁ｰ prerelease `2.0.1-beta.3` 縺ｸ譖ｴ譁ｰ縺吶ｋ縲・- Garnet/Tsavorite 縺ｯ read/upsert/delete/RMW 繧剃ｸｭ蠢・↓縺励◆ narrow-waist storage API 繧呈署萓帙☆繧九√→縺・≧蜈ｬ髢玖ｪｬ譏弱′縺ゅｋ縲・- `Tsavorite.Collections.HashSet<TKey>` 縺ｯ `AutoSerializer<TKey>` 繧剃ｽｿ縺｣縺ｦ縺翫ｊ縲～RuntimeHelpers.IsReferenceOrContainsReferences<T>()` 縺ｫ蠢懊§縺ｦ MemoryPack 縺ｾ縺溘・ blittable span 繧帝∈縺ｶ縲ゅ＠縺溘′縺｣縺ｦ receiver 縺梧里蟄・`HashSet<T>` 縺ｮ蝣ｴ蜷医［anaged/unmanaged serializer 蟾ｮ縺ｯ LINQ 螻暮幕縺ｮ荳ｻ縺ｪ蛻ｶ邏・〒縺ｯ縺ｪ縺・・
## 逶ｮ逧・
- `Tsavorite.Linq.Enumerable` 繧呈ｭ｣隕・API 縺ｫ縺吶ｋ縲・- 譌｢蟄・`Tsavorite.Linq.HashSet` 縺ｯ莠呈鋤繝ｬ繧､繝､繝ｼ縺ｫ遘ｻ縺吶・- `Tsavorite.Collections.HashSet<T>` / `IHashSet<T>` 繧・generator 縺ｮ intercept 蟇ｾ雎｡縺ｫ縺吶ｋ縲・- System LINQ 逕ｨ縺ｮ譌｢蟄倥ユ繧ｹ繝医ｒ縺ｧ縺阪ｋ縺縺大・譛峨＠縲∝酔縺伜・蜉帙た繝ｼ繧ｹ繧・System/Tsavorite 縺ｮ荳｡譁ｹ縺ｫ驕ｩ逕ｨ縺ｧ縺阪ｋ讒矩縺ｫ縺吶ｋ縲・- 螟ｧ螳ｹ驥丈ｸｭ髢薙ヰ繝・ヵ繧｡縺ｨ縺励※ `System.Collections.Generic` 繧剃ｽｿ繧上↑縺・・- Tsavorite 縺ｮ storage/session/scan/RMW 蜑肴署繧貞｣翫＆縺壹∽ｸｦ蛻玲ｧ繧呈ｮｺ縺吝・莉ｶ繝ｭ繝・け繧・・莉ｶ繝｡繝｢繝ｪ譚先侭蛹悶ｒ驕ｿ縺代ｋ縲・
## 髱樒岼逧・
- `System.Linq.Enumerable` 縺ｮ蜈ｨ API 繧剃ｸ蠎ｦ縺ｧ螳悟・莠呈鋤縺ｫ縺吶ｋ縺薙→縲・- 繝・せ繝医さ繝ｼ繝牙・縺ｮ蟆剰ｦ乗ｨ｡譛溷ｾ・､逕滓・繧・ｭ｣隕丞喧縺ｾ縺ｧ螳悟・縺ｫ `System.Collections.Generic` 遖∵ｭ｢縺ｫ縺吶ｋ縺薙→縲・- `List<T>` 繧・Tsavorite backed 縺ｫ螟画峩縺吶ｋ縺薙→縲ＡList<T>` 縺ｯ memory-mapped file 縺ｮ縺ｾ縺ｾ謇ｱ縺・・- 譌｢蟄伜・髢・API 繧貞叉譎ょ炎髯､縺吶ｋ縺薙→縲・
## 謠先｡郁ｨｭ險・
1. API 蜷・   - `namespace Tsavorite.Linq; public static partial class Enumerable` 繧定ｿｽ蜉縺吶ｋ縲・   - 螳溯｣・ヵ繧｡繧､繝ｫ縺ｯ `Enumerable.HashSet.cs`, `Enumerable.Dictionary.cs`, `Enumerable.List.cs`, `Enumerable.Set.cs`, `Enumerable.Internal.*.cs` 縺ｫ蛻・￠繧九・   - `Tsavorite.Linq.HashSet` 縺ｯ莠呈鋤逕ｨ partial class 縺ｨ縺励※谿九＠縲∝庄閭ｽ縺ｪ遽・峇縺ｧ `Enumerable` 縺ｫ蟋碑ｭｲ縺吶ｋ縲・
2. Tsavorite LINQ 縺ｮ謌ｻ繧雁､
   - 蜊倡ｴ斐↑ `Where` / `Select` / `Take` 縺ｪ縺ｩ縺ｯ驕・ｻｶ蛻玲嫌縺ｧ縺阪ｋ蟆ら畑 enumerable/enumerator 繧定ｿ斐☆縲・   - set/join/group/order 邉ｻ縺ｧ迥ｶ諷九′蠢・ｦ√↑繧ゅ・縺ｯ縲～System.Collections.Generic` 縺ｮ螟ｧ螳ｹ驥上ヰ繝・ヵ繧｡縺ｧ縺ｯ縺ｪ縺・Tsavorite backed `HashSet`/`Dictionary` 縺ｾ縺溘・ memory-mapped `List<T>` 繧剃ｽｿ縺・・   - `ToHashSet`/`ToDictionary` 縺ｯ Tsavorite collection 繧定ｿ斐☆ overload 繧定ｿｽ蜉縺吶ｋ縲４ystem 莠呈鋤蜷阪′譖匁乂縺ｫ縺ｪ繧句ｴ蜷医・ `ToTsavoriteHashSet` / `ToTsavoriteDictionary` 繧剃ｽｵ險ｭ縺吶ｋ縲・
3. 襍ｰ譟ｻ蝓ｺ逶､
   - 迴ｾ蝨ｨ縺ｮ `IEnumerable<T>` enumerator 縺ｯ蜈ｨ莉ｶ `InMemoryList<T>` 縺ｫ遨阪・縺溘ａ縲（ntercept 縺ｨ蟆ら畑貍皮ｮ励〒縺ｯ `ForEach<TCollection,TStrategy>` / `IScanIteratorFunctions` 繝吶・繧ｹ縺ｮ繧ｹ繝医Μ繝ｼ繝溘Φ繧ｰ襍ｰ譟ｻ繧貞━蜈医☆繧九・   - `IHashSet<T>` 縺ｮ髱咏噪蝙九〒繧ゅ～IEnumerable<T>` 縺ｨ蜷後§縺・`GetEnumerator()` 繧・root 縺ｫ縺励◆ LINQ-level 螻暮幕縺ｯ蜿ｯ閭ｽ縺ｨ縺吶ｋ縲・   - storage-level 螻暮幕縺ｧ縺ｯ `IHashSet<T>` 縺ｮ `foreach` 縺ｫ謌ｻ縺輔★縲ゝsavorite 蟆ら畑 scan strategy 繧貞他縺ｶ繧ｳ繝ｼ繝峨ｒ逕滓・縺吶ｋ縲・   - storage-level 螻暮幕繧定ｦ∵ｱゅ☆繧狗ｵ瑚ｷｯ縺ｧ蟇ｾ蠢懊〒縺阪↑縺・ｴ蜷医・譌｢蟄・enumerator 邨檎罰縺ｸ鮟吶▲縺ｦ謌ｻ縺輔★縲∽ｾ句､悶ｒ逕滓・縺吶ｋ縲ょｮ悟・螻暮幕縺ｧ縺阪↑縺・LINQ 縺ｯ繝・せ繝医〒讀懷・縺ｧ縺阪ｋ迥ｶ諷九↓縺吶ｋ縲・
4. Source Generator
   - `System.Linq.Enumerable` 蝗ｺ螳壼愛螳壹ｒ `LinqMethodFamily` 縺ｮ繧医≧縺ｪ蜀・Κ descriptor 縺ｫ鄂ｮ縺肴鋤縺医ｋ縲・   - family 縺ｯ譛菴朱剞 `SystemEnumerable` 縺ｨ `TsavoriteEnumerable` 繧呈戟縺､縲・- `TsavoriteEnumerable` 縺ｯ `Tsavorite.Linq.Enumerable` 縺ｮ reduced extension call縲√°縺､ receiver 縺・`Tsavorite.Collections.IHashSet<T>` 縺ｾ縺溘・蜈ｷ雎｡ `Tsavorite.Collections.HashSet<T>` 縺ｮ繧ゅ・繧貞ｯｾ雎｡縺ｫ縺吶ｋ縲・   - 蛻晄悄蟇ｾ蠢懊・ `IHashSet<T>` 縺ｮ LINQ-level 螻暮幕縺ｨ縲∝・雎｡ `Tsavorite.Collections.HashSet<T>` 縺ｮ storage-level 螻暮幕繧貞・縺代※謇ｱ縺・・   - 譛蛻昴・螳溯｣・ｯｾ雎｡縺ｯ `HashSet<T>` receiver 縺ｮ scalar 邉ｻ縺ｨ縺吶ｋ縲ょｯｾ雎｡縺ｯ `Any` / `All` / `Count` / `LongCount` / `Contains` / `First` / `FirstOrDefault` / `Aggregate`縲・   - `Where` / `Select` / `ToArray` 縺ｪ縺ｩ蛻玲嫌邨先棡繧定ｿ斐☆繧ゅ・縺ｯ縲´INQ-level 螻暮幕縺ｪ繧・`GetEnumerator()` root 縺ｧ蛻晄悄蟇ｾ蠢懊〒縺阪ｋ縲Ｔtorage-level 螻暮幕縺ｧ縺ｯ scan callback 縺ｨ邨先棡 enumerable 縺ｮ蟇ｿ蜻ｽ邂｡逅・ｒ蛻･騾碑ｨｭ險医＠縲∝・莉ｶ `GetEnumerator()` fallback 縺ｯ菴ｿ繧上↑縺・・   - `GroupBy` / `Join` / `OrderBy` / `Distinct` / `Except` / `Intersect` / `Union` 縺ｯ蟆ら畑 storage-backed 荳ｭ髢捺ｧ矩縺梧紛縺｣縺ｦ縺九ｉ谿ｵ髫主ｯｾ蠢懊☆繧九・
5. 繝・せ繝・繝吶Φ繝∝・騾壼喧
   - 蜈ｱ騾壹・繝・せ繝医す繝翫Μ繧ｪ縺ｯ `Shared` 蛹悶＠縲《ource factory 繧貞ｷｮ縺玲崛縺医ｋ縲・   - System 迚医・ `int[]` / `System.Collections.Generic.HashSet<T>` 繧定ｿ斐☆ factory縲・   - Tsavorite 迚医・ `Store.Create().Create<T>().CreateOwned()` 縺ｧ `Tsavorite.Collections.HashSet<T>` 繧定ｿ斐☆ factory縲・   - using/alias 縺ｧ `Enumerable` 縺ｮ隗｣豎ｺ蜈医ｒ蛻・ｊ譖ｿ縺医ｋ譯医ｒ菴ｿ縺・ゅ◆縺縺・extension method 縺ｮ隗｣豎ｺ陦晉ｪ√′蜃ｺ繧狗ｮ・園縺ｯ譏守､ｺ static call 縺ｾ縺溘・ namespace 蛻・屬縺ｫ縺吶ｋ縲・   - `sandbox/Benchmark.Shared` 縺ｯ豁｣蠑上↑蜈ｱ譛峨・繝ｭ繧ｸ繧ｧ繧ｯ繝医∪縺溘・ `Directory.Build.props` 縺九ｉ縺ｮ Compile Include 縺ｫ遘ｻ縺吶°縲∝ｰ代↑縺上→繧・`src/Benchmark` 縺ｨ `tests/Test.Tsavorite.Collections.Benchmark` 縺悟酔縺倥た繝ｼ繧ｹ繧貞盾辣ｧ縺吶ｋ讒区・縺ｫ縺吶ｋ縲・
6. 譛譁ｰ Tsavorite API 蟇ｾ蠢・   - `Microsoft.Garnet 2.0.1-beta.3` 縺ｫ荳翫￡縲～Tsavorite.core.xml` 縺ｨ reflection 縺ｧ `TsavoriteKV`, `ClientSession`, `IScanIteratorFunctions`, `IterateLookup`, `NewSession`, `BasicContext` 縺ｮ菴ｿ逕ｨ邂・園繧堤｢ｺ隱阪☆繧九・   - API 逶ｴ謗･萓晏ｭ倥ｒ `Collection` 蜀・Κ縺ｮ阮・＞ scan/session helper 縺ｫ髮・ｴ・＠縲∵ｬ｡縺ｮ beta 縺ｧ縺ｮ蟾ｮ蛻・ｒ螻謇蛹悶☆繧九・
## 螻暮幕蜿ｯ閭ｽ諤ｧ縺ｮ蛻・｡・
螻暮幕縺ｯ谺｡縺ｮ 2 谿ｵ髫弱↓蛻・￠縺ｦ謇ｱ縺・・
- LINQ-level 螻暮幕: `IHashSet<T>` 縺ｮ髱咏噪蝙九〒繧・`IEnumerable<T>` 縺ｨ蜷後§縺上～GetEnumerator()` 繧・root 縺ｫ縺励※ LINQ iterator/delegate 繧堤函謌舌さ繝ｼ繝峨・繝ｫ繝ｼ繝励∈逡ｳ縺ｿ霎ｼ繧縲ゅ％繧後・蜿ｯ閭ｽ縺ｧ縺ゅｊ縲∝・譛溷ｯｾ蠢懊↓蜷ｫ繧√ｋ縲・- storage-level 螻暮幕: `Tsavorite.Collections.HashSet<T>` 縺ｾ縺溘・蟆ら畑 scan interface 縺九ｉ逶ｴ謗･襍ｰ譟ｻ縺励～IEnumerable<T>` fallback 繧・､ｧ螳ｹ驥・`System.Collections.Generic` 荳ｭ髢捺攝譁吝喧繧剃ｽｿ繧上↑縺・ゅ％繧後ｒ譛邨ら噪縺ｪ螳悟・螻暮幕縺ｨ蜻ｼ縺ｶ縲・
### 蛻晄悄蟇ｾ蠢懷呵｣・
莉･荳九・迥ｶ諷九ｒ縺ｻ縺ｼ謖√◆縺ｪ縺・√∪縺溘・螳壽焚繧ｵ繧､繧ｺ縺ｮ迥ｶ諷九□縺代〒襍ｰ譟ｻ縺ｧ縺阪ｋ縺溘ａ縲～IHashSet<T>` 縺ｮ LINQ-level 螻暮幕縺ｫ繧ゅゝsavorite scan strategy 縺ｸ縺ｮ storage-level 螻暮幕縺ｫ繧り誠縺ｨ縺苓ｾｼ縺ｿ繧・☆縺・・
- `Where`
- `Select`
- `Any`
- `All`
- `Count`
- `LongCount`
- `Contains`
- `Aggregate`
- `First`
- `FirstOrDefault`
- `Single`
- `SingleOrDefault`
- `ElementAt`
- `ElementAtOrDefault`
- `Last`
- `LastOrDefault`
- `Sum`
- `Average`
- `Min`
- `Max`
- `MinBy`
- `MaxBy`
- `Take`
- `Skip`
- `TakeWhile`
- `SkipWhile`
- `Append`
- `Prepend`
- `Concat`
- `DefaultIfEmpty`
- `Index`
- `Zip`
- `SequenceEqual`
- `Cast`
- `OfType`

`ToArray` 縺ｯ譛邨らｵ先棡縺碁・蛻励〒縺ゅｋ縺溘ａ萓句､也噪縺ｫ險ｱ蜿ｯ縺吶ｋ縲ＡToList` 縺ｯ System `List<T>` 縺ｧ縺ｯ縺ｪ縺・memory-mapped `Tsavorite.Collections.List<T>` 繧定ｿ斐☆ overload 繧定ｿｽ蜉縺吶ｋ蝣ｴ蜷医↓蛻晄悄蟇ｾ蠢懷呵｣懊↓蜈･繧後ｋ縲・
### Tsavorite-backed 荳ｭ髢捺ｧ矩縺悟ｿ・ｦ・
莉･荳九・迴ｾ陦・System LINQ fragment 縺・`System.Collections.Generic.List/HashSet/Dictionary/Queue` 繧貞､ｧ螳ｹ驥丈ｸｭ髢捺ｧ矩縺ｨ縺励※逕滓・縺吶ｋ縺溘ａ縲√◎縺ｮ縺ｾ縺ｾ縺ｧ縺ｯ Tsavorite 迚医・螳悟・螻暮幕縺ｨ縺ｯ縺ｿ縺ｪ縺輔↑縺・５savorite backed `HashSet`/`Dictionary` 縺ｾ縺溘・ memory-mapped `List<T>` 縺ｸ鄂ｮ謠帙＠縺ｦ縺九ｉ蟇ｾ蠢懊☆繧九・
- `Distinct`
- `DistinctBy`
- `Except`
- `ExceptBy`
- `Intersect`
- `IntersectBy`
- `Union`
- `UnionBy`
- `Order`
- `OrderBy`
- `OrderDescending`
- `OrderByDescending`
- `ThenBy`
- `ThenByDescending`
- `GroupBy`
- `GroupJoin`
- `Join`
- `LeftJoin`
- `RightJoin`
- `Reverse`
- `Shuffle`
- `SkipLast`
- `TakeLast`
- `ToHashSet`
- `ToDictionary`
- `ToLookup`
- `CountBy`
- `AggregateBy`

### 蟇ｾ雎｡螟悶∪縺溘・迚ｹ谿頑桶縺・
- `AsQueryable`: LINQ to Objects 縺ｮ螻暮幕縺ｧ縺ｯ縺ｪ縺・`IQueryable` 蛹悶↑縺ｮ縺ｧ縲∝・譛溷ｯｾ蠢懷ｯｾ雎｡螟悶・- `TryGetNonEnumeratedCount`: LINQ chain fragment 縺ｧ縺ｯ縺ｪ縺・count 蜿門ｾ・API 縺ｨ縺励※縲～HashSet<T>.EntryCount` / `GetLongCount` / 蟆ら畑 count helper 縺ｫ逶ｴ謗･關ｽ縺ｨ縺吶・- `Empty`, `Range`, `Repeat`, `InfiniteSequence`, `Sequence`: collection receiver 繧呈戟縺溘↑縺・root generator縲５savorite collection 逕ｨ縺ｧ縺ｯ縺ｪ縺・root sequence 縺ｨ縺励※謇ｱ縺・・
### 髱咏噪蝙九↓繧医ｋ蛻ｶ邏・
receiver 縺ｮ髱咏噪蝙九′ `IHashSet<T>` 縺ｮ縺ｾ縺ｾ縺ｧ繧ゅ∫樟蝨ｨ縺ｮ interface 縺縺代〒 LINQ-level 螻暮幕縺ｯ縺ｧ縺阪ｋ縲ゅ◆縺縺・interface 縺悟・髢九＠縺ｦ縺・ｋ縺ｮ縺ｯ蛻玲嫌蟄舌〒縺ゅｊ縲∫樟蝨ｨ縺ｮ `HashSet<T>` enumerator 縺ｯ蜀・Κ縺ｧ `InMemoryList<T>` 縺ｫ遨阪・縺溘ａ縲《torage-level 螻暮幕縺ｫ縺ｯ縺ｪ繧峨↑縺・・
storage-level 螻暮幕縺ｾ縺ｧ陦後≧縺ｫ縺ｯ谺｡縺ｮ縺・★繧後°縺悟ｿ・ｦ√・
- `Tsavorite.Collections.HashSet<T>` 縺ｪ縺ｩ蜈ｷ雎｡蝙九ｒ generator 縺瑚ｪ崎ｭ倥☆繧九・- `IHashSet<T>` 縺ｫ髱槫・謖・scan/for-each 逕ｨ縺ｮ蟆ら畑 primitive 繧定ｿｽ蜉縺吶ｋ縲・- `where TSource : ITsavoriteEnumerableSource<T>` 縺ｮ繧医≧縺ｪ generic 蛻ｶ邏・〒縲∝ｱ暮幕蜿ｯ閭ｽ縺ｪ source contract 繧呈・遉ｺ縺吶ｋ縲・
### InMemoryList 縺ｨ螻暮幕縺ｮ髢｢菫・
`InMemoryList` 繧堤ｵ檎罰縺励※繧・LINQ-level 螻暮幕縺ｯ縺ｧ縺阪ｋ縲ゅ▽縺ｾ繧・`IHashSet<T>.GetEnumerator()` 繧・root 縺ｫ縺励※縲～Where` / `Select` / `Count` 縺ｪ縺ｩ縺ｮ LINQ chain 繧堤函謌舌さ繝ｼ繝峨・繝ｫ繝ｼ繝励∈逡ｳ縺ｿ霎ｼ繧縺薙→縺ｯ縺ｧ縺阪ｋ縲・
縺溘□縺・`HashSet<T>.GetEnumerator()` 縺ｯ `Collection.Default.Enumerator` 縺ｮ constructor 縺ｧ `new InMemoryList<T>` 繧剃ｽ懊ｊ縲～IterateLookup` 縺ｨ `CompletePending` 繧・constructor 荳ｭ縺ｫ螳御ｺ・＆縺帙～MoveNext()` 縺ｯ縺昴・ list 繧定ｪｭ繧縺縺代↓縺ｪ繧九ゅ％縺ｮ譎らせ縺ｧ Tsavorite storage 縺ｨ縺ｮ謗･邯壹′蛻・ｌ繧九◆繧√《torage-level 螻暮幕縺ｨ縺ｯ縺ｿ縺ｪ縺輔↑縺・・
`InMemoryList` 邨檎罰縺・storage-level 螻暮幕縺ｧ縺ｯ縺ｪ縺・炊逕ｱ縺ｯ谺｡縺ｮ騾壹ｊ縲・
- `Any` / `First` / `Take` 縺ｮ繧医≧縺ｪ譌ｩ譛溽ｵゆｺ・庄閭ｽ縺ｪ貍皮ｮ励〒繧ゅ∝・謖吝ｭ千函謌先凾轤ｹ縺ｧ蜈ｨ莉ｶ scan 縺ｨ蜈ｨ莉ｶ譬ｼ邏阪′襍ｰ繧九・- `Where` 縺ｮ predicate 繧・`Take` 縺ｮ莉ｶ謨ｰ蛻ｶ髯舌ｒ Tsavorite 縺ｮ scan callback 縺ｫ謚ｼ縺苓ｾｼ繧√↑縺・・- 荳ｭ髢薙↓ `System.Collections.Generic.List<T>` 縺梧ｮ九ｊ縲∝､ｧ螳ｹ驥丈ｸｭ髢薙ヰ繝・ヵ繧｡繧帝∩縺代ｋ縺ｨ縺・≧逶ｮ逧・ｒ貅縺溘○縺ｪ縺・・- scan 荳ｭ縺ｫ蜃ｦ逅・ｒ豁｢繧√ｋ縲・寔險医□縺代ｒ菫晄戟縺吶ｋ縲∫ｵ先棡縺縺代ｒ Tsavorite-backed collection 縺ｫ豬√☆縲√→縺・▲縺溷ｰら畑謌ｦ逡･繧・generator 縺碁∈縺ｹ縺ｪ縺・・
謗ｨ螂ｨ螳溯｣・・縲∽ｺ呈鋤諤ｧ縺ｮ縺溘ａ縺ｮ LINQ-level 螻暮幕縺ｨ縲・ｫ俶ｧ閭ｽ縺ｪ storage-level 螻暮幕繧呈・遉ｺ逧・↓蛻・￠繧九・
1. `IHashSet<T>` 縺ｧ縺ｯ `GetEnumerator()` root 縺ｮ LINQ-level 螻暮幕繧定ｨｱ螳ｹ縺吶ｋ縲ゅ◆縺縺励％繧後・莠呈鋤繝ｻ谿ｵ髫守ｧｻ陦檎畑縺ｧ縺ゅｊ縲《torage-level 螳悟・螻暮幕縺ｨ縺ｯ蜻ｼ縺ｰ縺ｪ縺・・2. `HashSet<T>` 縺ｫ縺ｯ譌｢蟄倥・ `HashSet.Scan<TStrategy>(ref TStrategy)` 繧貞悄蜿ｰ縺ｫ縲《ource generator 縺九ｉ蜿ら・縺ｧ縺阪ｋ scan contract 繧定ｿｽ蜉縺吶ｋ縲・3. scan contract 縺ｯ `void Scan<TStrategy>(ref TStrategy strategy)` 縺ｨ縺励～TStrategy` 縺ｯ `struct` + `static abstract` reader/invoker 繧呈戟縺､縲Ｅelegate 繧・interface dispatch 繧帝∩縺代｝redicate/projection/aggregate state 繧・struct 縺ｫ逡ｳ縺ｿ霎ｼ繧縲・4. generator 縺ｯ receiver 縺・scan contract 繧呈ｺ縺溘☆蝣ｴ蜷医□縺・storage-level code path 繧堤函謌舌☆繧九よｺ縺溘＆縺ｪ縺・`IHashSet<T>` 縺ｯ LINQ-level code path 縺ｫ關ｽ縺ｨ縺吶°縲《torage-level 繧定ｦ∵ｱゅ☆繧・API 縺ｧ縺ｯ萓句､悶↓縺吶ｋ縲・5. `Where` / `Select` / `Any` / `All` / `Count` / `First` / `Aggregate` 縺ｪ縺ｩ縺ｯ scan strategy 縺ｮ蜷域・縺ｧ螳溯｣・＠縲～Distinct` / `GroupBy` / `Join` / `OrderBy` 縺ｪ縺ｩ縺ｯ Tsavorite-backed 荳ｭ髢捺ｧ矩縺後〒縺阪※縺九ｉ蟇ｾ蠢懊☆繧九・
### scan contract 縺ｮ蜈ｷ菴謎ｾ・
scan contract 縺ｨ縺ｯ縲～IEnumerator<T>` 繧定ｿ斐☆莉｣繧上ｊ縺ｫ縲《torage scan 荳ｭ縺ｫ 1 繝ｬ繧ｳ繝ｼ繝峨★縺､ struct strategy 繧貞他縺ｶ縺溘ａ縺ｮ蜈ｱ騾・API 縺ｧ縺ゅｋ縲・
譛蟆丞ｽ｢縺ｯ谺｡縺ｮ繧医≧縺ｫ縺吶ｋ縲・
```csharp
public interface ITsavoriteScanStrategy<TSelf, T>
    where TSelf : struct, ITsavoriteScanStrategy<TSelf, T>
{
    // true: scan 邯咏ｶ壹’alse: scan 蛛懈ｭ｢
    static abstract bool Invoke(ref TSelf self, in T item);
}

public interface ITsavoriteScanSource<T>
{
    void Scan<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, ITsavoriteScanStrategy<TStrategy, T>;
}
```

`HashSet<T>` 縺ｯ譌｢蟄倥・ `HashSet.Scan<TStrategy>(ref TStrategy)` 縺ｨ `IHashSetScanStrategy<TSelf,T>` 繧偵％縺ｮ public/internal contract 縺ｫ蟇・○縺ｦ螳溯｣・☆繧九・
```csharp
public partial class HashSet<T> : ITsavoriteScanSource<T>
{
    public void Scan<TStrategy>(ref TStrategy strategy)
        where TStrategy : struct, ITsavoriteScanStrategy<TStrategy, T>
    {
        var state = new ScanState<TStrategy>(strategy);
        var adapter = new ScanAdapter<TStrategy>(state);

        this.Store.Log.ShiftReadOnlyAddress(this.Store.Log.TailAddress, wait: true);
        this.ReadSession.IterateLookupSnapshot(ref adapter, includeTombstones: false);
        this.ReadSession.BasicContext.CompletePending(wait: true);

        strategy = state.Strategy;
    }
}
```

萓九∴縺ｰ繝ｦ繝ｼ繧ｶ繝ｼ繧ｳ繝ｼ繝峨′谺｡縺ｮ蝣ｴ蜷医・
```csharp
var result = source.Where(x => x > 10).Count();
```

LINQ-level 螻暮幕縺ｧ縺ｯ谺｡縺ｮ繧医≧縺ｫ蛻玲嫌蟄・root 縺ｫ縺ｪ繧九ゅ％縺ｮ蝣ｴ蜷医～GetEnumerator()` 縺ｮ譎らせ縺ｧ `InMemoryList<T>` 縺ｸ縺ｮ蜈ｨ莉ｶ隱ｭ縺ｿ霎ｼ縺ｿ縺檎匱逕溘☆繧九・
```csharp
var count = 0;
using var e = source.GetEnumerator();
while (e.MoveNext())
{
    var x = e.Current;
    if (x > 10)
        count++;
}
var result = count;
```

storage-level 螻暮幕縺ｧ縺ｯ谺｡縺ｮ繧医≧縺ｪ strategy 縺ｫ螟画鋤縺吶ｋ縲・
```csharp
private struct WhereCountGreaterThan10
    : ITsavoriteScanStrategy<WhereCountGreaterThan10, int>
{
    public int Count;

    public static bool Invoke(ref WhereCountGreaterThan10 self, in int item)
    {
        if (item > 10)
            self.Count++;

        return true;
    }
}
```

蜻ｼ縺ｳ蜃ｺ縺怜・縺ｯ蛻玲嫌蟄舌ｒ菴懊ｉ縺壹ゝsavorite scan 縺ｫ strategy 繧呈ｸ｡縺吶・
```csharp
var strategy = new WhereCountGreaterThan10();
source.Scan(ref strategy);
var result = strategy.Count;
```

`First` 縺ｮ繧医≧縺ｪ譌ｩ譛溽ｵゆｺ・ｂ strategy 縺ｮ謌ｻ繧雁､縺ｧ陦ｨ迴ｾ縺吶ｋ縲・
```csharp
private struct FirstGreaterThan10
    : ITsavoriteScanStrategy<FirstGreaterThan10, int>
{
    public bool Found;
    public int Value;

    public static bool Invoke(ref FirstGreaterThan10 self, in int item)
    {
        if (item <= 10)
            return true;

        self.Found = true;
        self.Value = item;
        return false;
    }
}
```

縺薙・蠖｢縺ｪ繧・`Any` / `First` / `Take` 縺悟・莉ｶ `InMemoryList<T>` 繧剃ｽ懊ｉ縺壹↓騾比ｸｭ蛛懈ｭ｢縺ｧ縺阪ｋ縲ＡWhere` / `Select` / `Aggregate` 縺ｯ strategy 縺ｮ迥ｶ諷九→縺励※逡ｳ縺ｿ霎ｼ繧√ｋ縲ＡToArray` / `ToList` 縺ｮ繧医≧縺ｫ邨先棡繧定塘遨阪☆繧九ｂ縺ｮ縺ｯ縲《trategy 蜀・・蜃ｺ蜉帛・繧帝・蛻・builder縲［emory-mapped `List<T>`縲√∪縺溘・ Tsavorite-backed collection 縺ｫ縺吶ｋ縲・
### 螟夜Κ iterator 縺ｨ蜀・Κ iterator

縺薙・險ｭ險医・縲∝､夜Κ iterator 縺ｨ蜀・Κ iterator 縺ｮ菴ｿ縺・・縺代〒繧ゅ≠繧九・
- 螟夜Κ iterator: 蜻ｼ縺ｳ蜃ｺ縺怜・縺・`IEnumerator<T>` 繧貞女縺大叙繧翫～MoveNext()` / `Current` 縺ｧ pull 縺吶ｋ縲・INQ to Objects 縺ｮ讓呎ｺ門ｽ｢縲・- 蜀・Κ iterator: collection 蛛ｴ縺・scan 繧呈園譛峨＠縲∝推 record 縺ｫ蟇ｾ縺励※ strategy/callback 繧貞他縺ｶ縲５savorite storage 縺ｫ霑代＞蝣ｴ謇縺ｧ蜃ｦ逅・ｒ push 縺吶ｋ縲・
螟夜Κ iterator 縺ｧ繧・generator 縺ｯ LINQ chain 繧偵Ν繝ｼ繝励∈螻暮幕縺ｧ縺阪ｋ縲ゅ◆縺縺礼樟蝨ｨ縺ｮ `HashSet<T>.GetEnumerator()` 縺ｯ蛻玲嫌蟄先ｧ狗ｯ画凾縺ｫ `InMemoryList<T>` 縺ｸ蜈ｨ莉ｶ譚先侭蛹悶☆繧九◆繧√《torage 蛛ｴ縺ｧ predicate縲｝rojection縲∥ggregate縲∵掠譛溽ｵゆｺ・ｒ陞榊粋縺ｧ縺阪↑縺・・
蜀・Κ iterator 縺ｮ蛻ｩ轤ｹ縺ｯ縲√ロ繧､繝・ぅ繝悶さ繝ｼ繝峨↓縺ｪ繧九→縺・≧諢丞袖縺ｧ縺ｯ縺ｪ縺上《torage-native 縺ｪ螳溯｡瑚ｨ育判縺ｫ縺ｧ縺阪ｋ轤ｹ縺ｧ縺ゅｋ縲Ａstruct` strategy 縺ｨ generic/static abstract 蜻ｼ縺ｳ蜃ｺ縺励↓縺吶ｌ縺ｰ delegate allocation 繧・interface dispatch 繧帝∩縺代ｄ縺吶￥縲゛IT 縺・inline 縺励ｄ縺吶＞蠖｢縺ｫ縺ｪ繧九ゅ＆繧峨↓ `Any` / `First` / `Take` 縺ｯ scan 荳ｭ縺ｫ蛛懈ｭ｢縺ｧ縺阪～Count` / `Aggregate` 縺ｯ迥ｶ諷九□縺代ｒ謖√※繧九・
縺溘□縺怜・驛ｨ iterator 縺悟ｸｸ縺ｫ騾溘＞縺ｨ縺ｯ髯舌ｉ縺ｪ縺・ょｰ上＆縺・in-memory collection 縺ｧ縺ｯ螟夜Κ iterator 縺ｮ縺ｻ縺・′蜊倡ｴ斐〒蜊∝・縺ｪ蝣ｴ蜷医′縺ゅｋ縲ゆｻ雁屓蜀・Κ iterator 繧貞━蜈医☆繧狗炊逕ｱ縺ｯ縲ゝsavorite-backed collection 縺ｧ蜈ｨ莉ｶ `InMemoryList<T>` 譚先侭蛹悶ｒ驕ｿ縺代《torage/session/scan 縺ｮ荳ｦ蛻玲ｧ縺ｨ譌ｩ譛溽ｵゆｺ・ｒ菫昴▽縺溘ａ縺ｧ縺ゅｋ縲・
### GetEnumerator 縺ｮ螟夜Κ iterator 蛹匁婿驥・
`GetEnumerator()` 縺瑚｡後▲縺ｦ縺・ｋ迴ｾ蝨ｨ縺ｮ eager 蜈ｨ莉ｶ譚先侭蛹悶・縲∝､夜Κ iterator 譁ｹ蠑上∈鄂ｮ縺肴鋤縺医ｋ縲・
迴ｾ迥ｶ縺ｮ `Collection.Default.Enumerator` 縺ｯ constructor 縺ｧ `InMemoryList<T>` 繧剃ｽ懊ｊ縲～IterateLookup` 繧呈怙蠕後∪縺ｧ襍ｰ繧峨○縺ｦ縺九ｉ `MoveNext()` 縺ｧ list 繧定ｪｭ繧縲ゅ％縺ｮ縺溘ａ public API 縺ｯ `IEnumerator<T>` 縺縺後∝ｮ滓・縺ｯ蛻玲嫌髢句ｧ句燕縺ｮ蜈ｨ莉ｶ materialize 縺ｧ縺ゅｋ縲・
譁ｰ譁ｹ驥昴〒縺ｯ縲～MoveNext()` 縺悟ｿ・ｦ√↓縺ｪ縺｣縺滓凾縺縺・Tsavorite scan 繧帝ｲ繧√ｋ chunked external iterator 縺ｫ縺吶ｋ縲ょｮ溯｣・〒縺ｯ `ClientSession.ScanCursor(ref cursor, count, ...)` 繧剃ｽｿ縺・ＡIterateLookup(ref iterator, ref cursor, resetCursor: false)` 縺ｨ `CursorRecordResult.EndBatch` / `RetryLastRecord` 繧ょ呵｣懊□縺｣縺溘′縲∝庄螟蛾聞 disk record 繧貞ｰ上＆縺・count 縺ｧ蜀埼幕縺吶ｋ縺ｨ谺關ｽ縺吶ｋ繧ｱ繝ｼ繧ｹ縺後≠縺｣縺溘◆繧√∝・謖吝ｭ先悽菴薙〒縺ｯ `ScanCursor` 繧呈治逕ｨ縺吶ｋ縲よ里蟄倥・ `tests/Test.Tsavorite.Collections/Tsavorite/IterateLookupChunkTests.cs` 縺ｯ菴弱Ξ繝吶Ν cursor 蜍穂ｽ懊・蜿り・→縺励※谿九☆縲・
螟夜Κ iterator 蛹門ｾ後・讎ょｽ｢縺ｯ谺｡縺ｮ騾壹ｊ縲・
```csharp
public struct Enumerator<TIterator, T> : IEnumerator<T>
    where TIterator : ChunkScanIteratorFunctions<T>
{
    private long cursor;
    private int index = -1;
    private int count;
    private bool done;
    private TIterator iterator;

    public T Current => this.iterator.Chunk[this.index];

    public bool MoveNext()
    {
        if (++this.index < this.count)
            return true;

        if (this.done)
            return false;

        this.iterator.ResetChunk();

        var mayHaveMore = this.session.ScanCursor(
            ref this.cursor,
            this.iterator.Chunk.Length,
            this.iterator,
            this.untilAddress,
            validateCursor: true,
            maxAddress: this.untilAddress,
            resetCursor: false,
            includeTombstones: false);

        this.session.BasicContext.CompletePending(wait: true);

        this.count = this.iterator.Count;
        this.done = !mayHaveMore;

        if (this.count == 0)
            return false;

        this.index = 0;
        return true;
    }
}
```

縺薙・譁ｹ驥昴〒縺ｯ `InMemoryList<T>` 縺ｯ `GetEnumerator()` 縺ｮ荳ｭ髢薙ヰ繝・ヵ繧｡縺ｨ縺励※菴ｿ繧上↑縺・ょｿ・ｦ√↑縺ｮ縺ｯ蝗ｺ螳夐聞縺ｾ縺溘・蟆丞ｮｹ驥上・ chunk buffer 縺縺代〒縺ゅｊ縲∝・莉ｶ蛻・・ `System.Collections.Generic.List<T>` 縺ｯ菴懊ｉ縺ｪ縺・ょｮ溯｣・ｸ翫・荳矩剞縺ｯ `4096` records 縺ｨ縺励～Const.Chunk_Length` 縺後◎繧後ｈ繧雁､ｧ縺阪＞蝣ｴ蜷医・縺昴■繧峨ｒ菴ｿ縺・ゅ％繧後・蜿ｯ螟蛾聞 disk record 縺ｮ cursor validation 蠅・阜縺ｧ谺關ｽ縺励↑縺・◆繧√・蝗ｺ螳壻ｸ企剞 block 縺ｧ縺ゅｊ縲∝・莉ｶ譚先侭蛹悶〒縺ｯ縺ｪ縺・・
螟夜Κ iterator 蛹悶↓繧医▲縺ｦ縲・壼ｸｸ縺ｮ `foreach` / `IEnumerable<T>` / 譌｢蟄・LINQ 縺ｯ螟ｧ螳ｹ驥・materialize 繧帝∩縺代ｉ繧後ｋ縲ゅ◆縺縺・generator 縺・`Any` / `First` / `Take` 縺ｪ縺ｩ繧貞ｮ悟・縺ｫ storage-level fuse 縺励◆縺・ｴ蜷医・縲∝・驛ｨ iterator / scan strategy 邨瑚ｷｯ繧剃ｽｿ縺・ｽ吝慍繧呈ｮ九☆縲・
譛ｪ豎ｺ莠矩・・蛻玲嫌荳ｭ譖ｴ譁ｰ縺ｮ諢丞袖隲悶〒縺ゅｋ縲よ立螳溯｣・・ enumerator constructor 譎らせ縺ｧ蜈ｨ莉ｶ materialize 縺吶ｋ縺溘ａ縲√◎縺ｮ蠕後・譖ｴ譁ｰ縺ｯ蛻玲嫌邨先棡縺ｫ豺ｷ縺悶ｊ縺ｫ縺上＞縲Ｍazy 螟夜Κ iterator 縺ｧ縺ｯ `MoveNext()` 譎らせ縺ｧ scan 縺碁ｲ繧縺溘ａ縲∵峩譁ｰ縺瑚ｦｳ貂ｬ縺輔ｌ繧句庄閭ｽ諤ｧ縺悟､峨ｏ繧九ゆｺ呈鋤諤ｧ繧貞━蜈医☆繧九↑繧峨∝・謖咎幕蟋区凾縺ｫ TailAddress 繧貞崋螳壹☆繧・snapshot 蟇・ｊ縺ｮ險ｭ險医′蠢・ｦ√↓縺ｪ繧九・
### InMemoryList 蜈ｨ莉ｶ譚先侭蛹門ｾ後・ for loop 縺ｨ縺ｮ豈碑ｼ・
`InMemoryList` 縺ｫ蜈ｨ莉ｶ繧堤ｩ阪ｓ縺蠕後〒谺｡縺ｮ繧医≧縺ｫ蝗槭☆譁ｹ蠑上・縲∝､夜Κ iterator 縺ｧ縺ｯ縺ｪ縺上∝・莉ｶ materialize + index loop 縺ｧ縺ゅｋ縲・
```csharp
var items = new InMemoryList<T>(Const.Chunk_Length);
// IterateLookup 縺ｧ蜈ｨ莉ｶ items 縺ｫ Add 縺吶ｋ
for (var i = 0L; i < items.Count; i++)
{
    var item = items[i];
    // LINQ fragment 繧貞ｱ暮幕縺励◆蜃ｦ逅・}
```

縺薙・譁ｹ蠑上・縲∵攝譁吝喧蠕後・蜃ｦ逅・□縺題ｦ九ｌ縺ｰ `IEnumerator<T>` 繧医ｊ騾溘￥縺励ｄ縺吶＞縲ＡMoveNext()` / `Current` 縺ｮ迥ｶ諷区ｩ滓｢ｰ繧帝壹ｉ縺壹～Count` 縺ｨ indexer 縺ｧ蜊倡ｴ斐↑ loop 縺ｫ縺ｧ縺阪ｋ縺溘ａ縺ｧ縺ゅｋ縲ゅ∪縺溘∝・謖咎幕蟋区凾轤ｹ縺ｮ snapshot 縺ｫ霑代＞邨先棡繧剃ｽ懊ｊ繧・☆縺上∬､・焚蝗櫁ｵｰ譟ｻ縺吶ｋ蜃ｦ逅・ｄ `OrderBy` 縺ｮ繧医≧縺ｫ縺ｩ縺ｮ縺ｿ縺｡蜈ｨ莉ｶ buffer 縺悟ｿ・ｦ√↑蜃ｦ逅・↓縺ｯ菴ｿ縺医ｋ縲・
縺溘□縺励ゝsavorite LINQ 縺ｮ讓呎ｺ門ｮ溯｡檎ｵ瑚ｷｯ縺ｨ縺励※縺ｯ謗｡逕ｨ縺励↑縺・・
- `Any` / `First` / `Take` 縺ｧ繧ょ・逅・燕縺ｫ蜈ｨ莉ｶ `InMemoryList` 縺ｸ蜈･繧九・- `Where` 縺ｧ關ｽ縺｡繧玖ｦ∫ｴ縺ｾ縺ｧ蜈ｨ莉ｶ deserialization 縺ｨ list 霑ｽ蜉縺瑚ｵｰ繧九・- `System.Collections.Generic.List<T>` 繝吶・繧ｹ縺ｮ螟ｧ螳ｹ驥丈ｸｭ髢薙ヰ繝・ヵ繧｡繧剃ｽｿ縺・◆繧√∽ｻ雁屓縺ｮ蛻ｶ邏・↓蜿阪☆繧九・- storage scan 縺ｨ LINQ fragment 縺瑚檮蜷医○縺壹∽ｸｦ蛻・scan 縺ｮ邨先棡繧・lock 莉倥″ list add 縺ｫ髮・ｴ・＠繧・☆縺・・
謗｡逕ｨ縺吶ｋ蝓ｺ譛ｬ蠖｢縺ｯ縲∝・莉ｶ buffer 縺ｧ縺ｯ縺ｪ縺・chunk buffer + index loop 縺ｧ縺ゅｋ縲・
```csharp
while (true)
{
    iterator.ResetChunk();

    var mayHaveMore = session.ScanCursor(
        ref cursor,
        iterator.Chunk.Length,
        iterator,
        untilAddress,
        validateCursor: true,
        maxAddress: untilAddress,
        resetCursor: false,
        includeTombstones: false);

    session.BasicContext.CompletePending(wait: true);

    for (var i = 0; i < iterator.Count; i++)
    {
        var item = iterator.Chunk[i];
        // LINQ fragment 繧貞ｱ暮幕縺励◆蜃ｦ逅・    }

    if (!mayHaveMore)
        break;
}
```

縺薙・蠖｢縺ｪ繧峨∝・蛛ｴ縺ｯ `for` 縺ｮ蜊倡ｴ・loop 縺ｫ縺ｧ縺阪ｋ荳譁ｹ縺ｧ縲∝・莉ｶ `InMemoryList` 縺ｯ菴懊ｉ縺ｪ縺・ＡAny` / `First` / `Take` 縺ｯ chunk 蜀・〒譚｡莉ｶ繧呈ｺ縺溘＠縺滓凾轤ｹ縺ｧ邨ゆｺ・〒縺阪∵ｬ｡縺ｮ chunk 繧定ｪｭ縺ｾ縺ｪ縺・ＡToArray` / `OrderBy` / `GroupBy` 縺ｪ縺ｩ蜈ｨ莉ｶ闢・ｩ阪′譛ｬ雉ｪ逧・↓蠢・ｦ√↑貍皮ｮ励□縺代∝ｰら畑縺ｮ蜃ｺ蜉・buffer 縺ｾ縺溘・ storage-backed 荳ｭ髢捺ｧ矩縺ｸ譏守､ｺ逧・↓遨阪・縲・
### 迚ｩ逅・ヶ繝ｭ繝・け縺ｨ隲也炊繝√Ε繝ｳ繧ｯ

縺薙％縺ｧ縺・≧ chunk 縺ｯ縲∝ｸｸ縺ｫ memory-mapped file 縺ｮ迚ｩ逅・ヶ繝ｭ繝・け縺昴・繧ゅ・繧呈欠縺吶ｏ縺代〒縺ｯ縺ｪ縺・よｬ｡縺ｮ 2 遞ｮ鬘槭ｒ蛻・￠繧九・
- 迚ｩ逅・ヶ繝ｭ繝・け: memory-mapped file縲＾S page縲ゝsavorite log page/segment 縺ｪ縺ｩ縲《torage 縺悟ｮ滄圀縺ｫ隱ｭ縺ｿ譖ｸ縺阪☆繧句腰菴阪・- 隲也炊繝√Ε繝ｳ繧ｯ: LINQ/enumerator 縺御ｸ蠎ｦ縺ｮ `MoveNext()` 陬懷・縺ｧ謇ｱ縺・record 縺ｮ縺ｾ縺ｨ縺ｾ繧翫ょ・蛛ｴ繧・`for` 縺ｧ蝗槭☆縺溘ａ縺ｮ蟆上＆縺ｪ buffer縲・
`List<T>` 縺ｯ memory-mapped file 蜑肴署縺ｪ縺ｮ縺ｧ縲∬ｫ也炊繝√Ε繝ｳ繧ｯ繧堤黄逅・ヶ繝ｭ繝・け縺ｾ縺溘・ mapped view 縺ｮ蠅・阜縺ｫ蟇・○繧九・縺瑚・辟ｶ縺ｧ縺ゅｋ縲ょ崋螳夐聞隕∫ｴ縺ｪ繧・`blockBytes / sizeof(T)` 縺ｫ霑代＞莉ｶ謨ｰ縺ｧ chunk 繧貞・繧後ｋ縲ょ庄螟蛾聞隕∫ｴ繧・managed 蜿ら・繧貞性繧蝣ｴ蜷医・縲∽ｻｶ謨ｰ縺ｧ縺ｯ縺ｪ縺・byte budget 縺ｨ index table 繧呈戟縺､縲・
`HashSet<T>` / `Dictionary<TKey,TValue>` 縺ｯ Tsavorite storage 蜑肴署縺ｪ縺ｮ縺ｧ縲∬ｫ也炊繝√Ε繝ｳ繧ｯ縺ｯ memory-mapped file block 縺ｧ縺ｯ縺ｪ縺・`ScanCursor` 縺ｮ count/cursor 蜊倅ｽ阪〒縺ゅｋ縲Ｔcan 邨先棡繧貞崋螳壻ｸ企剞縺ｮ record buffer 縺ｫ蜿励￠縺ｦ縺九ｉ `for` 縺ｧ蜃ｦ逅・☆繧九・
縺励◆縺後▲縺ｦ縲～List<T>` 縺ｧ縺ｯ迚ｩ逅・ヶ繝ｭ繝・け縺ｨ隲也炊繝√Ε繝ｳ繧ｯ繧剃ｸ閾ｴ縺輔○繧・☆縺・ゆｸ譁ｹ縲～HashSet<T>` / `Dictionary<TKey,TValue>` 縺ｧ縺ｯ迚ｩ逅・・鄂ｮ縺ｫ蠑ｷ縺冗ｵ舌・縺､縺代★縲～Math.Max(Const.Chunk_Length, 4096)` 繧堤樟蝨ｨ縺ｮ蝗ｺ螳・block 縺ｨ縺励∝ｰ・擂 target byte size 縺ｧ隱ｿ謨ｴ縺ｧ縺阪ｋ隲也炊繝√Ε繝ｳ繧ｯ縺ｫ縺吶ｋ縲・
## 蠖ｱ髻ｿ遽・峇

- `src/Tsavorite.Collections/Linq/*`
- `src/Tsavorite.Collections/Collections/IHashSet.cs`
- `src/Tsavorite.Collections/Collections/Collection*.cs`
- `src/FLinq/SourceGenerator/*`
- `src/FLinq/SourceGenerator/Linq/Fragments/*`
- `tests/Test.Tsavorite.Collections/Intercept/System/Linq/*`
- `tests/Test.Tsavorite.Collections/Intercept/Tsavorite/Linq/*`
- `tests/Test.Tsavorite.Collections.Benchmark/*`
- `sandbox/Benchmark.Shared/*`
- `src/Benchmark/*`

## 遘ｻ陦梧焔鬆・
1. `Tsavorite.Linq.Enumerable` 繧定ｿｽ蜉縺励∵里蟄・`HashSet` API 繧堤ｧｻ讀阪☆繧九・2. `HashSet` 繧剃ｺ呈鋤繝ｬ繧､繝､繝ｼ縺ｫ縺励※縲∵里蟄倥ユ繧ｹ繝医ｒ螢翫＆縺ｪ縺・・3. `System.Collections.Generic` 螟ｧ螳ｹ驥乗攝譁吝喧繧剃ｽｿ縺・iterator 繧貞・鬘槭＠縲∝腰邏秘≦蟒ｶ繝ｻTsavorite-backed state繝ｻmemory-mapped state 縺ｫ蛻・￠繧九・4. generator 縺ｮ LINQ family 蛻､螳壹ｒ蟆主・縺吶ｋ縲・5. `IHashSet<T>` / `HashSet<T>` 蜷代￠縺ｮ譛蟆・fragment 繧帝壹☆縲・6. 譌｢蟄・Tsavorite HashSet intercept 繝・せ繝医ｒ `Enumerable` 蜷榊燕縺ｸ遘ｻ陦後☆繧九・7. System LINQ 繝・せ繝医ｒ蜈ｱ譛・source factory 蛹悶＠縲ゝsavorite 迚医ｒ霑ｽ蜉縺吶ｋ縲・8. 繝吶Φ繝∝・譛牙喧繧・`Benchmark.Shared` 繝吶・繧ｹ縺ｧ謨ｴ逅・☆繧九・9. 萓晏ｭ俶峩譁ｰ繧定｡後≧蝣ｴ蜷医・ Garnet 譖ｴ譁ｰ蠕後↓ build/test 繧帝壹☆縲・
## 繝・せ繝郁ｨ育判

- `dotnet test tests/Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj`
- `dotnet test tests/Test.Tsavorite.Collections.Benchmark/Test.Tsavorite.Collections.Benchmark.csproj`
- Chain1 縺ｮ縺・■蛻晄悄蟇ｾ蠢・fragment 縺ｮ System/Tsavorite 蜈ｱ譛峨ユ繧ｹ繝医・- Chain2 縺ｮ `Where_Select`, `Select_Where`, `Where_Count`, `Select_Sum` 縺九ｉ髢句ｧ九・- Generated source assertion 縺ｧ `Tsavorite.Linq.Enumerable` 縺・intercept 蟇ｾ雎｡縺ｫ縺ｪ繧九％縺ｨ繧堤｢ｺ隱阪・- 螟ｧ螳ｹ驥丈ｸｭ髢薙ヰ繝・ヵ繧｡遖∵ｭ｢縺ｮ髱咏噪讀懈渊縺ｨ縺励※縲～Tsavorite.Linq.Enumerable` 螳溯｣・・縺ｮ `global::System.Collections.Generic.List/HashSet/Dictionary` 菴ｿ逕ｨ繧・`rg` 縺ｧ讀懷・縺吶ｋ縲・
## 繝ｪ繧ｹ繧ｯ縺ｨ蟇ｾ遲・
- extension method 隗｣豎ｺ縺・`System.Linq` 縺ｨ `Tsavorite.Linq` 縺ｧ陦晉ｪ√☆繧九・  - 繝・せ繝亥錐蜑咲ｩｺ髢薙→ using alias 繧貞・縺代∝ｿ・ｦ√↑邂・園縺ｯ譏守､ｺ static call 縺ｫ縺吶ｋ縲・- `IEnumerable<T>` 謌ｻ繧雁､繧堤ｶｭ謖√☆繧九→蜈ｨ莉ｶ繝舌ャ繝輔ぃ enumerator 縺ｫ謌ｻ繧翫ｄ縺吶＞縲・  - generator 螻暮幕縺ｨ蟆ら畑 enumerable 繧貞━蜈医＠縲∵悴蟇ｾ蠢・fragment 縺ｯ萓句､悶↓縺吶ｋ縲・- `GroupBy`/`Join`/`OrderBy` 縺ｯ迥ｶ諷狗ｮ｡逅・′螟ｧ縺阪￥縲∝・譛溷ｯｾ蠢懊〒遐ｴ邯ｻ縺励ｄ縺吶＞縲・  - 蛻晄悄遽・峇縺九ｉ螟悶＠縲《torage-backed 荳ｭ髢捺ｧ矩繧貞・縺ｫ險ｭ險医☆繧九・- Garnet prerelease 譖ｴ譁ｰ縺ｧ Tsavorite API 縺悟虚縺丞庄閭ｽ諤ｧ縺後≠繧九・  - session/scan API 繧貞・驛ｨ helper 縺ｫ髢峨§繧九・
## 譛ｪ豎ｺ莠矩・
- `ToHashSet` / `ToDictionary` 縺ｮ謌ｻ繧雁､繧・Tsavorite collection 縺ｫ蟇・○繧九°縲ヾystem 莠呈鋤繧呈ｮ九＠縺ｦ `ToTsavorite*` 繧定ｿｽ蜉縺吶ｋ縺九・
## 蜷域э迥ｶ豕・
- 蜷域э貂医∩: 豁｣隕・API 蜷阪・ `Tsavorite.Linq.Enumerable`縲・- 蜷域э貂医∩: 譌｢蟄・`Tsavorite.Linq.HashSet` 縺ｯ螯･蠖灘錐縺ｧ縺ｯ縺ｪ縺・◆繧∫ｧｻ陦悟ｯｾ雎｡縲・- 蜷域э貂医∩: System LINQ 逕ｨ intercept/繝・せ繝医ｒ蜿り・↓ Tsavorite 迚医ｒ菴懊ｋ縲・- 蜷域э貂医∩: storage-level 螻暮幕繧定ｦ∵ｱゅ☆繧狗ｵ瑚ｷｯ縺ｧ縺ｯ縲∵悴蟇ｾ蠢・fragment 繧・ｮ悟・螻暮幕縺ｧ縺阪↑縺・LINQ 縺ｯ fallback 縺帙★萓句､悶↓縺吶ｋ縲・- 蜷域э貂医∩: `Microsoft.Garnet` 縺ｯ譛譁ｰ prerelease `2.0.1-beta.3` 縺ｸ譖ｴ譁ｰ縺吶ｋ縲・- 蜷域э貂医∩: `GetEnumerator()` 縺ｯ蜈ｨ莉ｶ `InMemoryList<T>` 譚先侭蛹悶〒縺ｯ縺ｪ縺上∝､夜Κ iterator 譁ｹ蠑上・蝗ｺ螳・block scan 縺ｫ縺吶ｋ縲・- 螳滓命貂医∩: `src/Tsavorite.Collections/Tsavorite.Collections.csproj` 縺ｨ `src/Seamless.Data/Seamless.Data.csproj` 縺ｮ `Microsoft.Garnet` 蜿ら・繧・`2.0.1-beta.3` 縺ｫ譖ｴ譁ｰ縺励◆縲・- 讀懆ｨｼ貂医∩: `dotnet restore` 縺ｨ `dotnet build --no-restore` 縺ｯ荳｡繝励Ο繧ｸ繧ｧ繧ｯ繝医〒謌仙粥縺励◆縲ＡTsavorite.Collections` 縺ｫ縺ｯ譌｢蟄倥・ nullable/CS9124 隴ｦ蜻翫′谿九ｋ縺後；arnet 譖ｴ譁ｰ縺ｫ繧医ｋ繧ｨ繝ｩ繝ｼ縺ｯ蜃ｺ縺ｦ縺・↑縺・・- 螳滓命貂医∩: `Collection.Default.Enumerator` 繧・constructor 蜈ｨ莉ｶ譚先侭蛹悶°繧・`MoveNext()` 譎ゅ・ `ScanCursor` 陬懷・縺ｸ螟画峩縺励◆縲ＡHashSet<T>` / `Dictionary<TKey,TValue>` / `Set<TKey,TRecord>` 縺ｯ蛻玲嫌髢句ｧ区凾縺ｮ `TailAddress` 繧呈ｸ｡縺吶・- 螳滓命貂医∩: scan buffer 縺ｯ `Math.Max(Const.Chunk_Length, 4096)` records 縺ｨ縺励∝庄螟蛾聞 disk record 縺ｮ cursor validation 蠅・阜縺ｧ谺關ｽ縺励↑縺・ｈ縺・`validateCursor: true` 繧剃ｽｿ縺・・- 讀懆ｨｼ貂医∩: `dotnet test tests/Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj --no-restore --filter "FullyQualifiedName~Enumerator|FullyQualifiedName~IterateLookupChunkTests"` 縺ｯ 41 莉ｶ謌仙粥縺励◆縲・
## 蜿ら・

- NuGet `Microsoft.Garnet`: https://www.nuget.org/packages/Microsoft.Garnet
- Garnet repository: https://github.com/microsoft/garnet
