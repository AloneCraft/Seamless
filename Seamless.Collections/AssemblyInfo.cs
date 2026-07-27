using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Test")]

// mmap リスト基盤(PersistedList/SerializedList/MemoryMappedListTuning)の internal メンバーは
// この基盤アセンブリ上に積まれる 2 つのコレクション実装からのみ利用する。公開面は変えない。
[assembly: InternalsVisibleTo("Seamless.Collections.Persisted")]
[assembly: InternalsVisibleTo("Seamless.Collections.Tsavorite")]
