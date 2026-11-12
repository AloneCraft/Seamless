using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Tds;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Sql;

// 変数・制御文・一時表を含むバッチを、文ごとに順に実行する。
// 式の評価と普通の文の実行は共通エンジンに任せ、変数は引数として渡す。
// 扱う文: DECLARE、SET @変数、SELECT @変数=…、IF/ELSE、WHILE/BREAK/CONTINUE、BEGIN…END、BEGIN TRY/CATCH、THROW、RETURN、
//         CREATE TABLE #表、DROP TABLE #表、INSERT INTO #表、SELECT … INTO #表、EXEC(xp_instance_regread などの OUTPUT を含む)。
// それ以外の文は今まで通り共通エンジンで1文ずつ実行する。
internal sealed class SqlBatchInterpreter(SqlExecutionEngine engine,string sql,SessionContext session,IReadOnlyList<SqlParameter>? parameters,IReadOnlyDictionary<string,object?>? values){
    private readonly Dictionary<string,Variable> variables=new(StringComparer.OrdinalIgnoreCase);
    private readonly List<QueryResult> results=[];
    private Exception? caught;

    // このバッチをこの実行器で扱う必要があるか。無ければ従来の1文ずつの実行に任せる。
    internal static bool Needs(TSqlScript script){
        var visitor=new NeedVisitor();script.Accept(visitor);return visitor.Found;
    }

    internal IReadOnlyList<QueryResult> Run(TSqlScript script){
        try{
            foreach(var batch in script.Batches){this.Block(batch.Statements);}
        }catch(ReturnSignal){
        }catch(Exception error) when(error is not OperationCanceledException){
            // 先に出た結果はそのまま返し、失敗はその後に伝える(SQL Server と同じ順)。
            this.results.Add(QueryResult.Defer(()=>throw error));
        }
        if(session.TransactionState<0){
            session.Dispose();session.LastTransactionOperation=10;
            this.results.Add(QueryResult.Defer(()=>throw new SqlQueryException("An uncommittable transaction was detected at the end of the batch and has been rolled back.",3998)));
        }
        if(this.results.Count==0){this.results.Add(QueryResult.Empty);}
        return this.results;
    }

    private void Block(IEnumerable<TSqlStatement> statements){
        foreach(var statement in statements){
            session.CancellationToken.ThrowIfCancellationRequested();
            this.Statement(statement);
        }
    }

    private void Statement(TSqlStatement statement){
        switch(statement){
            case DeclareVariableStatement declare:
                foreach(var element in declare.Declarations){
                    var typeText=TypeText(element.DataType);
                    var parameter=ParameterDeclarations.Parse(element.VariableName.Value+" "+typeText).Single();
                    var variable=new Variable(parameter,typeText);
                    this.variables[parameter.Name]=variable;
                    if(element.Value is not null){variable.Value=this.Scalar(element.Value,variable);}
                }
                return;
            case DeclareTableVariableStatement:
                throw new SqlQueryException("Table variables are not supported.",40515);
            case SetVariableStatement set when set.Expression is not null&&set.Variable is not null:
                this.Assign(set.Variable,set.AssignmentKind,set.Expression);
                return;
            case SelectStatement select when select.QueryExpression is QuerySpecification query&&query.SelectElements.Count>0&&query.SelectElements.All(static e=>e is SelectSetVariable):
                this.AssignFromSelect(select,query);
                return;
            case SelectStatement into when into.Into is not null&&IsTemporary(into.Into):
                this.SelectInto(into);
                return;
            case IfStatement branch:
                if(this.Condition(branch.Predicate)){this.Statement(branch.ThenStatement);}
                else if(branch.ElseStatement is not null){this.Statement(branch.ElseStatement);}
                return;
            case WhileStatement loop:
                while(this.Condition(loop.Predicate)){
                    session.CancellationToken.ThrowIfCancellationRequested();
                    try{this.Statement(loop.Statement);}
                    catch(BreakSignal){break;}
                    catch(ContinueSignal){}
                }
                return;
            case BreakStatement:throw new BreakSignal();
            case ContinueStatement:throw new ContinueSignal();
            case BeginEndBlockStatement block:
                this.Block(block.StatementList.Statements);
                return;
            case TryCatchStatement tryCatch:
                try{
                    session.TryCatchDepth++;
                    try{this.Block(tryCatch.TryStatements.Statements);}finally{session.TryCatchDepth--;}
                }catch(Exception error) when(error is not OperationCanceledException and not ControlSignal){
                    var previous=this.caught;this.caught=error;
                    try{this.Block(tryCatch.CatchStatements.Statements);}finally{this.caught=previous;}
                }
                return;
            case ThrowStatement raise:
                this.Throw(raise);
                return;
            case ReturnStatement:throw new ReturnSignal();
            case CreateTableStatement create when IsTemporary(create.SchemaObjectName):
                this.CreateTemporary(create);
                return;
            case DropTableStatement drop when drop.Objects.All(IsTemporary):
                // この接続で作った一時表なら消す。無いものは、模擬済みの一時表(#SVer など)の後始末を受け持つ従来の経路へ渡す。
                if(!drop.Objects.All(name=>session.TemporaryTables.ContainsKey(name.BaseIdentifier.Value))){
                    this.results.Add(this.Execute(statement));
                    return;
                }
                foreach(var name in drop.Objects){session.TemporaryTables.Remove(name.BaseIdentifier.Value);}
                return;
            case InsertStatement insert when insert.InsertSpecification.Target is NamedTableReference target&&IsTemporary(target.SchemaObject):
                this.InsertTemporary(insert.InsertSpecification,target.SchemaObject.BaseIdentifier.Value);
                return;
            case ExecuteStatement execute when this.TryExecuteProcedure(execute):
                return;
            default:
                this.results.Add(this.Execute(statement));
                return;
        }
    }

