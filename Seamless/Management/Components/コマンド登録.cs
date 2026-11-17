namespace Seamless.Management.Components;

public sealed class コマンド登録<T>{
    private readonly Dictionary<string,操作コマンド<T>> コマンド=new(StringComparer.Ordinal);
    private int 実行状態;
    public bool 実行中か=>Volatile.Read(ref this.実行状態)!=0;
    public コマンド登録(IEnumerable<操作コマンド<T>> 項目){
        ArgumentNullException.ThrowIfNull(項目);
        foreach(var 操作 in 項目){
            ArgumentException.ThrowIfNullOrWhiteSpace(操作.ID);
            if(!this.コマンド.TryAdd(操作.ID,操作 with{検索語=操作.検索語.ToArray()})){
                throw new ArgumentException("コマンドIDが重複しています。 "+操作.ID,nameof(項目));
            }
        }
    }
    public IEnumerable<操作コマンド<T>> 検索(T 対象,string 入力){
        var 語=入力.Trim();
        foreach(var 操作 in this.コマンド.Values){
            if(操作.表示するか(対象)&&(語.Length==0||操作.表示名.Contains(語,StringComparison.OrdinalIgnoreCase)||表示文言.取得(操作.表示名).Contains(語,StringComparison.OrdinalIgnoreCase)
                ||操作.検索語.Any(候補=>候補.Contains(語,StringComparison.OrdinalIgnoreCase)))){
                yield return 操作;
            }
        }
    }
    public async Task 実行Async(string ID,T 対象){
        if(Interlocked.CompareExchange(ref this.実行状態,1,0)!=0){throw new InvalidOperationException("操作を実行中です。");}
        try{
            if(!this.コマンド.TryGetValue(ID,out var 操作)){throw new KeyNotFoundException("操作がありません。 "+ID);}
            if(!操作.表示するか(対象)||!操作.実行できるか(対象)){throw new InvalidOperationException("現在の対象では実行できません。");}
            await 操作.実行Async(対象);
        }finally{Volatile.Write(ref this.実行状態,0);}
    }
}
