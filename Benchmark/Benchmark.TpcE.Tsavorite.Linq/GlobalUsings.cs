// 共有の取引コード(Shared/TpcE.Transactions)はこの 2 つの別名だけで保存先と LINQ の種類を切り替える。
global using TpcEDatabase=Benchmark.TpcE.Core.TsavoriteTpcEDatabase;
global using LinqInterceptAttribute=Benchmark.TpcE.Core.NoopLinqInterceptAttribute;