    // SELECT @a=式, @b=式 FROM … は、式だけを列にして実行し、最後の行の値を入れる。行が無ければ変数は変わらない。
    private void AssignFromSelect(SelectStatement select,QuerySpecification query){
        var elements=query.SelectElements.Cast<SelectSetVariable>().ToArray();
        var targets=elements.Select(e=>this.Find(e.Variable)).ToArray();
        var columns=new List<string>();
        for(var i=0;i<elements.Length;i++){columns.Add("CAST(("+this.Combined(elements[i].Variable,elements[i].AssignmentKind,elements[i].Expression)+") AS "+targets[i].TypeText+") AS [c"+i.ToString(global::System.Globalization.CultureInfo.InvariantCulture)+"]");}
        var first=query.SelectElements[0];var last=query.SelectElements[^1];
        var text=sql.Substring(select.StartOffset,first.StartOffset-select.StartOffset)+string.Join(",",columns)+sql.Substring(last.StartOffset+last.FragmentLength,select.StartOffset+select.FragmentLength-(last.StartOffset+last.FragmentLength));
        object?[]? row=null;
        foreach(var current in this.Run(text).Rows){row=current;}
        if(row is null){return;}
        for(var i=0;i<targets.Length;i++){targets[i].Value=row[i];}
    }

    private void Assign(VariableReference reference,AssignmentKind kind,ScalarExpression expression){
        var variable=this.Find(reference);
        variable.Value=this.ScalarText(this.Combined(reference,kind,expression),variable);
    }

    // SET @v += 式 などの複合代入を、ふつうの式に直す。
    private string Combined(VariableReference reference,AssignmentKind kind,ScalarExpression expression){
        var text=this.Text(expression);
        string? operation=kind switch{
            AssignmentKind.Equals=>null,
            AssignmentKind.AddEquals=>"+",AssignmentKind.SubtractEquals=>"-",AssignmentKind.MultiplyEquals=>"*",AssignmentKind.DivideEquals=>"/",AssignmentKind.ModEquals=>"%",
            AssignmentKind.BitwiseAndEquals=>"&",AssignmentKind.BitwiseOrEquals=>"|",AssignmentKind.BitwiseXorEquals=>"^",
            _=>throw new SqlQueryException("Unsupported assignment operator.",40515),
        };
        if(operation is null){return text;}
        return reference.Name+" "+operation+" ("+text+")";
    }

    private bool Condition(BooleanExpression predicate){
        return Equals(this.Single("SELECT CASE WHEN "+this.Text(predicate)+" THEN 1 ELSE 0 END"),1);
    }

    private object? Scalar(ScalarExpression expression,Variable target)=>this.ScalarText(this.Text(expression),target);
    private object? ScalarText(string expression,Variable target)=>this.Single("SELECT CAST(("+expression+") AS "+target.TypeText+")");

    private object? Single(string text){
        foreach(var row in this.Run(text).Rows){return row[0];}
        return null;
    }

