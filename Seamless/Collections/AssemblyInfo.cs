using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

// mmap リスト基盤(PersistedList/SerializedList/MemoryMappedListTuning)の internal メンバーは
// この基盤アセンブリ上に積まれる 2 つのコレクション実装からのみ利用する。公開面は変えない。
[assembly: InternalsVisibleTo("Runtime")]
[assembly: InternalsVisibleTo("Runtime")]

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Seamless.Core.Tests")]


[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Benchmark.Dynamic.Tests")]

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Dynamic.RegressionCases")]
