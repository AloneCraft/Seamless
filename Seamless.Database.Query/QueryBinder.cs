using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Linq;
namespace Seamless.Database.Query;
internal sealed partial class QueryBinder{
    private readonly ISqlCatalog catalog;
    private readonly string database;
    private readonly IReadOnlyList<SqlParameter>parameters;
    private readonly HashSet<SqlObjectName>viewStack;
    private readonly Dictionary<string,string>xmlNamespaces=new(StringComparer.Ordinal);
    private readonly Dictionary<string,BoundQuery>ctes=new(StringComparer.OrdinalIgnoreCase);
    internal QueryBinder(ISqlCatalog catalog,string database,IReadOnlyList<SqlParameter>parameters,HashSet<SqlObjectName>?viewStack=null){this.catalog=catalog;this.database=database;this.parameters=parameters;this.viewStack=viewStack??[];}
    internal BoundQuery BindStatement(TSqlStatement statement){
        if(statement is not SelectStatement select){throw Unsupported(statement);}
        if(select.Into is not null||select.On is not null||select.ComputeClauses.Count>0||select.OptimizerHints.Count>0){throw Unsupported(select);}
        if(select.WithCtesAndXmlNamespaces is{} with){
            if(with.ChangeTrackingContext is not null){throw Unsupported(with);}
            if(with.XmlNamespaces is{} namespaces){foreach(var entry in namespaces.XmlNamespacesElements){string prefix="";if(entry is XmlNamespacesAliasElement alias){prefix=alias.Identifier.Value;}else if(entry is not XmlNamespacesDefaultElement){throw Unsupported(entry);}this.xmlNamespaces.Add(prefix,entry.String.Value);}}
            foreach(var cte in with.CommonTableExpressions){var query=this.BindQuery(cte.QueryExpression,[]);if(cte.Columns.Count>0){query=Rename(query,cte.Columns);}this.ctes.Add(cte.ExpressionName.Value,query);}
        }
        return this.BindQuery(select.QueryExpression,[]);
    }
    private BoundQuery BindQuery(QueryExpression expression,IReadOnlyList<BindingColumn>outer){
        if(expression is QueryParenthesisExpression parenthesis){return this.BindQuery(parenthesis.QueryExpression,outer);}
        if(expression is BinaryQueryExpression binary){return this.BindUnion(binary,outer);}
        if(expression is not QuerySpecification query){throw Unsupported(expression);}
        if(query.ForClause is not null||query.OffsetClause is not null||query.WindowClause is not null){throw Unsupported(query);}
        BoundSource source=new([],(_,_)=>OneRow());
        if(query.FromClause is{} from){foreach(var table in from.TableReferences){var next=this.BindTable(table,outer);source=Cross(source,next);}}
        var aggregateBindings=new List<AggregateBinding>();
        Func<EvalFrame,bool?>?predicate=null;
        if(query.WhereClause is{} where){predicate=this.BindBoolean(where.SearchCondition,source.Columns,outer,null);}
        var groupExpressions=new List<BoundScalar>();
        if(query.GroupByClause is{} group){
            if(group.All||group.GroupByOption!=GroupByOption.None){throw Unsupported(group);}
            foreach(var spec in group.GroupingSpecifications){if(spec is not ExpressionGroupingSpecification item){throw Unsupported(spec);}groupExpressions.Add(this.BindScalar(item.Expression,source.Columns,outer,null));}
        }
        var projections=new List<BoundScalar>();var columns=new List<SqlColumn>();
        foreach(var element in query.SelectElements){
            if(element is SelectStarExpression star){
                var qualifier=star.Qualifier?.Identifiers.LastOrDefault()?.Value;
                var count=0;
                for(var index=0;index<source.Columns.Count;index++){var item=source.Columns[index];if(qualifier is not null&&!Equal(qualifier,item.Qualifier)){continue;}var position=index;projections.Add(new(item.Column.Type,item.Column.IsNullable,f=>f.Row[position],item.Column));columns.Add(item.Column with{Ordinal=columns.Count});count++;}
                if(count==0){throw new SqlQueryException("Star qualifier does not resolve.",4104);}
            }else if(element is SelectScalarExpression scalar){
                var value=this.BindScalar(scalar.Expression,source.Columns,outer,aggregateBindings);
                var name=scalar.ColumnName?.Value??value.Origin?.Name??"";
                projections.Add(value);columns.Add(new(name,value.Type,value.Nullable,value.Origin?.Source,columns.Count));
            }else{throw Unsupported(element);}
        }
        Func<EvalFrame,bool?>?having=null;
        if(query.HavingClause is{} havingClause){having=this.BindBoolean(havingClause.SearchCondition,source.Columns,outer,aggregateBindings);}
        var order=new List<(BoundScalar Value,bool Descending)>();
        if(query.OrderByClause is{} orderBy){foreach(var item in orderBy.OrderByElements){
            BoundScalar value;
            if(item.Expression is IntegerLiteral ordinal){var position=int.Parse(ordinal.Value,System.Globalization.CultureInfo.InvariantCulture)-1;if(position<0||position>=projections.Count){throw new SqlQueryException("ORDER BY position outside select list.",108);}value=projections[position];}
            else if(item.Expression is ColumnReferenceExpression reference&&reference.MultiPartIdentifier.Identifiers.Count==1&&columns.FindIndex(x=>Equal(x.Name,reference.MultiPartIdentifier.Identifiers[0].Value)) is var aliasIndex&&aliasIndex>=0){value=projections[aliasIndex];}
            else{value=this.BindScalar(item.Expression,source.Columns,outer,aggregateBindings);}
            order.Add((value,item.SortOrder==SortOrder.Descending));
        }}
        BoundScalar?top=null;
        if(query.TopRowFilter is{} topClause){if(topClause.Percent||topClause.WithTies){throw Unsupported(topClause);}top=this.BindScalar(topClause.Expression,[],outer,null);}
        var grouped=groupExpressions.Count>0||aggregateBindings.Count>0||having is not null;
        if(grouped){
            if(query.SelectElements.Any(x=>x is SelectStarExpression)){throw new SqlQueryException("A grouped SELECT requires explicit projection columns.",40515);}
            var groupSql=new HashSet<string>(query.GroupByClause?.GroupingSpecifications.OfType<ExpressionGroupingSpecification>().Select(x=>GroupText(x.Expression))??[],StringComparer.Ordinal);
            foreach(var scalar in query.SelectElements.OfType<SelectScalarExpression>()){ValidateGrouping(scalar.Expression,groupSql);}
            if(query.HavingClause is{} groupHaving){ValidateGrouping(groupHaving.SearchCondition,groupSql);}
            if(query.OrderByClause is{} groupedOrder){foreach(var item in groupedOrder.OrderByElements){if(item.Expression is IntegerLiteral){continue;}if(item.Expression is ColumnReferenceExpression reference&&reference.MultiPartIdentifier.Identifiers.Count==1&&columns.Any(x=>Equal(x.Name,reference.MultiPartIdentifier.Identifiers[0].Value))){continue;}ValidateGrouping(item.Expression,groupSql);}}
        }
        var captured=source;
        return new(columns,(execution,outerRow)=>Rows(execution,outerRow));
        IEnumerable<object?[]>Rows(QueryExecution execution,object?[]?outerRow){
            var rows=captured.Rows(execution,outerRow);
            if(predicate is not null){rows=rows.Where(row=>predicate(new(row,outerRow,execution))==true);}
            IEnumerable<(object?[] Values,object?[] Keys)>projected;
            if(grouped){projected=GroupRows(rows,execution,outerRow);}
            else{projected=Project(rows,execution,outerRow);}
            if(query.UniqueRowFilter==UniqueRowFilter.Distinct){
                projected=DistinctRows(projected,execution,columns.Select(x=>x.Type).ToArray());
            }
            if(order.Count>0){
                projected=SqlBoundedOperators.Sort(execution,projected,x=>x.Keys,new SqlRowComparer(order.Select(x=>x.Descending).ToArray(),order.Select(x=>x.Value.Type).ToArray()));
            }
            long remaining=long.MaxValue;
            if(top is not null){var limit=top.Value(new([],outerRow,execution));if(limit is null){throw new SqlQueryException("TOP cannot be NULL.",1014);}remaining=(long)SqlValues.ConvertTo(limit,SqlType.BigInt)!;if(remaining<0){throw new SqlQueryException("TOP cannot be negative.",1014);}}
            using var result=projected.GetEnumerator();
            while(remaining>0&&result.MoveNext()){remaining--;execution.Scope.ThrowIfUnavailable();yield return result.Current.Values;}
        }
        IEnumerable<(object?[],object?[])>Project(IEnumerable<object?[]>rows,QueryExecution execution,object?[]?outerRow){foreach(var row in rows){var frame=new EvalFrame(row,outerRow,execution);if(having is null||having(frame)==true){yield return(projections.Select(x=>x.Value(frame)).ToArray(),order.Select(x=>x.Value.Value(frame)).ToArray());}}}
        IEnumerable<(object?[],object?[])>GroupRows(IEnumerable<object?[]>rows,QueryExecution execution,object?[]?outerRow){
            IEnumerable<object?[]> ordered=rows;
            if(groupExpressions.Count>0){ordered=SqlBoundedOperators.Sort(execution,rows,row=>groupExpressions.Select(x=>x.Value(new(row,outerRow,execution))).ToArray(),new SqlRowComparer(types:groupExpressions.Select(x=>x.Type).ToArray()));}
            object?[]?key=null;object?[]?first=null;AggregateState[]?states=null;
            foreach(var row in ordered){
                var frame=new EvalFrame(row,outerRow,execution);var next=groupExpressions.Select(x=>x.Value(frame)).ToArray();
                if(states is not null&&new SqlRowComparer(types:groupExpressions.Select(x=>x.Type).ToArray()).Compare(key,next)!=0){var result=Finish();if(result is{} value){yield return value;}states=null;}
                if(states is null){key=next;first=row;states=aggregateBindings.Select(x=>new AggregateState(x,execution.Scope,execution.Options)).ToArray();}
                foreach(var state in states){state.Add(frame);}
            }
            if(states is null&&groupExpressions.Count==0){states=aggregateBindings.Select(x=>new AggregateState(x,execution.Scope,execution.Options)).ToArray();first=new object?[captured.Columns.Count];}
            if(states is not null){var result=Finish();if(result is{} value){yield return value;}}
            (object?[],object?[])?Finish(){
                var frame=new EvalFrame(first!,outerRow,execution,states!.Select(x=>x.Finish()).ToArray());
                foreach(var state in states!){state.Dispose();}
                if(having is not null&&having(frame)!=true){return null;}
                return(projections.Select(x=>x.Value(frame)).ToArray(),order.Select(x=>x.Value.Value(frame)).ToArray());
            }
        }
    }
    private static IEnumerable<(object?[],object?[])>DistinctRows(IEnumerable<(object?[] Values,object?[] Keys)>rows,QueryExecution execution,SqlType[]types){
        object?[]?previous=null;var hasPrevious=false;
        foreach(var item in SqlBoundedOperators.Sort(execution,rows,x=>x.Values,new SqlRowComparer(types:types))){if(!hasPrevious||new SqlRowComparer(types:types).Compare(previous,item.Values)!=0){yield return item;previous=item.Values;hasPrevious=true;}}
    }
    private BoundQuery BindUnion(BinaryQueryExpression binary,IReadOnlyList<BindingColumn>outer){
        if(binary.BinaryQueryExpressionType!=BinaryQueryExpressionType.Union||binary.OrderByClause is not null||binary.OffsetClause is not null){throw Unsupported(binary);}
        var left=this.BindQuery(binary.FirstQueryExpression,outer);var right=this.BindQuery(binary.SecondQueryExpression,outer);
        if(left.Columns.Count!=right.Columns.Count){throw new SqlQueryException("UNION column count differs.",205);}
        var columns=left.Columns.Select((column,index)=>column with{Type=SqlValues.CommonType([column.Type,right.Columns[index].Type]),IsNullable=column.IsNullable||right.Columns[index].IsNullable}).ToArray();var types=columns.Select(column=>column.Type).ToArray();var comparer=new SqlRowComparer(types:types);
        return new(columns,(execution,outerRow)=>Rows(execution,outerRow));
        IEnumerable<object?[]>Rows(QueryExecution execution,object?[]?outerRow){var rows=ConvertRows(left.Rows(execution,outerRow)).Concat(ConvertRows(right.Rows(execution,outerRow)));if(binary.All){foreach(var row in rows){yield return row;}}else{object?[]?previous=null;var has=false;foreach(var row in SqlBoundedOperators.Sort(execution,rows,x=>x,comparer)){if(!has||comparer.Compare(previous,row)!=0){yield return row;previous=row;has=true;}}}}
        IEnumerable<object?[]>ConvertRows(IEnumerable<object?[]>rows){foreach(var row in rows){var values=new object?[types.Length];for(var index=0;index<types.Length;index++){values[index]=SqlValues.ConvertTo(row[index],types[index],true);}yield return values;}}
    }
    private BoundSource BindTable(TableReference table,IReadOnlyList<BindingColumn>outer){
        switch(table){
            case NamedTableReference named:{
                if(named.TableHints.Count>0||named.TableSampleClause is not null||named.TemporalClause is not null||named.ForPath){throw Unsupported(named);}
                var name=this.ResolveName(named.SchemaObject);
                var alias=named.Alias?.Value??name.Name;
                if(named.SchemaObject.Identifiers.Count==1&&this.ctes.TryGetValue(name.Name,out var cte)){return new(cte.Columns.Select(x=>new BindingColumn(x,alias)).ToArray(),cte.Rows);}
                var definition=this.catalog.FindTable(name)??throw new SqlQueryException("Invalid object name '"+name+"'.",208);
                name=definition.Name;
                if(definition.ViewDefinition is{} view){
                    if(!this.viewStack.Add(name)){throw new SqlQueryException("Recursive view dependency: "+name,4429);}
                    try{
                        var parsed=SqlQueryEngine.Parse(view);
                        SelectStatement select;
                        if(parsed is CreateViewStatement create){select=create.SelectStatement;}else if(parsed is AlterViewStatement alter){select=alter.SelectStatement;}else if(parsed is SelectStatement direct){select=direct;}else{throw Unsupported(parsed);}
                        var binder=new QueryBinder(this.catalog,name.Database,this.parameters,this.viewStack);
                        var query=binder.BindStatement(select);
                        if(query.Columns.Count!=definition.Columns.Count){throw new SqlQueryException("View column count differs from metadata: "+name,4502);}
                        return new(definition.Columns.Select(x=>new BindingColumn(x with{Source=name},alias)).ToArray(),query.Rows);
                    }finally{this.viewStack.Remove(name);}
                }
                var read=definition.Rows??throw new SqlQueryException("No row source for "+name);
                return new(definition.Columns.Select(x=>new BindingColumn(x with{Source=name},alias)).ToArray(),(execution,_)=>Read(execution));
                IEnumerable<object?[]>Read(QueryExecution execution){foreach(var row in read(execution.Scope.CancellationToken)){execution.Scope.ThrowIfUnavailable();if(row.Length!=definition.Columns.Count){throw new SqlQueryException("Row column count differs from metadata: "+name);}yield return row;}}
            }
            case SchemaObjectFunctionTableReference function when Equal(function.SchemaObject.BaseIdentifier.Value,"nodes"):return this.BindXmlNodes(function,outer);
            case PivotedTableReference pivot:return this.BindPivot(pivot,outer);
            case QueryDerivedTable derived:{var query=this.BindQuery(derived.QueryExpression,outer);if(derived.Columns.Count>0){query=Rename(query,derived.Columns);}return new(query.Columns.Select(x=>new BindingColumn(x,derived.Alias.Value)).ToArray(),query.Rows);}
            case JoinParenthesisTableReference parenthesis:return this.BindTable(parenthesis.Join,outer);
            case QualifiedJoin join:{
                if(join.QualifiedJoinType is not(QualifiedJoinType.Inner or QualifiedJoinType.LeftOuter or QualifiedJoinType.RightOuter)||join.JoinHint!=JoinHint.None){throw Unsupported(join);}
                var left=this.BindTable(join.FirstTableReference,outer);var right=this.BindTable(join.SecondTableReference,outer);var columns=left.Columns.Concat(right.Columns).ToArray();
                var condition=this.BindBoolean(join.SearchCondition,columns,outer,null);
                if(join.QualifiedJoinType==QualifiedJoinType.LeftOuter){for(var index=left.Columns.Count;index<columns.Length;index++){columns[index]=columns[index] with{Column=columns[index].Column with{IsNullable=true}};}}
                if(join.QualifiedJoinType==QualifiedJoinType.RightOuter){
                    for(var index=0;index<left.Columns.Count;index++){columns[index]=columns[index] with{Column=columns[index].Column with{IsNullable=true}};}
                    var rightKeys=this.JoinKeys(join.SearchCondition,left,right,outer);
                    return new(columns,(execution,outerRow)=>RightRows(execution,outerRow));
                    IEnumerable<object?[]>RightRows(QueryExecution execution,object?[]?outerRow){
                        if(rightKeys.Left.Length>0){foreach(var row in MergeJoin(execution,outerRow,right,left,rightKeys.Right,rightKeys.Left,frame=>condition(frame with{Row=Reorder(frame.Row)}),true)){yield return Reorder(row);}}
                        else{foreach(var last in right.Rows(execution,outerRow)){var matched=false;foreach(var first in left.Rows(execution,outerRow)){var row=first.Concat(last).ToArray();if(condition(new(row,outerRow,execution))==true){matched=true;yield return row;}}if(!matched){yield return new object?[left.Columns.Count].Concat(last).ToArray();}}}
                    }
                    object?[]Reorder(object?[]row)=>row.Skip(right.Columns.Count).Concat(row.Take(right.Columns.Count)).ToArray();
                }
                var keys=this.JoinKeys(join.SearchCondition,left,right,outer);
                if(keys.Left.Length>0){return new(columns,(execution,outerRow)=>MergeJoin(execution,outerRow,left,right,keys.Left,keys.Right,condition,join.QualifiedJoinType==QualifiedJoinType.LeftOuter));}
                return new(columns,(execution,outerRow)=>Rows(execution,outerRow));
                IEnumerable<object?[]>Rows(QueryExecution execution,object?[]?outerRow){foreach(var first in left.Rows(execution,outerRow)){var matched=false;foreach(var second in right.Rows(execution,outerRow)){execution.Scope.ThrowIfUnavailable();var row=first.Concat(second).ToArray();if(condition(new(row,outerRow,execution))==true){matched=true;yield return row;}}if(!matched&&join.QualifiedJoinType==QualifiedJoinType.LeftOuter){yield return first.Concat(new object?[right.Columns.Count]).ToArray();}}}
            }
            case UnqualifiedJoin join:{
                var left=this.BindTable(join.FirstTableReference,outer);
                if(join.UnqualifiedJoinType==UnqualifiedJoinType.CrossJoin){return Cross(left,this.BindTable(join.SecondTableReference,outer));}
                if(join.UnqualifiedJoinType is not(UnqualifiedJoinType.CrossApply or UnqualifiedJoinType.OuterApply)){throw Unsupported(join);}
                var right=this.BindTable(join.SecondTableReference,left.Columns);
                var columns=left.Columns.Concat(right.Columns).ToArray();if(join.UnqualifiedJoinType==UnqualifiedJoinType.OuterApply){for(var index=left.Columns.Count;index<columns.Length;index++){columns[index]=columns[index] with{Column=columns[index].Column with{IsNullable=true}};}}
                return new(columns,(execution,outerRow)=>Rows(execution,outerRow));
                IEnumerable<object?[]>Rows(QueryExecution execution,object?[]?outerRow){foreach(var first in left.Rows(execution,outerRow)){var matched=false;foreach(var second in right.Rows(execution,first)){matched=true;yield return first.Concat(second).ToArray();}if(!matched&&join.UnqualifiedJoinType==UnqualifiedJoinType.OuterApply){yield return first.Concat(new object?[right.Columns.Count]).ToArray();}}}
            }
            default:throw Unsupported(table);
        }
    }
    private SqlObjectName ResolveName(SchemaObjectName name){if(name.ServerIdentifier is not null){throw Unsupported(name);}return new(name.DatabaseIdentifier?.Value??this.database,name.SchemaIdentifier?.Value??"dbo",name.BaseIdentifier.Value);}
    private static BoundQuery Rename(BoundQuery query,IList<Identifier>names){if(query.Columns.Count!=names.Count){throw new SqlQueryException("Column alias count differs.",8159);}return query with{Columns=query.Columns.Select((x,i)=>x with{Name=names[i].Value}).ToArray()};}
    private static BoundSource Cross(BoundSource left,BoundSource right){return new(left.Columns.Concat(right.Columns).ToArray(),(execution,outer)=>Rows(execution,outer));IEnumerable<object?[]>Rows(QueryExecution execution,object?[]?outer){foreach(var first in left.Rows(execution,outer)){foreach(var second in right.Rows(execution,outer)){execution.Scope.ThrowIfUnavailable();yield return first.Concat(second).ToArray();}}}}
    private static IEnumerable<object?[]>OneRow(){yield return [];}
    private static string GroupText(TSqlFragment fragment){
        var tokens=new List<string>();for(var index=fragment.FirstTokenIndex;index<=fragment.LastTokenIndex;index++){var token=fragment.ScriptTokenStream[index];if(token.TokenType is TSqlTokenType.WhiteSpace or TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment){continue;}var value=token.Text;if(token.TokenType==TSqlTokenType.QuotedIdentifier){value=Identifier.DecodeIdentifier(value,out _).ToUpperInvariant();}else if(token.TokenType==TSqlTokenType.Identifier){value=value.ToUpperInvariant();}if(!value.StartsWith("'",StringComparison.Ordinal)&&!value.StartsWith("N'",StringComparison.OrdinalIgnoreCase)){value=value.ToUpperInvariant();}tokens.Add(value);}return string.Join("|",tokens);
    }
    private static string Text(TSqlFragment fragment){var generator=new Sql170ScriptGenerator();generator.GenerateScript(fragment,out var value);return value;}
    private static bool Equal(string?left,string?right)=>string.Equals(left,right,StringComparison.OrdinalIgnoreCase);
    internal static SqlQueryException Unsupported(TSqlFragment fragment)=>new("Unsupported SQL construct: "+fragment.GetType().Name+" at line "+fragment.StartLine,40515,fragment.StartLine);
    private static void ValidateGrouping(TSqlFragment fragment,HashSet<string>groups){
        if(fragment is ScalarExpression scalar&&groups.Contains(GroupText(scalar))){return;}
        if(fragment is FunctionCall function&&IsAggregate(function.FunctionName.Value)){return;}
        if(fragment is ColumnReferenceExpression){throw new SqlQueryException("Column must occur in GROUP BY or an aggregate.",8120);}
        foreach(var property in fragment.GetType().GetProperties()){if(property.Name is "ScriptTokenStream"||property.GetIndexParameters().Length!=0){continue;}if(property.GetValue(fragment) is TSqlFragment child){ValidateGrouping(child,groups);}else if(property.GetValue(fragment) is System.Collections.IEnumerable items){foreach(var item in items){if(item is TSqlFragment descendant){ValidateGrouping(descendant,groups);}}}}
    }
}





