using Microsoft.SqlServer.TransactSql.ScriptDom;
namespace Seamless.Database.SqlTranslation;
/// <summary>
/// 構文木(ScriptDom)を、目録の列と型へ解決した中間表現(Bound*)にする。
/// 対応しない構文は <see cref="TranslationUnsupportedException"/> で、その位置と構文名を伝える。
/// </summary>
internal sealed partial class SqlBinder(TranslationCatalog catalog){
    private readonly TranslationCatalog catalog=catalog;
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
                return new BoundSetQuery(left,right,operation);
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
        var select=new BoundSelect(scope,from!);
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
        if(from is not BoundJoinFrom join||join.Kind==JoinKind.Left){
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
                if(unqualified.UnqualifiedJoinType!=UnqualifiedJoinType.CrossJoin){
                    throw Unsupported(unqualified,"SQLT053",unqualified.UnqualifiedJoinType.ToString());
                }
                var left=this.BindTableReference(unqualified.FirstTableReference,scope,joins);
                var right=this.BindTableReference(unqualified.SecondTableReference,scope,joins);
                return new BoundJoinFrom(left,right,JoinKind.Cross);
            }
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
    internal static TranslationUnsupportedException Unsupported(TSqlFragment fragment,string code,string what)=>new(code,$"未対応: {what}",fragment.StartLine,fragment.StartColumn);
}
