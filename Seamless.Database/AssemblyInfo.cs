using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Database.Query.Tests")]
[assembly: InternalsVisibleTo("Seamless.Database.Gateway")]
[assembly: InternalsVisibleTo("Management.Tests")]
[assembly: InternalsVisibleTo("Seamless.Management.Server")]
// 生成した行の型の列と作り方(GeneratedRecordShape)を、SQL Server からの取込と世代の移行が使う。
[assembly: InternalsVisibleTo("Seamless.Database.SqlServer")]
[assembly: InternalsVisibleTo("Seamless.Database.Query")]
// 書き出しで、DB の表の定義を行の型から読み直す(SeamlessCatalogReader が GeneratedRecordShape を使う)。
[assembly: InternalsVisibleTo("Seamless.Database.Transfer")]
