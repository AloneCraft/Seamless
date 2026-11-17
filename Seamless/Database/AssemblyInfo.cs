using Seamless.Collections.Tsavorite;
using Tsavorite.core;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Database.Query.Tests")]
[assembly: InternalsVisibleTo("Server")]
[assembly: InternalsVisibleTo("Management.Tests")]
[assembly: InternalsVisibleTo("Server")]
// 生成した行の型の列と作り方(GeneratedRecordShape)を、SQL Server からの取込と世代の移行が使う。
[assembly: InternalsVisibleTo("Database")]
[assembly: InternalsVisibleTo("Database")]
// 書き出しで、DB の表の定義を行の型から読み直す(SeamlessCatalogReader が GeneratedRecordShape を使う)。
[assembly: InternalsVisibleTo("Database")]
