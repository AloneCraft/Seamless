namespace Seamless.Database.SqlTranslation;
// 束縛済みの中間表現。名前は目録の列へ、式は型付きの木へ解決してある。C# の出力(CSharpEmitter)だけがこれを読む。

/// <summary>FROM の行がたどる道のり。タプルの Item1・Item2 をたどり、外部結合で NULL になりうる段では ?. を使う。</summary>
internal sealed record PathSegment(string Member,bool Nullable);
/// <summary>1つの問い合わせ(SELECT)の範囲。相関副問い合わせは外側の範囲の行を参照する。</summary>
internal sealed class BoundScope(BoundScope? outer){
    public BoundScope? Outer{get;}=outer;
    public List<RowBinding> Rows{get;}=[];
}
/// <summary>FROM の1つの表(別名つき)。Path は FROM 全体の行(結合のタプル)の根からの道のり。</summary>
internal sealed class RowBinding(BoundScope scope,string alias,CatalogRelation relation){
    public BoundScope Scope{get;}=scope;
    public string Alias{get;}=alias;
    public CatalogRelation Relation{get;}=relation;
    public List<PathSegment> Path{get;set;}=[];
    /// <summary>外部結合の NULL になりうる側か。</summary>
    public bool Nullable{get;set;}
}
// ---- FROM ----
internal abstract record BoundFrom{
    /// <summary>この部分の行の C# の型。</summary>
    public abstract string ElementType{get;}
    /// <summary>この部分の行が、FROM 全体の行の根からたどる道のり。</summary>
    public List<PathSegment> Prefix{get;set;}=[];
}
internal sealed record BoundTableFrom(RowBinding Row):BoundFrom{
    public override string ElementType=>this.Row.Relation.RowType;
}
/// <summary>派生表(FROM の中の SELECT)と PIVOT。行は値のタプルで、列は Item1・Item2…。</summary>
/// <remarks>
/// 重複を除かない派生表(GROUP BY・集約・DISTINCT・UNION の無い SELECT と UNION ALL)は、最後の要素に元の行(<see cref="Identity"/>)を持つ。
/// 同じ値の行が1つにまとまると、外側の集約(SUM・COUNT)が本物と変わるため。
/// </remarks>
internal sealed record BoundDerivedFrom(RowBinding Row,BoundQuery Query,IReadOnlyList<SqlTypeDescriptor> Types,bool Identity):BoundFrom{
    public override string ElementType=>CSharpEmitter.TupleType(this.Types,this.Identity);
}
/// <summary>CROSS APPLY・OUTER APPLY の右側の xml.nodes()。左の行ごとに、選んだ節の並びになる。行は ValueTuple&lt;SqlXmlNode?&gt;。</summary>
internal sealed record BoundNodesFrom(RowBinding Row,BoundExpression Source,string Query):BoundFrom{
    public override string ElementType=>$"global::System.ValueTuple<{SqlTypeRules.ClrType(SqlTypeRules.XmlNode)}>";
}
internal enum JoinKind{Inner,Left,Cross}
internal sealed record BoundJoinFrom(BoundFrom Left,BoundFrom Right,JoinKind Kind):BoundFrom{
    public BoundExpression? Condition{get;set;}
    public override string ElementType{
        get{
            var right=this.Right.ElementType;
            if(this.Kind==JoinKind.Left){
                right+="?";
            }
            return $"({this.Left.ElementType},{right})";
        }
    }
}
// ---- 問い合わせ ----
internal sealed record BoundSelectItem(string Name,BoundExpression Expression);
internal abstract record BoundQuery{
    public abstract IReadOnlyList<BoundSelectItem> Items{get;}
}
internal sealed record BoundSelect(BoundScope Scope,BoundFrom From):BoundQuery{
    public BoundExpression? Where{get;set;}
    public List<BoundExpression> GroupBy{get;}=[];
    public BoundExpression? Having{get;set;}
    public List<BoundSelectItem> SelectItems{get;}=[];
    public bool HasAggregates{get;set;}
    /// <summary>SELECT DISTINCT か。</summary>
    public bool Distinct{get;set;}
    public override IReadOnlyList<BoundSelectItem> Items=>this.SelectItems;
}
internal enum SetOperation{Union,Intersect,Except}
internal sealed record BoundSetQuery(BoundQuery Left,BoundQuery Right,SetOperation Operation,bool All=false):BoundQuery{
    public override IReadOnlyList<BoundSelectItem> Items=>this.Left.Items;
}
// ---- 式 ----
internal abstract record BoundExpression(SqlTypeDescriptor Type);
internal sealed record BoundColumn(RowBinding Row,CatalogColumn Column):BoundExpression(Column.Type);
/// <summary>C# のリテラル(型つき)をそのまま持つ。</summary>
internal sealed record BoundLiteral(string Code,SqlTypeDescriptor LiteralType):BoundExpression(LiteralType);
internal sealed record BoundConvert(BoundExpression Operand,SqlTypeDescriptor Target,int Style):BoundExpression(Target);
internal sealed record BoundArithmetic(char Operation,BoundExpression Left,BoundExpression Right,SqlTypeDescriptor ResultType):BoundExpression(ResultType);
internal sealed record BoundConcat(BoundExpression Left,BoundExpression Right,SqlTypeDescriptor ResultType):BoundExpression(ResultType);
internal sealed record BoundNegate(BoundExpression Operand):BoundExpression(Operand.Type);
internal sealed record BoundComparison(string Operation,BoundExpression Left,BoundExpression Right,string? Collation):BoundExpression(SqlTypeRules.Bit);
internal sealed record BoundLogical(char Operation,BoundExpression Left,BoundExpression Right):BoundExpression(SqlTypeRules.Bit);
internal sealed record BoundNot(BoundExpression Operand):BoundExpression(SqlTypeRules.Bit);
internal sealed record BoundIsNull(BoundExpression Operand,bool Negated):BoundExpression(SqlTypeRules.Bit);
internal sealed record BoundLike(BoundExpression Value,BoundExpression Pattern,string? Escape,string Collation,bool Negated):BoundExpression(SqlTypeRules.Bit);
internal sealed record BoundIn(BoundExpression Value,IReadOnlyList<BoundExpression> Items,string? Collation,bool Negated):BoundExpression(SqlTypeRules.Bit){
    public bool Equals(BoundIn? other)=>other is not null&&this.Value==other.Value&&this.Items.SequenceEqual(other.Items)&&this.Collation==other.Collation&&this.Negated==other.Negated;
    public override int GetHashCode()=>HashCode.Combine(this.Value,this.Items.Count);
}
internal sealed record BoundInSubquery(BoundExpression Value,BoundSelect Query,string? Collation,bool Negated):BoundExpression(SqlTypeRules.Bit);
internal sealed record BoundExists(BoundSelect Query,bool Negated):BoundExpression(SqlTypeRules.Bit);
internal sealed record BoundScalarSubquery(BoundSelect Query,SqlTypeDescriptor ResultType):BoundExpression(ResultType);
internal sealed record BoundCase(IReadOnlyList<(BoundExpression When,BoundExpression Then)> Branches,BoundExpression? Else,SqlTypeDescriptor ResultType):BoundExpression(ResultType){
    public bool Equals(BoundCase? other)=>other is not null&&this.Branches.SequenceEqual(other.Branches)&&this.Else==other.Else&&this.ResultType==other.ResultType;
    public override int GetHashCode()=>HashCode.Combine(this.Branches.Count,this.ResultType);
}
internal sealed record BoundCoalesce(IReadOnlyList<BoundExpression> Items,SqlTypeDescriptor ResultType):BoundExpression(ResultType){
    public bool Equals(BoundCoalesce? other)=>other is not null&&this.Items.SequenceEqual(other.Items)&&this.ResultType==other.ResultType;
    public override int GetHashCode()=>HashCode.Combine(this.Items.Count,this.ResultType);
}
internal sealed record BoundNullIf(BoundExpression Left,BoundExpression Right,string? Collation):BoundExpression(Left.Type);
/// <summary>関数の部品の呼び出し。引数は式か、そのまま書く C# のコード(セッション・照合順序・日付の単位)。</summary>
internal sealed record BoundCall(string Method,IReadOnlyList<BoundArgument> Arguments,SqlTypeDescriptor ResultType):BoundExpression(ResultType){
    public bool Equals(BoundCall? other)=>other is not null&&this.Method==other.Method&&this.Arguments.SequenceEqual(other.Arguments)&&this.ResultType==other.ResultType;
    public override int GetHashCode()=>HashCode.Combine(this.Method,this.Arguments.Count);
}
internal sealed record BoundArgument(BoundExpression? Expression,string? Code,string? Cast);
/// <summary>集約。Argument が null なら COUNT(*)。</summary>
internal sealed record BoundAggregate(string Kind,BoundExpression? Argument,bool Distinct,BoundExpression? Separator,SqlTypeDescriptor ResultType):BoundExpression(ResultType);
