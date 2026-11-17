using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
namespace Seamless.Database.Gateway.Sql;

public sealed partial class SqlExecutionEngine{
    private static bool TryIntegerPoint(TSqlStatement statement,IReadOnlyDictionary<string,object?>? values,out string column,out object key){
        column=string.Empty;key=null!;BooleanExpression? condition=null;if(statement is UpdateStatement update){condition=update.UpdateSpecification.WhereClause?.SearchCondition;}else if(statement is DeleteStatement delete){condition=delete.DeleteSpecification.WhereClause?.SearchCondition;}
        if(condition is not BooleanComparisonExpression{ComparisonType:BooleanComparisonType.Equals} comparison){return false;}ColumnReferenceExpression? reference=comparison.FirstExpression as ColumnReferenceExpression;ScalarExpression operand=comparison.SecondExpression;if(reference is null){reference=comparison.SecondExpression as ColumnReferenceExpression;operand=comparison.FirstExpression;}if(reference?.MultiPartIdentifier?.Identifiers.LastOrDefault() is not{} name){return false;}
        object? value=null;if(operand is IntegerLiteral literal&&int.TryParse(literal.Value,global::System.Globalization.NumberStyles.None,global::System.Globalization.CultureInfo.InvariantCulture,out var integer)){value=integer;}else if(operand is VariableReference variable&&values is not null){values.TryGetValue(variable.Name,out value);}if(value is not(byte or short or int or long)){return false;}column=name.Value;key=value;return true;
    }
}
