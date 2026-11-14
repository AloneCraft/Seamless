using System.Reflection;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.SqlServer.Functions;
namespace Seamless.Database.SqlTranslation;
internal sealed partial class SqlBinder{
    private const string FunctionsNamespace="global::Seamless.Database.SqlServer.Functions";
    private static readonly HashSet<string> datePartFunctions=new(StringComparer.OrdinalIgnoreCase){"DATEPART","DATENAME","DATEADD","DATEDIFF","DATEDIFF_BIG"};
    private static readonly HashSet<string> aggregates=new(StringComparer.OrdinalIgnoreCase){"COUNT","COUNT_BIG","SUM","AVG","MIN","MAX","STRING_AGG"};
    // ---- 関数 ----
    private BoundExpression BindFunction(FunctionCall call,BoundScope scope,bool aggregateAllowed){
        var name=call.FunctionName.Value.ToUpperInvariant();
        if(call.CallTarget is not null){
            throw Unsupported(call,"SQLT090",$"{name} の呼び出し先つきの呼び出し(XML のメソッド・ユーザー定義関数は段階3)");
        }
        if(call.OverClause is not null){
            throw Unsupported(call,"SQLT091","ウィンドウ関数(段階3)");
        }
        if(call.WithinGroupClause is not null){
            throw Unsupported(call,"SQLT092","WITHIN GROUP(段階3)");
        }
        if(aggregates.Contains(name)){
            if(!aggregateAllowed){
                throw Unsupported(call,"SQLT093","ここでは使えない集約(本物ではエラー 147)");
            }
            return this.BindAggregate(name,call,scope);
        }
        if(call.UniqueRowFilter==UniqueRowFilter.Distinct){
            throw Unsupported(call,"SQLT094",$"{name} の DISTINCT");
        }
        if(name=="ISNULL"){
            if(call.Parameters.Count!=2){
                throw Unsupported(call,"SQLT095","引数が2つでない ISNULL");
            }
            var first=this.BindScalar(call.Parameters[0],scope,aggregateAllowed);
            var second=this.BindScalar(call.Parameters[1],scope,aggregateAllowed);
            var type=first.Type;
            if(SqlTypeRules.IsNull(type)){
                type=second.Type;
            }
            return new BoundCoalesce([this.Convert(first,type,call),this.Convert(second,type,call)],type);
        }
        return this.BindNamedFunction(name,call.Parameters,call,scope,aggregateAllowed);
    }
    private BoundExpression BindNamedFunction(string name,IList<ScalarExpression> parameters,TSqlFragment at,BoundScope scope,bool aggregateAllowed){
        var methods=SqlFunctionCatalog.Find(name).Where(static method=>!method.GetParameters().Any(static parameter=>parameter.ParameterType.IsGenericType&&parameter.ParameterType.GetGenericTypeDefinition()==typeof(IEnumerable<>))).ToArray();
        if(methods.Length==0){
            throw Unsupported(at,"SQLT096",$"関数 {name}");
        }
        string? datePart=null;
        var start=0;
        if(datePartFunctions.Contains(name)){
            if(parameters.Count==0){
                throw Unsupported(at,"SQLT097",$"{name} の日付の単位");
            }
            // 日付の単位(day・month など)は、構文木では列の参照や別の種類の式として読まれるので、元の文字列で読む。
            var part=parameters[0];
            SqlDatePart parsed;
            try{
                parsed=SqlDateParts.Parse(Text(part));
            }catch(SqlFunctionException error){
                throw new TranslationUnsupportedException("SQLT097",error.Message,part.StartLine,part.StartColumn);
            }
            datePart=$"{FunctionsNamespace}.SqlDatePart.{parsed}";
            start=1;
        }
        var arguments=parameters.Skip(start).Select(parameter=>this.BindScalar(parameter,scope,aggregateAllowed)).ToList();
        var collation=this.CollationOf([..arguments.Where(static argument=>SqlTypeRules.IsString(argument.Type))]);
        if(name=="CHAR"){
            // CHAR はデータベースの既定の照合順序の文字コードを使う(本物で確かめた)。
            collation=this.catalog.DatabaseCollation;
        }
        (MethodInfo Method,List<BoundArgument> Arguments,int Cost)? best=null;
        foreach(var method in methods){
            if(this.Match(method,arguments,datePart,collation,at) is not{} match){
                continue;
            }
            if(best is null||match.Cost<best.Value.Cost){
                best=(method,match.Arguments,match.Cost);
            }
        }
        if(best is not{} chosen){
            throw Unsupported(at,"SQLT098",$"{name}({string.Join(",",arguments.Select(static argument=>argument.Type.ToString()))}) に合う実装");
        }
        var result=this.FunctionResultType(name,chosen.Method,arguments,collation,at);
        return new BoundCall($"{FunctionsNamespace}.{chosen.Method.DeclaringType!.Name}.{chosen.Method.Name}",chosen.Arguments,result);
    }
    private (List<BoundArgument> Arguments,int Cost)? Match(MethodInfo method,List<BoundExpression> arguments,string? datePart,string collation,TSqlFragment at){
        var bound=new List<BoundArgument>();
        var index=0;
        var cost=0;
        foreach(var parameter in method.GetParameters()){
            var type=parameter.ParameterType;
            if(type==typeof(SqlSession)){
                bound.Add(new(null,$"{FunctionsNamespace}.SqlSession.Default",null));
                continue;
            }
            if(type==typeof(SqlCollation)){
                bound.Add(new(null,$"{FunctionsNamespace}.SqlCollation.Get({CSharpNames.String(collation)})",null));
                continue;
            }
            if(type==typeof(SqlDatePart)){
                if(datePart is null){
                    return null;
                }
                bound.Add(new(null,datePart,null));
                continue;
            }
            if(type==typeof(bool)&&parameter.Name=="unicode"){
                var unicode=arguments.Count>0&&SqlTypeRules.IsUnicode(arguments[0].Type);
                bound.Add(new(null,unicode.ToString().ToLowerInvariant(),null));
                continue;
            }
            if(parameter.GetCustomAttribute<ParamArrayAttribute>() is not null){
                for(;index<arguments.Count;index++){
                    var argument=arguments[index];
                    var text=SqlTypeRules.NVarChar(-1,argument.Type.Collation??collation);
                    if(SqlTypeRules.IsString(argument.Type)){
                        text=argument.Type with{Length=-1};
                    }
                    bound.Add(new(this.Convert(argument,text,at,0,true),null,null));
                }
                continue;
            }
            if(index<arguments.Count){
                if(ParameterType(type,arguments[index].Type) is not{} target){
                    return null;
                }
                cost+=target.Cost;
                string? cast=null;
                if(type==typeof(IFormattable)){
                    cast="global::System.IFormattable?";
                }
                bound.Add(new(this.Convert(arguments[index],target.Type,at,0,true),null,cast));
                index++;
                continue;
            }
            if(parameter.HasDefaultValue){
                break;
            }
            return null;
        }
        if(index!=arguments.Count){
            return null;
        }
        return (bound,cost);
    }
    // 関数の引数の C# の型に、SQL の引数をそろえる型と、その手間(小さいほど優先)。
    private static (SqlTypeDescriptor Type,int Cost)? ParameterType(Type parameter,SqlTypeDescriptor argument){
        var isNull=SqlTypeRules.IsNull(argument);
        if(parameter==typeof(string)){
            if(SqlTypeRules.IsString(argument)){
                return (argument,0);
            }
            if(isNull){
                return (SqlTypeRules.NVarChar(-1,null),0);
            }
            if(SqlTypeRules.IsNumeric(argument)||SqlTypeRules.IsTemporal(argument)){
                return (SqlTypeRules.VarChar(30,null),5);
            }
            return null;
        }
        if(parameter==typeof(int?)||parameter==typeof(int)){
            return Numeric(argument,SqlTypeRules.Int,isNull);
        }
        if(parameter==typeof(long?)||parameter==typeof(long)){
            var numeric=Numeric(argument,SqlTypeRules.BigInt,isNull);
            if(numeric is{} value&&argument.Name!="bigint"&&value.Cost==0){
                return (value.Type,1);
            }
            return numeric;
        }
        if(parameter==typeof(decimal?)){
            if(SqlTypeRules.IsDecimal(argument)){
                return (argument,0);
            }
            if(SqlTypeRules.IsInteger(argument)||argument.Name=="bit"){
                var (precision,_)=SqlTypeRules.Shape(argument);
                return (SqlTypeRules.Decimal(precision,0),2);
            }
            if(isNull){
                return (SqlTypeRules.Decimal(18,0),2);
            }
            return null;
        }
        if(parameter==typeof(double?)){
            if(argument.Name is "float" or "real"){
                return (SqlTypeRules.Float,0);
            }
            if(SqlTypeRules.IsNumeric(argument)||isNull){
                return (SqlTypeRules.Float,3);
            }
            if(SqlTypeRules.IsString(argument)){
                return (SqlTypeRules.Float,6);
            }
            return null;
        }
        if(parameter==typeof(DateTime?)){
            if(SqlTypeRules.IsTemporal(argument)){
                return (argument,0);
            }
            if(SqlTypeRules.IsString(argument)||isNull){
                return (SqlTypeRules.DateTime,4);
            }
            return null;
        }
        if(parameter==typeof(DateTimeOffset?)){
            if(argument.Name=="datetimeoffset"){
                return (argument,0);
            }
            return null;
        }
        if(parameter==typeof(byte[])){
            if(SqlTypeRules.IsBinary(argument)){
                return (argument,0);
            }
            if(isNull){
                return (new("varbinary",-1),0);
            }
            return null;
        }
        if(parameter==typeof(Guid?)){
            if(argument.Name=="uniqueidentifier"||isNull){
                return (SqlTypeRules.UniqueIdentifier,0);
            }
            return null;
        }
        if(parameter==typeof(bool?)){
            if(argument.Name=="bit"||isNull){
                return (SqlTypeRules.Bit,0);
            }
            return null;
        }
        if(parameter==typeof(IFormattable)){
            if(SqlTypeRules.IsNumeric(argument)||SqlTypeRules.IsTemporal(argument)){
                return (argument,1);
            }
            return null;
        }
        return null;
    }
    private static (SqlTypeDescriptor Type,int Cost)? Numeric(SqlTypeDescriptor argument,SqlTypeDescriptor target,bool isNull){
        if(argument.Name==target.Name||isNull){
            return (target,0);
        }
        if(SqlTypeRules.IsInteger(argument)||argument.Name=="bit"){
            return (target,1);
        }
        if(SqlTypeRules.IsNumeric(argument)){
            return (target,3);
        }
        if(SqlTypeRules.IsString(argument)){
            return (target,6);
        }
        return null;
    }
    // 関数の結果の SQL の型。文字列の長さと照合順序、日付の型は関数ごとの規則で決める。
    private SqlTypeDescriptor FunctionResultType(string name,MethodInfo method,List<BoundExpression> arguments,string collation,TSqlFragment at){
        var firstString=arguments.FirstOrDefault(static argument=>SqlTypeRules.IsString(argument.Type))?.Type;
        switch(name){
            case "CHAR":
                return SqlTypeRules.VarChar(1,collation);
            case "NCHAR":
                return SqlTypeRules.NVarChar(1,collation);
            case "SPACE":
                return SqlTypeRules.VarChar(-1,collation);
            case "SOUNDEX":
                return SqlTypeRules.VarChar(4,collation);
            case "STR":
                return SqlTypeRules.VarChar(-1,collation);
            case "QUOTENAME":
                return SqlTypeRules.NVarChar(258,collation);
            case "DATENAME":
                return SqlTypeRules.NVarChar(30,collation);
            case "FORMAT":
                return SqlTypeRules.NVarChar(4000,collation);
            case "USER_NAME" or "CURRENT_USER" or "SESSION_USER" or "USER" or "ORIGINAL_LOGIN" or "SUSER_SNAME" or "SYSTEM_USER":
                return SqlTypeRules.NVarChar(128,collation);
            case "CONCAT" or "CONCAT_WS":{
                if(arguments.Any(static argument=>SqlTypeRules.IsUnicode(argument.Type))){
                    return SqlTypeRules.NVarChar(-1,collation);
                }
                return SqlTypeRules.VarChar(-1,collation);
            }
            case "REPLACE" or "REPLICATE" or "STUFF":
                return (firstString??SqlTypeRules.VarChar(-1,collation)) with{Length=-1};
            case "DATEADD":{
                var target=arguments[^1].Type;
                if(SqlTypeRules.IsTemporal(target)||target.Name=="datetimeoffset"){
                    return target;
                }
                return SqlTypeRules.DateTime;
            }
            case "GETDATE" or "GETUTCDATE" or "CURRENT_TIMESTAMP" or "DATETIMEFROMPARTS":
                return SqlTypeRules.DateTime;
            case "SYSDATETIME" or "SYSUTCDATETIME":
                return SqlTypeRules.DateTime2;
            case "EOMONTH" or "DATEFROMPARTS":
                return SqlTypeRules.Date;
            case "ABS" or "CEILING" or "FLOOR" or "ROUND" or "SIGN" or "POWER" or "RADIANS" or "DEGREES":{
                var type=arguments[0].Type;
                if(SqlTypeRules.IsDecimal(type)&&method.ReturnType==typeof(decimal?)){
                    return type;
                }
                break;
            }
        }
        if(method.ReturnType==typeof(string)&&firstString is not null){
            return firstString;
        }
        var returnType=method.ReturnType;
        if(returnType==typeof(int?)||returnType==typeof(int)){
            return SqlTypeRules.Int;
        }
        if(returnType==typeof(long?)){
            return SqlTypeRules.BigInt;
        }
        if(returnType==typeof(double?)||returnType==typeof(double)){
            return SqlTypeRules.Float;
        }
        if(returnType==typeof(decimal?)){
            return SqlTypeRules.Decimal(38,arguments.Select(static argument=>(int)(argument.Type.Scale??0)).DefaultIfEmpty(0).Max());
        }
        if(returnType==typeof(string)){
            return SqlTypeRules.NVarChar(-1,collation);
        }
        if(returnType==typeof(bool?)){
            return SqlTypeRules.Bit;
        }
        if(returnType==typeof(byte[])){
            return new("varbinary",-1);
        }
        if(returnType==typeof(Guid)||returnType==typeof(Guid?)){
            return SqlTypeRules.UniqueIdentifier;
        }
        if(returnType==typeof(DateTime?)||returnType==typeof(DateTime)){
            return SqlTypeRules.DateTime2;
        }
        if(returnType==typeof(DateTimeOffset)||returnType==typeof(DateTimeOffset?)){
            return new("datetimeoffset",null,null,7);
        }
        throw Unsupported(at,"SQLT099",$"{name} の結果の型({returnType.Name})");
    }
    internal static string Text(TSqlFragment fragment){
        var builder=new System.Text.StringBuilder();
        for(var index=fragment.FirstTokenIndex;index<=fragment.LastTokenIndex;index++){
            builder.Append(fragment.ScriptTokenStream[index].Text);
        }
        return builder.ToString();
    }
    // ---- 集約 ----
    private BoundExpression BindAggregate(string name,FunctionCall call,BoundScope scope){
        var distinct=call.UniqueRowFilter==UniqueRowFilter.Distinct;
        if(name is "COUNT" or "COUNT_BIG"){
            var countType=SqlTypeRules.Int;
            if(name=="COUNT_BIG"){
                countType=SqlTypeRules.BigInt;
            }
            if(call.Parameters.Count==1&&call.Parameters[0] is ColumnReferenceExpression{ColumnType:ColumnType.Wildcard}){
                return new BoundAggregate("COUNT",null,false,null,countType);
            }
            return new BoundAggregate("COUNT",this.BindScalar(call.Parameters[0],scope,false),distinct,null,countType);
        }
        if(name=="STRING_AGG"){
            if(distinct){
                throw Unsupported(call,"SQLT094","STRING_AGG の DISTINCT");
            }
            var value=this.BindScalar(call.Parameters[0],scope,false);
            var separator=this.BindScalar(call.Parameters[1],scope,false);
            var name2="varchar";
            if(SqlTypeRules.IsUnicode(value.Type)){
                name2="nvarchar";
            }
            var text=new SqlTypeDescriptor(name2,-1,null,null,value.Type.Collation??this.catalog.DatabaseCollation);
            return new BoundAggregate("STRING_AGG",this.Convert(value,text,call,0,true),false,this.Convert(separator,text,call,0,true),text);
        }
        var argument=this.BindScalar(call.Parameters[0],scope,false);
        var type=argument.Type;
        if(name is "MIN" or "MAX"){
            if(type.Name=="bit"){
                throw Unsupported(call,"SQLT100",$"bit の {name}(本物ではエラー 8117)");
            }
            return new BoundAggregate(name,argument,distinct,null,type);
        }
        if(!SqlTypeRules.IsNumeric(type)){
            throw Unsupported(call,"SQLT100",$"{type.Name} の {name}(本物ではエラー 8117)");
        }
        SqlTypeDescriptor result;
        switch(type.Name){
            case "tinyint" or "smallint" or "int":
                result=SqlTypeRules.Int;
                break;
            case "bigint":
                result=SqlTypeRules.BigInt;
                break;
            case "money" or "smallmoney":
                result=new("money",null,19,4);
                break;
            case "float" or "real":
                result=SqlTypeRules.Float;
                break;
            default:{
                int scale=type.Scale??0;
                if(name=="AVG"){
                    scale=Math.Max(scale,6);
                }
                result=SqlTypeRules.Decimal(38,scale);
                break;
            }
        }
        var operand=argument;
        if(!SqlTypeRules.IsDecimal(result)){
            operand=this.Convert(argument,result,call);
        }
        return new BoundAggregate(name,operand,distinct,null,result);
    }
    // ---- 変換 ----
    /// <summary>式を型へそろえる。同じ型ならそのまま。文字列どうしでは元の照合順序を残す。</summary>
    internal BoundExpression Convert(BoundExpression expression,SqlTypeDescriptor target,TSqlFragment at,int style=0,bool explicitConversion=false){
        if(SqlTypeRules.IsNull(target)){
            return expression;
        }
        var source=expression.Type;
        if(SqlTypeRules.IsString(source)&&SqlTypeRules.IsString(target)&&source.Collation is not null){
            target=target with{Collation=source.Collation};
        }
        if(source==target){
            return expression;
        }
        if(!SqlTypeRules.IsNull(source)&&!CanConvert(source,target)){
            throw Unsupported(at,"SQLT110",$"{source} から {target} への変換");
        }
        if(!explicitConversion&&!SqlTypeRules.IsNull(source)&&SqlTypeRules.ClrType(source)==SqlTypeRules.ClrType(target)&&!SqlTypeRules.IsTemporal(target)&&target.Name is not("char" or "nchar")){
            // C# の型が同じで値が変わらない暗黙の変換(varchar→nvarchar、int の別名など)は、型だけを付け替える。
            if(!SqlTypeRules.IsDecimal(target)||SqlTypeRules.IsDecimal(source)&&source.Scale==target.Scale){
                return new BoundConvert(expression,target,-1);
            }
        }
        return new BoundConvert(expression,target,style);
    }
    private static bool CanConvert(SqlTypeDescriptor source,SqlTypeDescriptor target){
        if(SqlTypeRules.IsString(target)){
            return SqlTypeRules.IsString(source)||SqlTypeRules.IsNumeric(source)||source.Name is "bit" or "uniqueidentifier"||SqlTypeRules.IsTemporal(source);
        }
        if(SqlTypeRules.IsNumeric(target)||target.Name=="bit"){
            return SqlTypeRules.IsNumeric(source)||source.Name=="bit"||SqlTypeRules.IsString(source);
        }
        if(SqlTypeRules.IsTemporal(target)){
            return SqlTypeRules.IsTemporal(source)||SqlTypeRules.IsString(source);
        }
        if(target.Name=="uniqueidentifier"){
            return source.Name=="uniqueidentifier"||SqlTypeRules.IsString(source);
        }
        if(SqlTypeRules.IsBinary(target)){
            return SqlTypeRules.IsBinary(source)||SqlTypeRules.IsString(source);
        }
        return source.Name==target.Name;
    }
}
