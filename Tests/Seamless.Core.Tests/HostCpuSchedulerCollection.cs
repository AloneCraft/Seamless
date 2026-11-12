namespace Execution.Tests;

// host共通の利用数が0へ戻る観測は、同じhost枠を使う他の試験と重ねない。
[CollectionDefinition("HostCpuScheduler",DisableParallelization=true)]
public sealed class HostCpuSchedulerCollection;
