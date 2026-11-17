using System.Globalization;
using System.Text;
namespace Seamless.Database.SqlTranslation;
/// <summary>
/// SQL の名前から C# の識別子とリテラルを作る。識別子の規則は取込の生成器(Seamless.Database.Rdbms の CSharpIdentifier)と同じ:
/// 文字・数字・_ 以外は _ に、数字で始まれば先頭に _、キーワードなら @ を付ける。
/// </summary>
public static class CSharpNames{
    private static readonly HashSet<string> keywords=[
        "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
        "continue","decimal","default","delegate","do","double","else","enum","event","explicit","extern",
        "false","finally","fixed","float","for","foreach","goto","if","implicit","in","int","interface",
        "internal","is","lock","long","namespace","new","null","object","operator","out","override","params",
        "private","protected","public","readonly","ref","return","sbyte","sealed","short","sizeof","stackalloc",
        "static","string","struct","switch","this","throw","true","try","typeof","uint","ulong","unchecked",
        "unsafe","ushort","using","virtual","void","volatile","while",
    ];
    /// <summary>C# の識別子。</summary>
    public static string Identifier(string name){
        var builder=new StringBuilder(name.Length+1);
        for(var index=0;index<name.Length;index++){
            var character=name[index];
            bool valid;
            if(index==0){
                valid=character=='_'||char.IsLetter(character);
            }else{
                valid=character=='_'||char.IsLetterOrDigit(character);
            }
            if(valid){
                builder.Append(character);
            }else{
                builder.Append('_');
            }
        }
        if(builder.Length==0||char.IsDigit(builder[0])){
            builder.Insert(0,'_');
        }
        var identifier=builder.ToString();
        if(keywords.Contains(identifier)){
            return "@"+identifier;
        }
        return identifier;
    }
    /// <summary>名前がぶつかったときに付ける8桁(名前の SHA-256 の先頭4バイト)。取込の生成器と同じ。</summary>
    public static string HashSuffix(string value)=>Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0,4));
    /// <summary>C# の文字列リテラル。</summary>
    public static string String(string value){
        var builder=new StringBuilder(value.Length+2);
        builder.Append('"');
        foreach(var c in value){
            switch(c){
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if(char.IsControl(c)||char.IsSurrogate(c)){
                        builder.Append("\\u").Append(((int)c).ToString("x4",CultureInfo.InvariantCulture));
                    }else{
                        builder.Append(c);
                    }
                    break;
            }
        }
        builder.Append('"');
        return builder.ToString();
    }
}