    private void Throw(ThrowStatement raise){
        if(raise.ErrorNumber is null){
            if(this.caught is null){throw new SqlQueryException("THROW without parameters should be in a CATCH block.",10704);}
            global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(this.caught).Throw();
        }
        var number=Convert.ToInt32(this.Single("SELECT "+this.Text(raise.ErrorNumber!)),global::System.Globalization.CultureInfo.InvariantCulture);
        var message=Convert.ToString(this.Single("SELECT "+this.Text(raise.Message!)),global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty;
        throw new SqlQueryException(message,number);
    }

    private void CreateTemporary(CreateTableStatement create){
        var name=create.SchemaObjectName.BaseIdentifier.Value;
        if(session.TemporaryTables.ContainsKey(name)){throw new SqlQueryException($"There is already an object named '{name}' in the database.",2714);}
        if(create.Definition is null){throw new SqlQueryException("CREATE TABLE requires column definitions.",40515);}
        var columns=new List<SqlColumn>();
        foreach(var definition in create.Definition.ColumnDefinitions){
            if(definition.DataType is null){throw new SqlQueryException("Computed columns in temporary tables are not supported.",40515);}
            var type=ParameterDeclarations.Parse("@c "+TypeText(definition.DataType)).Single().Type;
            var nullable=true;
            foreach(var constraint in definition.Constraints){
                if(constraint is NullableConstraintDefinition{Nullable:false}||constraint is UniqueConstraintDefinition{IsPrimaryKey:true}){nullable=false;}
            }
            columns.Add(new SqlColumn(definition.ColumnIdentifier.Value,type,nullable,new SqlObjectName("tempdb","dbo",name),columns.Count));
        }
        session.TemporaryTables.Add(name,new TemporaryTable(columns));
    }

    private void InsertTemporary(InsertSpecification insert,string name){
        if(!session.TemporaryTables.TryGetValue(name,out var table)){throw new SqlQueryException($"Invalid object name '{name}'.",208);}
        var targets=new List<int>();
        if(insert.Columns.Count==0){targets.AddRange(Enumerable.Range(0,table.Columns.Count));}
        else{
            foreach(var column in insert.Columns){
                var columnName=column.MultiPartIdentifier.Identifiers[^1].Value;
                var index=table.Columns.ToList().FindIndex(c=>string.Equals(c.Name,columnName,StringComparison.OrdinalIgnoreCase));
                if(index<0){throw new SqlQueryException($"Invalid column name '{columnName}'.",207);}
                targets.Add(index);
            }
        }
        var rows=new List<object?[]>();
        switch(insert.InsertSource){
            case SelectInsertSource source:
                rows.AddRange(this.Run(this.Text(source.Select)).Rows);
                break;
            case ValuesInsertSource source:
                foreach(var value in source.RowValues){rows.AddRange(this.Run("SELECT "+string.Join(",",value.ColumnValues.Select(this.Text))).Rows);}
                break;
            default:
                throw new SqlQueryException("Unsupported INSERT source for a temporary table.",40515);
        }
        var count=0;
        foreach(var row in rows){
            if(row.Length!=targets.Count){throw new SqlQueryException("Column name or number of supplied values does not match table definition.",213);}
            var stored=new object?[table.Columns.Count];
            for(var i=0;i<targets.Count;i++){
                var column=table.Columns[targets[i]];var value=ConvertValue(row[i],column.Type.ClrType);
                if(value is null&&!column.IsNullable){throw new SqlQueryException($"Cannot insert the value NULL into column '{column.Name}', table '{name}'; column does not allow nulls. INSERT fails.",515);}
                stored[targets[i]]=value;
            }
            table.Rows.Add(stored);count++;
        }
        this.results.Add(QueryResult.NonQuery(count));
    }

    // SELECT … INTO #表 FROM … は、INTO を外して実行し、結果の列で一時表を作る。
    private void SelectInto(SelectStatement select){
        var name=select.Into.BaseIdentifier.Value;
        if(session.TemporaryTables.ContainsKey(name)){throw new SqlQueryException($"There is already an object named '{name}' in the database.",2714);}
        var start=select.StartOffset;var text=sql.Substring(start,select.FragmentLength);
        // "INTO 名前" の部分を、元の文から取り除く。
        var intoStart=sql.LastIndexOf("into",select.Into.StartOffset,select.Into.StartOffset-start,StringComparison.OrdinalIgnoreCase);
        if(intoStart<0){throw new SqlQueryException("SELECT INTO could not be rewritten.",40515);}
        text=text.Remove(intoStart-start,select.Into.StartOffset+select.Into.FragmentLength-intoStart);
        var result=this.Run(text);
        var columns=result.Columns.Select((c,i)=>new SqlColumn(c.Name,new SqlType(c.SqlTypeName,c.ClrType,c.MaximumLength,c.Precision,c.Scale,c.Collation),c.IsNullable,new SqlObjectName("tempdb","dbo",name),i)).ToArray();
        var table=new TemporaryTable(columns);table.Rows.AddRange(result.Rows);
        session.TemporaryTables.Add(name,table);
        this.results.Add(QueryResult.NonQuery(table.Rows.Count));
    }

    // レジストリを読む拡張ストアドは、本物で値が無いときと同じく OUTPUT の変数を NULL にする。
    // それ以外の手続きで OUTPUT を使うものはまだ扱えない。OUTPUT が無ければ共通エンジンへ任せる。
    private bool TryExecuteProcedure(ExecuteStatement execute){
        if(execute.ExecuteSpecification.ExecutableEntity is not ExecutableProcedureReference procedure||procedure.ProcedureReference?.ProcedureReference is null){return false;}
        var name=procedure.ProcedureReference.ProcedureReference.Name.BaseIdentifier.Value;
        var outputs=procedure.Parameters.Where(static p=>p.IsOutput).ToArray();
        if(name.ToLowerInvariant() is "xp_instance_regread" or "xp_regread"){
            foreach(var output in outputs){
                if(output.ParameterValue is VariableReference reference){this.Find(reference).Value=null;}
            }
            if(outputs.Length==0){
                this.results.Add(new QueryResult([new ColumnMetadata("Value",typeof(string),"nvarchar",true,false,0,4000),new ColumnMetadata("Data",typeof(string),"nvarchar",true,false,1,4000)],[],0));
            }
            return true;
        }
        if(outputs.Length>0){throw new SqlQueryException($"OUTPUT parameters of '{name}' are not supported.",40515);}
        return false;
    }

    private QueryResult Execute(TSqlStatement statement)=>this.Run(SqlServerBuiltinRewriter.Rewrite(statement,sql));

    // 変数と CATCH の情報を引数にして共通エンジンで実行し、行を先に読み切る(後の文が一時表や変数を変えるため)。
    private QueryResult Run(string text){
        var parsed=new TSql160Parser(true).Parse(new StringReader(text),out var errors);
        if(errors.Count==0){text=SqlServerBuiltinRewriter.Rewrite(parsed,text);}
        var list=new List<SqlParameter>();var bound=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);
        if(parameters is not null){
            foreach(var parameter in parameters){
                if(!this.variables.ContainsKey(parameter.Name)){list.Add(parameter);bound[parameter.Name]=values?.GetValueOrDefault(parameter.Name);}
            }
        }
        foreach(var variable in this.variables.Values){list.Add(variable.Parameter);bound[variable.Parameter.Name]=variable.Value;}
        var error=this.caught;var sqlError=error as SqlQueryException;
        void Add(string name,SqlType type,object? value){list.Add(new SqlParameter(name,type));bound[name]=value;}
        if(text.Contains("@__seamless_error_",StringComparison.OrdinalIgnoreCase)){
            // CATCH の外では ERROR_*() はすべて NULL。
            object? number=null,severity=null,state=null,line=null;
            if(error is not null){number=sqlError?.Number??50000;severity=16;state=1;line=sqlError?.Line??1;}
            Add(SqlServerBuiltinRewriter.ErrorNumber,SqlType.Int,number);
            Add(SqlServerBuiltinRewriter.ErrorMessage,new SqlType("nvarchar",typeof(string),4000),error?.Message);
            Add(SqlServerBuiltinRewriter.ErrorSeverity,SqlType.Int,severity);
            Add(SqlServerBuiltinRewriter.ErrorState,SqlType.Int,state);
            Add(SqlServerBuiltinRewriter.ErrorLine,SqlType.Int,line);
            Add(SqlServerBuiltinRewriter.ErrorProcedure,new SqlType("nvarchar",typeof(string),128),null);
        }
        var previous=session.PreferQueryEngine;session.PreferQueryEngine=true;
        try{
            var result=engine.Execute(text,session,list,bound);
            var columns=result.Columns;var rows=result.Rows.ToList();
            return new QueryResult(columns,rows,result.RowsAffected);
        }finally{session.PreferQueryEngine=previous;}
    }

