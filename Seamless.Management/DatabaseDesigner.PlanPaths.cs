namespace Seamless.Management;

public static partial class DatabaseDesigner{
    private static string? ResolvePlanSourcePath(SourceSnapshot source,string recordedPath)=>
        ResolvePlanSourceFile(source,recordedPath)?.Path.Replace('\\','/');

    /// <summary>
    /// 記録パスを、正規化した相対パスの完全一致または絶対パスの一意な全体suffixで保存ソースへ対応付ける。
    /// ファイルIOは行わず、曖昧な対応はnullを返す。メソッドhash・caller・範囲は呼出側で別途照合する。
    /// </summary>
    public static SourceFile? ResolvePlanSourceFile(SourceSnapshot source,string recordedPath){
        var path=recordedPath.Replace('\\','/');
        if(path.Split('/').Any(part=>part is "." or "..")){return null;}
        var windows=path.StartsWith("//",StringComparison.Ordinal)
            ||path.Length>=3&&char.IsAsciiLetter(path[0])&&path[1]==':'&&path[2]=='/';
        var absolute=windows||path.StartsWith("/",StringComparison.Ordinal);
        var comparison=windows?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        var matches=source.Files.Where(file=>absolute
            ?path.EndsWith("/"+file.Path.Replace('\\','/'),comparison)
            :path.Equals(file.Path.Replace('\\','/'),comparison)).Take(2).ToArray();
        return matches.Length==1?matches[0]:null;
    }
}
