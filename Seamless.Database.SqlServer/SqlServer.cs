namespace Seamless.Database.SqlServer;

/// <summary>SQL Server固有の組み込み関数。</summary>
public static partial class SqlServer {
    /// <summary>SQL Serverホストのローカル日時をdatetime2(7)相当で返す。</summary>
    public static global::System.DateTime SYSDATETIME()=>global::System.TimeProvider.System.GetLocalNow().DateTime;
}
