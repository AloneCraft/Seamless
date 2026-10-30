using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Seamless.Database.Gateway.Sql;

internal static class SqlServerConnectionProbes{
    internal static bool TryContainedAuthentication(TSqlScript script,out string database){
        database="";
        if(script.Batches.Count!=1){return false;}
        var statements=script.Batches[0].Statements;
        if(statements.Count!=3||statements[0] is not UseStatement first||statements[1] is not IfStatement conditional||statements[2] is not UseStatement last||!string.Equals(first.DatabaseName.Value,last.DatabaseName.Value,StringComparison.OrdinalIgnoreCase)){return false;}
        var predicate=conditional.Predicate;while(predicate is BooleanParenthesisExpression parentheses){predicate=parentheses.Expression;}
        if(!TokensMatch(predicate,["db_id","(",")","=","1"])||Single(conditional.ThenStatement) is not SelectStatement selected||!TokensMatch(selected,["SELECT","0"])){return false;}
        if(Single(conditional.ElseStatement) is not ExecuteStatement executed||executed.ExecuteSpecification.ExecutableEntity is not ExecutableStringList strings||strings.Strings.Count!=1||strings.Strings[0] is not StringLiteral dynamicSql||!TokensMatch(executed,["EXEC","(","<sql>",")"],true)){return false;}
        var parser=new TSql160Parser(true);var inner=parser.Parse(new StringReader(dynamicSql.Value),out var errors) as TSqlScript;
        if(errors.Count!=0||inner is null||inner.Batches.Count!=1||inner.Batches[0].Statements.Count!=1||inner.Batches[0].Statements[0] is not SelectStatement query){return false;}
        if(!TokensMatch(query,["SELECT","CASE","WHEN","authenticating_database_id","=","1","THEN","0","ELSE","1","END","FROM","sys",".","dm_exec_sessions","WHERE","session_id","=","@@SPID"])){return false;}
        database=first.DatabaseName.Value;return true;
    }
    private static TSqlStatement? Single(TSqlStatement? statement){
        if(statement is BeginEndBlockStatement block){if(block.StatementList.Statements.Count!=1){return null;}return block.StatementList.Statements[0];}
        return statement;
    }
    private static bool TokensMatch(TSqlFragment fragment,string[] expected,bool maskSql=false){
        var tokens=fragment.ScriptTokenStream;if(tokens is null||fragment.FirstTokenIndex<0||fragment.LastTokenIndex>=tokens.Count){return false;}
        var position=0;
        for(var i=fragment.FirstTokenIndex;i<=fragment.LastTokenIndex;i++){
            var token=tokens[i];if(token.TokenType is TSqlTokenType.WhiteSpace or TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment or TSqlTokenType.EndOfFile or TSqlTokenType.Semicolon){continue;}
            var text=token.Text;
            if(maskSql&&token.TokenType is TSqlTokenType.AsciiStringLiteral or TSqlTokenType.UnicodeStringLiteral){text="<sql>";}
            if(maskSql&&position==0&&string.Equals(text,"EXECUTE",StringComparison.OrdinalIgnoreCase)){text="EXEC";}
            if(position>=expected.Length||!string.Equals(text,expected[position++],StringComparison.OrdinalIgnoreCase)){return false;}
        }
        return position==expected.Length;
    }
}
