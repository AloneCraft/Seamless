using Microsoft.SqlServer.TransactSql.ScriptDom;
namespace Seamless.Database.SqlTranslation;
/// <summary>
/// 構文木(ScriptDom)を、目録の列と型へ解決した中間表現(Bound*)にする。
/// 対応しない構文は <see cref="TranslationUnsupportedException"/> で、その位置と構文名を伝える。
/// </summary>
internal sealed partial class SqlBinder(TranslationCatalog catalog,string xmlNamespaces=""){
    private readonly TranslationCatalog catalog=catalog;
    /// <summary>WITH XMLNAMESPACES を XQuery の先頭の宣言にしたもの。xml のメソッドの XQuery の前に足す。</summary>
    private readonly string xmlNamespaces=xmlNamespaces;
    public List<TranslationDiagnostic> Warnings{get;}=[];
    // ---- 問い合わせ ----
    public BoundQuery BindQuery(QueryExpression query,BoundScope? outer){
        switch(query){
            case QuerySpecification specification:
                return this.BindSpecification(specification,outer);
            case QueryParenthesisExpression parenthesis:
                return this.BindQuery(parenthesis.QueryExpression,outer);
            case BinaryQueryExpression binary:{
                if(binary.OrderByClause is not null||binary.OffsetClause is not null){
                    throw Unsupported(binary,"SQLT030","集合演算の ORDER BY・OFFSET");
                }
                var left=this.BindQuery(binary.FirstQueryExpression,outer);
                var right=this.BindQuery(binary.SecondQueryExpression,outer);
                if(left.Items.Count!=right.Items.Count){
                    throw Unsupported(binary,"SQLT031","列の数が違う集合演算");
                }
                var operation=binary.BinaryQueryExpressionType switch{
                    BinaryQueryExpressionType.Union=>SetOperation.Union,
                    BinaryQueryExpressionType.Intersect=>SetOperation.Intersect,
                    BinaryQueryExpressionType.Except=>SetOperation.Except,
                    _=>throw Unsupported(binary,"SQLT032",binary.BinaryQueryExpressionType.ToString())
                };
                return new BoundSetQuery(left,right,operation,binary.All);
            }
            default:
                throw Unsupported(query,"SQLT001",query.GetType().Name);
        }
    }
    private BoundSelect BindSpecification(QuerySpecification query,BoundScope? outer){
        if(query.TopRowFilter is not null){
            throw Unsupported(query.TopRowFilter,"SQLT033","TOP");
        }
        if(query.OrderByClause is not null||query.OffsetClause is not null){
            throw Unsupported(query,"SQLT030","ORDER BY・OFFSET");
        }
        if(query.ForClause is not null){
            throw Unsupported(query.ForClause,"SQLT034","FOR XML・FOR JSON");
        }
        if(query.FromClause is null){
            throw Unsupported(query,"SQLT035","FROM の無い SELECT");
        }
        var scope=new BoundScope(outer);
        BoundFrom? from=null;
        var joins=new List<(BoundJoinFrom Join,BooleanExpression Condition)>();
        foreach(var reference in query.FromClause.TableReferences){
            var next=this.BindTableReference(reference,scope,joins);
            if(from is null){
                from=next;
            }else{
                from=new BoundJoinFrom(from,next,JoinKind.Cross);
            }
        }
        AssignPaths(from!,[],false);
        // 結合の条件は、FROM の表をすべて範囲に加えてから束縛する。
        foreach(var (join,condition) in joins){
            join.Condition=this.BindBoolean(condition,scope,false);
        }
        var select=new BoundSelect(scope,from!){Distinct=query.UniqueRowFilter==UniqueRowFilter.Distinct};
        if(query.WhereClause is{} where){
            if(where.Cursor is not null){
                throw Unsupported(where,"SQLT036","WHERE CURRENT OF");
            }
            select.Where=PushDownJoinConditions(this.BindBoolean(where.SearchCondition,scope,false),from!,scope);
        }
        if(query.GroupByClause is{} groupBy){
            if(groupBy.All||groupBy.GroupByOption!=GroupByOption.None){
                throw Unsupported(groupBy,"SQLT037","GROUP BY ALL・ROLLUP・CUBE");
            }
            foreach(var specification in groupBy.GroupingSpecifications){
                if(specification is not ExpressionGroupingSpecification expression){
                    throw Unsupported(specification,"SQLT037",specification.GetType().Name);
                }
                select.GroupBy.Add(this.BindScalar(expression.Expression,scope,false));
            }
        }
        var aggregateAllowed=true;
        foreach(var element in query.SelectElements){
            switch(element){
                case SelectStarExpression star:
                    foreach(var row in this.StarRows(star,scope)){
                        foreach(var column in row.Relation.Columns){
                            select.SelectItems.Add(new(column.Name,new BoundColumn(row,column)));
                        }
                    }
                    break;
                case SelectScalarExpression scalar:{
                    var bound=this.BindScalar(scalar.Expression,scope,aggregateAllowed);
                    var name=scalar.ColumnName?.Value;
                    if(name is null&&scalar.Expression is ColumnReferenceExpression reference){
                        name=reference.MultiPartIdentifier.Identifiers[^1].Value;
                    }
                    select.SelectItems.Add(new(name??"",bound));
                    break;
                }
                default:
                    throw Unsupported(element,"SQLT001",element.GetType().Name);
            }
        }
        if(query.HavingClause is{} having){
            select.Having=this.BindBoolean(having.SearchCondition,scope,true);
        }
        select.HasAggregates=select.SelectItems.Any(static item=>ContainsAggregate(item.Expression))||select.Having is not null&&ContainsAggregate(select.Having);
        if(select.GroupBy.Count>0||select.HasAggregates){
            this.CheckGrouped(select);
        }
        return select;
    }
    // GROUP BY のある問い合わせで、集約の外の列は GROUP BY の式でなければならない(SQL Server の 8120)。
    private void CheckGrouped(BoundSelect select){
        foreach(var item in select.SelectItems){
            if(!IsGroupedExpression(item.Expression,select)){
                throw new TranslationUnsupportedException("SQLT040",$"列 {item.Name} は集約にも GROUP BY にも含まれていない(本物ではエラー 8120)。",0,0);
            }
        }
    }
    private static bool IsGroupedExpression(BoundExpression expression,BoundSelect select){
        if(select.GroupBy.Contains(expression)){
            return true;
        }
        return expression switch{
            BoundAggregate=>true,
            BoundColumn column=>column.Row.Scope!=select.Scope,
            BoundLiteral=>true,
            BoundConvert convert=>IsGroupedExpression(convert.Operand,select),
            BoundArithmetic arithmetic=>IsGroupedExpression(arithmetic.Left,select)&&IsGroupedExpression(arithmetic.Right,select),
            BoundConcat concat=>IsGroupedExpression(concat.Left,select)&&IsGroupedExpression(concat.Right,select),
            BoundNegate negate=>IsGroupedExpression(negate.Operand,select),
            BoundCall call=>call.Arguments.All(argument=>argument.Expression is null||IsGroupedExpression(argument.Expression,select)),
            BoundCoalesce coalesce=>coalesce.Items.All(item=>IsGroupedExpression(item,select)),
            BoundCase @case=>@case.Branches.All(branch=>IsGroupedExpression(branch.When,select)&&IsGroupedExpression(branch.Then,select))&&(@case.Else is null||IsGroupedExpression(@case.Else,select)),
            BoundComparison comparison=>IsGroupedExpression(comparison.Left,select)&&IsGroupedExpression(comparison.Right,select),
            BoundLogical logical=>IsGroupedExpression(logical.Left,select)&&IsGroupedExpression(logical.Right,select),
            BoundNot not=>IsGroupedExpression(not.Operand,select),
            BoundIsNull isNull=>IsGroupedExpression(isNull.Operand,select),
            BoundNullIf nullIf=>IsGroupedExpression(nullIf.Left,select)&&IsGroupedExpression(nullIf.Right,select),
            _=>false
        };
    }
    internal static bool ContainsAggregate(BoundExpression expression)=>expression switch{
        BoundAggregate=>true,
        BoundConvert convert=>ContainsAggregate(convert.Operand),
        BoundArithmetic arithmetic=>ContainsAggregate(arithmetic.Left)||ContainsAggregate(arithmetic.Right),
        BoundConcat concat=>ContainsAggregate(concat.Left)||ContainsAggregate(concat.Right),
        BoundNegate negate=>ContainsAggregate(negate.Operand),
        BoundCall call=>call.Arguments.Any(static argument=>argument.Expression is not null&&ContainsAggregate(argument.Expression)),
        BoundCoalesce coalesce=>coalesce.Items.Any(ContainsAggregate),
        BoundCase @case=>@case.Branches.Any(static branch=>ContainsAggregate(branch.When)||ContainsAggregate(branch.Then))||@case.Else is not null&&ContainsAggregate(@case.Else),
        BoundComparison comparison=>ContainsAggregate(comparison.Left)||ContainsAggregate(comparison.Right),
        BoundLogical logical=>ContainsAggregate(logical.Left)||ContainsAggregate(logical.Right),
        BoundNot not=>ContainsAggregate(not.Operand),
        BoundIsNull isNull=>ContainsAggregate(isNull.Operand),
        BoundNullIf nullIf=>ContainsAggregate(nullIf.Left)||ContainsAggregate(nullIf.Right),
        _=>false
    };
    // ---- 条件の移動 ----
    /// <summary>
    /// WHERE の AND でつながった条件のうち、2つ以上の表にまたがるものを、それらを含む一番下の内部結合(またはクロス結合)の条件へ移す。
    /// 内部結合では WHERE に書いても ON に書いても結果は同じなので、等結合をハッシュ結合で出せるようにするため。
    /// 左外部結合の NULL になりうる側へは移さない。残った条件を返す(無ければ null)。
    /// </summary>
    private static BoundExpression? PushDownJoinConditions(BoundExpression where,BoundFrom from,BoundScope scope){
        var remaining=new List<BoundExpression>();
        foreach(var conjunct in Conjuncts(where)){
            var rows=RowsOf(conjunct,scope);
            if(rows.Count<2||!Place(from,conjunct,rows)){
                remaining.Add(conjunct);
            }
        }
        if(remaining.Count==0){
            return null;
        }
        return remaining.Aggregate(static (left,right)=>new BoundLogical('&',left,right));
    }
    internal static IEnumerable<BoundExpression> Conjuncts(BoundExpression expression){
        if(expression is BoundLogical{Operation:'&'} and){
            return Conjuncts(and.Left).Concat(Conjuncts(and.Right));
        }
        return [expression];
    }
    // 条件が参照する、この範囲の表。
    internal static HashSet<RowBinding> RowsOf(BoundExpression expression,BoundScope scope){
        var rows=new HashSet<RowBinding>();
        Visit(expression,expression=>{
            if(expression is BoundColumn column&&column.Row.Scope==scope){
                rows.Add(column.Row);
            }
        });
        return rows;
    }
    internal static HashSet<RowBinding> RowsUnder(BoundFrom from){
        var rows=new HashSet<RowBinding>();
        void Walk(BoundFrom node){
            switch(node){
                case BoundTableFrom table:
                    rows.Add(table.Row);
                    break;
                case BoundDerivedFrom derived:
                    rows.Add(derived.Row);
                    break;
                case BoundNodesFrom nodes:
                    rows.Add(nodes.Row);
                    break;
                case BoundJoinFrom join:
                    Walk(join.Left);
                    Walk(join.Right);
                    break;
            }
        }
        Walk(from);
        return rows;
    }
    private static bool Place(BoundFrom from,BoundExpression conjunct,HashSet<RowBinding> rows){
        // APPLY の右側(nodes)は左の行ごとに作るので、条件を移さない。
        if(from is not BoundJoinFrom join||join.Kind==JoinKind.Left||join.Right is BoundNodesFrom){
            return false;
        }
        if(rows.IsSubsetOf(RowsUnder(join.Left))){
            return Place(join.Left,conjunct,rows);
        }
        if(rows.IsSubsetOf(RowsUnder(join.Right))){
            return Place(join.Right,conjunct,rows);
        }
        if(!rows.IsSubsetOf(RowsUnder(join))){
            return false;
        }
        if(join.Condition is null){
            join.Condition=conjunct;
        }else{
            join.Condition=new BoundLogical('&',join.Condition,conjunct);
        }
        return true;
    }
    // 副問い合わせの中までたどる。
    internal static void Visit(BoundExpression expression,Action<BoundExpression> action){
        action(expression);
        switch(expression){
            case BoundConvert convert:
                Visit(convert.Operand,action);
                break;
            case BoundArithmetic arithmetic:
                Visit(arithmetic.Left,action);
                Visit(arithmetic.Right,action);
                break;
            case BoundConcat concat:
                Visit(concat.Left,action);
                Visit(concat.Right,action);
                break;
            case BoundNegate negate:
                Visit(negate.Operand,action);
                break;
            case BoundComparison comparison:
                Visit(comparison.Left,action);
                Visit(comparison.Right,action);
                break;
            case BoundLogical logical:
                Visit(logical.Left,action);
                Visit(logical.Right,action);
                break;
            case BoundNot not:
                Visit(not.Operand,action);
                break;
            case BoundIsNull isNull:
                Visit(isNull.Operand,action);
                break;
            case BoundLike like:
                Visit(like.Value,action);
                Visit(like.Pattern,action);
                break;
            case BoundIn @in:
                Visit(@in.Value,action);
                foreach(var item in @in.Items){
                    Visit(item,action);
                }
                break;
            case BoundInSubquery inSubquery:
                Visit(inSubquery.Value,action);
                VisitQuery(inSubquery.Query,action);
                break;
            case BoundExists exists:
                VisitQuery(exists.Query,action);
                break;
            case BoundScalarSubquery scalar:
                VisitQuery(scalar.Query,action);
                break;
            case BoundCase @case:
                foreach(var (when,then) in @case.Branches){
                    Visit(when,action);
                    Visit(then,action);
                }
                if(@case.Else is not null){
                    Visit(@case.Else,action);
                }
                break;
            case BoundCoalesce coalesce:
                foreach(var item in coalesce.Items){
                    Visit(item,action);
                }
                break;
            case BoundNullIf nullIf:
                Visit(nullIf.Left,action);
                Visit(nullIf.Right,action);
                break;
            case BoundCall call:
                foreach(var argument in call.Arguments){
                    if(argument.Expression is not null){
                        Visit(argument.Expression,action);
                    }
                }
                break;
            case BoundAggregate aggregate:
                if(aggregate.Argument is not null){
                    Visit(aggregate.Argument,action);
                }
                if(aggregate.Separator is not null){
                    Visit(aggregate.Separator,action);
                }
                break;
        }
    }
    private static void VisitQuery(BoundSelect query,Action<BoundExpression> action){
        if(query.Where is not null){
            Visit(query.Where,action);
        }
        foreach(var item in query.SelectItems){
            Visit(item.Expression,action);
        }
        void Walk(BoundFrom node){
            if(node is BoundJoinFrom join){
                if(join.Condition is not null){
                    Visit(join.Condition,action);
                }
                Walk(join.Left);
                Walk(join.Right);
            }
        }
        Walk(query.From);
    }
    // ---- FROM ----
    private BoundFrom BindTableReference(TableReference reference,BoundScope scope,List<(BoundJoinFrom Join,BooleanExpression Condition)> joins){
        switch(reference){
            case NamedTableReference named:{
                var name=named.SchemaObject;
                if(name.ServerIdentifier is not null||name.DatabaseIdentifier is not null){
                    throw Unsupported(named,"SQLT050","別のデータベース・サーバーの表(再現不可。模擬は段階3)");
                }
                // NOLOCK などの表のヒントは結果を変えないので読み飛ばす。
                var schema=name.SchemaIdentifier?.Value??this.catalog.DefaultSchema;
                var relation=this.catalog.Find(schema,name.BaseIdentifier.Value)
                    ??throw new TranslationUnsupportedException("SQLT051",$"表かビュー {schema}.{name.BaseIdentifier.Value} が目録に無い(本物ではエラー 208)。",named.StartLine,named.StartColumn);
                var row=new RowBinding(scope,named.Alias?.Value??relation.Name,relation);
                scope.Rows.Add(row);
                return new BoundTableFrom(row);
            }
            case QualifiedJoin join:{
                var left=this.BindTableReference(join.FirstTableReference,scope,joins);
                var right=this.BindTableReference(join.SecondTableReference,scope,joins);
                BoundJoinFrom bound;
                switch(join.QualifiedJoinType){
                    case QualifiedJoinType.Inner:
                        bound=new BoundJoinFrom(left,right,JoinKind.Inner);
                        break;
                    case QualifiedJoinType.LeftOuter:
                        bound=new BoundJoinFrom(left,right,JoinKind.Left);
                        break;
                    case QualifiedJoinType.RightOuter:
                        // 右外部結合は、左右を入れ替えた左外部結合と同じ。
                        bound=new BoundJoinFrom(right,left,JoinKind.Left);
                        break;
                    default:
                        throw Unsupported(join,"SQLT052",join.QualifiedJoinType.ToString());
                }
                // 結合ヒント(LOOP・HASH など)は結果を変えないので読み飛ばす。
                joins.Add((bound,join.SearchCondition));
                return bound;
            }
            case UnqualifiedJoin unqualified:{
                if(unqualified.UnqualifiedJoinType is UnqualifiedJoinType.CrossApply or UnqualifiedJoinType.OuterApply){
                    return this.BindApply(unqualified,scope,joins);
                }
                if(unqualified.UnqualifiedJoinType!=UnqualifiedJoinType.CrossJoin){
                    throw Unsupported(unqualified,"SQLT053",unqualified.UnqualifiedJoinType.ToString());
                }
                var left=this.BindTableReference(unqualified.FirstTableReference,scope,joins);
                var right=this.BindTableReference(unqualified.SecondTableReference,scope,joins);
                return new BoundJoinFrom(left,right,JoinKind.Cross);
            }
            case QueryDerivedTable derived:{
                // 派生表は、外側の問い合わせの範囲(相関)だけを見る。同じ FROM のほかの表は見えない。
                var query=this.Unify(this.BindQuery(derived.QueryExpression,scope.Outer));
                IList<string>? names=null;
                if(derived.Columns.Count>0){
                    names=derived.Columns.Select(static column=>column.Value).ToList();
                }
                return this.Derived(query,derived.Alias?.Value??"",names,scope,derived);
            }
            case PivotedTableReference pivot:
                return this.BindPivot(pivot,scope);
            case JoinParenthesisTableReference parenthesis:
                return this.BindTableReference(parenthesis.Join,scope,joins);
            default:
                throw Unsupported(reference,"SQLT054",reference.GetType().Name);
        }
    }
    private static void AssignPaths(BoundFrom from,List<PathSegment> prefix,bool nullable){
        from.Prefix=prefix;
        switch(from){
            case BoundTableFrom table:
                table.Row.Path=prefix;
                table.Row.Nullable=nullable;
                break;
            case BoundDerivedFrom derived:
                derived.Row.Path=prefix;
                derived.Row.Nullable=nullable;
                break;
            case BoundNodesFrom nodes:
                nodes.Row.Path=prefix;
                nodes.Row.Nullable=nullable;
                break;
            case BoundJoinFrom join:{
                AssignPaths(join.Left,[..prefix,new("Item1",false)],nullable);
                var rightNullable=join.Kind==JoinKind.Left;
                AssignPaths(join.Right,[..prefix,new("Item2",rightNullable)],nullable||rightNullable);
                break;
            }
        }
    }
    private IEnumerable<RowBinding> StarRows(SelectStarExpression star,BoundScope scope){
        if(star.Qualifier is null){
            return scope.Rows;
        }
        var alias=star.Qualifier.Identifiers[^1].Value;
        var rows=scope.Rows.Where(row=>row.Alias.Equals(alias,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(rows.Length==0){
            throw new TranslationUnsupportedException("SQLT055",$"別名 {alias} が FROM に無い(本物ではエラー 4104)。",star.StartLine,star.StartColumn);
        }
        return rows;
    }
    // ---- APPLY ----
    /// <summary>
    /// CROSS APPLY・OUTER APPLY。右側は、左の列の xml に対する nodes() だけを扱う(サンプルDBのビューの使い方)。
    /// OUTER APPLY は、節が1つも無い左の行を、右を NULL にして残す。
    /// </summary>
    private BoundFrom BindApply(UnqualifiedJoin apply,BoundScope scope,List<(BoundJoinFrom Join,BooleanExpression Condition)> joins){
        var left=this.BindTableReference(apply.FirstTableReference,scope,joins);
        if(apply.SecondTableReference is not SchemaObjectFunctionTableReference function
            ||!function.SchemaObject.BaseIdentifier.Value.Equals("nodes",StringComparison.OrdinalIgnoreCase)
            ||function.Parameters.Count!=1||function.Parameters[0] is not StringLiteral query||function.Columns.Count!=1){
            throw Unsupported(apply.SecondTableReference,"SQLT059","xml の nodes() でない APPLY");
        }
        // 列の参照は、nodes の前の名前(別名.列 または 列)。
        var parts=new MultiPartIdentifier();
        foreach(var identifier in function.SchemaObject.Identifiers.Take(function.SchemaObject.Identifiers.Count-1)){
            parts.Identifiers.Add(identifier);
        }
        var source=this.ResolveColumn(new ColumnReferenceExpression{MultiPartIdentifier=parts},scope);
        if(source.Type.Name is not("xml" or "xmlnode")){
            throw Unsupported(function,"SQLT059",$"{source.Type.Name} の nodes()");
        }
        var column=new CatalogColumn(function.Columns[0].Value,"Item1",SqlTypeRules.XmlNode,true);
        var relation=new CatalogRelation("",function.Alias?.Value??"",$"",$"global::System.ValueTuple<{SqlTypeRules.ClrType(SqlTypeRules.XmlNode)}>",[column]){InternalValues=true};
        var row=new RowBinding(scope,function.Alias?.Value??"",relation);
        scope.Rows.Add(row);
        var kind=JoinKind.Cross;
        if(apply.UnqualifiedJoinType==UnqualifiedJoinType.OuterApply){
            kind=JoinKind.Left;
        }
        return new BoundJoinFrom(left,new BoundNodesFrom(row,source,this.xmlNamespaces+query.Value),kind);
    }
    // ---- 派生表 ----
    /// <summary>問い合わせを、FROM の1つの表(行は値のタプル)にする。</summary>
    private BoundDerivedFrom Derived(BoundQuery query,string alias,IList<string>? names,BoundScope scope,TSqlFragment at){
        var items=query.Items;
        if(names is not null&&names.Count!=items.Count){
            throw new TranslationUnsupportedException("SQLT056","派生表の列の名前の数が、SELECT の列の数と違う(本物ではエラー 8158)。",at.StartLine,at.StartColumn);
        }
        var types=new List<SqlTypeDescriptor>();
        var columns=new List<CatalogColumn>();
        for(var index=0;index<items.Count;index++){
            var type=items[index].Expression.Type;
            if(SqlTypeRules.IsNull(type)){
                type=SqlTypeRules.Int;
            }
            var name=names?[index]??items[index].Name;
            if(name.Length==0){
                throw new TranslationUnsupportedException("SQLT057",$"派生表の{index+1}番目の列に名前が無い(本物ではエラー 8155)。",at.StartLine,at.StartColumn);
            }
            types.Add(type);
            columns.Add(new(name,$"Item{index+1}",type,true));
        }
        var identity=!IsSetLike(query);
        var relation=new CatalogRelation("",alias,"",CSharpEmitter.TupleType(types,identity),columns){InternalValues=true};
        var row=new RowBinding(scope,alias,relation);
        scope.Rows.Add(row);
        return new BoundDerivedFrom(row,query,types,identity);
    }
    // 結果が初めから重複を持たない問い合わせか(GROUP BY・集約・DISTINCT、UNION・INTERSECT・EXCEPT)。
    private static bool IsSetLike(BoundQuery query)=>query switch{
        BoundSelect select=>select.GroupBy.Count>0||select.HasAggregates||select.Distinct,
        BoundSetQuery set=>!set.All,
        _=>true
    };
    /// <summary>集合演算の左右の列の型を、共通の型へそろえる。</summary>
    internal BoundQuery Unify(BoundQuery query){
        if(query is not BoundSetQuery set){
            return query;
        }
        var left=this.Unify(set.Left);
        var right=this.Unify(set.Right);
        var types=left.Items.Zip(right.Items,static (a,b)=>SqlTypeRules.Common(a.Expression.Type,b.Expression.Type)).ToList();
        return new BoundSetQuery(this.Retype(left,types),this.Retype(right,types),set.Operation,set.All);
    }
    private BoundQuery Retype(BoundQuery query,List<SqlTypeDescriptor> types){
        switch(query){
            case BoundSelect select:
                for(var index=0;index<select.SelectItems.Count;index++){
                    var item=select.SelectItems[index];
                    select.SelectItems[index]=item with{Expression=this.Convert(item.Expression,types[index],new SelectStatement())};
                }
                return select;
            case BoundSetQuery set:
                return new BoundSetQuery(this.Retype(set.Left,types),this.Retype(set.Right,types),set.Operation,set.All);
            default:
                return query;
        }
    }
    // ---- PIVOT ----
    /// <summary>
    /// PIVOT を、本物と同じ意味の GROUP BY に書き換えて派生表にする。
    /// 元の表の列のうち、集める列と並べ替える列を除いたものでまとめ、IN の値ごとに 集約(CASE WHEN 並べ替える列 = 値 THEN 集める列 END) を列にする。
    /// </summary>
    private BoundDerivedFrom BindPivot(PivotedTableReference pivot,BoundScope scope){
        if(pivot.ValueColumns.Count!=1){
            throw Unsupported(pivot,"SQLT058","列が1つでない PIVOT");
        }
        var inner=new BoundScope(scope.Outer);
        var joins=new List<(BoundJoinFrom Join,BooleanExpression Condition)>();
        var source=this.BindTableReference(pivot.TableReference,inner,joins);
        AssignPaths(source,[],false);
        foreach(var (join,condition) in joins){
            join.Condition=this.BindBoolean(condition,inner,false);
        }
        var select=new BoundSelect(inner,source);
        if(this.ResolveColumn(pivot.PivotColumn,inner) is not BoundColumn pivotColumn||this.ResolveColumn(pivot.ValueColumns[0],inner) is not BoundColumn valueColumn){
            throw Unsupported(pivot,"SQLT058","PIVOT の列");
        }
        foreach(var row in inner.Rows){
            foreach(var column in row.Relation.Columns){
                if(row==pivotColumn.Row&&column==pivotColumn.Column||row==valueColumn.Row&&column==valueColumn.Column){
                    continue;
                }
                var bound=new BoundColumn(row,column);
                select.GroupBy.Add(bound);
                select.SelectItems.Add(new(column.Name,bound));
            }
        }
        var aggregate=pivot.AggregateFunctionIdentifier.Identifiers[^1].Value.ToUpperInvariant();
        foreach(var value in pivot.InColumns){
            var when=this.Compare("Equal",pivotColumn,this.LiteralOf(value.Value,pivotColumn.Type,value),value);
            var @case=new BoundCase([(when,(BoundExpression)valueColumn)],null,valueColumn.Type);
            select.SelectItems.Add(new(value.Value,this.Aggregate(aggregate,@case,false,pivot)));
        }
        select.HasAggregates=true;
        return this.Derived(select,pivot.Alias?.Value??"",null,scope,pivot);
    }
    // PIVOT の IN の値(識別子として書く)を、並べ替える列の型のリテラルにする。
    private BoundExpression LiteralOf(string text,SqlTypeDescriptor type,TSqlFragment at){
        BoundExpression literal;
        if(SqlTypeRules.IsInteger(type)){
            literal=IntegerLiteralOf(text,false);
        }else if(SqlTypeRules.IsNumeric(type)){
            literal=DecimalLiteralOf(text,false);
        }else{
            literal=new BoundLiteral(CSharpNames.String(text),SqlTypeRules.NVarChar(Math.Max(1,text.Length),this.catalog.DatabaseCollation));
        }
        return this.Convert(literal,type,at,0,true);
    }
    internal static TranslationUnsupportedException Unsupported(TSqlFragment fragment,string code,string what)=>new(code,$"未対応: {what}",fragment.StartLine,fragment.StartColumn);
}
