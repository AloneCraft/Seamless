using System.Globalization;
using Microsoft.SqlServer.TransactSql.ScriptDom;
namespace Seamless.Database.SqlTranslation;
internal sealed partial class SqlBinder{
    // ---- 真偽の式 ----
    public BoundExpression BindBoolean(BooleanExpression expression,BoundScope scope,bool aggregateAllowed){
        switch(expression){
            case BooleanParenthesisExpression parenthesis:
                return this.BindBoolean(parenthesis.Expression,scope,aggregateAllowed);
            case BooleanBinaryExpression binary:{
                var left=this.BindBoolean(binary.FirstExpression,scope,aggregateAllowed);
                var right=this.BindBoolean(binary.SecondExpression,scope,aggregateAllowed);
                var operation=binary.BinaryExpressionType switch{
                    BooleanBinaryExpressionType.And=>'&',
                    BooleanBinaryExpressionType.Or=>'|',
                    _=>throw Unsupported(binary,"SQLT060",binary.BinaryExpressionType.ToString())
                };
                return new BoundLogical(operation,left,right);
            }
            case BooleanNotExpression not:
                return new BoundNot(this.BindBoolean(not.Expression,scope,aggregateAllowed));
            case BooleanComparisonExpression comparison:{
                var left=this.BindScalar(comparison.FirstExpression,scope,aggregateAllowed);
                var right=this.BindScalar(comparison.SecondExpression,scope,aggregateAllowed);
                var operation=comparison.ComparisonType switch{
                    BooleanComparisonType.Equals=>"Equal",
                    BooleanComparisonType.NotEqualToBrackets or BooleanComparisonType.NotEqualToExclamation=>"NotEqual",
                    BooleanComparisonType.GreaterThan=>"Greater",
                    BooleanComparisonType.LessThan=>"Less",
                    BooleanComparisonType.GreaterThanOrEqualTo or BooleanComparisonType.NotLessThan=>"GreaterOrEqual",
                    BooleanComparisonType.LessThanOrEqualTo or BooleanComparisonType.NotGreaterThan=>"LessOrEqual",
                    _=>throw Unsupported(comparison,"SQLT061",comparison.ComparisonType.ToString())
                };
                return this.Compare(operation,left,right,comparison);
            }
            case BooleanIsNullExpression isNull:
                return new BoundIsNull(this.BindScalar(isNull.Expression,scope,aggregateAllowed),isNull.IsNot);
            case BooleanTernaryExpression ternary:{
                var value=this.BindScalar(ternary.FirstExpression,scope,aggregateAllowed);
                var low=this.BindScalar(ternary.SecondExpression,scope,aggregateAllowed);
                var high=this.BindScalar(ternary.ThirdExpression,scope,aggregateAllowed);
                var between=new BoundLogical('&',this.Compare("GreaterOrEqual",value,low,ternary),this.Compare("LessOrEqual",value,high,ternary));
                if(ternary.TernaryExpressionType==BooleanTernaryExpressionType.NotBetween){
                    return new BoundNot(between);
                }
                return between;
            }
            case LikePredicate like:{
                if(like.OdbcEscape){
                    throw Unsupported(like,"SQLT062","ODBC の ESCAPE");
                }
                var value=this.BindScalar(like.FirstExpression,scope,aggregateAllowed);
                var pattern=this.BindScalar(like.SecondExpression,scope,aggregateAllowed);
                string? escape=null;
                if(like.EscapeExpression is not null){
                    if(like.EscapeExpression is not StringLiteral literal||literal.Value.Length!=1){
                        throw Unsupported(like.EscapeExpression,"SQLT062","1文字のリテラルでない ESCAPE");
                    }
                    escape=literal.Value;
                }
                var stringType=SqlTypeRules.NVarChar(-1,value.Type.Collation??pattern.Type.Collation);
                return new BoundLike(this.Convert(value,stringType,like),this.Convert(pattern,stringType,like),escape,this.CollationOf(value,pattern),like.NotDefined);
            }
            case InPredicate @in:{
                var value=this.BindScalar(@in.Expression,scope,aggregateAllowed);
                if(@in.Subquery is{} subquery){
                    var query=this.BindSubquery(subquery.QueryExpression,scope,1);
                    var item=query.SelectItems[0].Expression;
                    var common=SqlTypeRules.Common(value.Type,item.Type);
                    query.SelectItems[0]=query.SelectItems[0] with{Expression=this.Convert(item,common,@in)};
                    return new BoundInSubquery(this.Convert(value,common,@in),query,this.CollationOf(value,item),@in.NotDefined);
                }
                var items=@in.Values.Select(item=>this.BindScalar(item,scope,aggregateAllowed)).ToList();
                var type=items.Aggregate(value.Type,static (current,item)=>SqlTypeRules.Common(current,item.Type));
                return new BoundIn(this.Convert(value,type,@in),items.Select(item=>this.Convert(item,type,@in)).ToList(),this.CollationOf([value,..items]),@in.NotDefined);
            }
            case ExistsPredicate exists:
                return new BoundExists(this.BindSubquery(exists.Subquery.QueryExpression,scope,null),false);
            case FullTextPredicate:
                throw Unsupported(expression,"SQLT063","全文検索(再現不可。模擬は段階3)");
            default:
                throw Unsupported(expression,"SQLT001",expression.GetType().Name);
        }
    }
    private BoundExpression Compare(string operation,BoundExpression left,BoundExpression right,TSqlFragment at){
        var type=SqlTypeRules.Common(left.Type,right.Type);
        if(SqlTypeRules.IsNull(type)){
            type=SqlTypeRules.Int;
        }
        if(type.Name is "xml" or "text" or "ntext" or "image"){
            throw Unsupported(at,"SQLT064",$"{type.Name} の比較(本物ではエラー 402)");
        }
        return new BoundComparison(operation,this.Convert(left,type,at),this.Convert(right,type,at),this.CollationOf(left,right));
    }
    private string CollationOf(params BoundExpression[] expressions){
        foreach(var expression in expressions){
            if(expression.Type.Collation is{} collation){
                return collation;
            }
        }
        return this.catalog.DatabaseCollation;
    }
    // ---- 値の式 ----
    public BoundExpression BindScalar(ScalarExpression expression,BoundScope scope,bool aggregateAllowed){
        switch(expression){
            case ParenthesisExpression parenthesis:
                return this.BindScalar(parenthesis.Expression,scope,aggregateAllowed);
            case ColumnReferenceExpression column:
                return this.ResolveColumn(column,scope);
            case IntegerLiteral integer:
                return IntegerLiteralOf(integer.Value,false);
            case NumericLiteral numeric:
                return DecimalLiteralOf(numeric.Value,false);
            case RealLiteral real:{
                var value=double.Parse(real.Value,NumberStyles.Float,CultureInfo.InvariantCulture);
                return new BoundLiteral($"(double?){value.ToString("R",CultureInfo.InvariantCulture)}d",SqlTypeRules.Float);
            }
            case MoneyLiteral money:{
                var value=decimal.Parse(money.Value.TrimStart('$'),NumberStyles.Float,CultureInfo.InvariantCulture);
                return new BoundLiteral($"(decimal?){value.ToString(CultureInfo.InvariantCulture)}m",new("money",null,19,4));
            }
            case StringLiteral text:{
                var length=Math.Max(1,text.Value.Length);
                if(text.IsNational){
                    return new BoundLiteral(CSharpNames.String(text.Value),SqlTypeRules.NVarChar(length,this.catalog.DatabaseCollation));
                }
                return new BoundLiteral(CSharpNames.String(text.Value),SqlTypeRules.VarChar(length,this.catalog.DatabaseCollation));
            }
            case NullLiteral:
                return new BoundLiteral("null",SqlTypeRules.Null);
            case BinaryLiteral binary:{
                var hex=binary.Value[2..];
                if(hex.Length%2==1){
                    hex="0"+hex;
                }
                return new BoundLiteral($"global::System.Convert.FromHexString({CSharpNames.String(hex)})",new("varbinary",hex.Length/2));
            }
            case UnaryExpression unary:{
                if(unary.UnaryExpressionType==UnaryExpressionType.Negative){
                    // 負の数のリテラルは、型を変えずに1つのリテラルにする。
                    switch(unary.Expression){
                        case IntegerLiteral integer:
                            return IntegerLiteralOf(integer.Value,true);
                        case NumericLiteral numeric:
                            return DecimalLiteralOf(numeric.Value,true);
                    }
                    var operand=this.BindScalar(unary.Expression,scope,aggregateAllowed);
                    if(!SqlTypeRules.IsNumeric(operand.Type)){
                        throw Unsupported(unary,"SQLT065",$"{operand.Type.Name} の符号反転");
                    }
                    if(operand.Type.Name is "tinyint" or "bit"){
                        operand=this.Convert(operand,SqlTypeRules.Int,unary);
                    }
                    return new BoundNegate(operand);
                }
                if(unary.UnaryExpressionType==UnaryExpressionType.Positive){
                    return this.BindScalar(unary.Expression,scope,aggregateAllowed);
                }
                throw Unsupported(unary,"SQLT065",unary.UnaryExpressionType.ToString());
            }
            case BinaryExpression binary:
                return this.BindArithmetic(binary,scope,aggregateAllowed);
            case CastCall cast:
                return this.Convert(this.BindScalar(cast.Parameter,scope,aggregateAllowed),this.DataType(cast.DataType),cast,0,true);
            case ConvertCall convert:{
                var style=0;
                if(convert.Style is not null){
                    if(convert.Style is not IntegerLiteral literal){
                        throw Unsupported(convert.Style,"SQLT066","リテラルでない CONVERT の style");
                    }
                    style=int.Parse(literal.Value,CultureInfo.InvariantCulture);
                }
                return this.Convert(this.BindScalar(convert.Parameter,scope,aggregateAllowed),this.DataType(convert.DataType),convert,style,true);
            }
            case SearchedCaseExpression searched:{
                var branches=searched.WhenClauses.Select(clause=>(When:this.BindBoolean(clause.WhenExpression,scope,aggregateAllowed),Then:this.BindScalar(clause.ThenExpression,scope,aggregateAllowed))).ToList();
                BoundExpression? @else=null;
                if(searched.ElseExpression is not null){
                    @else=this.BindScalar(searched.ElseExpression,scope,aggregateAllowed);
                }
                return this.Case(branches,@else,searched);
            }
            case SimpleCaseExpression simple:{
                var input=this.BindScalar(simple.InputExpression,scope,aggregateAllowed);
                var branches=simple.WhenClauses.Select(clause=>(When:this.Compare("Equal",input,this.BindScalar(clause.WhenExpression,scope,aggregateAllowed),clause),Then:this.BindScalar(clause.ThenExpression,scope,aggregateAllowed))).ToList();
                BoundExpression? @else=null;
                if(simple.ElseExpression is not null){
                    @else=this.BindScalar(simple.ElseExpression,scope,aggregateAllowed);
                }
                return this.Case(branches,@else,simple);
            }
            case IIfCall iif:{
                var branches=new List<(BoundExpression When,BoundExpression Then)>{(this.BindBoolean(iif.Predicate,scope,aggregateAllowed),this.BindScalar(iif.ThenExpression,scope,aggregateAllowed))};
                return this.Case(branches,this.BindScalar(iif.ElseExpression,scope,aggregateAllowed),iif);
            }
            case CoalesceExpression coalesce:{
                var items=coalesce.Expressions.Select(item=>this.BindScalar(item,scope,aggregateAllowed)).ToList();
                var type=items.Aggregate(SqlTypeRules.Null,static (current,item)=>SqlTypeRules.Common(current,item.Type));
                return new BoundCoalesce(items.Select(item=>this.Convert(item,type,coalesce)).ToList(),type);
            }
            case NullIfExpression nullIf:{
                var left=this.BindScalar(nullIf.FirstExpression,scope,aggregateAllowed);
                var right=this.BindScalar(nullIf.SecondExpression,scope,aggregateAllowed);
                return new BoundNullIf(left,this.Convert(right,left.Type,nullIf),this.CollationOf(left,right));
            }
            case ScalarSubquery subquery:{
                var query=this.BindSubquery(subquery.QueryExpression,scope,1);
                return new BoundScalarSubquery(query,query.SelectItems[0].Expression.Type);
            }
            case FunctionCall call:
                return this.BindFunction(call,scope,aggregateAllowed);
            case LeftFunctionCall left:
                return this.BindNamedFunction("LEFT",left.Parameters,left,scope,aggregateAllowed);
            case RightFunctionCall right:
                return this.BindNamedFunction("RIGHT",right.Parameters,right,scope,aggregateAllowed);
            case ParameterlessCall parameterless:
                return parameterless.ParameterlessCallType switch{
                    ParameterlessCallType.CurrentTimestamp=>this.BindNamedFunction("GETDATE",[],parameterless,scope,aggregateAllowed),
                    ParameterlessCallType.CurrentUser or ParameterlessCallType.User or ParameterlessCallType.SessionUser=>this.BindNamedFunction("USER_NAME",[],parameterless,scope,aggregateAllowed),
                    ParameterlessCallType.SystemUser=>this.BindNamedFunction("ORIGINAL_LOGIN",[],parameterless,scope,aggregateAllowed),
                    _=>throw Unsupported(parameterless,"SQLT001",parameterless.ParameterlessCallType.ToString())
                };
            default:
                throw Unsupported(expression,"SQLT001",expression.GetType().Name);
        }
    }
    private BoundExpression Case(List<(BoundExpression When,BoundExpression Then)> branches,BoundExpression? @else,TSqlFragment at){
        var type=branches.Aggregate(@else?.Type??SqlTypeRules.Null,static (current,branch)=>SqlTypeRules.Common(current,branch.Then.Type));
        if(SqlTypeRules.IsNull(type)){
            throw Unsupported(at,"SQLT067","結果がすべて NULL の CASE(本物ではエラー 8133)");
        }
        BoundExpression? converted=null;
        if(@else is not null){
            converted=this.Convert(@else,type,at);
        }
        return new BoundCase(branches.Select(branch=>(branch.When,this.Convert(branch.Then,type,at))).ToList(),converted,type);
    }
    private BoundExpression BindArithmetic(BinaryExpression binary,BoundScope scope,bool aggregateAllowed){
        var left=this.BindScalar(binary.FirstExpression,scope,aggregateAllowed);
        var right=this.BindScalar(binary.SecondExpression,scope,aggregateAllowed);
        var operation=binary.BinaryExpressionType switch{
            BinaryExpressionType.Add=>'+',
            BinaryExpressionType.Subtract=>'-',
            BinaryExpressionType.Multiply=>'*',
            BinaryExpressionType.Divide=>'/',
            BinaryExpressionType.Modulo=>'%',
            _=>throw Unsupported(binary,"SQLT068",binary.BinaryExpressionType.ToString())
        };
        // 文字列どうしの + は連結。片方が文字列で片方が数なら、文字列を数へ変換して足す(型の優先順位)。
        if(operation=='+'&&(SqlTypeRules.IsString(left.Type)||SqlTypeRules.IsNull(left.Type))&&(SqlTypeRules.IsString(right.Type)||SqlTypeRules.IsNull(right.Type))&&!(SqlTypeRules.IsNull(left.Type)&&SqlTypeRules.IsNull(right.Type))){
            var name="varchar";
            if(SqlTypeRules.IsUnicode(left.Type)||SqlTypeRules.IsUnicode(right.Type)){
                name="nvarchar";
            }
            var type=new SqlTypeDescriptor(name,-1,null,null,this.CollationOf(left,right));
            return new BoundConcat(this.Convert(left,type,binary),this.Convert(right,type,binary),type);
        }
        if(SqlTypeRules.IsTemporal(left.Type)||SqlTypeRules.IsTemporal(right.Type)){
            throw Unsupported(binary,"SQLT069","日付の算術(DATEADD に書き換えてから訳す)");
        }
        if(SqlTypeRules.IsString(left.Type)){
            left=this.Convert(left,right.Type,binary);
        }
        if(SqlTypeRules.IsString(right.Type)){
            right=this.Convert(right,left.Type,binary);
        }
        if(!SqlTypeRules.IsNumeric(left.Type)&&!SqlTypeRules.IsNull(left.Type)||!SqlTypeRules.IsNumeric(right.Type)&&!SqlTypeRules.IsNull(right.Type)){
            throw Unsupported(binary,"SQLT068",$"{left.Type.Name} と {right.Type.Name} の算術");
        }
        // 整数のリテラルは、decimal との算術では桁数を精度にした decimal(n,0) として型を決める(本物と同じ。3 は decimal(1,0))。
        var result=SqlTypeRules.Arithmetic(LiteralShape(left,right),LiteralShape(right,left),operation);
        if(result.Name=="bit"){
            result=SqlTypeRules.Int;
        }
        // 計算は結果の C# の型で行う(decimal の桁は出力時に結果の型へ合わせる)。
        var operandType=result;
        if(SqlTypeRules.IsDecimal(result)){
            operandType=new("decimal",null,38,result.Scale);
        }
        return new BoundArithmetic(operation,this.ConvertOperand(left,operandType,binary),this.ConvertOperand(right,operandType,binary),result);
    }
    private static SqlTypeDescriptor LiteralShape(BoundExpression operand,BoundExpression other){
        if(operand is BoundLiteral{LiteralType.Name:"int"} literal&&other.Type.Name=="decimal"){
            var digits=literal.Code.Replace("(int?)","",StringComparison.Ordinal).TrimStart('-').Length;
            return SqlTypeRules.Decimal(Math.Max(1,digits),0);
        }
        return operand.Type;
    }
    // 算術の被演算子を、結果の C# の型へ。decimal どうしは桁を合わせずに値だけ渡す。
    private BoundExpression ConvertOperand(BoundExpression operand,SqlTypeDescriptor type,TSqlFragment at){
        if(SqlTypeRules.IsDecimal(type)&&SqlTypeRules.IsDecimal(operand.Type)){
            return operand;
        }
        return this.Convert(operand,type,at);
    }
    private static BoundLiteral IntegerLiteralOf(string text,bool negative){
        var value=decimal.Parse(text,NumberStyles.Integer,CultureInfo.InvariantCulture);
        if(negative){
            value=-value;
        }
        if(value>=int.MinValue&&value<=int.MaxValue){
            return new BoundLiteral($"(int?){value.ToString(CultureInfo.InvariantCulture)}",SqlTypeRules.Int);
        }
        // int に入らない整数のリテラルは decimal(p,0)。
        var digits=text.TrimStart('0').Length;
        return new BoundLiteral($"(decimal?){value.ToString(CultureInfo.InvariantCulture)}m",SqlTypeRules.Decimal(Math.Max(1,digits),0));
    }
    private static BoundLiteral DecimalLiteralOf(string text,bool negative){
        var value=decimal.Parse(text,NumberStyles.Float,CultureInfo.InvariantCulture);
        if(negative){
            value=-value;
        }
        var point=text.IndexOf('.');
        var scale=0;
        if(point>=0){
            scale=text.Length-point-1;
        }
        var digits=text.Replace(".","",StringComparison.Ordinal).TrimStart('0').Length;
        var precision=Math.Max(Math.Max(1,digits),scale);
        return new BoundLiteral($"(decimal?){value.ToString(CultureInfo.InvariantCulture)}m",SqlTypeRules.Decimal(precision,scale));
    }
    // ---- 列 ----
    private BoundExpression ResolveColumn(ColumnReferenceExpression reference,BoundScope scope){
        if(reference.ColumnType!=ColumnType.Regular){
            throw Unsupported(reference,"SQLT070",reference.ColumnType.ToString());
        }
        var parts=reference.MultiPartIdentifier.Identifiers.Select(static identifier=>identifier.Value).ToArray();
        var columnName=parts[^1];
        string? qualifier=null;
        if(parts.Length>=2){
            qualifier=parts[^2];
        }
        for(var current=scope;current is not null;current=current.Outer){
            var found=new List<(RowBinding Row,CatalogColumn Column)>();
            foreach(var row in current.Rows){
                if(qualifier is not null&&!row.Alias.Equals(qualifier,StringComparison.OrdinalIgnoreCase)){
                    continue;
                }
                var column=row.Relation.Columns.FirstOrDefault(column=>column.Name.Equals(columnName,StringComparison.OrdinalIgnoreCase));
                if(column is not null){
                    found.Add((row,column));
                }
            }
            if(found.Count>1){
                throw new TranslationUnsupportedException("SQLT071",$"列 {columnName} があいまい(本物ではエラー 209)。",reference.StartLine,reference.StartColumn);
            }
            if(found.Count==1){
                return new BoundColumn(found[0].Row,found[0].Column);
            }
        }
        throw new TranslationUnsupportedException("SQLT072",$"列 {string.Join(".",parts)} が見つからない(本物ではエラー 207)。",reference.StartLine,reference.StartColumn);
    }
    // ---- 副問い合わせ ----
    private BoundSelect BindSubquery(QueryExpression query,BoundScope scope,int? columns){
        var bound=this.BindQuery(query,scope);
        if(bound is not BoundSelect select){
            throw Unsupported(query,"SQLT073","集合演算を含む副問い合わせ");
        }
        if(columns is{} count&&select.SelectItems.Count!=count){
            throw Unsupported(query,"SQLT074","列が1つでない副問い合わせ(本物ではエラー 116)");
        }
        if(select.GroupBy.Count>0){
            throw Unsupported(query,"SQLT075","GROUP BY を含む副問い合わせ");
        }
        return select;
    }
    // ---- 型 ----
    private SqlTypeDescriptor DataType(DataTypeReference reference){
        string name;
        var parameters=new List<string>();
        switch(reference){
            case SqlDataTypeReference sql:
                name=sql.SqlDataTypeOption.ToString().ToLowerInvariant();
                parameters.AddRange(sql.Parameters.Select(static parameter=>parameter.Value));
                break;
            case UserDataTypeReference user:
                name=user.Name.BaseIdentifier.Value.ToLowerInvariant();
                break;
            default:
                throw Unsupported(reference,"SQLT080",reference.GetType().Name);
        }
        name=name switch{
            "integer"=>"int",
            "character"=>"char",
            "dec"=>"decimal",
            "numeric"=>"decimal",
            _=>name
        };
        int? length=null;
        byte? precision=null;
        byte? scale=null;
        if(name is "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary"){
            if(parameters.Count==0){
                // 長さを書かない CAST・CONVERT は 30(本物と同じ)。
                length=30;
            }else if(parameters[0].Equals("max",StringComparison.OrdinalIgnoreCase)){
                length=-1;
            }else{
                length=int.Parse(parameters[0],CultureInfo.InvariantCulture);
            }
        }
        if(name=="decimal"){
            precision=18;
            scale=0;
            if(parameters.Count>=1){
                precision=byte.Parse(parameters[0],CultureInfo.InvariantCulture);
            }
            if(parameters.Count>=2){
                scale=byte.Parse(parameters[1],CultureInfo.InvariantCulture);
            }
        }
        if(name is "datetime2" or "datetimeoffset" or "time"){
            scale=7;
            if(parameters.Count>=1){
                scale=byte.Parse(parameters[0],CultureInfo.InvariantCulture);
            }
        }
        if(name=="float"&&parameters.Count>=1&&int.Parse(parameters[0],CultureInfo.InvariantCulture)<=24){
            name="real";
        }
        string? collation=null;
        if(name is "char" or "varchar" or "nchar" or "nvarchar" or "text" or "ntext"){
            collation=this.catalog.DatabaseCollation;
        }
        var type=SqlTypeDescriptor.Of(name,length,precision,scale,collation);
        SqlTypeRules.ClrType(type);
        return type;
    }
}
