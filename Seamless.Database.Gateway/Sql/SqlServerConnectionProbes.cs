using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Seamless.Database.Gateway.Sql;

internal static class SqlServerConnectionProbes{
    private const string ServerInformationResource="Seamless.Database.Gateway.Sql.Compatibility.SsmsServerInformation.sql";
    private static readonly Lazy<string[][]> connectionInitializationTokens=new(()=>{
        using var stream=typeof(SqlServerConnectionProbes).Assembly.GetManifestResourceStream("Seamless.Database.Gateway.Sql.Compatibility.SsmsConnectionInitialization.sql")??throw new InvalidOperationException("Connection compatibility resource is missing.");using var reader=new StreamReader(stream);var sql=reader.ReadToEnd();
        return [ParseTokens(sql),ParseTokens(sql.Replace("'DatabaseEngineType'","DatabaseEngineType",StringComparison.Ordinal))];
    });
    private static string[] ParseTokens(string sql){var parser=new TSql160Parser(true);var script=parser.Parse(new StringReader(sql),out var errors) as TSqlScript;if(errors.Count!=0||script is null){throw new InvalidOperationException("Connection compatibility resource is invalid.");}return SemanticTokens(script);}
    internal static bool IsConnectionInitialization(TSqlScript script){
        if(script.Batches.Count!=1||script.Batches[0].Statements.Count!=5){return false;}
        var tokens=SemanticTokens(script);return connectionInitializationTokens.Value.Any(expected=>tokens.SequenceEqual(expected,StringComparer.Ordinal));
    }
    internal static bool IsConnectionInitializationSql(string sql){var parser=new TSql160Parser(true);var script=parser.Parse(new StringReader(sql),out var errors) as TSqlScript;return errors.Count==0&&script is not null&&IsConnectionInitialization(script);}
    private static readonly Lazy<string[]> serverInformationTokens=new(()=>{
        using var stream=typeof(SqlServerConnectionProbes).Assembly.GetManifestResourceStream(ServerInformationResource)??throw new InvalidOperationException("Server-information compatibility resource is missing.");using var reader=new StreamReader(stream);
        var parser=new TSql160Parser(true);var script=parser.Parse(reader,out var errors) as TSqlScript;
        if(errors.Count!=0||script is null){throw new InvalidOperationException("Server-information compatibility resource is invalid.");}return SemanticTokens(script);
    });
    internal static bool IsServerInformation(TSqlScript script){
        if(script.Batches.Count!=1||script.Batches[0].Statements.Count!=6||script.Batches[0].Statements[0] is not CreateTableStatement create||!string.Equals(create.SchemaObjectName.BaseIdentifier.Value,"#SVer",StringComparison.OrdinalIgnoreCase)){return false;}
        return SemanticTokens(script).SequenceEqual(serverInformationTokens.Value,StringComparer.Ordinal);
    }
    internal static bool IsServerInformationSql(string sql){var parser=new TSql160Parser(true);var script=parser.Parse(new StringReader(sql),out var errors) as TSqlScript;return errors.Count==0&&script is not null&&IsServerInformation(script);}
    internal static bool IsServerInformationCleanup(TSqlStatement statement)=>statement is DropTableStatement&&TokensMatch(statement,["DROP","TABLE","#SVer"]);
    private static string[] SemanticTokens(TSqlScript script){
        return script.ScriptTokenStream.Where(token=>token.TokenType is not (TSqlTokenType.WhiteSpace or TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment or TSqlTokenType.EndOfFile or TSqlTokenType.Semicolon)).Select(token=>{
            var text=token.Text;if(token.TokenType is not (TSqlTokenType.AsciiStringLiteral or TSqlTokenType.UnicodeStringLiteral)){text=text.ToUpperInvariant();}return ((int)token.TokenType).ToString(global::System.Globalization.CultureInfo.InvariantCulture)+":"+text;
        }).ToArray();
    }
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
