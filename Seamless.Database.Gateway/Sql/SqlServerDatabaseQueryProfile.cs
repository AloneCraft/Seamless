using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Sql;

internal static class SqlServerDatabaseQueryProfile{
    private static readonly Lazy<TSqlScript> options=new(()=>Read("SsmsDatabaseOptions.sql"));
    private static readonly Lazy<TSqlScript> list=new(()=>Read("SsmsDatabaseList.sql"));
    internal static TSqlScript Read(string name){using var stream=typeof(SqlServerDatabaseQueryProfile).Assembly.GetManifestResourceStream("Seamless.Database.Gateway.Sql.Compatibility."+name)??throw new InvalidOperationException("Missing database query profile.");using var text=new StreamReader(stream);var parser=new TSql160Parser(true);var result=parser.Parse(text,out var errors) as TSqlScript;if(result is null||errors.Count!=0){throw new InvalidOperationException("Invalid database query profile.");}return result;}
    internal static bool Same(TSqlFragment first,TSqlFragment second)=>Tokens(first).SequenceEqual(Tokens(second),StringComparer.Ordinal);
    private static IEnumerable<string> Tokens(TSqlFragment fragment){foreach(var token in fragment.ScriptTokenStream.Skip(fragment.FirstTokenIndex).Take(fragment.LastTokenIndex-fragment.FirstTokenIndex+1)){
        if(token.TokenType is TSqlTokenType.WhiteSpace or TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment or TSqlTokenType.Semicolon or TSqlTokenType.EndOfFile){continue;}
        var text=token.Text;if(token.TokenType is not(TSqlTokenType.AsciiStringLiteral or TSqlTokenType.UnicodeStringLiteral)){text=text.ToUpperInvariant();}yield return ((int)token.TokenType)+":"+text;
    }}
    internal static bool IsPrefix(TSqlScript script){var expected=options.Value.Batches[0].Statements;return SystemCatalogEmulator.ProfileEngineEdition!=9&&script.Batches.Count==1&&script.Batches[0].Statements.Count==2&&Same(script.Batches[0].Statements[0],expected[0])&&Same(script.Batches[0].Statements[1],expected[1]);}
    internal static bool IsCleanup(TSqlStatement statement)=>Same(statement,options.Value.Batches[0].Statements[3]);
    internal static bool TryBatch(TSqlScript script,string sql,out string select){
        select="";if(SystemCatalogEmulator.ProfileEngineEdition==9||script.Batches.Count!=1||script.Batches[0].Statements.Count!=4){return false;}var actual=script.Batches[0].Statements;var expected=options.Value.Batches[0].Statements;
        if(actual[2] is not SelectStatement query||!Same(actual[0],expected[0])||!Same(actual[1],expected[1])||!Same(actual[3],expected[3])){return false;}
        return Rewrite(query,sql,true,true,out select);
    }
    internal static bool TrySelect(SelectStatement statement,string sql,bool emptyRetention,out string result)=>Rewrite(statement,sql,emptyRetention,false,out result);
    private static bool Rewrite(SelectStatement statement,string sql,bool emptyRetention,bool force,out string result){
        result="";var visitor=new Expressions();statement.Accept(visitor);if(!visitor.HasDatabases){return false;}
        var expectedOptions=(QuerySpecification)((SelectStatement)options.Value.Batches[0].Statements[2]).QueryExpression;
        var retention=((SelectScalarExpression)expectedOptions.SelectElements[0]).Expression;
        var expectedList=(QuerySpecification)((SelectStatement)list.Value.Batches[0].Statements[0]).QueryExpression;
        var status=((SelectScalarExpression)expectedList.SelectElements[5]).Expression;
        var changes=new List<(int Offset,int Length,string Value)>();
        foreach(var expression in visitor.Values){
            if(emptyRetention&&Same(expression,retention)){changes.Add((expression.StartOffset,expression.FragmentLength,"CAST(0 AS bit)"));}
            else if(Same(expression,status)){
                var text=sql.Substring(expression.StartOffset,expression.FragmentLength);
                // StatusのCASEはintへ暗黙変換される既知のビットマスク。業務のbinary値には適用しない。
                var hex=new HexLiterals();expression.Accept(hex);foreach(var value in hex.Values.OrderByDescending(v=>v.StartOffset)){var number=Convert.ToInt32(value.Value[2..],16);text=text.Remove(value.StartOffset-expression.StartOffset,value.FragmentLength).Insert(value.StartOffset-expression.StartOffset,number.ToString(global::System.Globalization.CultureInfo.InvariantCulture));}
                changes.Add((expression.StartOffset,expression.FragmentLength,text));
            }
        }
        if(changes.Count==0&&!force){return false;}
        result=sql.Substring(statement.StartOffset,statement.FragmentLength);
        foreach(var change in changes.OrderByDescending(c=>c.Offset)){var index=change.Offset-statement.StartOffset;result=result.Remove(index,change.Length).Insert(index,change.Value);}
        return true;
    }
    private sealed class Expressions:TSqlFragmentVisitor{
        internal bool HasDatabases;internal readonly List<ScalarExpression> Values=[];
        public override void ExplicitVisit(NamedTableReference table){if(table.SchemaObject.SchemaIdentifier?.Value.Equals("sys",StringComparison.OrdinalIgnoreCase)==true&&table.SchemaObject.BaseIdentifier.Value.Equals("databases",StringComparison.OrdinalIgnoreCase)){this.HasDatabases=true;}}
        public override void ExplicitVisit(SelectScalarExpression expression){this.Values.Add(expression.Expression);base.ExplicitVisit(expression);}
    }
    private sealed class HexLiterals:TSqlFragmentVisitor{internal readonly List<BinaryLiteral>Values=[];public override void ExplicitVisit(BinaryLiteral literal)=>this.Values.Add(literal);}
}
