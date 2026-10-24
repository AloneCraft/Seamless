// 共有の取引コード(Shared/TpcC.Transactions)はこの 3 つの別名だけで保存先と LINQ の種類を切り替える。
global using TpcCDatabase=Benchmark.TpcC.Core.TsavoriteTpcCDatabase;
global using TpcCPartition=Benchmark.TpcC.Core.TsavoriteTpcCPartition;
global using LinqInterceptAttribute=Benchmark.Tpc.Core.NoopLinqInterceptAttribute;
