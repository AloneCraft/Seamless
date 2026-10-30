using System.Globalization;

namespace Seamless.Database.Query;

internal sealed partial class QueryBinder{
    internal BoundQuery BindBasic(SqlSelectSyntax query){
        var source=new BoundSource([],(_,_)=>OneRow());if(query.From is not null){source=this.BindBasicSource(query.From);}
        var aggregates=new List<AggregateBinding>();
        Func<EvalFrame,bool?>?predicate=null;if(query.Where is not null){predicate=this.BindBasicBoolean(query.Where,source.Columns,null);}
        var groups=query.GroupBy.Select(expression=>this.BindBasicScalar(expression,source.Columns,null)).ToArray();
        var values=new List<BoundScalar>();var columns=new List<SqlColumn>();
        foreach(var item in query.Projection){
            if(item.IsStar){
                var qualifier=item.StarQualifier?.Parts.LastOrDefault()?.Value;var matches=0;
                foreach(var pair in source.Columns.Select((column,index)=>(column,index))){
                    if(qualifier is not null&&!Equal(qualifier,pair.column.Qualifier)){continue;}
                    var position=pair.index;var column=pair.column.Column;values.Add(new(this.GetColumnType(column),column.IsNullable,frame=>frame.Row[position],column));columns.Add(column with{Ordinal=columns.Count});matches++;
                }
                if(matches==0){throw new SqlQueryException("Star qualifier does not resolve.",4104);}
            }else{
                var value=this.BindBasicScalar(item.Expression!,source.Columns,aggregates);values.Add(value);columns.Add(new(item.Alias?.Value??value.Origin?.Name??"",value.Type,value.Nullable,value.Origin?.Source,columns.Count){IsComputedFloatingPoint=IsFloating(value.Type)&&value.IsComputedFloatingPoint});
            }
        }
        Func<EvalFrame,bool?>?having=null;if(query.Having is not null){having=this.BindBasicBoolean(query.Having,source.Columns,aggregates);}
        var order=new List<(BoundScalar Value,bool Descending)>();
        foreach(var item in query.OrderBy){
            if(item.NullsFirst is not null){throw new SqlQueryException("NULLS FIRST/LAST is outside basic SQL Server syntax.",40515);}
            BoundScalar value;var projectedOrder=false;
            if(item.Expression is SqlLiteralSyntax{Kind:SqlLiteralKind.Integer}ordinal){
                if(!int.TryParse(ordinal.Text,NumberStyles.None,CultureInfo.InvariantCulture,out var position)||position<1||position>values.Count){throw new SqlQueryException("ORDER BY position outside select list.",108);}value=values[position-1];projectedOrder=true;
            }else if(item.Expression is SqlColumnSyntax{Name.Parts.Count:1}reference&&columns.FindIndex(column=>Equal(column.Name,reference.Name.Parts[0].Value)) is var index&&index>=0){value=values[index];projectedOrder=true;}
            else{value=this.BindBasicScalar(item.Expression,source.Columns,aggregates);}
            if(query.Distinct&&!projectedOrder&&!query.Projection.Any(projection=>!projection.IsStar&&BasicSame(projection.Expression!,item.Expression))&&(value.Origin is null||!values.Any(projection=>projection.Origin==value.Origin))){throw new SqlQueryException("ORDER BY expressions must appear in the select list when DISTINCT is specified.",145);}
            order.Add((value,item.Descending));
        }
        BoundScalar?top=null;if(query.Limit is not null){top=this.BindBasicScalar(query.Limit,[],null);}
        var grouped=groups.Length>0||aggregates.Count>0||having is not null;
        if(grouped){
            if(query.Projection.Any(item=>item.IsStar)){throw new SqlQueryException("A grouped SELECT requires explicit projection columns.",40515);}
            foreach(var item in query.Projection){ValidateBasicGrouping(item.Expression!,query.GroupBy);}
            if(query.Having is not null){ValidateBasicGrouping(query.Having,query.GroupBy);}
            foreach(var item in query.OrderBy){
                if(item.Expression is SqlLiteralSyntax{Kind:SqlLiteralKind.Integer}){continue;}
                if(item.Expression is SqlColumnSyntax{Name.Parts.Count:1}reference&&columns.Any(column=>Equal(column.Name,reference.Name.Parts[0].Value))){continue;}
                ValidateBasicGrouping(item.Expression,query.GroupBy);
            }
        }
        return new(columns,(execution,_)=>Rows(execution));
        IEnumerable<object?[]>Rows(QueryExecution execution){
            var rows=source.Rows(execution,null);
            if(predicate is not null){rows=rows.Where(row=>predicate(new(row,null,execution))==true);}
            IEnumerable<(object?[] Values,object?[] Keys)>projected;if(grouped){projected=GroupRows(rows,execution);}else{projected=Project(rows,execution);}
            if(query.Distinct){projected=DistinctRows(projected,execution,columns.Select(column=>column.Type).ToArray());}
            if(order.Count>0){projected=SqlBoundedOperators.Sort(execution,projected,item=>item.Keys,new SqlRowComparer(order.Select(item=>item.Descending).ToArray(),order.Select(item=>item.Value.Type).ToArray()));}
            long remaining=long.MaxValue;
            if(top is not null){
                var limit=top.Value(new([],null,execution));if(limit is null){throw new SqlQueryException("TOP cannot be NULL.",1014);}
                remaining=(long)SqlValues.ConvertTo(limit,SqlType.BigInt)!;if(remaining<0){throw new SqlQueryException("TOP cannot be negative.",1014);}
            }
            using var iterator=projected.GetEnumerator();while(remaining>0&&iterator.MoveNext()){remaining--;execution.Scope.ThrowIfUnavailable();yield return iterator.Current.Values;}
        }
        IEnumerable<(object?[],object?[])>Project(IEnumerable<object?[]>rows,QueryExecution execution){
            foreach(var row in rows){var frame=new EvalFrame(row,null,execution);yield return(values.Select(value=>value.Value(frame)).ToArray(),order.Select(item=>item.Value.Value(frame)).ToArray());}
        }
        IEnumerable<(object?[],object?[])>GroupRows(IEnumerable<object?[]>rows,QueryExecution execution){
            var ordered=rows;
            if(groups.Length>0){ordered=SqlBoundedOperators.Sort(execution,rows,row=>groups.Select(group=>group.Value(new(row,null,execution))).ToArray(),new SqlRowComparer(types:groups.Select(group=>group.Type).ToArray()));}
            object?[]?key=null;object?[]?first=null;AggregateState[]?states=null;
            try{
                foreach(var row in ordered){
                    var frame=new EvalFrame(row,null,execution);var next=groups.Select(group=>group.Value(frame)).ToArray();
                    if(states is not null&&new SqlRowComparer(types:groups.Select(group=>group.Type).ToArray()).Compare(key,next)!=0){var result=Finish();if(result is{}completed){yield return completed;}}
                    if(states is null){key=next;BeginGroup(row);}
                    foreach(var state in states!){state.Add(frame);}
                }
                if(states is null&&groups.Length==0){BeginGroup(new object?[source.Columns.Count]);}
                if(states is not null){var result=Finish();if(result is{}completed){yield return completed;}}
            }finally{if(states is not null){DisposeBasicAggregates(states);states=null;}}
            void BeginGroup(object?[]row){
                first=row;states=new AggregateState[aggregates.Count];
                for(var index=0;index<states.Length;index++){states[index]=new AggregateState(aggregates[index],execution.Scope,execution.Options);}
            }
            (object?[],object?[])?Finish(){
                var current=states!;states=null;
                try{
                    var frame=new EvalFrame(first!,null,execution,current.Select(state=>state.Finish()).ToArray());
                    if(having is not null&&having(frame)!=true){return null;}return(values.Select(value=>value.Value(frame)).ToArray(),order.Select(item=>item.Value.Value(frame)).ToArray());
                }finally{DisposeBasicAggregates(current);}
            }
        }
    }
    private BoundSource BindBasicSource(SqlSourceSyntax source){
        switch(source){
            case SqlTableSyntax table:{
                var parts=table.Name.Parts;if(parts.Count is <1 or >3){throw new SqlQueryException("A basic table name must have one to three parts.",40515);}
                var schema="dbo";var databaseName=this.database;if(parts.Count>=2){schema=parts[^2].Value;}if(parts.Count==3){databaseName=parts[0].Value;}
                var name=new SqlObjectName(databaseName,schema,parts[^1].Value);var definition=this.catalog.FindTable(name)??throw new SqlQueryException("Invalid object name '"+name+"'.",208);
                name=definition.Name;var qualifier=table.Alias?.Value??name.Name;
                if(definition.ViewDefinition is{}view){
                    if(this.viewStack.Count>=32||!this.viewStack.Add(name)){throw new SqlQueryException("Recursive or excessively deep view dependency: "+name,4429);}
                    try{
                        var syntax=new SqlParserCsAdapter().ParseViewDefinition(view,name);var bound=new QueryBinder(this.catalog,name.Database,this.parameters,this.viewStack).BindBasic(syntax);
                        if(bound.Columns.Count!=definition.Columns.Count){throw new SqlQueryException("View column count differs from metadata: "+name,4502);}
                        return new(definition.Columns.Select((column,index)=>new BindingColumn(column with{Source=name,IsComputedFloatingPoint=IsFloating(column.Type)&&bound.Columns[index].IsComputedFloatingPoint},qualifier)).ToArray(),(execution,outer)=>ReadView(execution,outer));
                        IEnumerable<object?[]>ReadView(QueryExecution execution,object?[]?outer){
                            foreach(var row in bound.Rows(execution,outer)){
                                execution.Scope.ThrowIfUnavailable();var result=new object?[definition.Columns.Count];
                                for(var index=0;index<result.Length;index++){var column=definition.Columns[index];result[index]=SqlValues.ConvertTo(row[index],column.Type);if(result[index] is null&&!column.IsNullable){throw new SqlQueryException("View returned NULL for a non-null column: "+column.Name,515);}}
                                yield return result;
                            }
                        }
                    }finally{this.viewStack.Remove(name);}
                }
                var read=definition.Rows??throw new SqlQueryException("No row source for "+name);
                return new(definition.Columns.Select(column=>new BindingColumn(column with{Source=column.Source??definition.Name,IsComputedFloatingPoint=false},qualifier)).ToArray(),(execution,_)=>Read(execution));
                IEnumerable<object?[]>Read(QueryExecution execution){
                    execution.Scope.ThrowIfUnavailable();foreach(var row in read(execution.Scope.CancellationToken)){execution.Scope.ThrowIfUnavailable();if(row.Length!=definition.Columns.Count){throw new SqlQueryException("Row column count differs from metadata: "+name);}yield return row;}
                }
            }
            case SqlJoinSyntax join:{
                var left=this.BindBasicSource(join.Left);var right=this.BindBasicSource(join.Right);var columns=left.Columns.Concat(right.Columns).ToArray();var condition=this.BindBasicBoolean(join.Condition,columns,null);
                if(join.Kind==SqlSyntaxJoinKind.Left){for(var index=left.Columns.Count;index<columns.Length;index++){columns[index]=columns[index]with{Column=columns[index].Column with{IsNullable=true}};}}
                var leftKeys=new List<BoundScalar>();var rightKeys=new List<BoundScalar>();Keys(join.Condition);
                if(leftKeys.Count>0){return new(columns,(execution,outer)=>MergeJoin(execution,outer,left,right,leftKeys.ToArray(),rightKeys.ToArray(),condition,join.Kind==SqlSyntaxJoinKind.Left));}
                return new(columns,(execution,_)=>Rows(execution));
                void Keys(SqlExpressionSyntax expression){
                    if(expression is SqlBinarySyntax{Operator:SqlSyntaxBinaryOperator.And}and){Keys(and.Left);Keys(and.Right);return;}
                    if(expression is not SqlBinarySyntax{Operator:SqlSyntaxBinaryOperator.Equal}equal){return;}
                    var first=Try(equal.Left,left.Columns);var second=Try(equal.Right,right.Columns);if(first is null||second is null){first=Try(equal.Right,left.Columns);second=Try(equal.Left,right.Columns);}
                    if(first is not null&&second is not null){
                        var type=SqlValues.CommonType([first.Type,second.Type]);
                        var firstKey=first;var secondKey=second;
                        leftKeys.Add(new(type,first.Nullable,frame=>SqlValues.ConvertTo(firstKey.Value(frame),type,true)));
                        rightKeys.Add(new(type,second.Nullable,frame=>SqlValues.ConvertTo(secondKey.Value(frame),type,true)));
                    }
                }
                BoundScalar?Try(SqlExpressionSyntax expression,IReadOnlyList<BindingColumn>columns){try{return this.BindBasicScalar(expression,columns,null);}catch(SqlQueryException){return null;}}
                IEnumerable<object?[]>Rows(QueryExecution execution){
                    foreach(var first in left.Rows(execution,null)){var matched=false;foreach(var second in right.Rows(execution,null)){execution.Scope.ThrowIfUnavailable();var row=first.Concat(second).ToArray();if(condition(new(row,null,execution))==true){matched=true;yield return row;}}if(!matched&&join.Kind==SqlSyntaxJoinKind.Left){yield return first.Concat(new object?[right.Columns.Count]).ToArray();}}
                }
            }
            default:throw new SqlQueryException("Unsupported basic SQL source.",40515);
        }
    }
    private BoundScalar BindBasicScalar(SqlExpressionSyntax expression,IReadOnlyList<BindingColumn>columns,List<AggregateBinding>?aggregates){
        try{return this.BindBasicScalarCore(expression,columns,aggregates);}
        catch(OverflowException exception){throw new SqlQueryException("Basic SQL numeric value exceeds the supported range: "+exception.Message,8115);}
        catch(FormatException exception){throw new SqlQueryException("Invalid basic SQL numeric value: "+exception.Message,245);}
    }
    private BoundScalar BindBasicScalarCore(SqlExpressionSyntax expression,IReadOnlyList<BindingColumn>columns,List<AggregateBinding>?aggregates){
        switch(expression){
            case SqlLiteralSyntax literal:{
                switch(literal.Kind){
                    case SqlLiteralKind.Null:return new(SqlType.Int with{IsUntypedNull=true},true,_=>null);
                    case SqlLiteralKind.Integer:{
                        var text=literal.Text!;if(int.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out var integer)){return new(SqlType.Int with{LiteralPrecision=(byte)Math.Max(1,text.TrimStart('0').Length)},false,_=>integer);}
                        if(!decimal.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out var value)){throw new SqlQueryException("Numeric literal exceeds the supported decimal range.",40515);}return new(new("decimal",typeof(decimal),null,checked((byte)text.Length),0),false,_=>value);
                    }
                    case SqlLiteralKind.Decimal:{
                        var text=literal.Text!;var parts=text.Split('.');var scale=0;if(parts.Length>1){scale=parts[1].Length;}
                        var coefficient=text.Replace(".","",StringComparison.Ordinal).TrimStart('0');
                        if(scale>28||coefficient.Length>28){throw new SqlQueryException("Decimal literal is outside the exact CLR decimal subset.",40515);}
                        var value=decimal.Parse(text,CultureInfo.InvariantCulture);
                        return new(new("decimal",typeof(decimal),null,checked((byte)Math.Max(1,parts[0].TrimStart('0').Length+scale)),checked((byte)scale)),false,_=>value);
                    }
                    case SqlLiteralKind.Real:{var value=double.Parse(literal.Text!,CultureInfo.InvariantCulture);if(!double.IsFinite(value)){throw new SqlQueryException("Floating literal exceeds the finite range.",8115);}return new(new("float",typeof(double)),false,_=>value);}
                    case SqlLiteralKind.String:case SqlLiteralKind.NationalString:{var name="varchar";if(literal.Kind==SqlLiteralKind.NationalString){name="nvarchar";}var type=this.DefaultStringType(new(name,typeof(string)));var value=(string)SqlValues.ConvertTo(literal.Text,type,true)!;return new(type with{Length=SqlValues.StringLength(value,type)},false,_=>value);}
                    case SqlLiteralKind.Boolean:throw new SqlQueryException("Boolean literals are outside SQL Server basic syntax.",40515);
                    default:throw new SqlQueryException("Unsupported basic literal.",40515);
                }
            }
            case SqlColumnSyntax reference:{
                var parts=reference.Name.Parts;
                var matches=columns.Select((column,index)=>(column,index)).Where(item=>BasicMatches(item.column,parts)).ToArray();
                if(matches.Length>1){throw new SqlQueryException("Ambiguous column name '"+parts[^1].Value+"'.",209);}if(matches.Length==0){throw new SqlQueryException("Invalid column name '"+string.Join(".",parts.Select(part=>part.Value))+"'.",207);}
                var match=matches[0];return new(this.GetColumnType(match.column.Column),match.column.Column.IsNullable,frame=>frame.Row[match.index],match.column.Column);
            }
            case SqlParameterSyntax parameter:{var definition=this.parameters.FirstOrDefault(item=>Equal(item.Name,parameter.Name))??throw new SqlQueryException("Undeclared parameter "+parameter.Name,137);return new(this.DefaultStringType(definition.Type),definition.IsNullable,frame=>frame.Execution.Parameters[definition.Name]);}
            case SqlCastSyntax cast:{var value=this.BindBasicScalar(cast.Value,columns,aggregates);var type=this.DefaultStringType(BasicType(cast.Type),value.Type);return new(type,value.Nullable,frame=>SqlValues.ConvertTo(value.Value(frame),type,true)){IsComputedFloatingPoint=FloatingConversion(type,value)};}
            case SqlUnarySyntax{Operator:SqlSyntaxUnaryOperator.Positive}positive:return this.BindBasicScalar(positive.Value,columns,aggregates);
            case SqlUnarySyntax{Operator:SqlSyntaxUnaryOperator.Negative}negative:{var value=this.BindBasicScalar(negative.Value,columns,aggregates);return new(value.Type,value.Nullable,frame=>SqlValues.Unary(value.Value(frame),SqlUnaryOperator.Negative)){IsComputedFloatingPoint=IsFloating(value.Type)};}
            case SqlBinarySyntax binary when binary.Operator is SqlSyntaxBinaryOperator.Add or SqlSyntaxBinaryOperator.Subtract or SqlSyntaxBinaryOperator.Multiply or SqlSyntaxBinaryOperator.Divide or SqlSyntaxBinaryOperator.Modulo:{
                var operation=BasicArithmetic(binary.Operator);var left=this.BindBasicScalar(binary.Left,columns,aggregates);var right=this.BindBasicScalar(binary.Right,columns,aggregates);var type=SqlValues.Promote(left.Type,right.Type,operation);return new(type,left.Nullable||right.Nullable,frame=>SqlValues.Binary(left.Value(frame),right.Value(frame),operation,type)){IsComputedFloatingPoint=IsFloating(type)};
            }
            case SqlCaseSyntax conditional:{
                BoundScalar?input=null;if(conditional.Operand is not null){input=this.BindBasicScalar(conditional.Operand,columns,aggregates);}
                var conditions=conditional.Arms.Select(arm=>{if(input is null){return this.BindBasicBoolean(arm.When,columns,aggregates);}return CompareCase(arm.When);}).ToArray();
                var results=conditional.Arms.Select(arm=>this.BindBasicScalar(arm.Then,columns,aggregates)).ToArray();BoundScalar?otherwise=null;if(conditional.Else is not null){otherwise=this.BindBasicScalar(conditional.Else,columns,aggregates);}
                var types=results.Where((_,index)=>conditional.Arms[index].Then is not SqlLiteralSyntax{Kind:SqlLiteralKind.Null}).Select(value=>value.Type).ToList();if(otherwise is not null&&conditional.Else is not SqlLiteralSyntax{Kind:SqlLiteralKind.Null}){types.Add(otherwise.Type);}var type=SqlValues.CommonType(types);
                return new(type,true,frame=>{for(var index=0;index<conditions.Length;index++){if(conditions[index](frame)==true){return SqlValues.ConvertTo(results[index].Value(frame),type,true);}}return SqlValues.ConvertTo(otherwise?.Value(frame),type,true);}){IsComputedFloatingPoint=AllComputedFloating(type,results,otherwise)};
                Func<EvalFrame,bool?>CompareCase(SqlExpressionSyntax condition){var value=this.BindBasicScalar(condition,columns,aggregates);var comparison=SqlValues.CommonType([input!.Type,value.Type]);return frame=>SqlValues.Compare(input.Value(frame),value.Value(frame),SqlComparisonOperator.Equals,comparison);}
            }
            case SqlFunctionSyntax function:{
                if(IsAggregate(function.Name)){
                    if(aggregates is null||!function.Star&&function.Arguments.Count!=1){throw new SqlQueryException("Invalid basic aggregate.",40515);}
                    BoundScalar?value=null;if(!function.Star){value=this.BindBasicScalar(function.Arguments[0],columns,null);}var type=AggregateType(function.Name,value?.Type??SqlType.Int);var index=aggregates.Count;aggregates.Add(new(function.Name,value,function.Distinct,type));return new(type,true,frame=>frame.Aggregates![index]){IsComputedFloatingPoint=ComputedFloatingAggregate(function.Name,type,value)};
                }
                if(function.Distinct||function.Star){throw new SqlQueryException("Invalid basic scalar function modifier.",40515);}
                if(function.Name=="COALESCE"&&function.Arguments.Count<2){throw new SqlQueryException("COALESCE requires at least two arguments.",174);}
                var arguments=function.Arguments.Select(argument=>this.BindBasicScalar(argument,columns,aggregates)).ToArray();
                if(function.Name=="COALESCE"){
                    var type=SqlValues.CommonType(arguments.Where((_,index)=>function.Arguments[index] is not SqlLiteralSyntax{Kind:SqlLiteralKind.Null}).Select(value=>value.Type));
                    return new(type,true,frame=>{foreach(var argument in arguments){var value=argument.Value(frame);if(value is not null){return SqlValues.ConvertTo(value,type,true);}}return null;}){IsComputedFloatingPoint=AllComputedFloating(type,arguments)};
                }
                if(function.Name=="NULLIF"&&arguments.Length==2){var comparison=SqlValues.CommonType(arguments.Select(value=>value.Type));return new(arguments[0].Type,true,frame=>{var value=arguments[0].Value(frame);if(SqlValues.Compare(value,arguments[1].Value(frame),SqlComparisonOperator.Equals,comparison)==true){return null;}return value;}){IsComputedFloatingPoint=arguments[0].IsComputedFloatingPoint};}
                if(function.Name=="ISNULL"&&arguments.Length==2){var type=arguments[0].Type;if(function.Arguments[0] is SqlLiteralSyntax{Kind:SqlLiteralKind.Null}){type=arguments[1].Type;}return new(type,true,frame=>SqlValues.ConvertTo(arguments[0].Value(frame)??arguments[1].Value(frame),type,true)){IsComputedFloatingPoint=AllComputedFloating(type,arguments)};}
                if(function.Name=="ABS"&&arguments.Length==1){var type=arguments[0].Type with{LiteralPrecision=null};return new(type,true,frame=>SqlValues.ConvertTo(SqlValues.Function("ABS",[arguments[0].Value(frame)],this.database,frame.Execution.Timestamp),type,true)){IsComputedFloatingPoint=IsFloating(type)};}
                throw new SqlQueryException("Unsupported basic scalar function: "+function.Name,40515);
            }
            default:throw new SqlQueryException("A basic scalar expression was required.",40515);
        }
    }
    private Func<EvalFrame,bool?>BindBasicBoolean(SqlExpressionSyntax expression,IReadOnlyList<BindingColumn>columns,List<AggregateBinding>?aggregates){
        switch(expression){
            case SqlUnarySyntax{Operator:SqlSyntaxUnaryOperator.Not}not:{var value=this.BindBasicBoolean(not.Value,columns,aggregates);return frame=>!value(frame);}
            case SqlIsNullSyntax isNull:{var value=this.BindBasicScalar(isNull.Value,columns,aggregates);var negate=isNull.Negate;return frame=>(value.Value(frame)is null)!=negate;}
            case SqlBinarySyntax{Operator:SqlSyntaxBinaryOperator.And or SqlSyntaxBinaryOperator.Or}binary:{var left=this.BindBasicBoolean(binary.Left,columns,aggregates);var right=this.BindBasicBoolean(binary.Right,columns,aggregates);if(binary.Operator==SqlSyntaxBinaryOperator.And){return frame=>left(frame)&right(frame);}return frame=>left(frame)|right(frame);}
            case SqlBinarySyntax comparison:{
                var operation=comparison.Operator switch{SqlSyntaxBinaryOperator.Equal=>SqlComparisonOperator.Equals,SqlSyntaxBinaryOperator.NotEqual=>SqlComparisonOperator.NotEqual,SqlSyntaxBinaryOperator.Greater=>SqlComparisonOperator.GreaterThan,SqlSyntaxBinaryOperator.Less=>SqlComparisonOperator.LessThan,SqlSyntaxBinaryOperator.GreaterOrEqual=>SqlComparisonOperator.GreaterThanOrEqualTo,SqlSyntaxBinaryOperator.LessOrEqual=>SqlComparisonOperator.LessThanOrEqualTo,_=>throw new SqlQueryException("A basic predicate was required.",40515)};
                var left=this.BindBasicScalar(comparison.Left,columns,aggregates);var right=this.BindBasicScalar(comparison.Right,columns,aggregates);var type=SqlValues.CommonType([left.Type,right.Type]);return frame=>SqlValues.Compare(left.Value(frame),right.Value(frame),operation,type);
            }
            default:throw new SqlQueryException("A basic predicate was required.",40515);
        }
    }
    private static SqlBinaryOperator BasicArithmetic(SqlSyntaxBinaryOperator operation)=>operation switch{SqlSyntaxBinaryOperator.Add=>SqlBinaryOperator.Add,SqlSyntaxBinaryOperator.Subtract=>SqlBinaryOperator.Subtract,SqlSyntaxBinaryOperator.Multiply=>SqlBinaryOperator.Multiply,SqlSyntaxBinaryOperator.Divide=>SqlBinaryOperator.Divide,SqlSyntaxBinaryOperator.Modulo=>SqlBinaryOperator.Modulo,_=>throw new SqlQueryException("Unsupported basic arithmetic.",40515)};
    private static void DisposeBasicAggregates(AggregateState[]states){
        List<Exception>?errors=null;
        foreach(var state in states){try{state?.Dispose();}catch(Exception exception){(errors??=[]).Add(exception);}}
        if(errors is not null){throw new AggregateException("Basic aggregate cleanup failed.",errors);}
    }
    private static bool BasicMatches(BindingColumn column,IReadOnlyList<SqlIdentifierSyntax>parts){
        if(parts.Count is <1 or >4||!Equal(column.Column.Name,parts[^1].Value)){return false;}if(parts.Count==1){return true;}if(!Equal(column.Qualifier,parts[^2].Value)){return false;}if(parts.Count==2){return true;}var source=column.Column.Source;if(source is null||!Equal(column.Qualifier,source.Name)||!Equal(source.Schema,parts[^3].Value)){return false;}return parts.Count==3||Equal(source.Database,parts[0].Value);
    }
    private static SqlType BasicType(SqlTypeSyntax type){
        switch(type.Name){
            case "int":case "integer":return SqlType.Int;case "bigint":return SqlType.BigInt;case "smallint":return new("smallint",typeof(short));case "tinyint":return new("tinyint",typeof(byte));case "bit":return new("bit",typeof(bool));
            case "decimal":case "numeric":{var precision=type.Precision??18;var scale=type.Scale??0;if(precision is <1 or >38||scale>precision){throw new SqlQueryException("Invalid SQL decimal precision or scale.",40515);}return new(type.Name,typeof(decimal),null,precision,scale);}
            case "float":return new("float",typeof(double));case "real":return new("real",typeof(float));case "date":return new("date",typeof(DateOnly));
            case "char":case "varchar":case "nvarchar":{var length=type.Length??30;var maximum=8000;if(type.Name=="nvarchar"){maximum=4000;}if(length<1||length>maximum){throw new SqlQueryException("Invalid SQL data type length.",40515);}return new(type.Name,typeof(string),length);}
            default:throw new SqlQueryException("Unsupported basic SQL Server datatype: "+type.Name,40515);
        }
    }
    private static void ValidateBasicGrouping(SqlExpressionSyntax expression,IReadOnlyList<SqlExpressionSyntax>groups){
        if(groups.Any(group=>BasicSame(expression,group))||expression is SqlFunctionSyntax function&&IsAggregate(function.Name)){return;}
        if(expression is SqlColumnSyntax){throw new SqlQueryException("Column must occur in GROUP BY or an aggregate.",8120);}
        foreach(var child in BasicChildren(expression)){ValidateBasicGrouping(child,groups);}
    }
    private static IEnumerable<SqlExpressionSyntax>BasicChildren(SqlExpressionSyntax expression){
        switch(expression){
            case SqlBinarySyntax binary:yield return binary.Left;yield return binary.Right;break;
            case SqlUnarySyntax unary:yield return unary.Value;break;
            case SqlIsNullSyntax isNull:yield return isNull.Value;break;
            case SqlCastSyntax cast:yield return cast.Value;break;
            case SqlFunctionSyntax function:foreach(var argument in function.Arguments){yield return argument;}break;
            case SqlCaseSyntax conditional:
                if(conditional.Operand is not null){yield return conditional.Operand;}
                foreach(var arm in conditional.Arms){yield return arm.When;yield return arm.Then;}
                if(conditional.Else is not null){yield return conditional.Else;}break;
        }
    }
    private static bool BasicSame(SqlExpressionSyntax left,SqlExpressionSyntax right){
        return(left,right)switch{
            (SqlLiteralSyntax first,SqlLiteralSyntax second)=>first==second,
            (SqlColumnSyntax first,SqlColumnSyntax second)=>first.Name.Parts.Select(part=>part.Value).SequenceEqual(second.Name.Parts.Select(part=>part.Value),StringComparer.OrdinalIgnoreCase),
            (SqlParameterSyntax first,SqlParameterSyntax second)=>Equal(first.Name,second.Name),
            (SqlBinarySyntax first,SqlBinarySyntax second)=>first.Operator==second.Operator&&BasicSame(first.Left,second.Left)&&BasicSame(first.Right,second.Right),
            (SqlUnarySyntax first,SqlUnarySyntax second)=>first.Operator==second.Operator&&BasicSame(first.Value,second.Value),
            (SqlIsNullSyntax first,SqlIsNullSyntax second)=>first.Negate==second.Negate&&BasicSame(first.Value,second.Value),
            (SqlCastSyntax first,SqlCastSyntax second)=>first.Type==second.Type&&BasicSame(first.Value,second.Value),
            (SqlFunctionSyntax first,SqlFunctionSyntax second)=>first.Name==second.Name&&first.Star==second.Star&&first.Distinct==second.Distinct&&first.Arguments.Count==second.Arguments.Count&&first.Arguments.Zip(second.Arguments).All(pair=>BasicSame(pair.First,pair.Second)),
            (SqlCaseSyntax first,SqlCaseSyntax second)=>BasicOptionalSame(first.Operand,second.Operand)&&BasicOptionalSame(first.Else,second.Else)&&first.Arms.Count==second.Arms.Count&&first.Arms.Zip(second.Arms).All(pair=>BasicSame(pair.First.When,pair.Second.When)&&BasicSame(pair.First.Then,pair.Second.Then)),
            _=>false
        };
    }
    private static bool BasicOptionalSame(SqlExpressionSyntax?left,SqlExpressionSyntax?right){if(left is null||right is null){return left is null&&right is null;}return BasicSame(left,right);}
}