    private Variable Find(VariableReference reference){
        if(!this.variables.TryGetValue(reference.Name,out var variable)){throw new SqlQueryException($"Must declare the scalar variable \"{reference.Name}\".",137);}
        return variable;
    }

    private string Text(TSqlFragment fragment)=>sql.Substring(fragment.StartOffset,fragment.FragmentLength);

    // 共通エンジンが知らない別名型は、同じ意味の基本型に直す。
    private string TypeText(DataTypeReference type){
        var text=this.Text(type).Trim();
        if(type is UserDataTypeReference user&&user.Name.SchemaIdentifier is null){
            var name=user.Name.BaseIdentifier.Value.ToLowerInvariant();
            if(name=="sysname"){return "nvarchar(128)";}
        }
        if(type is SqlDataTypeReference{SqlDataTypeOption:SqlDataTypeOption.Sql_Variant}){return "nvarchar(4000)";}
        if(type is SqlDataTypeReference{SqlDataTypeOption:SqlDataTypeOption.VarChar or SqlDataTypeOption.NVarChar or SqlDataTypeOption.Char or SqlDataTypeOption.NChar or SqlDataTypeOption.VarBinary or SqlDataTypeOption.Binary} sized&&sized.Parameters.Count==0){
            // 長さを省いた宣言は長さ 1(DECLARE @x varchar)。
            return text+"(1)";
        }
        return text;
    }

