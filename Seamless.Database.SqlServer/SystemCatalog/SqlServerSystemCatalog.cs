using System.Globalization;
using System.IO;
using System.Text;
namespace Seamless.Database.SqlServer;

// 本物の SQL Server から写し取ったシステムオブジェクトの形(名前・列・引数・結果の列)。
// 中身は持たない(列ごとの「いつもの値」だけ持つ)。定義ファイルは SystemCatalog\SqlServer17 にあり、Export-SqlServerSystemCatalog.ps1 で作り直す。
// 文字列の列の照合順序は写し取り元のサーバーの既定値なので、使う側でデータベースの既定値へ置き換えること。
public sealed class SqlServerSystemCatalog {
    private const string ResourcePrefix="Seamless.Database.SqlServer.SystemCatalog.SqlServer17.";
    private static readonly Lazy<SqlServerSystemCatalog> DefaultCatalog=new(static ()=>Load(OpenResource));
    private static TextReader OpenResource(string name) {
        var stream=typeof(SqlServerSystemCatalog).Assembly.GetManifestResourceStream(ResourcePrefix+name);
        if(stream is null) {
            throw new InvalidDataException($"System catalog resource '{name}' is missing.");
        }
        return new StreamReader(stream,new UTF8Encoding(false));
    }
    private readonly Dictionary<(SqlServerSystemScope Scope,string Schema,string Name),SqlServerSystemObject> objects;
    private SqlServerSystemCatalog(string productVersion,IReadOnlyList<SqlServerSystemObject> list) {
        this.ProductVersion=productVersion;
        this.Objects=list;
        this.objects=list.ToDictionary(static o=>(o.Scope,o.Schema,o.Name),SystemObjectKeyComparer.Instance);
    }
    public static SqlServerSystemCatalog Default=>DefaultCatalog.Value;
    public string ProductVersion { get; }
    public IReadOnlyList<SqlServerSystemObject> Objects { get; }
    public SqlServerSystemObject? Find(SqlServerSystemScope scope,string schema,string name)=>this.objects.GetValueOrDefault((scope,schema,name));
    // データベース名・スキーマ名・名前から、SQL Server が実際に探す範囲を決めて引く。sys と INFORMATION_SCHEMA はどのデータベースでも同じものを返す。
    public SqlServerSystemObject? Resolve(string database,string schema,string name) {
        if(string.Equals(schema,"sys",StringComparison.OrdinalIgnoreCase)||string.Equals(schema,"INFORMATION_SCHEMA",StringComparison.OrdinalIgnoreCase)) {
            return this.Find(SqlServerSystemScope.System,schema,name);
        }
        if(string.Equals(database,"master",StringComparison.OrdinalIgnoreCase)) {
            return this.Find(SqlServerSystemScope.Master,schema,name);
        }
        if(string.Equals(database,"msdb",StringComparison.OrdinalIgnoreCase)) {
            return this.Find(SqlServerSystemScope.Msdb,schema,name);
        }
        return null;
    }
    public static SqlServerSystemCatalog Load(Func<string,TextReader> open) {
        ArgumentNullException.ThrowIfNull(open);
        var builders=new Dictionary<(SqlServerSystemScope,string,string),Builder>(SystemObjectKeyComparer.Instance);
        var order=new List<Builder>();
        foreach(var (line,f) in Read(open,"objects.tsv",["scope","schema","name","type","object_id","result_set"])) {
            var (state,reason)=ParseResultSet(f[5],line);
            var builder=new Builder(ParseScope(f[0],line),f[1],f[2],f[3],ParseInt(f[4],line),state,reason);
            if(!builders.TryAdd((builder.Scope,builder.Schema,builder.Name),builder)) {
                throw Invalid(line,$"duplicate object {f[0]}.{f[1]}.{f[2]}");
            }
            order.Add(builder);
        }
        Builder Owner(string[] f,string line) {
            if(!builders.TryGetValue((ParseScope(f[0],line),f[1],f[2]),out var owner)) {
                throw Invalid(line,$"unknown object {f[0]}.{f[1]}.{f[2]}");
            }
            return owner;
        }
        foreach(var (line,f) in Read(open,"columns.tsv",["scope","schema","object","column_id","name","type","max_length","precision","scale","is_nullable","collation"])) {
            var owner=Owner(f,line);
            var column=new SqlServerSystemColumn(ParseInt(f[3],line),f[4],f[5],ParseShort(f[6],line),ParseByte(f[7],line),ParseByte(f[8],line),ParseBool(f[9],line),Optional(f[10]));
            if(column.ColumnId!=owner.Columns.Count+1) {
                throw Invalid(line,$"column_id {column.ColumnId} is out of order");
            }
            owner.Columns.Add(column);
        }
        foreach(var (line,f) in Read(open,"typical_values.tsv",["scope","schema","object","column","value"])) {
            var owner=Owner(f,line);
            var index=owner.Columns.FindIndex(c=>string.Equals(c.Name,f[3],StringComparison.OrdinalIgnoreCase));
            if(index<0) {
                throw Invalid(line,$"unknown column '{f[3]}'");
            }
            owner.Columns[index]=owner.Columns[index] with { Typical=f[4] };
        }
        foreach(var (line,f) in Read(open,"parameters.tsv",["scope","schema","object","parameter_id","name","type","max_length","precision","scale","is_output","has_default_value"])) {
            var owner=Owner(f,line);
            var parameter=new SqlServerSystemParameter(ParseInt(f[3],line),f[4],f[5],ParseShort(f[6],line),ParseByte(f[7],line),ParseByte(f[8],line),ParseBool(f[9],line),ParseBool(f[10],line));
            if(owner.Parameters.Count>0&&parameter.ParameterId<=owner.Parameters[^1].ParameterId) {
                throw Invalid(line,$"parameter_id {parameter.ParameterId} is out of order");
            }
            owner.Parameters.Add(parameter);
        }
        foreach(var (line,f) in Read(open,"result_columns.tsv",["scope","schema","object","ordinal","name","type","is_nullable"])) {
            var owner=Owner(f,line);
            if(owner.ResultSet!=SqlServerSystemResultSet.Columns) {
                throw Invalid(line,"result columns for an object without a described result set");
            }
            var column=new SqlServerSystemResultColumn(ParseInt(f[3],line),Optional(f[4]),f[5],ParseBool(f[6],line));
            if(column.Ordinal!=owner.ResultColumns.Count+1) {
                throw Invalid(line,$"ordinal {column.Ordinal} is out of order");
            }
            owner.ResultColumns.Add(column);
        }
        foreach(var builder in order) {
            if(builder.ResultSet==SqlServerSystemResultSet.Columns&&builder.ResultColumns.Count==0) {
                throw new InvalidDataException($"objects.tsv: {builder.Scope}.{builder.Schema}.{builder.Name} is described as having result columns but none were found.");
            }
        }
        string? version=null;
        using(var reader=open("source.txt")) {
            for(var text=reader.ReadLine();text is not null;text=reader.ReadLine()) {
                var parts=text.Split('\t');
                if(parts.Length==2&&parts[0]=="product_version") {
                    version=parts[1];
                }
            }
        }
        if(version is null) {
            throw new InvalidDataException("source.txt: product_version is missing.");
        }
        return new(version,order.Select(static b=>b.Build()).ToArray());
    }
    private static IEnumerable<(string Line,string[] Fields)> Read(Func<string,TextReader> open,string name,string[] header) {
        using var reader=open(name);
        var first=reader.ReadLine();
        if(first!=string.Join('\t',header)) {
            throw new InvalidDataException($"{name}: unexpected header '{first}'.");
        }
        var number=1;
        for(var text=reader.ReadLine();text is not null;text=reader.ReadLine()) {
            number++;
            var fields=text.Split('\t');
            var line=$"{name}:{number}";
            if(fields.Length!=header.Length) {
                throw Invalid(line,$"expected {header.Length} fields but found {fields.Length}");
            }
            yield return (line,fields);
        }
    }
    private static SqlServerSystemScope ParseScope(string value,string line)=>value switch {
        "system"=>SqlServerSystemScope.System,
        "master"=>SqlServerSystemScope.Master,
        "msdb"=>SqlServerSystemScope.Msdb,
        _=>throw Invalid(line,$"unknown scope '{value}'"),
    };
    private static (SqlServerSystemResultSet State,string? Reason) ParseResultSet(string value,string line) {
        if(value.Length==0) {
            return (SqlServerSystemResultSet.NotApplicable,null);
        }
        if(value=="columns") {
            return (SqlServerSystemResultSet.Columns,null);
        }
        if(value=="none") {
            return (SqlServerSystemResultSet.None,null);
        }
        if(value.StartsWith("error:",StringComparison.Ordinal)) {
            return (SqlServerSystemResultSet.Undescribable,value["error:".Length..]);
        }
        throw Invalid(line,$"unknown result_set '{value}'");
    }
    private static int ParseInt(string value,string line) {
        if(!int.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var result)) {
            throw Invalid(line,$"'{value}' is not an int");
        }
        return result;
    }
    private static short ParseShort(string value,string line) {
        if(!short.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var result)) {
            throw Invalid(line,$"'{value}' is not a smallint");
        }
        return result;
    }
    private static byte ParseByte(string value,string line) {
        if(!byte.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out var result)) {
            throw Invalid(line,$"'{value}' is not a tinyint");
        }
        return result;
    }
    // 空欄は NULL。
    private static string? Optional(string value) {
        if(value.Length==0) {
            return null;
        }
        return value;
    }
    private static bool ParseBool(string value,string line)=>value switch {
        "1"=>true,
        "0"=>false,
        _=>throw Invalid(line,$"'{value}' is not a bit"),
    };
    private static InvalidDataException Invalid(string line,string message)=>new($"{line}: {message}.");
    private sealed class Builder(SqlServerSystemScope scope,string schema,string name,string type,int objectId,SqlServerSystemResultSet resultSet,string? reason) {
        public SqlServerSystemScope Scope=>scope;
        public string Schema=>schema;
        public string Name=>name;
        public SqlServerSystemResultSet ResultSet=>resultSet;
        public List<SqlServerSystemColumn> Columns { get; }=[];
        public List<SqlServerSystemParameter> Parameters { get; }=[];
        public List<SqlServerSystemResultColumn> ResultColumns { get; }=[];
        public SqlServerSystemObject Build()=>new(scope,schema,name,type,objectId,resultSet,reason,this.Columns.ToArray(),this.Parameters.ToArray(),this.ResultColumns.ToArray());
    }
    // SQL Server の既定の照合順序と同じく、名前は大文字小文字を区別しない。
    private sealed class SystemObjectKeyComparer:IEqualityComparer<(SqlServerSystemScope Scope,string Schema,string Name)> {
        public static readonly SystemObjectKeyComparer Instance=new();
        public bool Equals((SqlServerSystemScope Scope,string Schema,string Name) x,(SqlServerSystemScope Scope,string Schema,string Name) y)=>x.Scope==y.Scope&&StringComparer.OrdinalIgnoreCase.Equals(x.Schema,y.Schema)&&StringComparer.OrdinalIgnoreCase.Equals(x.Name,y.Name);
        public int GetHashCode((SqlServerSystemScope Scope,string Schema,string Name) obj)=>HashCode.Combine(obj.Scope,StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Schema),StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }
}

