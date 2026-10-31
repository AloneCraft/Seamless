using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Seamless.Database.Rdbms;
using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using SqlParser.Tokens;

namespace Seamless.Database.SqlServer;

/// <summary>SQL Server scalar CASE の限定形を生成時に C# へ変換する。未対応入力は必ず拒否する。</summary>
public static class SqlServerRoutineCompiler{
    public const string CompilerProfile="SqlServerScalarTinyintCaseNvarcharV1";
    public static bool TryCompile(RoutineCatalog routine,out GeneratedRoutineImplementation?implementation,out string?reason){
        ArgumentNullException.ThrowIfNull(routine);
        implementation=null;reason=null;
        try{
            implementation=Compile(routine);return true;
        }catch(Unsupported exception){reason=exception.Message;return false;}
        catch(ParserException exception){reason="CASE parser: "+exception.Message;return false;}
        catch(TokenizeException exception){reason="Tokenizer: "+exception.Message;return false;}
    }
    private static GeneratedRoutineImplementation Compile(RoutineCatalog routine){
        if(routine.Kind!=RoutineKind.ScalarFunction||routine.Parameters.Count!=1||routine.ResultColumns.Count!=0||routine.HasMutation){throw new Unsupported("Expected one-parameter read-only scalar function.");}
        var parameter=routine.Parameters[0];var returnType=routine.ReturnType;var definition=routine.Definition;
        if(string.IsNullOrWhiteSpace(definition)||definition.Length>1048576||definition.Contains('\\')){throw new Unsupported("Definition missing, too long, or contains an unsupported backslash.");}
        for(var index=0;index<definition.Length;index++){if(definition[index]=='\r'&&(index+1==definition.Length||definition[index+1]!='\n')){throw new Unsupported("Lone CR is unsupported in the routine definition.");}}
        if(parameter.IsOutput||parameter.IsTableValued||parameter.Type.Kind!=DatabaseTypeKind.Byte||parameter.Type.RuntimeType is not null||!string.Equals(parameter.Type.SqlTypeName,"tinyint",StringComparison.OrdinalIgnoreCase)){throw new Unsupported("Expected tinyint parameter metadata.");}
        if(returnType is null||returnType.Kind!=DatabaseTypeKind.String||returnType.RuntimeType is not null||!string.Equals(returnType.SqlTypeName,"nvarchar",StringComparison.OrdinalIgnoreCase)||returnType.MaximumLength is not int length||length is <1 or >4000){throw new Unsupported("Expected bounded nvarchar return metadata.");}
        if(parameter.Name.Length<2||parameter.Name[0]!='@'||!IsIdentifier(parameter.Name[1..])){throw new Unsupported("Unsupported parameter identifier.");}
        var tokens=new Tokenizer().Tokenize(definition.AsSpan(),new MsSqlDialect()).Where(static token=>token is not Whitespace).ToArray();
        var reader=new Reader(tokens);
        reader.Keyword("CREATE");reader.Keyword("FUNCTION");
        reader.Identifier(routine.SchemaName);reader.Punctuation<Period>();reader.Identifier(routine.Name);
        reader.Punctuation<LeftParen>();reader.Identifier(parameter.Name);reader.Identifier("tinyint");reader.Punctuation<RightParen>();
        reader.Keyword("RETURNS");reader.Identifier("nvarchar");reader.Punctuation<LeftParen>();reader.Number(length);reader.Punctuation<RightParen>();
        reader.Keyword("AS");reader.Keyword("BEGIN");reader.Keyword("DECLARE");
        var local=reader.Variable();if(string.Equals(local,parameter.Name,StringComparison.OrdinalIgnoreCase)){throw new Unsupported("Local variable conflicts with parameter.");}reader.Identifier("nvarchar");reader.Punctuation<LeftParen>();reader.Number(length);reader.Punctuation<RightParen>();reader.Optional<SemiColon>();
        reader.Keyword("SET");reader.Identifier(local);reader.Punctuation<Equal>();reader.Keyword("CASE");reader.Identifier(parameter.Name);
        var expression=new StringBuilder("SELECT CASE ").Append(parameter.Name);
        var arms=0;
        while(reader.TryKeyword("WHEN")){
            var value=reader.ByteNumber();reader.Keyword("THEN");var text=reader.StringLiteral();
            CheckString(text.Value,text.National,length);
            expression.Append(" WHEN ").Append(value.ToString(CultureInfo.InvariantCulture)).Append(" THEN ");AppendSqlString(expression,text.Value,text.National);arms++;
        }
        if(arms==0||arms>256){throw new Unsupported("Expected 1 to 256 CASE arms.");}
        reader.Keyword("ELSE");var fallback=reader.StringLiteral();CheckString(fallback.Value,fallback.National,length);
        expression.Append(" ELSE ");AppendSqlString(expression,fallback.Value,fallback.National);reader.Keyword("END");expression.Append(" END");reader.Optional<SemiColon>();
        reader.Keyword("RETURN");reader.Identifier(local);reader.Optional<SemiColon>();reader.Keyword("END");reader.Optional<SemiColon>();reader.End();
        var parsed=new SqlQueryParser().Parse(expression.ToString().AsSpan(),new MsSqlDialect());
        if(parsed.Count!=1||parsed[0] is not Statement.Select statement||statement.Query.Body is not SetExpression.SelectExpression body||body.Select.Projection.Count!=1||body.Select.Projection[0] is not SelectItem.UnnamedExpression projection||projection.Expression is not Expression.Case choice||choice.Operand is not Expression.Identifier operand||operand.Ident.Value!=parameter.Name||choice.Conditions.Count!=arms||choice.Results.Count!=arms||choice.ElseResult is null){throw new Unsupported("Unexpected CASE AST.");}
        var result=new StringBuilder("return @").Append(parameter.Name[1..]).Append(" switch {");
        var seen=new HashSet<int>();
        for(var i=0;i<arms;i++){
            if(choice.Conditions[i] is not Expression.LiteralValue{Value:Value.Number number}||number.Long||!byte.TryParse(number.Value,NumberStyles.None,CultureInfo.InvariantCulture,out var key)||!seen.Add(key)){throw new Unsupported("Unsupported or duplicate CASE key.");}
            result.Append(key.ToString(CultureInfo.InvariantCulture)).Append("=>").Append(CSharpString(choice.Results[i],length)).Append(',');
        }
        result.Append("_=>").Append(CSharpString(choice.ElseResult,length)).Append("};");
        return new(result.ToString(),Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition))),CompilerProfile);
    }
    private static string CSharpString(Expression expression,int length){
        if(expression is not Expression.LiteralValue literal){throw new Unsupported("CASE results must be strings.");}
        string value;bool national;
        switch(literal.Value){case Value.NationalStringLiteral n:value=n.Value;national=true;break;case Value.SingleQuotedString s:value=s.Value;national=false;break;default:throw new Unsupported("CASE results must be string literals.");}
        CheckString(value,national,length);return JsonSerializer.Serialize(value);
    }
    private static void CheckString(string value,bool national,int length){
        if(value.Length>length||value.Any(char.IsSurrogate)||(!national&&value.Any(static c=>c>127))){throw new Unsupported("CASE string exceeds supported encoding or length.");}
    }
    private static void AppendSqlString(StringBuilder builder,string text,bool national){if(national){builder.Append('N');}builder.Append('\'').Append(text.Replace("'","''",StringComparison.Ordinal)).Append('\'');}
    private static bool IsIdentifier(string name){return name.Length>0&&(char.IsAsciiLetter(name[0])||name[0]=='_')&&name.All(static c=>char.IsAsciiLetterOrDigit(c)||c=='_');}
    private sealed class Unsupported(string message):Exception(message);
    private sealed class Reader(Token[]tokens){
        private int position;
        public void End(){if(this.position!=tokens.Length){throw new Unsupported("Unexpected trailing token.");}}
        public bool TryKeyword(string value){if(this.position<tokens.Length&&tokens[this.position] is Word word&&word.QuoteStyle is null&&string.Equals(word.Value,value,StringComparison.OrdinalIgnoreCase)){this.position++;return true;}return false;}
        public void Keyword(string value){if(!this.TryKeyword(value)){throw new Unsupported("Expected "+value+".");}}
        public void Identifier(string value){if(this.position>=tokens.Length||tokens[this.position] is not Word word||word.QuoteStyle is not(null or '[')||!string.Equals(word.Value,value,StringComparison.Ordinal)){throw new Unsupported("Unexpected identifier: "+value);}this.position++;}
        public string Variable(){if(this.position>=tokens.Length||tokens[this.position] is not Word word||word.QuoteStyle is not null||word.Value.Length<2||word.Value[0]!='@'||!IsIdentifier(word.Value[1..])){throw new Unsupported("Expected local variable.");}this.position++;return word.Value;}
        public void Punctuation<T>()where T:Token{if(this.position>=tokens.Length||tokens[this.position] is not T){throw new Unsupported("Expected "+typeof(T).Name+".");}this.position++;}
        public void Optional<T>()where T:Token{if(this.position<tokens.Length&&tokens[this.position] is T){this.position++;}}
        public void Number(int expected){if(this.position>=tokens.Length||tokens[this.position] is not Number n||n.Long||!int.TryParse(n.Value,NumberStyles.None,CultureInfo.InvariantCulture,out var value)||value!=expected){throw new Unsupported("Unexpected numeric declaration.");}this.position++;}
        public byte ByteNumber(){if(this.position>=tokens.Length||tokens[this.position] is not Number n||n.Long||!byte.TryParse(n.Value,NumberStyles.None,CultureInfo.InvariantCulture,out var value)){throw new Unsupported("Expected tinyint CASE key.");}this.position++;return value;}
        public (string Value,bool National)StringLiteral(){if(this.position>=tokens.Length){throw new Unsupported("Expected string literal.");}var token=tokens[this.position++];if(token is NationalStringLiteral n){return(n.Value,true);}if(token is SingleQuotedString s){return(s.Value,false);}throw new Unsupported("Expected string literal.");}
    }
}
