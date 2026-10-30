namespace Seamless.Database.Query;
/// <summary>SQL上の実名。識別子のC#向け変換は行わない。</summary>
public sealed record SqlObjectName(string Database,string Schema,string Name);
/// <summary>SQL型と元の列順/照合順序を保持する。</summary>
public sealed record SqlType(string Name,Type ClrType,int? Length=null,byte? Precision=null,byte? Scale=null,string? Collation=null){
    internal bool IsUntypedNull{get;init;}
    internal byte?LiteralPrecision{get;init;}
    internal SqlCollationCoercibility Coercibility{get;init;}
    public static SqlType Int{get;}=new("int",typeof(int));
    public static SqlType BigInt{get;}=new("bigint",typeof(long));
    public static SqlType Bit{get;}=new("bit",typeof(bool));
    public static SqlType String{get;}=new("nvarchar",typeof(string),-1);
    public static SqlType Decimal{get;}=new("decimal",typeof(decimal),null,38,6);
}
internal enum SqlCollationCoercibility{Implicit,CoercibleDefault}
public sealed record SqlColumn(string Name,SqlType Type,bool IsNullable,SqlObjectName? Source=null,int Ordinal=0){
    /// <summary>浮動値の全生成経路が計算と確認できる場合だけtrue。直接値や不明な経路はfalse。</summary>
    public bool IsComputedFloatingPoint{get;init;}
}
/// <summary>
/// 表または定義から実行するビュー。ビューにRowsを指定する必要はない。
/// Rowsは毎回独立した走査を返し、列順に対応する所有配列とSQL NULLを表すnullを返す。
/// カタログ世代中はスキーマと定義を固定し、読取の一貫性は供給元が保証する。
/// </summary>
public sealed record SqlTable(SqlObjectName Name,IReadOnlyList<SqlColumn> Columns,Func<CancellationToken,IEnumerable<object?[]>>? Rows=null,string? ViewDefinition=null);
public sealed record SqlRoutine(SqlObjectName Name,string? Definition,SqlNativeRoutine? Native=null);
/// <summary>生成済みスカラー関数の型契約と、世代に固定された呼出し先。</summary>
public sealed record SqlNativeRoutine(IReadOnlyList<SqlParameter> Parameters,SqlType ReturnType,bool IsNullable,Func<IReadOnlyList<object?>,object?> Invoke);
public interface ISqlCatalog{
    long Generation{get;}
    SqlTable? FindTable(SqlObjectName name);
    SqlRoutine? FindRoutine(SqlObjectName name)=>null;
    string? GetDatabaseCollation(string database)=>null;
}
public sealed record SqlParameter(string Name,SqlType Type,bool IsNullable=true);
public sealed class SqlQueryException(string message,int number=50000,int line=0):InvalidOperationException(message){
    public int Number{get;}=number;
    public int Line{get;}=line;
}
public sealed class SqlQueryResult{
    internal SqlQueryResult(IReadOnlyList<SqlColumn> columns,IEnumerable<object?[]> rows){this.Columns=columns;this.Rows=rows;}
    public IReadOnlyList<SqlColumn> Columns{get;}
    public IEnumerable<object?[]> Rows{get;}
}