    private static bool IsTemporary(SchemaObjectName name)=>name.BaseIdentifier.Value.StartsWith('#');

    private static object? ConvertValue(object? value,Type target){
        if(value is null||value is DBNull){return null;}
        if(target.IsInstanceOfType(value)){return value;}
        if(target==typeof(string)){return Convert.ToString(value,global::System.Globalization.CultureInfo.InvariantCulture);}
        if(value is DateTime date&&target==typeof(DateOnly)){return DateOnly.FromDateTime(date);}
        if(value is IConvertible&&(target.IsPrimitive||target==typeof(decimal)||target==typeof(DateTime))){return Convert.ChangeType(value,target,global::System.Globalization.CultureInfo.InvariantCulture);}
        return value;
    }

    private sealed class Variable(SqlParameter parameter,string typeText){
        public SqlParameter Parameter=>parameter;
        public string TypeText=>typeText;
        public object? Value{get;set;}
    }
    private abstract class ControlSignal:Exception;
    private sealed class BreakSignal:ControlSignal;
    private sealed class ContinueSignal:ControlSignal;
    private sealed class ReturnSignal:ControlSignal;

    private sealed class NeedVisitor:TSqlFragmentVisitor{
        public bool Found{get;private set;}
        public override void Visit(DeclareVariableStatement node){this.Found=true;}
        public override void Visit(SetVariableStatement node){this.Found=true;}
        public override void Visit(SelectSetVariable node){this.Found=true;}
        public override void Visit(IfStatement node){this.Found=true;}
        public override void Visit(WhileStatement node){this.Found=true;}
        public override void Visit(TryCatchStatement node){this.Found=true;}
        public override void Visit(ThrowStatement node){this.Found=true;}
        public override void Visit(BeginEndBlockStatement node){this.Found=true;}
        public override void Visit(ReturnStatement node){this.Found=true;}
        public override void Visit(CreateTableStatement node){if(IsTemporary(node.SchemaObjectName)){this.Found=true;}}
        public override void Visit(DropTableStatement node){if(node.Objects.Any(IsTemporary)){this.Found=true;}}
        public override void Visit(InsertStatement node){if(node.InsertSpecification.Target is NamedTableReference target&&IsTemporary(target.SchemaObject)){this.Found=true;}}
        public override void Visit(SelectStatement node){if(node.Into is not null&&IsTemporary(node.Into)){this.Found=true;}}
        public override void Visit(ExecuteParameter node){if(node.IsOutput){this.Found=true;}}
        public override void Visit(IIfCall node){this.Found=true;}
        public override void Visit(FunctionCall node){if(node.CallTarget is null&&node.FunctionName.Value.StartsWith("ERROR_",StringComparison.OrdinalIgnoreCase)){this.Found=true;}}
    }
}
