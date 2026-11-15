using System.Globalization;
using System.Text;
namespace Seamless.Database.SqlTranslation;
/// <summary>
/// 束縛済みの中間表現から C# の式を作る。集合の演算は Seamless.Linq.Set(ISet を受けて ISet を返す)だけを使い、
/// 集約や副問い合わせの中の列挙は System.Linq.Enumerable を静的に呼ぶ(ISet の Select の重複除去を通さないため)。
/// 値の演算は関数の部品(SqlCompare・SqlArithmetic など)を呼び、SQL の NULL と三値論理に合わせる。
/// </summary>
internal sealed class CSharpEmitter{
    private const string F="global::Seamless.Database.SqlServer.Functions";
    private const string Enumerable="global::System.Linq.Enumerable";
    private int counter;
    /// <summary>範囲ごとの、いまの行の変数と、その変数が表す FROM の部分の道のり。Groups は集約する行の集まりの式。</summary>
    private sealed record RowContext(string Variable,List<PathSegment> Prefix);
    private sealed class Context{
        public Dictionary<BoundScope,RowContext> Rows{get;}=[];
        public Dictionary<BoundScope,string> Groups{get;}=[];
        public Context With(BoundScope scope,RowContext row){
            var copy=this.Copy();
            copy.Rows[scope]=row;
            return copy;
        }
        public Context WithGroup(BoundScope scope,string group,RowContext row){
            var copy=this.With(scope,row);
            copy.Groups[scope]=group;
            return copy;
        }
        private Context Copy(){
            var copy=new Context();
            foreach(var pair in this.Rows){
                copy.Rows[pair.Key]=pair.Value;
            }
            foreach(var pair in this.Groups){
                copy.Groups[pair.Key]=pair.Value;
            }
            return copy;
        }
    }
    private string Variable(string prefix)=>prefix+(++this.counter).ToString(CultureInfo.InvariantCulture);
    // ---- 問い合わせ ----
    /// <summary>問い合わせを、行を rowType の値にした ISet の式にする。</summary>
    public string Query(BoundQuery query,string rowType,IReadOnlyList<SqlTypeDescriptor> columnTypes){
        return this.Query(query,rowType,columnTypes,new Context());
    }
    private string Query(BoundQuery query,string rowType,IReadOnlyList<SqlTypeDescriptor> columnTypes,Context context){
        switch(query){
            case BoundSetQuery set:{
                var left=this.Query(set.Left,rowType,columnTypes,context);
                var right=this.Query(set.Right,rowType,columnTypes,context);
                // UNION ALL も集合として重複を除く(ビューは ISet を返す。利用者の決定 2026-11-15)。
                return $"{left}.{set.Operation}({right})";
            }
            case BoundSelect select:
                return this.Select(select,context,(row,inner)=>{
                    var columns=select.SelectItems.Select((item,index)=>Output(this.Expression(item.Expression,inner),columnTypes[index]));
                    return $"new {rowType}({string.Join(",",columns)})";
                });
            default:
                throw new InvalidOperationException(query.GetType().Name);
        }
    }
    // FROM・WHERE・GROUP BY・HAVING を ISet の演算に、行の作り方を project にする。
    private string Select(BoundSelect select,Context context,Func<string,Context,string> project){
        var source=this.Filtered(select,context,out var x);
        if(select.GroupBy.Count==0){
            if(select.HasAggregates){
                // GROUP BY の無い集約は、表が空でも1行を返す。1つの要素の集合から、絞り込んだ行の全体を集約する。
                var unit=this.Variable("u");
                var whole=context.WithGroup(select.Scope,source,new(unit,[]));
                var start=$"global::Seamless.Linq.Set.Of(0)";
                if(select.Having is not null){
                    start=$"{start}.Where({unit}=>({this.Expression(select.Having,whole)})==true)";
                }
                return $"{start}.Select({unit}=>{project(unit,whole)})";
            }
            var inner=context.With(select.Scope,new(x,[]));
            return $"{source}.Select({x}=>{project(x,inner)})";
        }
        // GROUP BY: キーの集合を作り、キーごとに元の行を集め直す。文字列のキーは照合順序で等しいものを同じにする。
        var scope=select.Scope;
        var key=this.Key(select,context.With(scope,new(x,[])));
        var k=this.Variable("k");
        var g=this.Variable("g");
        var first=this.Variable("f");
        var memberSource=this.Filtered(select,context,out var member);
        var members=$"{memberSource}.Where({member}=>global::System.Object.Equals({this.Key(select,context.With(scope,new(member,[])))},{k}))";
        var groupContext=context.WithGroup(scope,g,new(first,[]));
        var body=new StringBuilder();
        body.Append($"{source}.Select({x}=>{key})");
        if(select.Having is not null){
            body.Append($".Where({k}=>{{var {g}={members};var {first}={Enumerable}.First({g});return ({this.Expression(select.Having,groupContext)})==true;}})");
        }
        body.Append($".Select({k}=>{{var {g}={members};var {first}={Enumerable}.First({g});return {project(first,groupContext)};}})");
        return body.ToString();
    }
    private string Filtered(BoundSelect select,Context context,out string variable){
        var from=this.From(select.From,context,select.Scope);
        variable=this.Variable("x");
        if(select.Where is null){
            return from;
        }
        return $"{from}.Where({variable}=>({this.Expression(select.Where,context.With(select.Scope,new(variable,[])))})==true)";
    }
    private string Key(BoundSelect select,Context context){
        var parts=select.GroupBy.Select(expression=>{
            var code=this.Expression(expression,context);
            if(SqlTypeRules.IsString(expression.Type)){
                return $"{Collation(expression.Type.Collation)}.GroupKey({code})";
            }
            return code;
        }).ToList();
        if(parts.Count==1){
            return parts[0];
        }
        return $"({string.Join(",",parts)})";
    }
    // ---- FROM ----
    private string From(BoundFrom from,Context context,BoundScope scope){
        switch(from){
            case BoundTableFrom table:
                return "q."+table.Row.Relation.Access;
            case BoundJoinFrom join:{
                var left=this.From(join.Left,context,scope);
                var right=this.From(join.Right,context,scope);
                var l=this.Variable("l");
                var r=this.Variable("r");
                var j=this.Variable("j");
                if(join.Kind==JoinKind.Left){
                    var tuple=$"({join.Left.ElementType},{join.Right.ElementType}?)";
                    if(this.HashLeftJoin(join,left,right,tuple,context,scope,l,r) is{} hashed){
                        return hashed;
                    }
                    // = で結べない条件(左右にまたがる不等式など)は、左の行ごとに右を全部見る。
                    var condition="true";
                    if(join.Condition is not null){
                        condition=$"({this.Expression(join.Condition,context.With(scope,new(j,join.Prefix)))})==true";
                    }
                    return $"{left}.SelectMany({l}=>{Enumerable}.DefaultIfEmpty({right}.Select({r}=>({tuple})({l},{r})).Where({j}=>{condition}),({tuple})({l},null)))";
                }
                if(join.Condition is null){
                    return $"{left}.SelectMany({l}=>{right}.Select({r}=>({l},{r})))";
                }
                // 左右の表を = で結ぶ条件は、Set.Join(右側をキーで引く)のキーにする。残りの条件は結んだあとに絞る。
                var leftRows=SqlBinder.RowsUnder(join.Left);
                var rightRows=SqlBinder.RowsUnder(join.Right);
                var leftKeys=new List<string>();
                var rightKeys=new List<string>();
                var residual=new List<BoundExpression>();
                foreach(var conjunct in SqlBinder.Conjuncts(join.Condition)){
                    if(conjunct is BoundComparison{Operation:"Equal"} equal&&this.EquiKeys(equal,scope,leftRows,rightRows,join,context,l,r) is{} keys){
                        leftKeys.Add(keys.Left);
                        rightKeys.Add(keys.Right);
                    }else{
                        residual.Add(conjunct);
                    }
                }
                string joined;
                if(leftKeys.Count==0){
                    joined=$"{left}.SelectMany({l}=>{right}.Select({r}=>({l},{r})))";
                }else{
                    var leftKey=leftKeys[0];
                    var rightKey=rightKeys[0];
                    if(leftKeys.Count>1){
                        leftKey=$"{F}.SqlJoinKey.Of({string.Join(",",leftKeys)})";
                        rightKey=$"{F}.SqlJoinKey.Of({string.Join(",",rightKeys)})";
                    }
                    var a=this.Variable("l");
                    var b=this.Variable("r");
                    joined=$"{left}.Join({right},{l}=>{leftKey},{r}=>{rightKey},({a},{b})=>({a},{b}))";
                }
                if(residual.Count==0){
                    return joined;
                }
                var rest=residual.Aggregate(static (x,y)=>new BoundLogical('&',x,y));
                return $"{joined}.Where({j}=>({this.Expression(rest,context.With(scope,new(j,join.Prefix)))})==true)";
            }
            default:
                throw new InvalidOperationException(from.GetType().Name);
        }
    }
    /// <summary>
    /// 左外部結合を Set.LeftJoin(右側をキーで引く)で出す。ON の条件は、左右を = で結ぶもの(キー)と、右の表だけを見るもの(先に右を絞る)に分けられるときだけ使う。
    /// それ以外の条件を含むときは null を返す(呼び出し側が総当たりで出す)。
    /// </summary>
    private string? HashLeftJoin(BoundJoinFrom join,string left,string right,string tuple,Context context,BoundScope scope,string l,string r){
        if(join.Condition is null){
            return null;
        }
        var leftRows=SqlBinder.RowsUnder(join.Left);
        var rightRows=SqlBinder.RowsUnder(join.Right);
        var leftKeys=new List<string>();
        var rightKeys=new List<string>();
        var rightOnly=new List<BoundExpression>();
        foreach(var conjunct in SqlBinder.Conjuncts(join.Condition)){
            if(conjunct is BoundComparison{Operation:"Equal"} equal&&this.EquiKeys(equal,scope,leftRows,rightRows,join,context,l,$"{r}.Value") is{} keys){
                leftKeys.Add(keys.Left);
                rightKeys.Add(keys.Right);
                continue;
            }
            var rows=SqlBinder.RowsOf(conjunct,scope);
            if(rows.IsSubsetOf(rightRows)&&!ContainsSubquery(conjunct)){
                rightOnly.Add(conjunct);
                continue;
            }
            return null;
        }
        if(leftKeys.Count==0){
            return null;
        }
        var source=right;
        if(rightOnly.Count>0){
            var v=this.Variable("r");
            var predicate=rightOnly.Aggregate(static (x,y)=>new BoundLogical('&',x,y));
            source=$"{right}.Where({v}=>({this.Expression(predicate,context.With(scope,new(v,join.Right.Prefix)))})==true)";
        }
        var leftKey=leftKeys[0];
        var rightKey=rightKeys[0];
        if(leftKeys.Count>1){
            leftKey=$"{F}.SqlJoinKey.Of({string.Join(",",leftKeys)})";
            rightKey=$"{F}.SqlJoinKey.Of({string.Join(",",rightKeys)})";
        }
        // 右の行は Nullable にして渡す(対応の無い左の行で、右が既定値の構造体ではなく null になるように)。
        var n=this.Variable("r");
        var a=this.Variable("l");
        var b=this.Variable("r");
        return $"{left}.LeftJoin({source}.Select({n}=>({join.Right.ElementType}?){n}),{l}=>{leftKey},{r}=>{rightKey},({a},{b})=>({tuple})({a},{b}))";
    }
    private static bool ContainsSubquery(BoundExpression expression){
        var found=false;
        SqlBinder.Visit(expression,node=>{
            if(node is BoundExists or BoundInSubquery or BoundScalarSubquery){
                found=true;
            }
        });
        return found;
    }
    // = の片側が左の表だけ、もう片側が右の表だけを参照していれば、両側のキーの式を返す。文字列は照合順序で等しいものを同じキーにする。
    private (string Left,string Right)? EquiKeys(BoundComparison equal,BoundScope scope,HashSet<RowBinding> leftRows,HashSet<RowBinding> rightRows,BoundJoinFrom join,Context context,string l,string r){
        var a=SqlBinder.RowsOf(equal.Left,scope);
        var b=SqlBinder.RowsOf(equal.Right,scope);
        BoundExpression leftSide;
        BoundExpression rightSide;
        if(a.Count>0&&b.Count>0&&a.IsSubsetOf(leftRows)&&b.IsSubsetOf(rightRows)){
            leftSide=equal.Left;
            rightSide=equal.Right;
        }else if(a.Count>0&&b.Count>0&&a.IsSubsetOf(rightRows)&&b.IsSubsetOf(leftRows)){
            leftSide=equal.Right;
            rightSide=equal.Left;
        }else{
            return null;
        }
        if(SqlTypeRules.IsBinary(leftSide.Type)){
            return null;
        }
        var leftKey=this.Expression(leftSide,context.With(scope,new(l,join.Left.Prefix)));
        var rightKey=this.Expression(rightSide,context.With(scope,new(r,join.Right.Prefix)));
        if(SqlTypeRules.IsString(leftSide.Type)){
            leftKey=$"{Collation(equal.Collation)}.GroupKey({leftKey})";
            rightKey=$"{Collation(equal.Collation)}.GroupKey({rightKey})";
        }
        return (leftKey,rightKey);
    }
    // ---- 式 ----
    private string Expression(BoundExpression expression,Context context){
        switch(expression){
            case BoundColumn column:
                return this.Column(column,context);
            case BoundLiteral literal:
                return literal.Code;
            case BoundConvert convert:
                return this.Convert(convert,context);
            case BoundArithmetic arithmetic:{
                var name=arithmetic.Operation switch{
                    '+'=>"Add",
                    '-'=>"Subtract",
                    '*'=>"Multiply",
                    '/'=>"Divide",
                    _=>"Modulo"
                };
                var code=$"{F}.SqlArithmetic.{name}({this.Expression(arithmetic.Left,context)},{this.Expression(arithmetic.Right,context)})";
                if(arithmetic.Operation=='/'&&arithmetic.Type.Name is "money" or "smallmoney"){
                    var (precision,scale)=SqlTypeRules.Shape(arithmetic.Type);
                    return $"{F}.SqlArithmetic.FitTruncated({code},{precision},{scale})";
                }
                return FitDecimal(code,arithmetic.Type);
            }
            case BoundConcat concat:
                return $"{F}.SqlArithmetic.Concat({this.Expression(concat.Left,context)},{this.Expression(concat.Right,context)})";
            case BoundNegate negate:
                return FitDecimal($"{F}.SqlArithmetic.Negate({this.Expression(negate.Operand,context)})",negate.Type);
            case BoundComparison comparison:
                return $"{F}.SqlCompare.{comparison.Operation}({this.CompareCall(comparison.Left,comparison.Right,comparison.Collation,context)})";
            case BoundLogical logical:
                return $"({this.Expression(logical.Left,context)}{logical.Operation}{this.Expression(logical.Right,context)})";
            case BoundNot not:
                return $"(!{this.Expression(not.Operand,context)})";
            case BoundIsNull isNull:{
                var test=$"(({this.Expression(isNull.Operand,context)}) is null)";
                if(isNull.Negated){
                    return $"(!{test})";
                }
                return test;
            }
            case BoundLike like:{
                var escape="null";
                if(like.Escape is{} character){
                    escape=$"(char?){CharLiteral(character[0])}";
                }
                var code=$"{F}.SqlLike.IsMatch({this.Expression(like.Value,context)},{this.Expression(like.Pattern,context)},{Collation(like.Collation)},{escape})";
                if(like.Negated){
                    return $"(!{code})";
                }
                return code;
            }
            case BoundIn @in:{
                var items=@in.Items.Select(item=>$"{F}.SqlCompare.Equal({this.CompareCall(@in.Value,item,@in.Collation,context)})");
                var code=$"{F}.SqlCompare.In(new bool?[]{{{string.Join(",",items)}}})";
                if(@in.Negated){
                    return $"(!{code})";
                }
                return code;
            }
            case BoundInSubquery inSubquery:{
                var source=this.Filtered(inSubquery.Query,context,out var y);
                var inner=context.With(inSubquery.Query.Scope,new(y,[]));
                var item=inSubquery.Query.SelectItems[0].Expression;
                var code=$"{F}.SqlCompare.In({Enumerable}.Select({source},{y}=>{F}.SqlCompare.Equal({this.CompareCall(inSubquery.Value,item,inSubquery.Collation,context,inner)})))";
                if(inSubquery.Negated){
                    return $"(!{code})";
                }
                return code;
            }
            case BoundExists exists:{
                var source=this.Filtered(exists.Query,context,out _);
                var code=$"{Enumerable}.Any({source})";
                if(exists.Negated){
                    return $"(!{code})";
                }
                return code;
            }
            case BoundScalarSubquery scalar:{
                var query=scalar.Query;
                var item=query.SelectItems[0].Expression;
                var source=this.Filtered(query,context,out var y);
                if(query.HasAggregates){
                    // 集約だけの副問い合わせは、絞り込んだ行の全体を1つの組として集約する(行が無くても1行になる)。
                    var inner=context.WithGroup(query.Scope,source,new(y,[]));
                    return this.Expression(item,inner);
                }
                var rowContext=context.With(query.Scope,new(y,[]));
                var type=SqlTypeRules.ClrType(item.Type);
                return $"{F}.SqlSubquery.Scalar<{type}>({Enumerable}.Select({source},{y}=>{this.Expression(item,rowContext)}))";
            }
            case BoundCase @case:{
                var type=SqlTypeRules.ClrType(@case.Type);
                var code=$"({type})null";
                if(@case.Else is not null){
                    code=this.Expression(@case.Else,context);
                }
                for(var index=@case.Branches.Count-1;index>=0;index--){
                    var (when,then)=@case.Branches[index];
                    code=$"(({this.Expression(when,context)})==true?({type})({this.Expression(then,context)}):({type})({code}))";
                }
                return code;
            }
            case BoundCoalesce coalesce:
                return "("+string.Join("??",coalesce.Items.Select(item=>this.Expression(item,context)))+")";
            case BoundNullIf nullIf:{
                var left=this.Expression(nullIf.Left,context);
                var type=SqlTypeRules.ClrType(nullIf.Type);
                return $"({F}.SqlCompare.Equal({this.CompareCall(nullIf.Left,nullIf.Right,nullIf.Collation,context)})==true?({type})null:{left})";
            }
            case BoundCall call:{
                var arguments=call.Arguments.Select(argument=>{
                    if(argument.Code is not null){
                        return argument.Code;
                    }
                    var code=this.Expression(argument.Expression!,context);
                    if(argument.Cast is not null){
                        return $"({argument.Cast})({code})";
                    }
                    return code;
                });
                var result=$"{call.Method}({string.Join(",",arguments)})";
                return Normalize($"({SqlTypeRules.ClrType(call.Type)})({result})",call.Type);
            }
            case BoundAggregate aggregate:
                return this.Aggregate(aggregate,context);
            default:
                throw new InvalidOperationException(expression.GetType().Name);
        }
    }
    private string CompareCall(BoundExpression left,BoundExpression right,string? collation,Context context,Context? rightContext=null){
        var a=this.Expression(left,context);
        var b=this.Expression(right,rightContext??context);
        if(SqlTypeRules.IsString(left.Type)){
            return $"{F}.SqlCompare.Compare({a},{b},{Collation(collation)})";
        }
        return $"{F}.SqlCompare.Compare({a},{b})";
    }
    private string Column(BoundColumn column,Context context){
        if(!context.Rows.TryGetValue(column.Row.Scope,out var row)){
            throw new InvalidOperationException($"列 {column.Column.Name} の範囲の行が無い。");
        }
        var path=column.Row.Path;
        if(path.Count<row.Prefix.Count){
            throw new InvalidOperationException($"列 {column.Column.Name} が、いまの FROM の部分の外にある。");
        }
        var code=new StringBuilder(row.Variable);
        var nullable=false;
        for(var index=row.Prefix.Count;index<path.Count;index++){
            code.Append(Access(nullable)).Append(path[index].Member);
            nullable=path[index].Nullable;
        }
        code.Append(Access(nullable)).Append(column.Column.CSharpName);
        var value=code.ToString();
        return column.Column.Type.Name switch{
            "date"=>$"{F}.SqlTemporal.FromDateOnly({value})",
            "real"=>$"(float?)({value})",
            "char" or "varchar" or "text" or "nchar" or "nvarchar" or "ntext" or "xml" or "binary" or "varbinary" or "image" or "timestamp" or "rowversion"=>value,
            _=>$"({SqlTypeRules.ClrType(column.Column.Type)})({value})"
        };
    }
    private static string Access(bool nullable){
        if(nullable){
            return "?.";
        }
        return ".";
    }
    // ---- 集約 ----
    private string Aggregate(BoundAggregate aggregate,Context context){
        var scope=context.Groups.Keys.Last();
        if(aggregate.Argument is not null){
            scope=FindScope(aggregate.Argument,context);
        }
        if(!context.Groups.TryGetValue(scope,out var group)){
            throw new InvalidOperationException("集約の組が無い。");
        }
        if(aggregate.Argument is null){
            if(aggregate.Type.Name=="bigint"){
                return $"(long?){Enumerable}.LongCount({group})";
            }
            return $"(int?){Enumerable}.Count({group})";
        }
        var y=this.Variable("y");
        var inner=context.With(scope,new(y,[]));
        var value=this.Expression(aggregate.Argument,inner);
        var collation=aggregate.Argument.Type.Collation;
        var values=$"{Enumerable}.Select({group},{y}=>{value})";
        if(aggregate.Distinct){
            if(SqlTypeRules.IsString(aggregate.Argument.Type)&&aggregate.Kind=="COUNT"){
                values=$"{Enumerable}.Select({group},{y}=>{Collation(collation)}.GroupKey({value}))";
            }
            values=$"{Enumerable}.Distinct({values})";
        }
        switch(aggregate.Kind){
            case "COUNT":{
                var count=$"{F}.AggregateFunctions.Count({values})";
                if(aggregate.Type.Name=="bigint"){
                    return $"(long?){count}";
                }
                return $"(int?){count}";
            }
            case "SUM":
                return FitDecimal($"{F}.AggregateFunctions.Sum({values})",aggregate.Type);
            case "AVG":{
                if(SqlTypeRules.IsDecimal(aggregate.Argument.Type)){
                    var scale=aggregate.Argument.Type.Scale??SqlTypeRules.Shape(aggregate.Argument.Type).Scale;
                    return FitDecimal($"{F}.AggregateFunctions.Avg({values},(byte){scale})",aggregate.Type);
                }
                return $"{F}.AggregateFunctions.Avg({values})";
            }
            case "MIN" or "MAX":{
                var method="Max";
                if(aggregate.Kind=="MIN"){
                    method="Min";
                }
                if(SqlTypeRules.IsString(aggregate.Argument.Type)){
                    return $"{F}.AggregateFunctions.{method}({values},{Collation(collation)})";
                }
                if(SqlTypeRules.IsBinary(aggregate.Argument.Type)){
                    throw new TranslationUnsupportedException("SQLT121",$"バイト列の {aggregate.Kind}",0,0);
                }
                return $"{F}.AggregateFunctions.{method}({values})";
            }
            case "STRING_AGG":
                return $"{F}.AggregateFunctions.StringAgg({values},{this.Expression(aggregate.Separator!,context)})";
            default:
                throw new InvalidOperationException(aggregate.Kind);
        }
    }
    // 集約の引数が参照する範囲(その中で一番内側のもの)。
    private static BoundScope FindScope(BoundExpression expression,Context context){
        var scopes=new List<BoundScope>();
        Collect(expression,scopes);
        foreach(var scope in context.Groups.Keys.Reverse()){
            if(scopes.Count==0||scopes.Contains(scope)){
                return scope;
            }
        }
        return context.Groups.Keys.Last();
    }
    private static void Collect(BoundExpression expression,List<BoundScope> scopes){
        switch(expression){
            case BoundColumn column:
                scopes.Add(column.Row.Scope);
                break;
            case BoundConvert convert:
                Collect(convert.Operand,scopes);
                break;
            case BoundArithmetic arithmetic:
                Collect(arithmetic.Left,scopes);
                Collect(arithmetic.Right,scopes);
                break;
            case BoundConcat concat:
                Collect(concat.Left,scopes);
                Collect(concat.Right,scopes);
                break;
            case BoundNegate negate:
                Collect(negate.Operand,scopes);
                break;
            case BoundCall call:
                foreach(var argument in call.Arguments){
                    if(argument.Expression is not null){
                        Collect(argument.Expression,scopes);
                    }
                }
                break;
            case BoundCoalesce coalesce:
                foreach(var item in coalesce.Items){
                    Collect(item,scopes);
                }
                break;
            case BoundCase @case:
                foreach(var (when,then) in @case.Branches){
                    Collect(when,scopes);
                    Collect(then,scopes);
                }
                if(@case.Else is not null){
                    Collect(@case.Else,scopes);
                }
                break;
            case BoundComparison comparison:
                Collect(comparison.Left,scopes);
                Collect(comparison.Right,scopes);
                break;
            case BoundLogical logical:
                Collect(logical.Left,scopes);
                Collect(logical.Right,scopes);
                break;
            case BoundNot not:
                Collect(not.Operand,scopes);
                break;
            case BoundIsNull isNull:
                Collect(isNull.Operand,scopes);
                break;
            case BoundNullIf nullIf:
                Collect(nullIf.Left,scopes);
                Collect(nullIf.Right,scopes);
                break;
        }
    }
    // ---- 変換 ----
    private string Convert(BoundConvert convert,Context context){
        var source=convert.Operand.Type;
        var target=convert.Target;
        var targetClr=SqlTypeRules.ClrType(target);
        if(SqlTypeRules.IsNull(source)){
            return $"({targetClr})null";
        }
        var value=this.Expression(convert.Operand,context);
        if(convert.Style==-1){
            return value;
        }
        var style=convert.Style;
        if(SqlTypeRules.IsString(target)){
            string text;
            if(SqlTypeRules.IsString(source)){
                text=value;
            }else if(SqlTypeRules.IsInteger(source)){
                return Fit($"{F}.ConversionFunctions.IntegerToString((long?)({value}),{target.Length??-1})",target,false);
            }else if(source.Name=="bit"){
                text=$"{F}.ConversionFunctions.BitToString({value})";
            }else if(source.Name is "money" or "smallmoney"){
                text=$"{F}.ConversionFunctions.MoneyToString({value},{style})";
            }else if(source.Name=="decimal"){
                text=$"{F}.ConversionFunctions.DecimalToString({value},{source.Scale??0})";
            }else if(source.Name is "float" or "real"){
                text=$"{F}.ConversionFunctions.FloatToString({value},{style})";
            }else if(source.Name=="uniqueidentifier"){
                text=$"{F}.ConversionFunctions.GuidToString({value})";
            }else if(SqlTypeRules.IsTemporal(source)){
                text=$"{F}.ConversionFunctions.DateTimeToString({value},{F}.SqlTemporalType.{TemporalType(source)},{style},{source.Scale??7})";
            }else{
                throw new TranslationUnsupportedException("SQLT110",$"{source} から {target} への変換",0,0);
            }
            return Fit(text,target,true);
        }
        if(SqlTypeRules.IsInteger(target)){
            var (minimum,maximum)=target.Name switch{
                "tinyint"=>("0","255"),
                "smallint"=>("-32768","32767"),
                "int"=>("-2147483648","2147483647"),
                _=>("-9223372036854775808","9223372036854775807")
            };
            if(SqlTypeRules.IsInteger(source)){
                if(SqlTypeRules.Precedence(source)<=SqlTypeRules.Precedence(target)){
                    return $"({targetClr})({value})";
                }
                return $"({targetClr}){F}.ConversionFunctions.ToInteger((decimal?)({value}),{minimum},{maximum},\"{target.Name}\")";
            }
            if(SqlTypeRules.IsDecimal(source)){
                return $"({targetClr}){F}.ConversionFunctions.ToInteger({value},{minimum},{maximum},\"{target.Name}\")";
            }
            if(source.Name is "float" or "real"){
                return $"({targetClr}){F}.ConversionFunctions.ToInteger({value},{minimum},{maximum},\"{target.Name}\")";
            }
            if(source.Name=="bit"){
                return $"({targetClr}){F}.ConversionFunctions.BitToInteger({value})";
            }
            if(SqlTypeRules.IsString(source)){
                if(target.Name=="int"){
                    return $"{F}.ConversionFunctions.StringToInt32({value})";
                }
                return $"({targetClr}){F}.ConversionFunctions.ToInteger((decimal?){F}.ConversionFunctions.StringToInt64({value}),{minimum},{maximum},\"{target.Name}\")";
            }
        }
        if(SqlTypeRules.IsDecimal(target)){
            var (precision,scale)=SqlTypeRules.Shape(target);
            string number;
            if(SqlTypeRules.IsExactNumeric(source)){
                number=$"(decimal?)({value})";
            }else if(source.Name is "float" or "real"){
                number=$"{F}.ConversionFunctions.ToDecimal({value})";
            }else if(source.Name=="bit"){
                number=$"(decimal?){F}.ConversionFunctions.BitToInteger({value})";
            }else if(SqlTypeRules.IsString(source)){
                number=$"{F}.ConversionFunctions.StringToDecimal({value},{scale})";
            }else{
                throw new TranslationUnsupportedException("SQLT110",$"{source} から {target} への変換",0,0);
            }
            return $"{F}.SqlArithmetic.Fit({number},{precision},{scale})";
        }
        if(target.Name is "float" or "real"){
            var cast="(double?)";
            if(target.Name=="real"){
                cast="(float?)";
            }
            if(SqlTypeRules.IsNumeric(source)){
                return $"{cast}({value})";
            }
            if(source.Name=="bit"){
                return $"{cast}{F}.ConversionFunctions.BitToInteger({value})";
            }
            if(SqlTypeRules.IsString(source)){
                return $"{cast}{F}.ConversionFunctions.StringToDouble({value})";
            }
        }
        if(target.Name=="bit"){
            if(source.Name=="bit"){
                return value;
            }
            if(source.Name is "float" or "real"){
                return $"{F}.ConversionFunctions.ToBit({value})";
            }
            if(SqlTypeRules.IsNumeric(source)){
                return $"{F}.ConversionFunctions.ToBit((decimal?)({value}))";
            }
            if(SqlTypeRules.IsString(source)){
                return $"{F}.ConversionFunctions.StringToBit({value})";
            }
        }
        if(SqlTypeRules.IsTemporal(target)){
            if(SqlTypeRules.IsString(source)){
                return Normalize($"{F}.ConversionFunctions.StringToDateTime({F}.SqlSession.Default,{value},{style})",target);
            }
            if(SqlTypeRules.IsTemporal(source)){
                return Normalize(value,target);
            }
        }
        if(target.Name=="uniqueidentifier"){
            if(source.Name=="uniqueidentifier"){
                return value;
            }
            if(SqlTypeRules.IsString(source)){
                return $"{F}.ConversionFunctions.StringToGuid({value})";
            }
        }
        if(SqlTypeRules.IsBinary(target)){
            if(SqlTypeRules.IsBinary(source)){
                return value;
            }
            if(SqlTypeRules.IsString(source)){
                return $"{F}.ConversionFunctions.StringToBinary({value},{SqlTypeRules.IsUnicode(source).ToString().ToLowerInvariant()},{Collation(source.Collation)})";
            }
        }
        throw new TranslationUnsupportedException("SQLT110",$"{source} から {target} への変換",0,0);
    }
    // 文字列の型の長さへ。char・nchar は空白で埋める。
    private static string Fit(string text,SqlTypeDescriptor target,bool truncate){
        if(target.Name is "char" or "nchar"&&target.Length is{} fixedLength&&fixedLength>0){
            return $"{F}.ConversionFunctions.Fixed({text},{fixedLength})";
        }
        if(truncate&&target.Length is{} length&&length>=0){
            return $"{F}.ConversionFunctions.Truncate({text},{length})";
        }
        return text;
    }
    // 日付の型の精度へ丸める。
    private static string Normalize(string code,SqlTypeDescriptor type)=>type.Name switch{
        "datetime"=>$"{F}.SqlTemporal.ToDateTime({code})",
        "smalldatetime"=>$"{F}.SqlTemporal.ToSmallDateTime({code})",
        "datetime2"=>$"{F}.SqlTemporal.ToDateTime2({code},{type.Scale??7})",
        "date"=>$"{F}.SqlTemporal.ToDate({code})",
        _=>code
    };
    private static string FitDecimal(string code,SqlTypeDescriptor type){
        if(!SqlTypeRules.IsDecimal(type)){
            return code;
        }
        var (precision,scale)=SqlTypeRules.Shape(type);
        return $"{F}.SqlArithmetic.Fit({code},{precision},{scale})";
    }
    private static string TemporalType(SqlTypeDescriptor type)=>type.Name switch{
        "datetime"=>"DateTime",
        "smalldatetime"=>"SmallDateTime",
        "date"=>"Date",
        _=>"DateTime2"
    };
    internal static string Collation(string? name)=>$"{F}.SqlCollation.Get({CSharpNames.String(name??"SQL_Latin1_General_CP1_CI_AS")})";
    private static string CharLiteral(char value){
        if(value is '\''or '\\'){
            return "'\\"+value+"'";
        }
        return "'"+value+"'";
    }
    // ---- 出力 ----
    /// <summary>ビューの行の列の C# の型(NULL 許容)。date は DateOnly?、real は float?、time は TimeOnly?。</summary>
    public static string OutputType(SqlTypeDescriptor type)=>type.Name switch{
        "date"=>"global::System.DateOnly?",
        "real"=>"float?",
        _=>SqlTypeRules.ClrType(type)
    };
    private static string Output(string code,SqlTypeDescriptor type)=>type.Name switch{
        "date"=>$"{F}.SqlTemporal.ToDateOnly({code})",
        "real"=>$"(float?)({code})",
        _=>code
    };
}
