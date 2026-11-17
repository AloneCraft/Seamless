namespace Seamless.Management;

/// <summary>
/// Managerが自分で作る宣言の断片に使う改行。利用者の本文と既存のソースは書き換えない。
/// 既存のファイルへ差し込む断片は、そのファイルで最初に現れる改行に合わせる。改行を含まないファイルと
/// 新しく作るファイルはLFにする(OSに依存させず、同じ定義からOSごとに違うソースを作らない)。
/// </summary>
public static class SourceNewLine{
    public const string Default="\n";

    /// <summary>対象のファイルの本文から、差し込む断片に使う改行を決める。</summary>
    public static string Of(string? text){
        if(string.IsNullOrEmpty(text)){return Default;}
        for(var index=0;index<text.Length;index++){
            if(text[index]=='\n'){return Default;}
            if(text[index]=='\r'){return index+1<text.Length&&text[index+1]=='\n'?"\r\n":"\r";}
        }
        return Default;
    }
}
