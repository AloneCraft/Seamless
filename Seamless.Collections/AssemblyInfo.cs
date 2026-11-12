using System.Runtime.CompilerServices;

// mmap リスト基盤(PersistedList/SerializedList/MemoryMappedListTuning)の internal メンバーは
// この基盤アセンブリ上に積まれる 2 つのコレクション実装からのみ利用する。公開面は変えない。
[assembly: InternalsVisibleTo("Seamless.Collections.Persisted")]
[assembly: InternalsVisibleTo("Seamless.Collections.Tsavorite")]

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Test.Tsavorite.Collections")]

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Test.Persisted.Collections")]

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Benchmark.Dynamic.Tests")]

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Dynamic.RegressionCases")]
