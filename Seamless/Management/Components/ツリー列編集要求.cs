namespace Seamless.Management.Components;

public sealed record ツリー列編集要求(ObjectDefinitionDesign 基準,ColumnDesign[] 列一覧){
    public ParameterDesign[]? 引数一覧{get;init;}
    public string? 戻り型{get;init;}
    internal (string 元,string? 次)? 名前編集{get;init;}
    internal string? 復元する表の列{get;init;}
}
