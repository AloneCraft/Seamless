# FLinq source共通基底化・unordered Set契約変更

## 公開契約の変更

- `Seamless.Collections.IUnorderedEnumerable<T>`を追加し、`ISet<T>`はこのmarkerを継承する。
- `Seamless.Linq.Enumerable`のunordered Set向け遅延演算はmarkerを返し、後続chainへunordered由来を伝播する。
- 順序依存演算をunordered Setへ直接適用するとWarningが発生し、実行時に`NotSupportedException`を送出する。
- `AsEnumerable()`、inline `IEnumerable<T>` cast、完全修飾`System.Linq.Enumerable`呼び出しは明示opt-inとして扱う。Array/Listへのmaterialize後も順序依存演算を利用できる。
- marker導入により戻り型の公開・バイナリ契約が変わるため、利用側は新しいSeamless assemblyを参照して再コンパイルする必要がある。

## native走査

- Array/List/PersistedListは共通direct-span rootを使用する。
- Tsavorite HashSet/SetとPersisted Setは共通provider-native root/terminalを使用する。
- Tsavorite `Set<TKey,TRecord>`にpublic `ISetIterateStrategy<TSelf,TRecord>`と`Iterate(ref strategy)`を追加した。
