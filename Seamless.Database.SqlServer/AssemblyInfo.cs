using System.Runtime.CompilerServices;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Database.Query.Tests")]
// bit の型は、取り込んだ DB の成果物が SqlClient などを持たずに済むよう、組み込み関数の部品へ移した(名前空間は同じ)。
[assembly: TypeForwardedTo(typeof(Seamless.Database.SqlServer.Types.Bit))]
