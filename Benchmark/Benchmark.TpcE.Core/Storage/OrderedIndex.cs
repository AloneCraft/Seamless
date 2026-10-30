using System.Runtime.CompilerServices;
namespace Benchmark.TpcE.Core;
/// <summary>順序付きチャンク列の見出し。チャンク番号 First..Last を使い、Total は全要素数。</summary>
public struct IndexHeader{
    public long First;
    public long Last;
    public long Total;
}
/// <summary>チャンクのキー。持ち主の型は 8 バイトの倍数にする(パディングを出さない)。</summary>
public readonly record struct ChunkKey<TOwner>(TOwner Owner,long No) where TOwner:unmanaged;
[InlineArray(32)] // IndexChunk.Capacity と同じ値
public struct ChunkItems<TEntry> where TEntry:unmanaged{
    private TEntry first;
}
/// <summary>
/// 整列済みの要素を最大 <see cref="Capacity"/> 件持つチャンク。Low はこのチャンクに入る要素の下限で、
/// チャンク番号の順に Low が昇順に並ぶ(中身が空になっても Low は残す)。
/// </summary>
public struct IndexChunk<TEntry> where TEntry:unmanaged{
    public const int Capacity=32;
    public long Count;
    public TEntry Low;
    public ChunkItems<TEntry> Items;
}
/// <summary>初期投入の間だけ書き込みをメモリに溜められるもの。</summary>
public interface IBulkLoadable{
    void BeginBulk();
    void EndBulk();
}
/// <summary>
/// 初期投入の間だけ読み書きをメモリに溜める入れ物の包み。Persisted は書くたびに追記するので、
/// 末尾のチャンクを何度も書き直す初期投入でファイルが膨らむのを防ぐ。溜めすぎたら書き出して空にする。
/// </summary>
internal sealed class BulkCache<TKey,TValue>(IKeyValueStore<TKey,TValue> inner)
    where TKey:unmanaged
    where TValue:unmanaged{
    private const int Limit=1<<16;
    private System.Collections.Generic.Dictionary<TKey,(bool Live,TValue Value)>? cache;
    public void Begin(){
        this.cache??=[];
    }
    public void End(){
        this.FlushCache();
        this.cache=null;
    }
    public bool TryGet(TKey key,out TValue value){
        if(this.cache is not null&&this.cache.TryGetValue(key,out var cached)){
            value=cached.Value;
            return cached.Live;
        }
        return inner.TryGet(key,out value);
    }
    public void Put(TKey key,TValue value){
        if(this.cache is null){
            inner.Put(key,value);
            return;
        }
        this.cache[key]=(true,value);
        if(this.cache.Count>=Limit){
            this.FlushCache();
        }
    }
    public void Remove(TKey key){
        if(this.cache is null){
            inner.Remove(key);
            return;
        }
        this.cache[key]=(false,default);
    }
    public IEnumerable<KeyValuePair<TKey,TValue>> Enumerate(){
        this.FlushCache();
        return inner.Enumerate();
    }
    private void FlushCache(){
        if(this.cache is null){
            return;
        }
        foreach(var pair in this.cache){
            if(pair.Value.Live){
                inner.Put(pair.Key,pair.Value.Value);
            }else{
                inner.Remove(pair.Key);
            }
        }
        this.cache.Clear();
    }
}
/// <summary>保存先に依らない索引の口(ダイジェストと整合検査用)。</summary>
public interface IIndexView{
    string Name{get;}
    /// <summary>(持ち主, 要素) を順不同で列挙する。チャンクの分け方には依らない。</summary>
    IEnumerable<(object Owner,object Entry)> EnumerateEntriesBoxed();
}
/// <summary>
/// ハッシュ表しか無い保存先の上に作る、持ち主ごとの順序付きの列(副索引)。
/// 持ち主ごとに見出し 1 行と固定長チャンクの列を持ち、追加はほぼ末尾に来る前提で作る。
/// 結果は上限件数つきの配列で返す(列挙中に同じ索引へ書かないため)。
/// </summary>
public sealed class OrderedIndex<TOwner,TEntry>:IIndexView,IBulkLoadable
    where TOwner:unmanaged
    where TEntry:unmanaged,IComparable<TEntry>{
    private readonly BulkCache<TOwner,IndexHeader> headers;
    private readonly BulkCache<ChunkKey<TOwner>,IndexChunk<TEntry>> chunks;
    public OrderedIndex(string name,IKeyValueStore<TOwner,IndexHeader> headers,IKeyValueStore<ChunkKey<TOwner>,IndexChunk<TEntry>> chunks){
        this.Name=name;
        this.headers=new(headers);
        this.chunks=new(chunks);
    }
    public string Name{get;}
    public void BeginBulk(){
        this.headers.Begin();
        this.chunks.Begin();
    }
    public void EndBulk(){
        this.headers.End();
        this.chunks.End();
    }
    /// <summary>持ち主の要素数。</summary>
    public long CountOf(TOwner owner){
        if(this.headers.TryGet(owner,out var header)){
            return header.Total;
        }
        return 0;
    }
    public void Insert(TOwner owner,TEntry entry){
        if(!this.headers.TryGet(owner,out var header)){
            var chunk=new IndexChunk<TEntry>{Count=1,Low=entry};
            chunk.Items[0]=entry;
            this.chunks.Put(new(owner,0),chunk);
            this.headers.Put(owner,new IndexHeader{First=0,Last=0,Total=1});
            return;
        }
        var number=this.FindChunk(owner,header,entry);
        var target=this.ReadChunk(owner,number);
        if(entry.CompareTo(target.Low)<0){
            // 先頭のチャンクより小さい要素だけがここに来る。
            target.Low=entry;
        }
        this.InsertInto(owner,header,number,target,entry);
        header=this.ReadHeader(owner);
        header.Total++;
        this.headers.Put(owner,header);
    }
    public void Remove(TOwner owner,TEntry entry){
        var header=this.ReadHeader(owner);
        var number=this.FindChunk(owner,header,entry);
        var chunk=this.ReadChunk(owner,number);
        var position=IndexOf(ref chunk,entry);
        if(position<0){
            throw new InvalidOperationException($"{this.Name}: entry {entry} of owner {owner} was not found.");
        }
        for(var index=position;index<chunk.Count-1;index++){
            chunk.Items[index]=chunk.Items[index+1];
        }
        chunk.Count--;
        chunk.Items[(int)chunk.Count]=default;
        header.Total--;
        if(header.Total==0){
            for(var no=header.First;no<=header.Last;no++){
                this.chunks.Remove(new(owner,no));
            }
            this.headers.Remove(owner);
            return;
        }
        if(chunk.Count==0&&number==header.First){
            this.chunks.Remove(new(owner,number));
            header.First++;
            // 先頭側の空チャンクも片付ける。
            while(header.First<header.Last&&this.ReadChunk(owner,header.First).Count==0){
                this.chunks.Remove(new(owner,header.First));
                header.First++;
            }
        }else if(chunk.Count==0&&number==header.Last){
            this.chunks.Remove(new(owner,number));
            header.Last--;
            while(header.Last>header.First&&this.ReadChunk(owner,header.Last).Count==0){
                this.chunks.Remove(new(owner,header.Last));
                header.Last--;
            }
        }else{
            this.chunks.Put(new(owner,number),chunk);
        }
        this.headers.Put(owner,header);
    }
    /// <summary>持ち主の全要素を昇順で返す。</summary>
    public TEntry[] All(TOwner owner)=>this.Ascending(owner,default,false,int.MaxValue);
    /// <summary>lo 以上の要素を昇順で最大 limit 件返す。</summary>
    public TEntry[] From(TOwner owner,TEntry lo,int limit)=>this.Ascending(owner,lo,true,limit);
    /// <summary>[lo,hi] の要素を昇順で最大 limit 件返す。</summary>
    public TEntry[] Range(TOwner owner,TEntry lo,TEntry hi,int limit){
        var found=this.Ascending(owner,lo,true,limit);
        var count=0;
        while(count<found.Length&&found[count].CompareTo(hi)<=0){
            count++;
        }
        if(count==found.Length){
            return found;
        }
        return found[..count];
    }
    /// <summary>新しい順(降順)に最大 limit 件返す。</summary>
    public TEntry[] TopDesc(TOwner owner,int limit){
        if(limit<=0||!this.headers.TryGet(owner,out var header)){
            return [];
        }
        var result=new List<TEntry>(Math.Min(limit,(int)Math.Min(header.Total,1024)));
        for(var no=header.Last;no>=header.First&&result.Count<limit;no--){
            var chunk=this.ReadChunk(owner,no);
            for(var index=(int)chunk.Count-1;index>=0&&result.Count<limit;index--){
                result.Add(chunk.Items[index]);
            }
        }
        return [..result];
    }
    public IEnumerable<(object Owner,object Entry)> EnumerateEntriesBoxed(){
        foreach(var pair in this.headers.Enumerate()){
            for(var no=pair.Value.First;no<=pair.Value.Last;no++){
                var chunk=this.ReadChunk(pair.Key,no);
                for(var index=0;index<chunk.Count;index++){
                    yield return (pair.Key,chunk.Items[index]);
                }
            }
        }
    }
    private TEntry[] Ascending(TOwner owner,TEntry lo,bool bounded,int limit){
        if(limit<=0||!this.headers.TryGet(owner,out var header)){
            return [];
        }
        var start=header.First;
        if(bounded){
            start=this.FindChunk(owner,header,lo);
        }
        var result=new List<TEntry>(Math.Min(limit,(int)Math.Min(header.Total,1024)));
        for(var no=start;no<=header.Last&&result.Count<limit;no++){
            var chunk=this.ReadChunk(owner,no);
            for(var index=0;index<chunk.Count&&result.Count<limit;index++){
                var item=chunk.Items[index];
                if(bounded&&item.CompareTo(lo)<0){
                    continue;
                }
                result.Add(item);
            }
        }
        return [..result];
    }
    /// <summary>entry が入るべきチャンク(Low ≤ entry となる最後のチャンク。無ければ先頭)の番号。</summary>
    private long FindChunk(TOwner owner,IndexHeader header,TEntry entry){
        var last=this.ReadChunk(owner,header.Last);
        if(entry.CompareTo(last.Low)>=0){
            return header.Last;
        }
        long lo=header.First;
        var hi=header.Last-1;
        var answer=header.First;
        while(lo<=hi){
            var mid=lo+(hi-lo)/2;
            var chunk=this.ReadChunk(owner,mid);
            if(chunk.Low.CompareTo(entry)<=0){
                answer=mid;
                lo=mid+1;
            }else{
                hi=mid-1;
            }
        }
        return answer;
    }
    private void InsertInto(TOwner owner,IndexHeader header,long number,IndexChunk<TEntry> chunk,TEntry entry){
        while(true){
            if(chunk.Count<IndexChunk<TEntry>.Capacity){
                InsertSorted(ref chunk,entry);
                this.chunks.Put(new(owner,number),chunk);
                return;
            }
            var max=chunk.Items[IndexChunk<TEntry>.Capacity-1];
            if(entry.CompareTo(max)>0){
                // 満杯のチャンクより大きい要素は次のチャンクへ回す。
                if(number==header.Last){
                    this.AppendChunk(owner,header,entry);
                    return;
                }
                number++;
                chunk=this.ReadChunk(owner,number);
                if(entry.CompareTo(chunk.Low)<0){
                    chunk.Low=entry;
                }
                continue;
            }
            // 満杯のチャンクの途中に入る: 最大の要素を次のチャンクへ押し出す。
            chunk.Count--;
            chunk.Items[(int)chunk.Count]=default;
            InsertSorted(ref chunk,entry);
            this.chunks.Put(new(owner,number),chunk);
            if(number==header.Last){
                this.AppendChunk(owner,header,max);
                return;
            }
            number++;
            chunk=this.ReadChunk(owner,number);
            chunk.Low=max;
            entry=max;
        }
    }
    private void AppendChunk(TOwner owner,IndexHeader header,TEntry entry){
        var created=new IndexChunk<TEntry>{Count=1,Low=entry};
        created.Items[0]=entry;
        header.Last++;
        this.chunks.Put(new(owner,header.Last),created);
        // 件数は呼び出し側が足す。番号だけ先に書く。
        this.headers.Put(owner,header);
    }
    private static void InsertSorted(ref IndexChunk<TEntry> chunk,TEntry entry){
        var position=(int)chunk.Count;
        while(position>0&&chunk.Items[position-1].CompareTo(entry)>0){
            chunk.Items[position]=chunk.Items[position-1];
            position--;
        }
        chunk.Items[position]=entry;
        chunk.Count++;
    }
    private static int IndexOf(ref IndexChunk<TEntry> chunk,TEntry entry){
        var lo=0;
        var hi=(int)chunk.Count-1;
        while(lo<=hi){
            var mid=lo+(hi-lo)/2;
            var compared=chunk.Items[mid].CompareTo(entry);
            if(compared==0){
                return mid;
            }
            if(compared<0){
                lo=mid+1;
            }else{
                hi=mid-1;
            }
        }
        return -1;
    }
    private IndexHeader ReadHeader(TOwner owner){
        if(!this.headers.TryGet(owner,out var header)){
            throw new InvalidOperationException($"{this.Name}: owner {owner} has no entries.");
        }
        return header;
    }
    private IndexChunk<TEntry> ReadChunk(TOwner owner,long number){
        if(!this.chunks.TryGet(new(owner,number),out var chunk)){
            throw new InvalidOperationException($"{this.Name}: chunk {number} of owner {owner} is missing.");
        }
        return chunk;
    }
}
