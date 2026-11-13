using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Sql;

internal static partial class GatewayDdlCompiler{
    private static GatewayDdlPlan Function(GatewaySchemaCatalog original,TSqlStatement statement,string sql,string database,SqlSchemaResolution resolution){
        var schemas=original.Definition.Schemas.ToArray();
        foreach(var name in Names(statement)){
            if(!Database(name,database).Equals(database,StringComparison.OrdinalIgnoreCase)){continue;}var schemaName=SchemaOf(name,resolution,schemas,statement is CreateFunctionStatement?null:owner=>owner.Routines.Any(routine=>routine.Name.Equals(name.BaseIdentifier.Value,StringComparison.OrdinalIgnoreCase)));var index=Array.FindIndex(schemas,schema=>schema.Name.Equals(schemaName,StringComparison.OrdinalIgnoreCase));if(index<0){throw new SqlQueryException("The schema does not exist.",2760);}var schema=schemas[index];var routines=schema.Routines.ToList();var previous=routines.FindIndex(routine=>routine.Name.Equals(name.BaseIdentifier.Value,StringComparison.OrdinalIgnoreCase));
            if(statement is DropFunctionStatement drop){if(previous<0){if(drop.IsIfExists){continue;}throw new SqlQueryException("The function does not exist.",3701);}EnsureNoFunctionDependencies(original,schemaName,name);routines.RemoveAt(previous);}
            else{
                var function=(FunctionStatementBody)statement;if(function.Name.DatabaseIdentifier is not null){throw new SqlQueryException("CREATE/ALTER FUNCTION cannot specify a database name.",166);}if(function.ReturnType is not ScalarFunctionReturnType scalar||function.MethodSpecifier is not null||function.OrderHint is not null){throw Unsupported("This function return or external implementation requires a table-function execution contract.");}
                if(statement is CreateFunctionStatement&&previous>=0){throw new SqlQueryException("The function already exists.",2714);}if(statement is AlterFunctionStatement&&previous<0){throw new SqlQueryException("The function does not exist.",208);}if(schema.Tables.Any(table=>table.Name.Equals(name.BaseIdentifier.Value,StringComparison.OrdinalIgnoreCase))||schema.Views.Any(view=>view.Name.Equals(name.BaseIdentifier.Value,StringComparison.OrdinalIgnoreCase))){throw new SqlQueryException("An object with this name already exists.",2714);}
                if(function.Options.Any(static option=>option.OptionKind is not (FunctionOptionKind.SchemaBinding or FunctionOptionKind.ReturnsNullOnNullInput or FunctionOptionKind.CalledOnNullInput))){throw Unsupported("This function option requires security or external compilation semantics before acceptance.");}function.StatementList.Accept(new FunctionSideEffects());
                var parameters=function.Parameters.Select((parameter,ordinal)=>{if(parameter.Value is not null||parameter.Modifier!=ParameterModifier.None){throw Unsupported("Function parameter defaults and modifiers are not connected.");}return new ParameterCatalog(ordinal,parameter.VariableName.Value,Type(parameter.DataType,true,original.Definition.DefaultCollation),false);}).ToArray();if(parameters.Select(static parameter=>parameter.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=parameters.Length){throw new SqlQueryException("A parameter name is repeated.",134);}
                var definition=sql.Substring(function.StartOffset,function.FragmentLength);var catalog=new RoutineCatalog(schema.Name,name.BaseIdentifier.Value,RoutineKind.ScalarFunction,Type(scalar.DataType,true,original.Definition.DefaultCollation),parameters,[],definition,false,0,1){SourceObjectType="FN",IsSchemaBound=function.Options.Any(static option=>option.OptionKind==FunctionOptionKind.SchemaBinding)};if(previous>=0){EnsureNoFunctionDependencies(original,schemaName,name);routines[previous]=catalog;}else{routines.Add(catalog);}
            }
            schemas[index]=new(schema.Name,schema.Tables,schema.Views,routines.ToArray());
        }
        var result=original with{Definition=new(original.Definition.Name,schemas,original.Definition.DefaultCollation)};ValidateSchemaBinding(original,result);return new(result,new Dictionary<long,string?>());
    }
    private static void EnsureNoFunctionDependencies(GatewaySchemaCatalog schema,string schemaName,SchemaObjectName name){
        foreach(var owner in schema.Definition.Schemas){foreach(var table in owner.Tables){foreach(var expression in table.Columns.SelectMany(static column=>new[]{column.DefaultDefinition,column.ComputedDefinition}).Concat(table.CheckConstraints.Select(static check=>check.Definition)).Where(static expression=>expression is not null)){var parser=new TSql160Parser(true);var fragment=parser.Parse(new StringReader("SELECT "+expression),out var errors);if(errors.Count!=0){fragment=parser.Parse(new StringReader("SELECT 1 WHERE "+expression),out errors);}if(errors.Count==0){var visitor=new FunctionReference(schemaName,name.BaseIdentifier.Value);fragment.Accept(visitor);if(visitor.Found){throw new SqlQueryException("A column default, computed column, or constraint depends on the function.",3729);}}}}}
    }
    private sealed class FunctionReference(string schema,string name):TSqlFragmentVisitor{
        internal bool Found{get;private set;}
        public override void ExplicitVisit(FunctionCall node){if(node.FunctionName.Value.Equals(name,StringComparison.OrdinalIgnoreCase)&&node.CallTarget is MultiPartIdentifierCallTarget target&&target.MultiPartIdentifier.Identifiers.Last().Value.Equals(schema,StringComparison.OrdinalIgnoreCase)){this.Found=true;}base.ExplicitVisit(node);}
    }
    private sealed class FunctionSideEffects:TSqlFragmentVisitor{
        private static void Reject(TSqlFragment fragment)=>throw new SqlQueryException("Invalid use of a side-effecting operator within a function: "+fragment.GetType().Name+".",443);
        public override void ExplicitVisit(InsertStatement node)=>Reject(node);
        public override void ExplicitVisit(UpdateStatement node)=>Reject(node);
        public override void ExplicitVisit(DeleteStatement node)=>Reject(node);
        public override void ExplicitVisit(MergeStatement node)=>Reject(node);
        public override void ExplicitVisit(ExecuteStatement node)=>Reject(node);
        public override void ExplicitVisit(BeginTransactionStatement node)=>Reject(node);
        public override void ExplicitVisit(CommitTransactionStatement node)=>Reject(node);
        public override void ExplicitVisit(RollbackTransactionStatement node)=>Reject(node);
        public override void ExplicitVisit(SaveTransactionStatement node)=>Reject(node);
        public override void ExplicitVisit(RaiseErrorStatement node)=>Reject(node);
        public override void ExplicitVisit(ThrowStatement node)=>Reject(node);
        public override void ExplicitVisit(FunctionCall node){if(node.FunctionName.Value.ToUpperInvariant() is "NEWID" or "NEWSEQUENTIALID" or "RAND" or "TEXTPTR"){Reject(node);}base.ExplicitVisit(node);}
    }
}
