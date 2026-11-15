using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Database.Transactions.Tests")]
[assembly: InternalsVisibleTo("Seamless.Database.Gateway")]
[assembly: InternalsVisibleTo("Seamless.Database.Gateway.Tests")]
[assembly: InternalsVisibleTo("Seamless.Management.Server")]
// 生成した行の型の列と作り方(GeneratedRecordShape)を、SQL Server からの取込と世代の移行が使う。
[assembly: InternalsVisibleTo("Seamless.Database.SqlServer")]
[assembly: InternalsVisibleTo("Seamless.Database.Query")]
