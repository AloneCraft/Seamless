namespace Seamless.Management.Components;

public sealed record 操作コマンド<T>(string ID,string 表示名,Func<T,bool> 表示するか,Func<T,bool> 実行できるか,Func<T,Task> 実行Async,string[] 検索語,
    string? グループ=null,string? ショートカット=null,bool 破壊的か=false,string アイコン="");
