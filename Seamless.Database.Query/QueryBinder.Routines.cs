using Microsoft.SqlServer.TransactSql.ScriptDom;
namespace Seamless.Database.Query;
internal sealed partial class QueryBinder{
    private Func<EvalFrame,(bool Returned,object?Value)>BindRoutineStatements(IList<TSqlStatement>statements){
        var actions=statements.Select(this.BindRoutineStatement).ToArray();
        return frame=>{foreach(var action in actions){frame.Execution.Scope.ThrowIfUnavailable();var result=action(frame);if(result.Returned){return result;}}return(false,null);};
    }
    private Func<EvalFrame,(bool Returned,object?Value)>BindRoutineStatement(TSqlStatement statement){
        switch(statement){
            case ReturnStatement returned:{var value=this.BindScalar(returned.Expression,[],[],null);return frame=>(true,value.Value(frame));}
            case BeginEndBlockStatement block:return this.BindRoutineStatements(block.StatementList.Statements);
            case DeclareVariableStatement declaration:{var initializers=declaration.Declarations.Select(x=>(Name:x.VariableName.Value,Type:BindType(x.DataType),Value:BindInitializer(x.Value))).ToArray();return frame=>{var variables=(IDictionary<string,object?>)frame.Execution.Parameters;foreach(var initializer in initializers){variables[initializer.Name]=SqlValues.ConvertTo(initializer.Value?.Value(frame),initializer.Type,true);}return(false,null);};}
            case SetVariableStatement assignment:{if(assignment.AssignmentKind!=AssignmentKind.Equals||assignment.FunctionCallExists){throw Unsupported(assignment);}var parameter=this.parameters.FirstOrDefault(x=>Equal(x.Name,assignment.Variable.Name))??throw new SqlQueryException("Unknown routine local "+assignment.Variable.Name,137);var value=this.BindScalar(assignment.Expression,[],[],null);return frame=>{((IDictionary<string,object?>)frame.Execution.Parameters)[parameter.Name]=SqlValues.ConvertTo(value.Value(frame),parameter.Type,true);return(false,null);};}
            case IfStatement conditional:{var predicate=this.BindBoolean(conditional.Predicate,[],[],null);var yes=this.BindRoutineStatement(conditional.ThenStatement);Func<EvalFrame,(bool Returned,object?Value)>no=_=>(false,null);if(conditional.ElseStatement is not null){no=this.BindRoutineStatement(conditional.ElseStatement);}return frame=>{if(predicate(frame)==true){return yes(frame);}return no(frame);};}
            default:throw Unsupported(statement);
        }
    }
    private static ScalarExpression?SingleRoutineReturn(IList<TSqlStatement>statements){if(statements.Count!=1){return null;}if(statements[0] is BeginEndBlockStatement block){return SingleRoutineReturn(block.StatementList.Statements);}if(statements[0] is ReturnStatement returned){return returned.Expression;}return null;}
    private BoundScalar?BindInitializer(ScalarExpression?expression){if(expression is null){return null;}return this.BindScalar(expression,[],[],null);}
    private static IEnumerable<SqlParameter>RoutineLocals(TSqlFragment fragment){
        if(fragment is DeclareVariableElement variable){yield return new(variable.VariableName.Value,BindType(variable.DataType));yield break;}
        foreach(var property in fragment.GetType().GetProperties()){if(property.Name=="ScriptTokenStream"||property.GetIndexParameters().Length!=0){continue;}if(property.GetValue(fragment) is TSqlFragment child){foreach(var parameter in RoutineLocals(child)){yield return parameter;}}else if(property.GetValue(fragment) is global::System.Collections.IEnumerable items){foreach(var item in items){if(item is TSqlFragment descendant){foreach(var parameter in RoutineLocals(descendant)){yield return parameter;}}}}}
    }
}

