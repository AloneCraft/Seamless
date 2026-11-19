namespace Seamless.Management.Components;

/// <summary>ツリー上の操作の結果。案内がnullなら成功。未対応か=trueの案内は赤い太字で表示する。</summary>
public sealed record ツリー操作結果(string? 案内=null,bool 未対応か=false){
    public static readonly ツリー操作結果 成功=new();
}

/// <summary>左ツリーの「＋…を追加」行と、テーブル・ビュー・関数の右端の名前変更・コピー・削除から呼ぶ操作。</summary>
public sealed class ツリー対象操作{
    /// <summary>所属(DB直下は空文字)・分類(Table/View/Function)・名前を受け取り、下書きへ追加する。</summary>
    public required Func<string,string,string,Task<ツリー操作結果>> 作成{get;init;}
    public required Func<string,Task<ツリー操作結果>> スキーマ作成{get;init;}
    public required Func<CatalogObject,string,Task<ツリー操作結果>> 名前変更{get;init;}
    public required Func<CatalogObject,Task<ツリー操作結果>> コピー{get;init;}
    public required Func<CatalogObject,Task<ツリー操作結果>> 削除{get;init;}
    /// <summary>空のスキーマを下書きから消す。中身のあるスキーマは理由を返して消さない。</summary>
    public Func<string,Task<ツリー操作結果>>? スキーマ削除{get;init;}
    public Func<CatalogObject,Task<ツリー操作結果>>? 変更取消{get;init;}
    public Func<string,string,Task<ツリー操作結果>>? 削除取消{get;init;}
}
