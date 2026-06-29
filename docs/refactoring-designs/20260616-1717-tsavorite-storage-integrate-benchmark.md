# Tsavorite Storage Integrate Benchmark 移行

## 背景
`tests/Test.Tsavorite.Collections/Integrate` には、storage device を使った正しさ検証と、セグメント増加・compaction 効果・大量 churn のような性能/規模観察が混在している。

## 現状の観察
- `HashSetIntegrate.cs` はセグメントファイル増加、単調増加、compaction 後の active segment 減少、複数セッション refresh、checkpoint restore を同じ統合テスト基底で扱っている。
- `DictionaryIntegrate.cs` も同様に、セグメント増加系と refresh/checkpoint restore を同じ統合テストに含めている。
- `src/Benchmark` には `HashSetApiSurfaceBenchmark`、`DictionaryApiSurfaceBenchmark`、`ListApiSurfaceBenchmark` があり、`Managed/Blittable` と `Mutable/Immutable/Storage` の比較軸がすでにある。
- `src/Benchmark/Seamlesscsproj` は `Compile Include` を明示列挙しているため、新しいベンチファイルは csproj に追加する必要がある。

## 目的
統合テストは「メモリ上の領域と storage 領域の境界で値が壊れない」ことを最小限に検証する。性能・規模・セグメント挙動の観察は BenchmarkDotNet の `src/Benchmark` へ移す。

## 非目的
- Tsavorite.Collections の public API は変更しない。
- 既存 benchmark runner の起動方式は変更しない。
- 大量データの正しさを unit test で保証しようとしない。

## 提案設計
- `HashSetIntegrate.cs` と `DictionaryIntegrate.cs` は、小さな storage correctness テストに縮小する。
  - `FlushAndEvict` 後に `Contains` / `TryGetValue` と値一致が正しいことを確認する。
  - checkpoint `Save` / `Load` 後に `Contains` / `TryGetValue` と値一致が正しいことを確認する。
  - managed/string と blittable/int の両方を残す。
  - full evict 後の列挙網羅は既存の `Unit/Collections/Enumerator.cs` 側の責務とし、この統合テストには含めない。
- セグメントファイル増加、compaction、refresh churn は `src/Benchmark` の新しい storage maintenance benchmark に移す。
  - `HashSetStorageMaintenanceBenchmark`
  - `DictionaryStorageMaintenanceBenchmark`
  - `Impl = Managed/Blittable`
  - `Operation = BulkInsertSegments / ChurnWithCompaction / RefreshSiblingChurn`
  - 結果は件数、セグメント数、または `BeginAddress` 差分を返し、JIT 最適化で消えないようにする。

## 影響範囲
- `tests/Test.Tsavorite.Collections/Integrate/HashSetIntegrate.cs`
- `tests/Test.Tsavorite.Collections/Integrate/DictionaryIntegrate.cs`
- `src/Benchmark/TsavoriteStorageMaintenancecs`
- `src/Benchmark/Seamlesscsproj`

## 移行手順
1. 統合テストを storage boundary correctness のみに書き換える。
2. 旧統合テストの性能/規模シナリオを BenchmarkDotNet クラスへ移植する。
3. `src/Benchmark/Seamlesscsproj` に新ファイルを追加する。
4. 対象テストと benchmark project build を通す。

## テスト計画
- `dotnet test tests/Test.Tsavorite.Collections/Test.Tsavorite.Collections.csproj --no-restore --filter FullyQualifiedName~Integrate`
- `dotnet build src/Benchmark/Seamlesscsproj --no-restore`

## リスクと対策
- 統合テストを縮小すると、セグメント挙動の CI 回帰検出力は下がる。代わりに正しさ境界だけは小さく残し、性能/規模観察は BenchmarkDotNet で比較可能にする。
- Benchmark に storage device 作成/削除が入るため、iteration cleanup で必ず dispose と directory delete を行う。

## 未決事項
- なし。ユーザー合意: 統合テストはメモリ/storage 境界 correctness に縮小し、性能・規模は `src/Benchmark` へ移す。

## 合意状況
- 合意済み。
