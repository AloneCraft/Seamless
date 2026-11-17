namespace Seamless.Database.SqlTranslation;
/// <summary>翻訳の結果の分類。</summary>
public enum TranslationSeverity{
    /// <summary>訳せないので、その定義は出力しない。</summary>
    Unsupported,
    /// <summary>訳したが、本物と同じ結果を約束しない(再現不可の模擬など)。</summary>
    Warning
}
/// <summary>翻訳の診断。行と列は定義の文字列の中の位置(1始まり)。</summary>
public sealed record TranslationDiagnostic(TranslationSeverity Severity,string Code,string Message,int Line,int Column){
    /// <inheritdoc/>
    public override string ToString()=>$"{this.Code} ({this.Line},{this.Column}): {this.Message}";
}
/// <summary>訳せない構文に出会ったときに投げ、翻訳の入口で診断に変える。</summary>
internal sealed class TranslationUnsupportedException(string code,string message,int line,int column):Exception(message){
    public TranslationDiagnostic Diagnostic{get;}=new(TranslationSeverity.Unsupported,code,message,line,column);
}