public enum SqlServerSystemScope {
    // sys と INFORMATION_SCHEMA。どのデータベースから見ても同じ。
    System,
    // master の dbo にある出荷時オブジェクト。
    Master,
    // msdb の dbo にある出荷時オブジェクト。
    Msdb,
}

public enum SqlServerSystemResultSet {
    // 手続きではないので結果の形を持たない。
    NotApplicable,
    // 最初の結果の列が分かっている。
    Columns,
    // 結果を返さない。
    None,
    // 一時表や動的SQLなどのため、実行せずには結果の形が分からない。
    Undescribable,
}

// Type は sys.objects.type(V=ビュー、S=システム表、P=ストアド、X=拡張ストアド、FN/IF/TF=関数など)。
public sealed record SqlServerSystemObject(
    SqlServerSystemScope Scope,
    string Schema,
    string Name,
    string Type,
    int ObjectId,
    SqlServerSystemResultSet ResultSet,
    string? UndescribableReason,
    IReadOnlyList<SqlServerSystemColumn> Columns,
    IReadOnlyList<SqlServerSystemParameter> Parameters,
    IReadOnlyList<SqlServerSystemResultColumn> ResultColumns);

// TypeName は sys.types の名前(sysname などの別名型を含む)。MaxLength はバイト数で、-1 は max。
// Typical は、利用者のオブジェクトが載る行のほとんどが同じ値を持つ数値・bit の列の、その値(例: sys.all_columns.is_dropped_ledger_column は 0)。無ければ null。
public sealed record SqlServerSystemColumn(int ColumnId,string Name,string TypeName,short MaxLength,byte Precision,byte Scale,bool IsNullable,string? Collation,string? Typical=null);

// ParameterId 0 は関数の戻り値。
public sealed record SqlServerSystemParameter(int ParameterId,string Name,string TypeName,short MaxLength,byte Precision,byte Scale,bool IsOutput,bool HasDefaultValue);

// TypeName は長さ付きの型の表記(例: nvarchar(128))。
public sealed record SqlServerSystemResultColumn(int Ordinal,string? Name,string TypeName,bool IsNullable);
