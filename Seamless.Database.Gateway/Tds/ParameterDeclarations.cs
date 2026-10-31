using System.Globalization;
using Seamless.Database.Query;
using SqlParser;
using SqlParser.Dialects;
using SqlParser.Tokens;

namespace Seamless.Database.Gateway.Tds;

internal static class ParameterDeclarations{
    public static IReadOnlyList<SqlParameter> Parse(string declarations){
        if(string.IsNullOrWhiteSpace(declarations)){return [];}
        try{
            var tokens=new Tokenizer().Tokenize(declarations.AsSpan(),new MsSqlDialect()).Where(static t=>t is not Whitespace).ToArray();
            if(tokens.Length==0){return [];}
            var reader=new Reader(tokens);var result=new List<SqlParameter>();var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while(true){
                var name=reader.Variable();if(!names.Add(name)){throw Error("Duplicate parameter: "+name,134);}
                var type=reader.Type();if(reader.Keyword("OUT")||reader.Keyword("OUTPUT")){throw Error("Output parameters are not supported.",40515);}
                result.Add(new SqlParameter(name,type));
                if(reader.End){return result;}reader.Comma();if(reader.End){throw Error("Parameter declaration expected after comma.");}
            }
        }catch(TokenizeException e){throw Error("Invalid parameter declaration: "+e.Message);}
    }
    private static SqlQueryException Error(string text,int number=102)=>new(text,number);
    private sealed class Reader(Token[] tokens){
        private int position;
        public bool End=>this.position==tokens.Length;
        public string Variable(){
            if(this.position>=tokens.Length||tokens[this.position++] is not Word {QuoteStyle:null} word||word.Value.Length<2||word.Value[0]!='@'){throw Error("Expected a local parameter name.");}
            var name=new global::System.Text.StringBuilder(word.Value);var line=word.Location.Line;var next=word.Location.Column+word.Value.Length;
            while(this.position<tokens.Length&&tokens[this.position].Location.Line==line&&tokens[this.position].Location.Column==next){
                if(tokens[this.position] is SingleCharacterToken single&&IdentifierCharacter(single.Character)){name.Append(single.Character);next++;this.position++;continue;}
                if(tokens[this.position] is Word {QuoteStyle:null} more&&more.Value.All(IdentifierCharacter)){name.Append(more.Value);next+=more.Value.Length;this.position++;continue;}
                if(tokens[this.position] is Number {Long:false} digits&&digits.Value.All(char.IsDigit)){name.Append(digits.Value);next+=digits.Value.Length;this.position++;continue;}
                break;
            }
            var value=name.ToString();
            if(value.Length>129||value[1]=='@'||!(char.IsLetter(value[1])||value[1]=='_')||value.Skip(2).Any(static c=>!IdentifierCharacter(c))){throw Error("Expected a local parameter name.");}
            return value;
        }
        private static bool IdentifierCharacter(char c)=>char.IsLetterOrDigit(c)||c is '_' or '$' or '#' or '@'||char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
        public bool Keyword(string name){if(this.position<tokens.Length&&tokens[this.position] is Word {QuoteStyle:null} word&&word.Value.Equals(name,StringComparison.OrdinalIgnoreCase)){this.position++;return true;}return false;}
        public void Comma(){if(this.position>=tokens.Length||tokens[this.position++] is not global::SqlParser.Tokens.Comma){throw Error("Expected a comma between parameters.");}}
        private int Integer(){
            if(this.position>=tokens.Length||tokens[this.position++] is not Number {Long:false} number||!int.TryParse(number.Value,NumberStyles.None,CultureInfo.InvariantCulture,out var n)){throw Error("Expected an integer type argument.");}
            return n;
        }
        private void RightParen(){if(this.position>=tokens.Length||tokens[this.position++] is not global::SqlParser.Tokens.RightParen){throw Error("Expected ')' in parameter type.");}}
        public SqlType Type(){
            if(this.position>=tokens.Length||tokens[this.position++] is not Word {QuoteStyle:null} word){throw Error("Expected a SQL parameter type.");}
            var name=word.Value.ToLowerInvariant();
            var clr=name switch{
                "bigint"=>typeof(long),"int"=>typeof(int),"smallint"=>typeof(short),"tinyint"=>typeof(byte),"bit"=>typeof(bool),
                "decimal" or "numeric" or "money" or "smallmoney"=>typeof(decimal),"real"=>typeof(float),"float"=>typeof(double),
                "uniqueidentifier"=>typeof(Guid),"date"=>typeof(DateOnly),"time"=>typeof(TimeOnly),"datetimeoffset"=>typeof(DateTimeOffset),
                "datetime" or "datetime2" or "smalldatetime"=>typeof(DateTime),"binary" or "varbinary" or "image"=>typeof(byte[]),
                "char" or "varchar" or "nchar" or "nvarchar" or "text" or "ntext" or "xml"=>typeof(string),
                _=>throw Error("Unsupported SQL parameter type: "+name,40515)
            };
            int? length=null;byte? precision=null,scale=null;
            if(this.position<tokens.Length&&tokens[this.position] is LeftParen){
                this.position++;
                if(name is "decimal" or "numeric"){
                    var p=this.Integer();var s=0;if(this.position<tokens.Length&&tokens[this.position] is global::SqlParser.Tokens.Comma){this.position++;s=this.Integer();}
                    if(p is <1 or >38||s<0||s>p){throw Error("Invalid decimal precision or scale.");}precision=(byte)p;scale=(byte)s;
                }else if(name is "time" or "datetime2" or "datetimeoffset"){
                    var n=this.Integer();if(n is <0 or >7){throw Error("Invalid temporal scale.");}scale=(byte)n;
                }else if(name is "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary"){
                    if(this.Keyword("MAX")){
                        if(name is not ("varchar" or "nvarchar" or "varbinary")){throw Error("MAX is not supported for this type.");}length=-1;
                    }else{var n=this.Integer();int max;if(name is "nchar" or "nvarchar"){max=4000;}else{max=8000;}if(n<1||n>max){throw Error("Invalid type length.");}length=n;}
                }else if(name=="float"){var n=this.Integer();if(n is <1 or >53){throw Error("Invalid float precision.");}if(n!=53){throw Error("Only float(53) precision is supported.",40515);}length=n;}
                else{throw Error("This parameter type does not accept arguments.");}
                this.RightParen();
            }
            return new(name,clr,length,precision,scale);
        }
    }
}
