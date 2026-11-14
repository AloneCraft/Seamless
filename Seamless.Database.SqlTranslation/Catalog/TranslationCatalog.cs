namespace Seamless.Database.SqlTranslation;
/// <summary>SQL の型。名前は小文字の SQL Server の型名(nvarchar・decimal など)。長さ -1 は max。</summary>
public sealed record SqlTypeDescriptor(string Name,int? Length=null,byte? Precision=null,byte? Scale=null,string? Collation=null){
    /// <summary>型名から作る。別名の型(sysname など)は元の型へ直す。</summary>
    public static SqlTypeDescriptor Of(string name,int? length=null,byte? precision=null,byte? scale=null,string? collation=null){
        var lower=name.ToLowerInvariant();
        if(lower=="sysname"){
            return new("nvarchar",128,null,null,collation);
        }
        if(lower=="numeric"){
            lower="decimal";
        }
        return new(lower,length,precision,scale,collation);
    }
    /// <inheritdoc/>
    public override string ToString(){
        if(this.Length is not null&&(SqlTypeRules.IsString(this)||SqlTypeRules.IsBinary(this))){
            if(this.Length==-1){
                return this.Name+"(max)";
            }
            return $"{this.Name}({this.Length})";
        }
        if(this.Name=="decimal"){
            return $"decimal({this.Precision},{this.Scale})";
        }
        return this.Name;
    }
}
/// <summary>表またはビューの列。</summary>
/// <param name="Name">SQL 上の列名。</param>
/// <param name="CSharpName">生成された行の型のプロパティ名。</param>
/// <param name="Type">SQL の型。</param>
/// <param name="IsNullable">NULL を許すか。生成された行のプロパティの型(int か int? か)を決める。</param>
public sealed record CatalogColumn(string Name,string CSharpName,SqlTypeDescriptor Type,bool IsNullable);
/// <summary>FROM に書ける表またはビュー。</summary>
/// <param name="Schema">SQL 上のスキーマ名。</param>
/// <param name="Name">SQL 上の名前。</param>
/// <param name="Access">読み取りの入口 q からたどる式(q を除く。例: <c>Database.Sales.Customer</c>)。</param>
/// <param name="RowType">行の型の完全な名前(global:: から)。</param>
/// <param name="Columns">列。</param>
/// <param name="IsView">ビューのとき true。</param>
public sealed record CatalogRelation(string Schema,string Name,string Access,string RowType,IReadOnlyList<CatalogColumn> Columns,bool IsView=false);
/// <summary>
/// 翻訳に使う目録。表とビューの列と型、生成されるコンテナの名前を持つ。
/// 取込の生成器が、自分の付けた C# の名前で作って渡す。
/// </summary>
public sealed class TranslationCatalog{
    private readonly Dictionary<(string Schema,string Name),CatalogRelation> relations=new(SchemaNameComparer.Instance);
    /// <param name="rootNamespace">生成されるコードのルートの名前空間。</param>
    /// <param name="containerType">コンテナの型の完全な名前(global:: から)。読み取りの入口はこの入れ子の I{スキーマ}Reader。</param>
    /// <param name="databaseCollation">データベースの既定の照合順序。リテラルの文字列と CHAR() に使う。</param>
    /// <param name="relations">表とビュー。</param>
    public TranslationCatalog(string rootNamespace,string containerType,string databaseCollation,IEnumerable<CatalogRelation> relations){
        this.RootNamespace=rootNamespace;
        this.ContainerType=containerType;
        this.DatabaseCollation=databaseCollation;
        foreach(var relation in relations){
            this.relations[(relation.Schema,relation.Name)]=relation;
        }
    }
    /// <summary>ルートの名前空間。</summary>
    public string RootNamespace{get;}
    /// <summary>コンテナの型。</summary>
    public string ContainerType{get;}
    /// <summary>データベースの既定の照合順序。</summary>
    public string DatabaseCollation{get;}
    /// <summary>スキーマを書かない名前を探すときのスキーマ。</summary>
    public string DefaultSchema{get;init;}="dbo";
    /// <summary>表かビューを探す。大文字小文字は区別しない(SQL Server の既定と同じ)。</summary>
    public CatalogRelation? Find(string schema,string name){
        this.relations.TryGetValue((schema,name),out var relation);
        return relation;
    }
    /// <summary>スキーマの読み取りの入口の型(拡張ブロックの受け手)。</summary>
    public string ReaderType(string schema)=>$"{this.ContainerType}.I{CSharpNames.Identifier(schema)}Reader";
    private sealed class SchemaNameComparer:IEqualityComparer<(string Schema,string Name)>{
        public static SchemaNameComparer Instance{get;}=new();
        public bool Equals((string Schema,string Name) x,(string Schema,string Name) y)=>string.Equals(x.Schema,y.Schema,StringComparison.OrdinalIgnoreCase)&&string.Equals(x.Name,y.Name,StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string Schema,string Name) value)=>HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(value.Schema),StringComparer.OrdinalIgnoreCase.GetHashCode(value.Name));
    }
}
