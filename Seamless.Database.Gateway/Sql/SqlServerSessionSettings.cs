using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Sql;

internal static class SqlServerSessionSettings{
    internal static bool TryExecute(TSqlStatement statement,SessionContext session,out QueryResult result){
        result=QueryResult.Empty;
        if(statement is SetTextSizeStatement textSize){
            if(textSize.TextSize is IntegerLiteral{Value:"2147483647"}){return true;}
            throw new SqlQueryException("SET TEXTSIZE supports only the unlimited profile value 2147483647. Values that would truncate results are not supported.",40515);
        }
        if(statement is SetCommandStatement{Commands.Count:1} defaults&&defaults.Commands[0] is GeneralSetCommand defaultCommand){
            if(defaultCommand.CommandType==GeneralSetCommandType.QueryGovernorCostLimit&&defaultCommand.Parameter is IntegerLiteral{Value:"0"}){return true;}
            if(defaultCommand.CommandType==GeneralSetCommandType.DeadlockPriority&&(defaultCommand.Parameter is IntegerLiteral{Value:"0"}||defaultCommand.Parameter is IdentifierLiteral priority&&priority.Value.Equals("NORMAL",StringComparison.OrdinalIgnoreCase))){return true;}
        }
        if(statement is SetCommandStatement{Commands.Count:1} settings&&settings.Commands[0] is GeneralSetCommand{CommandType:GeneralSetCommandType.LockTimeout} command){
            var value=-2;
            if(command.Parameter is IntegerLiteral literal&&int.TryParse(literal.Value,global::System.Globalization.NumberStyles.None,global::System.Globalization.CultureInfo.InvariantCulture,out var number)){value=number;}
            else if(command.Parameter is UnaryExpression{UnaryExpressionType:UnaryExpressionType.Negative,Expression:IntegerLiteral{Value:"1"}}){value=-1;}
            if(value!=-1&&value<=0){throw new SqlQueryException("SET LOCK_TIMEOUT supports -1 or a positive Int32 integer literal. Immediate acquisition (0) and expressions are not supported.",40515);}
            session.LockTimeoutMilliseconds=value;return true;
        }
        if(statement is SelectStatement{Into:null,WithCtesAndXmlNamespaces:null,ComputeClauses.Count:0,OptimizerHints.Count:0,QueryExpression:QuerySpecification{SelectElements.Count:1,FromClause:null,WhereClause:null,GroupByClause:null,HavingClause:null,WindowClause:null,TopRowFilter:null,OrderByClause:null,OffsetClause:null,ForClause:null,UniqueRowFilter:UniqueRowFilter.NotSpecified} query}&&query.SelectElements[0] is SelectScalarExpression{Expression:GlobalVariableExpression variable} selected){
            int value;if(variable.Name.Equals("@@LOCK_TIMEOUT",StringComparison.OrdinalIgnoreCase)){value=session.LockTimeoutMilliseconds;}else if(variable.Name.Equals("@@TEXTSIZE",StringComparison.OrdinalIgnoreCase)){value=int.MaxValue;}else{return false;}
            result=new([new ColumnMetadata(selected.ColumnName?.Value??"",typeof(int),"int",false,false,0)],[new object?[]{value}],1);return true;
        }
        return false;
    }
}
