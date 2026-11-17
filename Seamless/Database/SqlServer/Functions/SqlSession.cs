namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// 結果がセッションの設定や接続者で変わる関数(DATEPART の週・DATENAME・GETDATE・USER_NAME など)に渡す状態。
/// 既定値は SQL Server を us_english・DATEFIRST 7・DATEFORMAT mdy で使ったときと同じ。
/// </summary>
public sealed class SqlSession{
    /// <summary>週の始まりの曜日(1=月曜 … 7=日曜)。SET DATEFIRST。</summary>
    public byte DateFirst{get;init;}=7;
    /// <summary>SET LANGUAGE の名前。月名・曜日名に使う。</summary>
    public string Language{get;init;}="us_english";
    /// <summary>SET DATEFORMAT。文字列から日付への変換で、区切りのある数字の並びの読み方に使う。</summary>
    public string DateFormat{get;init;}="mdy";
    /// <summary>文の開始時刻(ローカル)。同じ文の中の GETDATE は同じ値を返す。</summary>
    public DateTime StatementTime{get;init;}=DateTime.Now;
    /// <summary>文の開始時刻(UTC)。</summary>
    public DateTime StatementUtcTime{get;init;}=DateTime.UtcNow;
    /// <summary>データベースの利用者名。USER_NAME()。</summary>
    public string UserName{get;init;}="dbo";
    /// <summary>接続したときのログイン名。ORIGINAL_LOGIN()。</summary>
    public string OriginalLogin{get;init;}="sa";
    /// <summary>利用者が属するデータベースのロールと Windows グループ。IS_MEMBER・IS_ROLEMEMBER。</summary>
    public IReadOnlySet<string> Roles{get;init;}=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"public","db_owner"};
    /// <summary>sp_set_session_context で置いた値。SESSION_CONTEXT。</summary>
    public IReadOnlyDictionary<string,object?> SessionContext{get;init;}=new Dictionary<string,object?>(StringComparer.Ordinal);
    /// <summary>既定の設定。</summary>
    public static SqlSession Default{get;}=new();
}
