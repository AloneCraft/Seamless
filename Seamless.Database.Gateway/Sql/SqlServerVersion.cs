namespace Seamless.Database.Gateway.Sql;

// Seamless が SQL Server として名乗る版。写し取ったシステムカタログ(Seamless.Database.SqlServer\SystemCatalog\SqlServer17)の元と同じ 17.0 に揃える。
// SSMS は相手の版を見て送る SQL を変えるので、PreLogin・LOGINACK・SERVERPROPERTY・@@VERSION・compatibility_level はすべてここから出す。
internal static class SqlServerVersion{
    internal const byte Major=17;
    internal const byte Minor=0;
    internal const ushort Build=1000;
    internal const byte Revision=7;
    internal const byte CompatibilityLevel=170;
    internal const string ProductVersion="17.0.1000.7";
    internal const string Banner="Microsoft SQL Server 2025 (RTM) - 17.0.1000.7 (X64) - SeamlessDB Server 1.0";
}
