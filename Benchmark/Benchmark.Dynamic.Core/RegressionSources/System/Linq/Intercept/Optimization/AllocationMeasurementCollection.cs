namespace Test.System.Linq.Intercept.Optimization;

/// <summary>
/// スレッドの割り当て量を測るテストを、ほかのテストと並行させないためのコレクション。
/// 生成コードは ArrayPool.Shared から作業用配列を借りる。プールはプロセス全体で共有されるため、
/// 並行するテストが配列を持ち去ると、温めたあとの測定でも新しい配列を確保して割り当て量が膨らむ。
/// </summary>
[CollectionDefinition("AllocationMeasurement",DisableParallelization = true)]
public sealed class AllocationMeasurementCollection;
