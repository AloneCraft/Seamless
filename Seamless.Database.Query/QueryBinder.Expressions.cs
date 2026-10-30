using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Globalization;
namespace Seamless.Database.Query;
internal sealed partial class QueryBinder{
    private (BoundScalar[]Left,BoundScalar[]Right)JoinKeys(BooleanExpression expression,BoundSource left,BoundSource right,IReadOnlyList<BindingColumn>outer){
        var leftKeys=new List<BoundScalar>();var rightKeys=new List<BoundScalar>();Visit(expression);return(leftKeys.ToArray(),rightKeys.ToArray());
        void Visit(BooleanExpression condition){
            if(condition is BooleanParenthesisExpression parenthesis){Visit(parenthesis.Expression);return;}
            if(condition is BooleanBinaryExpression{BinaryExpressionType:BooleanBinaryExpressionType.And}and){Visit(and.FirstExpression);Visit(and.SecondExpression);return;}
            if(condition is not BooleanComparisonExpression{ComparisonType:BooleanComparisonType.Equals}equality){return;}
            var first=TryBind(equality.FirstExpression,left.Columns);var second=TryBind(equality.SecondExpression,right.Columns);
            if(first is null||second is null){first=TryBind(equality.SecondExpression,left.Columns);second=TryBind(equality.FirstExpression,right.Columns);}
            if(first is not null&&second is not null){leftKeys.Add(first);rightKeys.Add(second);}
        }
        BoundScalar?TryBind(ScalarExpression scalar,IReadOnlyList<BindingColumn>columns){try{return this.BindScalar(scalar,columns,outer,null);}catch(SqlQueryException){return null;}}
    }
    private BoundScalar BindScalar(ScalarExpression expression,IReadOnlyList<BindingColumn>columns,IReadOnlyList<BindingColumn>outer,List<AggregateBinding>?aggregates){
        if(expression is PrimaryExpression{Collation:not null}){throw Unsupported(expression);}
        switch(expression){
            case NullLiteral:return new(SqlType.Int with{IsUntypedNull=true},true,_=>null);
            case IntegerLiteral literal:{if(int.TryParse(literal.Value,global::System.Globalization.NumberStyles.None,CultureInfo.InvariantCulture,out var integer)){return new(SqlType.Int with{LiteralPrecision=(byte)Math.Max(1,literal.Value.TrimStart('0').Length)},false,_=>integer);}if(decimal.TryParse(literal.Value,global::System.Globalization.NumberStyles.None,CultureInfo.InvariantCulture,out var number)){return new(new("decimal",typeof(decimal),null,(byte)literal.Value.Length,0),false,_=>number);}throw new SqlQueryException("Numeric literal exceeds the supported decimal range.",40515);}
            case NumericLiteral literal:{var value=decimal.Parse(literal.Value,CultureInfo.InvariantCulture);var parts=literal.Value.Split('.');var integral=parts[0].TrimStart('0').Length;var scale=0;if(parts.Length>1){scale=parts[1].Length;}return new(new("decimal",typeof(decimal),null,(byte)Math.Max(1,integral+scale),(byte)scale),false,_=>value);}
            case RealLiteral literal:{var value=double.Parse(literal.Value,CultureInfo.InvariantCulture);return new(new("float",typeof(double)),false,_=>value);}
            case StringLiteral literal:{var typeName="varchar";if(literal.IsNational){typeName="nvarchar";}var type=this.DefaultStringType(new(typeName,typeof(string)));var value=(string)SqlValues.ConvertTo(literal.Value,type,true)!;type=type with{Length=SqlValues.StringLength(value,type)};return new(type,false,_=>value);}
            case BinaryLiteral literal:{var value=Convert.FromHexString(literal.Value[2..]);return new(new("varbinary",typeof(byte[]),value.Length),false,_=>value);}
            case VariableReference variable:{var parameter=this.parameters.FirstOrDefault(x=>Equal(x.Name,variable.Name))??throw new SqlQueryException("Undeclared parameter "+variable.Name,137);return new(this.DefaultStringType(parameter.Type),parameter.IsNullable,f=>f.Execution.Parameters[variable.Name]);}
            case ColumnReferenceExpression reference:{
                if(reference.Collation is not null){throw Unsupported(reference);}
                if(reference.ColumnType!=ColumnType.Regular){throw Unsupported(reference);}
                var identifiers=reference.MultiPartIdentifier.Identifiers;var name=identifiers[^1].Value;
                var matches=columns.Select((column,index)=>(column,index)).Where(x=>MatchesColumn(x.column,identifiers)).ToArray();
                if(matches.Length>1){throw new SqlQueryException("Ambiguous column name '"+name+"'.",209);}
                if(matches.Length==1){var match=matches[0];return new(this.GetColumnType(match.column.Column),match.column.Column.IsNullable,f=>f.Row[match.index],match.column.Column);}
                var enclosing=outer.Select((column,index)=>(column,index)).Where(x=>MatchesColumn(x.column,identifiers)).ToArray();
                if(enclosing.Length!=1){throw new SqlQueryException("Invalid column name '"+string.Join(".",identifiers.Select(x=>x.Value))+"'.",207);}
                var item=enclosing[0];return new(this.GetColumnType(item.column.Column),item.column.Column.IsNullable,f=>f.Outer![item.index],item.column.Column);
            }
            case ParenthesisExpression parenthesis:return this.BindScalar(parenthesis.Expression,columns,outer,aggregates);
            case CastCall cast:{var value=this.BindScalar(cast.Parameter,columns,outer,aggregates);var type=this.DefaultStringType(BindType(cast.DataType),value.Type);return new(type,value.Nullable,f=>SqlValues.ConvertTo(value.Value(f),type,true)){IsComputedFloatingPoint=FloatingConversion(type,value)};}
            case ConvertCall convert:{var value=this.BindScalar(convert.Parameter,columns,outer,aggregates);var type=this.DefaultStringType(BindType(convert.DataType),value.Type);if(convert.Style is not null){if(convert.Style is not IntegerLiteral style||!int.TryParse(style.Value,out var number)||number is not(0 or 100 or 101 or 112 or 120 or 121 or 126 or 127)||type.ClrType!=typeof(DateTime)&&type.ClrType!=typeof(DateOnly)){throw Unsupported(convert);}return new(type,value.Nullable,f=>SqlValues.ConvertDate(value.Value(f),type,number));}return new(type,value.Nullable,f=>SqlValues.ConvertTo(value.Value(f),type,true)){IsComputedFloatingPoint=FloatingConversion(type,value)};}
            case UnaryExpression unary:{var value=this.BindScalar(unary.Expression,columns,outer,aggregates);var operation=BindUnaryOperator(unary);if(operation==SqlUnaryOperator.Positive){return value;}return new(value.Type,value.Nullable,f=>SqlValues.Unary(value.Value(f),operation)){IsComputedFloatingPoint=IsFloating(value.Type)};}
            case BinaryExpression binary:{var operation=BindBinaryOperator(binary);var left=this.BindScalar(binary.FirstExpression,columns,outer,aggregates);var right=this.BindScalar(binary.SecondExpression,columns,outer,aggregates);var type=SqlValues.Promote(left.Type,right.Type,operation);return new(type,left.Nullable||right.Nullable,f=>SqlValues.Binary(left.Value(f),right.Value(f),operation,type)){IsComputedFloatingPoint=IsFloating(type)};}
            case SearchedCaseExpression conditional:{var items=conditional.WhenClauses.Select(x=>(Condition:this.BindBoolean(x.WhenExpression,columns,outer,aggregates),Value:this.BindScalar(x.ThenExpression,columns,outer,aggregates))).ToArray();BoundScalar?otherwise=null;if(conditional.ElseExpression is not null){otherwise=this.BindScalar(conditional.ElseExpression,columns,outer,aggregates);}var resultTypes=new List<SqlType>();for(var index=0;index<items.Length;index++){if(conditional.WhenClauses[index].ThenExpression is not NullLiteral){resultTypes.Add(items[index].Value.Type);}}if(otherwise is not null&&conditional.ElseExpression is not NullLiteral){resultTypes.Add(otherwise.Type);}var type=SqlValues.CommonType(resultTypes);return new(type,true,f=>{foreach(var item in items){if(item.Condition(f)==true){return SqlValues.ConvertTo(item.Value.Value(f),type,true);}}return SqlValues.ConvertTo(otherwise?.Value(f),type,true);}){IsComputedFloatingPoint=AllComputedFloating(type,items.Select(item=>item.Value),otherwise)};}
            case SimpleCaseExpression conditional:{var input=this.BindScalar(conditional.InputExpression,columns,outer,aggregates);var items=conditional.WhenClauses.Select(x=>(When:this.BindScalar(x.WhenExpression,columns,outer,aggregates),Value:this.BindScalar(x.ThenExpression,columns,outer,aggregates))).ToArray();BoundScalar?otherwise=null;if(conditional.ElseExpression is not null){otherwise=this.BindScalar(conditional.ElseExpression,columns,outer,aggregates);}var resultTypes=new List<SqlType>();for(var index=0;index<items.Length;index++){if(conditional.WhenClauses[index].ThenExpression is not NullLiteral){resultTypes.Add(items[index].Value.Type);}}if(otherwise is not null&&conditional.ElseExpression is not NullLiteral){resultTypes.Add(otherwise.Type);}var type=SqlValues.CommonType(resultTypes);var comparisons=items.Select(item=>SqlValues.CommonType([input.Type,item.When.Type])).ToArray();return new(type,true,f=>{var value=input.Value(f);for(var index=0;index<items.Length;index++){var item=items[index];if(SqlValues.Compare(value,item.When.Value(f),SqlComparisonOperator.Equals,comparisons[index])==true){return SqlValues.ConvertTo(item.Value.Value(f),type,true);}}return SqlValues.ConvertTo(otherwise?.Value(f),type,true);}){IsComputedFloatingPoint=AllComputedFloating(type,items.Select(item=>item.Value),otherwise)};}
            case CoalesceExpression coalesce:{var items=coalesce.Expressions.Select(x=>this.BindScalar(x,columns,outer,aggregates)).ToArray();var type=SqlValues.CommonType(items.Where((_,index)=>coalesce.Expressions[index] is not NullLiteral).Select(x=>x.Type));return new(type,true,f=>{foreach(var item in items){var value=item.Value(f);if(value is not null){return SqlValues.ConvertTo(value,type,true);}}return null;}){IsComputedFloatingPoint=AllComputedFloating(type,items)};}
            case NullIfExpression nullIf:{var first=this.BindScalar(nullIf.FirstExpression,columns,outer,aggregates);var second=this.BindScalar(nullIf.SecondExpression,columns,outer,aggregates);var comparison=SqlValues.CommonType([first.Type,second.Type]);return new(first.Type,true,f=>{var value=first.Value(f);if(SqlValues.Compare(value,second.Value(f),SqlComparisonOperator.Equals,comparison)==true){return null;}return value;}){IsComputedFloatingPoint=first.IsComputedFloatingPoint};}
            case LeftFunctionCall function:{var call=new FunctionCall{FunctionName=new Identifier{Value="LEFT"}};foreach(var argument in function.Parameters){call.Parameters.Add(argument);}return this.BindFunction(call,columns,outer,aggregates);}
            case RightFunctionCall function:{var call=new FunctionCall{FunctionName=new Identifier{Value="RIGHT"}};foreach(var argument in function.Parameters){call.Parameters.Add(argument);}return this.BindFunction(call,columns,outer,aggregates);}
            case FunctionCall function:return this.BindFunction(function,columns,outer,aggregates);
            case ScalarSubquery subquery:{var bound=this.BindQuery(subquery.QueryExpression,columns);if(bound.Columns.Count!=1){throw new SqlQueryException("Scalar subquery must return one column.",116);}return new(bound.Columns[0].Type,true,f=>{using var enumerator=bound.Rows(f.Execution,f.Row).GetEnumerator();if(!enumerator.MoveNext()){return null;}var result=enumerator.Current[0];if(enumerator.MoveNext()){throw new SqlQueryException("Subquery returned more than one value.",512);}return result;}){IsComputedFloatingPoint=bound.Columns[0].IsComputedFloatingPoint};}
            default:throw Unsupported(expression);
        }
    }
    private Func<EvalFrame,bool?>BindBoolean(BooleanExpression expression,IReadOnlyList<BindingColumn>columns,IReadOnlyList<BindingColumn>outer,List<AggregateBinding>?aggregates){
        switch(expression){
            case BooleanParenthesisExpression parenthesis:return this.BindBoolean(parenthesis.Expression,columns,outer,aggregates);
            case BooleanNotExpression not:{var value=this.BindBoolean(not.Expression,columns,outer,aggregates);return f=>!value(f);}
            case BooleanBinaryExpression binary:{var left=this.BindBoolean(binary.FirstExpression,columns,outer,aggregates);var right=this.BindBoolean(binary.SecondExpression,columns,outer,aggregates);if(binary.BinaryExpressionType==BooleanBinaryExpressionType.And){return f=>left(f)&right(f);}return f=>left(f)|right(f);}
            case BooleanComparisonExpression comparison:{var operation=BindComparisonOperator(comparison);var left=this.BindScalar(comparison.FirstExpression,columns,outer,aggregates);var right=this.BindScalar(comparison.SecondExpression,columns,outer,aggregates);var type=SqlValues.CommonType([left.Type,right.Type]);return f=>SqlValues.Compare(left.Value(f),right.Value(f),operation,type);}
            case BooleanIsNullExpression isNull:{var value=this.BindScalar(isNull.Expression,columns,outer,aggregates);return f=>(value.Value(f) is null)!=isNull.IsNot;}
            case InPredicate predicate:{var value=this.BindScalar(predicate.Expression,columns,outer,aggregates);if(predicate.Subquery is not null){var subquery=this.BindQuery(predicate.Subquery.QueryExpression,columns);if(subquery.Columns.Count!=1){throw new SqlQueryException("IN subquery must return one column.",116);}var type=SqlValues.CommonType([value.Type,subquery.Columns[0].Type]);return f=>EvaluateIn(value.Value(f),subquery.Rows(f.Execution,f.Row).Select(x=>x[0]),predicate.NotDefined,type);}var items=predicate.Values.Select(x=>this.BindScalar(x,columns,outer,aggregates)).ToArray();var comparisonType=SqlValues.CommonType(items.Select(x=>x.Type).Prepend(value.Type));return f=>EvaluateIn(value.Value(f),items.Select(x=>x.Value(f)),predicate.NotDefined,comparisonType);}
            case BooleanTernaryExpression between:{var value=this.BindScalar(between.FirstExpression,columns,outer,aggregates);var low=this.BindScalar(between.SecondExpression,columns,outer,aggregates);var high=this.BindScalar(between.ThirdExpression,columns,outer,aggregates);var lowType=SqlValues.CommonType([value.Type,low.Type]);var highType=SqlValues.CommonType([value.Type,high.Type]);var negate=between.TernaryExpressionType==BooleanTernaryExpressionType.NotBetween;return f=>{var result=SqlValues.Compare(value.Value(f),low.Value(f),SqlComparisonOperator.GreaterThanOrEqualTo,lowType)&SqlValues.Compare(value.Value(f),high.Value(f),SqlComparisonOperator.LessThanOrEqualTo,highType);if(negate){return !result;}return result;};}
            case LikePredicate like:throw Unsupported(like);
            case ExistsPredicate exists:{var subquery=this.BindQuery(exists.Subquery.QueryExpression,columns);return f=>{using var enumerator=subquery.Rows(f.Execution,f.Row).GetEnumerator();return enumerator.MoveNext();};}
            default:throw Unsupported(expression);
        }
    }
    private static bool MatchesColumn(BindingColumn column,IList<Identifier>identifiers){
        if(!Equal(column.Column.Name,identifiers[^1].Value)){return false;}if(identifiers.Count==1){return true;}if(!Equal(column.Qualifier,identifiers[^2].Value)){return false;}if(identifiers.Count==2){return true;}var source=column.Column.Source;if(source is null||!Equal(column.Qualifier,source.Name)||identifiers.Count>4){return false;}if(!Equal(identifiers[^3].Value,source.Schema)){return false;}if(identifiers.Count==4&&!Equal(identifiers[0].Value,source.Database)){return false;}return true;
    }
    private static bool?EvaluateIn(object?value,IEnumerable<object?>items,bool negate,SqlType?type=null){bool?result=false;foreach(var item in items){var equal=SqlValues.Compare(value,item,SqlComparisonOperator.Equals,type);if(equal==true){return !negate;}if(equal is null){result=null;}}if(negate){return !result;}return result;}
    private static bool IsFloating(SqlType type)=>type.ClrType==typeof(float)||type.ClrType==typeof(double);
    private static bool FloatingConversion(SqlType target,BoundScalar value)=>FloatingConversion(target,value.Type,value.IsComputedFloatingPoint);
    private static bool FloatingConversion(SqlType target,SqlType source,bool computed)=>IsFloating(target)&&(computed||source.ClrType!=target.ClrType);
    private static bool AllComputedFloating(SqlType type,IEnumerable<BoundScalar>values,BoundScalar?otherwise=null){
        if(!IsFloating(type)){return false;}var found=false;foreach(var value in values){if(value.Type.IsUntypedNull){continue;}found=true;if(!FloatingConversion(type,value)){return false;}}
        if(otherwise is not null&&!otherwise.Type.IsUntypedNull){found=true;if(!FloatingConversion(type,otherwise)){return false;}}return found;
    }
    private static bool ComputedFloatingAggregate(string name,SqlType type,BoundScalar?value)=>IsFloating(type)&&(name is "SUM" or "AVG"||value?.IsComputedFloatingPoint==true);
    private static SqlType AggregateType(string name,SqlType type){
        if(name is "SUM" or "AVG"&&type.ClrType!=typeof(byte)&&type.ClrType!=typeof(short)&&type.ClrType!=typeof(int)&&type.ClrType!=typeof(long)&&type.ClrType!=typeof(decimal)&&type.ClrType!=typeof(float)&&type.ClrType!=typeof(double)){throw new SqlQueryException("Operand type is invalid for "+name,8117);}
        if(name is "MIN" or "MAX"&&type.ClrType==typeof(bool)){throw new SqlQueryException("Operand type bit is invalid for "+name,8117);}
        if(name=="COUNT"){type=SqlType.Int;}else if(name=="COUNT_BIG"){type=SqlType.BigInt;}else if(name is "SUM" or "AVG"){if(type.Name is "money" or "smallmoney"){type=new("money",typeof(decimal),null,19,4);}else if(type.ClrType==typeof(decimal)){type=type with{Precision=38};if(name=="AVG"){type=type with{Scale=(byte)Math.Max(6,(int)type.Scale.GetValueOrDefault())};}}else if(type.ClrType==typeof(float)){type=new("float",typeof(double));}else if(type.ClrType==typeof(short)||type.ClrType==typeof(byte)){type=SqlType.Int;}}
        return type;
    }
    private static SqlType VariableStringType(SqlType type){if(type.Name=="char"){return type with{Name="varchar"};}if(type.Name=="nchar"){return type with{Name="nvarchar"};}return type;}
    private SqlType DefaultStringType(SqlType type,SqlType?input=null){
        if(type.ClrType!=typeof(string)||type.Name=="xml"){return type;}
        if(input is{ClrType:var clr}&&clr==typeof(string)&&input.Name!="xml"){return type with{Collation=input.Collation,Coercibility=input.Coercibility};}
        return type with{Collation=type.Collation??this.catalog.GetDatabaseCollation(this.database),Coercibility=SqlCollationCoercibility.CoercibleDefault};
    }
    private SqlType GetColumnType(SqlColumn column){var type=column.Type with{IsUntypedNull=false};if(type.ClrType!=typeof(string)||type.Name=="xml"){return type;}return type with{Collation=type.Collation??this.catalog.GetDatabaseCollation(column.Source?.Database??this.database),Coercibility=SqlCollationCoercibility.Implicit};}
    private static bool IsAggregate(string name)=>name.ToUpperInvariant() is "COUNT" or "COUNT_BIG" or "SUM" or "AVG" or "MIN" or "MAX";
    private BoundScalar BindFunction(FunctionCall function,IReadOnlyList<BindingColumn>columns,IReadOnlyList<BindingColumn>outer,List<AggregateBinding>?aggregates){
        if(function.Collation is not null||function.TrimOptions is not null||function.JsonParameters.Count>0||function.JsonOrderByClause is not null||function.WithArrayWrapper||function.OverClause is not null||function.WithinGroupClause is not null||function.IgnoreRespectNulls.Count>0){throw Unsupported(function);}
        var name=function.FunctionName.Value.ToUpperInvariant();
        if(function.CallTarget is not null){return this.BindTargetFunction(function,columns,outer,aggregates);}
        if(name is "QUOTENAME" or "OBJECTPROPERTYEX"){
            if(name=="QUOTENAME"&&(function.Parameters.Count<1||function.Parameters.Count>2)||name=="OBJECTPROPERTYEX"&&function.Parameters.Count!=2){throw Unsupported(function);}
            IList<ScalarExpression>arguments=function.Parameters;
            if(name=="QUOTENAME"&&arguments.Count==1){arguments=[arguments[0],new StringLiteral{Value="["}];}
            return this.BindRoutine(new(this.database,"sys",name),arguments,columns,outer,aggregates);
        }
        if(name is "SCHEMA_NAME" or "SCHEMA_ID" or "OBJECT_NAME" or "OBJECT_ID" or "HAS_DBACCESS"){
            if(function.Parameters.Count>1||(function.Parameters.Count==0&&name is "OBJECT_NAME" or "OBJECT_ID" or "HAS_DBACCESS")){throw Unsupported(function);}
            var routineName=new SqlObjectName(this.database,"sys",name);
            if(this.catalog.FindRoutine(routineName) is null){throw Unsupported(function);}
            IList<ScalarExpression>arguments=function.Parameters;
            if(arguments.Count==0){
                if(name=="SCHEMA_NAME"){arguments=[new IntegerLiteral{Value="1"}];}
                else{arguments=[new StringLiteral{Value="dbo"}];}
            }
            return this.BindRoutine(routineName,arguments,columns,outer,aggregates);
        }
        if(IsAggregate(name)){
            if(aggregates is null||function.Parameters.Count!=1){throw Unsupported(function);}
            BoundScalar?value=null;if(function.Parameters[0] is not ColumnReferenceExpression{ColumnType:ColumnType.Wildcard}){value=this.BindScalar(function.Parameters[0],columns,outer,null);}else if(name is not("COUNT" or "COUNT_BIG")||function.UniqueRowFilter==UniqueRowFilter.Distinct){throw Unsupported(function);}
            var type=AggregateType(name,value?.Type??SqlType.Int);
            var index=aggregates.Count;aggregates.Add(new(name,value,function.UniqueRowFilter==UniqueRowFilter.Distinct,type));return new(type,true,f=>f.Aggregates![index]){IsComputedFloatingPoint=ComputedFloatingAggregate(name,type,value)};
        }
        if(function.UniqueRowFilter!=UniqueRowFilter.NotSpecified){throw Unsupported(function);}
        var args=function.Parameters.Select((x,index)=>{
            if(index==0&&name is "DATEDIFF" or "DATEADD" or "DATEPART"){var part=Text(x).Trim().Trim('[',']');return new BoundScalar(SqlType.String,false,_=>part);}
            return this.BindScalar(x,columns,outer,aggregates);
        }).ToArray();
        SqlType resultType=this.DefaultStringType(SqlType.String);
        switch(name){
            case "ISNULL":Require(2);resultType=args[0].Type;if(function.Parameters[0] is NullLiteral){resultType=args[1].Type;}break;
            case "LEN":case "DATALENGTH":case "YEAR":case "MONTH":case "DAY":Require(1);resultType=SqlType.Int;break;
            case "CHARINDEX":Require(2);resultType=SqlType.Int;break;
            case "DATEPART":Require(2);resultType=SqlType.Int;break;
            case "ABS":Require(1);resultType=args[0].Type;break;
            case "ROUND":Require(2);resultType=args[0].Type;break;
            case "LEFT":case "RIGHT":Require(2);resultType=VariableStringType(args[0].Type);break;
            case "REPLICATE":Require(2);resultType=VariableStringType(args[0].Type) with{Length=8000};if(args[0].Type.Length==-1){resultType=resultType with{Length=-1};}break;
            case "SUBSTRING":Require(3);resultType=VariableStringType(args[0].Type);break;
            case "REPLACE":Require(3);resultType=VariableStringType(args[0].Type) with{Length=8000};if(args[0].Type.Length==-1){resultType=resultType with{Length=-1};}break;
            case "UPPER":case "LOWER":case "LTRIM":case "RTRIM":Require(1);resultType=VariableStringType(args[0].Type);break;
            case "CONCAT":if(args.Length<2){throw Unsupported(function);}break;
            case "DB_NAME":Require(0);break;
            case "GETDATE":Require(0);resultType=new("datetime",typeof(DateTime));break;
            case "DATEDIFF":Require(3);resultType=SqlType.Int;break;
            case "DATEADD":Require(3);resultType=args[2].Type;break;
            case "DECOMPRESS":Require(1);resultType=new("varbinary",typeof(byte[]),-1);break;
            default:throw Unsupported(function);
        }
        resultType=resultType with{LiteralPrecision=null};
        if(name=="DATALENGTH"){return new(resultType,true,f=>SqlValues.DataLength(args[0].Value(f),args[0].Type));}
        var computed=false;if(IsFloating(resultType)){if(name is "ABS" or "ROUND"){computed=true;}else if(name=="ISNULL"){computed=AllComputedFloating(resultType,args);}}
        return new(resultType,true,f=>SqlValues.ConvertTo(SqlValues.Function(name,args.Select(x=>x.Value(f)).ToArray(),this.database,f.Execution.Timestamp),resultType,true)){IsComputedFloatingPoint=computed};
        void Require(int count){if(args.Length!=count){throw Unsupported(function);}}
    }
    private BoundScalar BindTargetFunction(FunctionCall function,IReadOnlyList<BindingColumn>columns,IReadOnlyList<BindingColumn>outer,List<AggregateBinding>?aggregates){
        var name=function.FunctionName.Value;
        if(function.CallTarget is MultiPartIdentifierCallTarget target){
            var parts=target.MultiPartIdentifier.Identifiers;
            if(Equal(name,"value")){var column=new ColumnReferenceExpression{MultiPartIdentifier=target.MultiPartIdentifier};return this.BindXmlValue(this.BindScalar(column,columns,outer,aggregates),function);}
            var full=new SchemaObjectName();foreach(var part in parts){full.Identifiers.Add(part);}full.Identifiers.Add(function.FunctionName);
            return this.BindRoutine(this.ResolveName(full),function.Parameters,columns,outer,aggregates);
        }
        if(function.CallTarget is ExpressionCallTarget expression&&Equal(name,"value")){return this.BindXmlValue(this.BindScalar(expression.Expression,columns,outer,aggregates),function);}
        throw Unsupported(function);
    }
    private BoundScalar BindRoutine(SqlObjectName name,IList<ScalarExpression>arguments,IReadOnlyList<BindingColumn>columns,IReadOnlyList<BindingColumn>outer,List<AggregateBinding>?aggregates){
        var routine=this.catalog.FindRoutine(name)??throw new SqlQueryException("Unknown function: "+name,4121);
        if(routine.Native is{}native){
            if(arguments.Count!=native.Parameters.Count){throw new SqlQueryException("Routine argument count differs: "+name,313);}
            var argumentsBound=arguments.Select(x=>this.BindScalar(x,columns,outer,aggregates)).ToArray();
            var outputType=this.DefaultStringType(native.ReturnType);
            return new(outputType,native.IsNullable,frame=>{
                frame.Execution.Scope.ThrowIfUnavailable();
                try{
                    var values=new object?[argumentsBound.Length];
                    for(var i=0;i<values.Length;i++){
                        values[i]=SqlValues.ConvertTo(argumentsBound[i].Value(frame),native.Parameters[i].Type,true);
                        if(values[i] is null&&!native.Parameters[i].IsNullable){throw new SqlQueryException("NULL cannot be passed to generated routine parameter: "+native.Parameters[i].Name,515);}
                    }
                    var value=native.Invoke(values);
                    frame.Execution.Scope.ThrowIfUnavailable();
                    if(value is null&&!native.IsNullable){throw new SqlQueryException("Generated routine returned NULL for a non-nullable result.",515);}
                    return SqlValues.ConvertTo(value,outputType,true);
                }catch(SqlQueryException){throw;}catch(OperationCanceledException){throw;}catch(OverflowException){throw new SqlQueryException("Arithmetic overflow in generated routine argument or result.",8115);}catch(FormatException){throw new SqlQueryException("Conversion failed in generated routine argument or result.",245);}catch(Exception exception){throw new SqlQueryException("Generated routine execution failed: "+exception.Message,50000);}
            });
        }
        if(routine.Definition is null){throw new SqlQueryException("SQL routine definition is unavailable: "+name,40515);}
        if(SqlQueryEngine.Parse(routine.Definition) is not CreateFunctionStatement definition||definition.ReturnType is not ScalarFunctionReturnType scalar){throw new SqlQueryException("Unsupported SQL routine definition: "+name,40515);}
        if(arguments.Count!=definition.Parameters.Count){throw new SqlQueryException("Routine argument count differs: "+name,313);}

        var parameters=definition.Parameters.Select(x=>new SqlParameter(x.VariableName.Value,BindType(x.DataType))).ToArray();
        var locals=RoutineLocals(definition.StatementList).ToArray();
        var binder=new QueryBinder(this.catalog,name.Database,parameters.Concat(locals).ToArray(),this.viewStack);
        var body=binder.BindRoutineStatements(definition.StatementList.Statements);
        var values=arguments.Select(x=>this.BindScalar(x,columns,outer,aggregates)).ToArray();var returnType=binder.DefaultStringType(BindType(scalar.DataType));
        var computed=false;var returnedExpression=SingleRoutineReturn(definition.StatementList.Statements);
        if(returnedExpression is not null){var returned=binder.BindScalar(returnedExpression,[],[],null);var direct=returnedExpression;while(direct is ParenthesisExpression parenthesis){direct=parenthesis.Expression;}if(direct is VariableReference variable){for(var index=0;index<parameters.Length;index++){if(Equal(parameters[index].Name,variable.Name)){returned=returned with{IsComputedFloatingPoint=FloatingConversion(parameters[index].Type,values[index])};break;}}}computed=FloatingConversion(returnType,returned);}
        return new(returnType,true,f=>{var variables=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);for(var i=0;i<parameters.Length;i++){variables.Add(parameters[i].Name,SqlValues.ConvertTo(values[i].Value(f),parameters[i].Type,true));}return SqlValues.ConvertTo(body(new([],null,new(f.Execution.Scope,variables,f.Execution.Options))).Value,returnType,true);}){IsComputedFloatingPoint=computed};
    }
    private static SqlType BindType(DataTypeReference dataType){
        if(dataType is not SqlDataTypeReference sql){throw Unsupported(dataType);}
        int?length=null;byte?precision=null;byte?scale=null;
        if(sql.Parameters.Count>0){if(sql.Parameters[0] is MaxLiteral){length=-1;}else{length=int.Parse(sql.Parameters[0].Value,CultureInfo.InvariantCulture);}}
        var name=sql.SqlDataTypeOption.ToString().ToLowerInvariant();Type type;
        switch(sql.SqlDataTypeOption){
            case SqlDataTypeOption.Int:type=typeof(int);break;case SqlDataTypeOption.BigInt:type=typeof(long);break;case SqlDataTypeOption.SmallInt:type=typeof(short);break;case SqlDataTypeOption.TinyInt:type=typeof(byte);break;case SqlDataTypeOption.Bit:type=typeof(bool);break;
            case SqlDataTypeOption.Decimal:case SqlDataTypeOption.Numeric:{type=typeof(decimal);var p=length??18;var s=0;if(sql.Parameters.Count>1&&!int.TryParse(sql.Parameters[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out s)||p<1||p>38||s<0||s>p||sql.Parameters.Count>2){throw new SqlQueryException("Invalid SQL decimal precision or scale.",40515);}precision=(byte)p;scale=(byte)s;length=null;break;}
            case SqlDataTypeOption.Money:type=typeof(decimal);precision=19;scale=4;length=null;break;
            case SqlDataTypeOption.SmallMoney:type=typeof(decimal);precision=10;scale=4;length=null;break;
            case SqlDataTypeOption.Float:type=typeof(double);break;case SqlDataTypeOption.Real:type=typeof(float);break;
            case SqlDataTypeOption.DateTime:case SqlDataTypeOption.DateTime2:case SqlDataTypeOption.SmallDateTime:type=typeof(DateTime);break;
            case SqlDataTypeOption.Date:type=typeof(DateOnly);break;
            case SqlDataTypeOption.Time:type=typeof(TimeOnly);break;case SqlDataTypeOption.DateTimeOffset:type=typeof(DateTimeOffset);break;
            case SqlDataTypeOption.Char:case SqlDataTypeOption.VarChar:case SqlDataTypeOption.NChar:case SqlDataTypeOption.NVarChar:case SqlDataTypeOption.Text:case SqlDataTypeOption.NText:type=typeof(string);length??=30;break;
            case SqlDataTypeOption.Binary:case SqlDataTypeOption.VarBinary:case SqlDataTypeOption.Image:type=typeof(byte[]);length??=30;break;
            case SqlDataTypeOption.UniqueIdentifier:type=typeof(Guid);break;
            default:throw Unsupported(dataType);
        }
        if(sql.SqlDataTypeOption is SqlDataTypeOption.Char or SqlDataTypeOption.VarChar or SqlDataTypeOption.NChar or SqlDataTypeOption.NVarChar or SqlDataTypeOption.Binary or SqlDataTypeOption.VarBinary){var maximum=8000;if(sql.SqlDataTypeOption is SqlDataTypeOption.NChar or SqlDataTypeOption.NVarChar){maximum=4000;}var allowsMax=sql.SqlDataTypeOption is SqlDataTypeOption.VarChar or SqlDataTypeOption.NVarChar or SqlDataTypeOption.VarBinary;if(sql.Parameters.Count>1||length>maximum||length<1&&(length!= -1||!allowsMax)){throw new SqlQueryException("Invalid SQL data type length.",40515);}}
        return new(name,type,length,precision,scale);
    }
}




