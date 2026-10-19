namespace Sample.FLinqChainVerifier.Core;

/// <summary>
/// 4段階のメソッドチェーン(段階1: 2段ペア網羅、段階2: 代表3〜4段チェーン、段階3: 全組み合わせ自動生成、段階4: 長尺ランダム探索)を生成する。
/// </summary>
public static class チェーン生成器{
    /// <summary>段階1: 2段ペア(A.B)の網羅リストを生成する(手書き理想コード付き)。</summary>
    public static IReadOnlyList<チェーン検証項目>段階1ペア網羅を生成(入力ソース種別 ソース種別,ulong シード値=0x1018UL){
        var 定義一覧=new (string[]メソッド群,string? 手書き理想コード)[]{
            (["Where","Sum"],"""
                var sum=0L;
                for(var i=0;i<raw.Length;i++){
                    var x=raw[i];
                    if((x&1)==0){
                        sum+=x;
                    }
                }
                return sum;
                """),
            (["Select","Sum"],"""
                var sum=0L;
                for(var i=0;i<raw.Length;i++){
                    var x=raw[i];
                    sum+=(long)(x*3+1);
                }
                return sum;
                """),
            (["Where","Count"],"""
                var count=0;
                for(var i=0;i<raw.Length;i++){
                    if((raw[i]&1)==0){
                        count++;
                    }
                }
                return count;
                """),
            (["Where","Any"],"""
                for(var i=0;i<raw.Length;i++){
                    if((raw[i]&1)==0){
                        return true;
                    }
                }
                return false;
                """),
            (["Where","FirstOrDefault"],"""
                for(var i=0;i<raw.Length;i++){
                    var x=raw[i];
                    if((x&1)==0){
                        return x;
                    }
                }
                return 0;
                """),
            (["Skip","Take","ToArray"],"""
                if(raw.Length<=4){
                    return Array.Empty<int>();
                }
                var len=raw.Length-4;
                if(len>12){
                    len=12;
                }
                var res=new int[len];
                Array.Copy(raw,4,res,0,len);
                return res;
                """),
            (["OrderBy","FirstOrDefault"],"""
                if(raw.Length==0){
                    return 0;
                }
                var best=raw[0];
                var bestKey=best&31;
                for(var i=1;i<raw.Length;i++){
                    var x=raw[i];
                    var k=x&31;
                    if(k<bestKey){
                        bestKey=k;
                        best=x;
                    }
                }
                return best;
                """),
            (["GroupBy_Sum","Sum"],"""
                if(raw.Length==0){
                    return 0L;
                }
                Span<long> sums=stackalloc long[16];
                Span<bool> seen=stackalloc bool[16];
                for(var i=0;i<raw.Length;i++){
                    var x=raw[i];
                    var k=x&15;
                    seen[k]=true;
                    sums[k]+=x;
                }
                var total=0L;
                for(var k=0;k<16;k++){
                    if(seen[k]){
                        total+=k+sums[k];
                    }
                }
                return total;
                """),
            (["GroupBy_KeyOnly","Count"],"""
                Span<bool> seen=stackalloc bool[16];
                var count=0;
                for(var i=0;i<raw.Length;i++){
                    var k=raw[i]&15;
                    if(!seen[k]){
                        seen[k]=true;
                        count++;
                    }
                }
                return count;
                """),
            (["Distinct","Count"],"""
                var set=new HashSet<int>(raw.Length);
                for(var i=0;i<raw.Length;i++){
                    set.Add(raw[i]);
                }
                return set.Count;
                """),
            (["Reverse","FirstOrDefault"],"""
                if(raw.Length==0){
                    return 0;
                }
                return raw[raw.Length-1];
                """),
            (["Append","Sum"],"""
                var sum=77L;
                for(var i=0;i<raw.Length;i++){
                    sum+=raw[i];
                }
                return sum;
                """),
            (["Prepend","First"],"""
                return 11;
                """),
            (["Concat","Count"],"""
                return raw.Length+second.Length;
                """),
            (["SelectMany","Sum"],"""
                var secondSum=0L;
                for(var j=0;j<second.Length;j++){
                    secondSum+=second[j];
                }
                return (long)raw.Length*secondSum;
                """),
        };

        var 結果一覧=new List<チェーン検証項目>();
        var 番号=0;
        foreach(var (メソッド群,理想コード) in 定義一覧){
            結果一覧.Add(項目を構築(検証段階.ペア網羅,ソース種別,シード値,番号,メソッド群,理想コード));
            番号++;
        }

        // さらにカタログ内の全中間演算子 × 主要終端演算子の2段ペアを追加網羅
        string[]主要終端=["ToArray","Count","Sum","FirstOrDefault","Any"];
        foreach(var 中間 in 拡張メソッドカタログ.中間演算子一覧){
            foreach(var 終端名 in 主要終端){
                if(定義一覧.Any(d=>d.メソッド群.Length==2&&d.メソッド群[0]==中間.メソッド名&&d.メソッド群[1]==終端名)){
                    continue;
                }
                結果一覧.Add(項目を構築(検証段階.ペア網羅,ソース種別,シード値,番号,[中間.メソッド名,終端名],null));
                番号++;
            }
        }
        return 結果一覧;
    }

    /// <summary>段階2: 6つの基本プリミティブを組み合わせた代表3〜4段チェーン(A.B.C.D)を生成する(手書き理想コード付き)。</summary>
    public static IReadOnlyList<チェーン検証項目>段階2代表チェーンを生成(入力ソース種別 ソース種別,ulong シード値=0x2026UL){
        var 定義一覧=new (string[]メソッド群,string 手書き理想コード)[]{
            (["Where","Select","Sum"],"""
                // A.B(Where->Select) + B.C(Select->Sum) => 1本のループでフィルタ・射影・加算
                var sum=0L;
                for(var i=0;i<raw.Length;i++){
                    var x=raw[i];
                    if((x&1)==0){
                        var y=x*3+1;
                        sum+=y;
                    }
                }
                return sum;
                """),
            (["Where","Select","Take","ToArray"],"""
                // A.B(Where->Select) + B.C(Select->Take) + C.D(Take->ToArray) => 最大12件で即時breakする単一ループ
                var buf=new List<int>(12);
                for(var i=0;i<raw.Length;i++){
                    var x=raw[i];
                    if((x&1)==0){
                        buf.Add(x*3+1);
                        if(buf.Count>=12){
                            break;
                        }
                    }
                }
                return buf.ToArray();
                """),
            (["Skip","Where","Select","FirstOrDefault"],"""
                // A.B(Skip->Where) + B.C(Where->Select) + C.D(Select->FirstOrDefault) => インデックス4から走査し最初の一致で即return
                for(var i=4;i<raw.Length;i++){
                    var x=raw[i];
                    if((x&1)==0){
                        return x*3+1;
                    }
                }
                return 0;
                """),
            (["OrderBy","Where","Select","FirstOrDefault"],"""
                // A.B(OrderBy->Where Pushdown) + B.C(Where->Select) + C.D(Select->FirstOrDefault) => 偶数のみから最小キー要素を1パス選択
                var found=false;
                var bestVal=0;
                var bestKey=int.MaxValue;
                for(var i=0;i<raw.Length;i++){
                    var x=raw[i];
                    if((x&1)==0){
                        var k=x&31;
                        if(!found||k<bestKey){
                            found=true;
                            bestKey=k;
                            bestVal=x*3+1;
                        }
                    }
                }
                if(found){
                    return bestVal;
                }else{
                    return 0;
                }
                """),
            (["GroupBy_Sum","OrderByDescending","Take","ToArray"],"""
                // A.B(GroupBy 1パス集計) + B.C(OrderByDescending直接転送) + C.D(Take Top-K抽出)
                var dict=new Dictionary<int,int>(16);
                var order=new List<int>(16);
                for(var i=0;i<raw.Length;i++){
                    var x=raw[i];
                    var k=x&15;
                    if(dict.TryGetValue(k,out var cur)){
                        dict[k]=cur+x;
                    }else{
                        dict[k]=x;
                        order.Add(k);
                    }
                }
                var items=new int[order.Count];
                for(var i=0;i<order.Count;i++){
                    var k=order[i];
                    items[i]=k+dict[k];
                }
                return items.OrderByDescending(x=>x&31).Take(12).ToArray();
                """),
            (["GroupBy_Count","Where","Sum"],"""
                // A.B(GroupBy 1パス件数集計) + B.C(Whereフィルタ) + C.D(Sum直接加算)
                var counts=new Dictionary<int,int>(16);
                for(var i=0;i<raw.Length;i++){
                    var k=raw[i]&15;
                    if(counts.TryGetValue(k,out var c)){
                        counts[k]=c+1;
                    }else{
                        counts[k]=1;
                    }
                }
                var sum=0L;
                foreach(var kv in counts){
                    var val=kv.Key*10+kv.Value;
                    if((val&1)==0){
                        sum+=val;
                    }
                }
                return sum;
                """),
            (["Distinct","Where","Select","Count"],"""
                // A.B(Distinct<-Where Pushdown) + B.C(Select) + C.D(Count) => 偶数のみHashSet投入して件数取得
                var set=new HashSet<int>();
                for(var i=0;i<raw.Length;i++){
                    var x=raw[i];
                    if((x&1)==0){
                        set.Add(x);
                    }
                }
                return set.Count;
                """),
            (["Reverse","Where","Select","Take","ToArray"],"""
                // A.B(Reverse逆順走査) + B.C(Where) + C.D(Select->Take) => 末尾から逆順forループで最大12件収集
                var list=new List<int>(12);
                for(var i=raw.Length-1;i>=0;i--){
                    var x=raw[i];
                    if((x&1)==0){
                        list.Add(x*3+1);
                        if(list.Count>=12){
                            break;
                        }
                    }
                }
                return list.ToArray();
                """),
            (["Concat","Where","Select","Sum"],"""
                // A.B(Concat 2ループ連結) + B.C(Where->Select) + C.D(Sum)
                var sum=0L;
                for(var i=0;i<raw.Length;i++){
                    var x=raw[i];
                    if((x&1)==0){
                        sum+=(long)(x*3+1);
                    }
                }
                for(var j=0;j<second.Length;j++){
                    var y=second[j];
                    if((y&1)==0){
                        sum+=(long)(y*3+1);
                    }
                }
                return sum;
                """),
            (["SelectMany","Where","Select","Sum"],"""
                // A.B(SelectMany 2重ループ) + B.C(Where->Select) + C.D(Sum)
                var innerSum=0L;
                for(var j=0;j<second.Length;j++){
                    var y=second[j];
                    if((y&1)==0){
                        innerSum+=(long)(y*3+1);
                    }
                }
                return (long)raw.Length*innerSum;
                """),
        };

        var 結果一覧=new List<チェーン検証項目>(定義一覧.Length);
        for(var 番号=0;番号<定義一覧.Length;番号++){
            var (メソッド群,理想コード)=定義一覧[番号];
            結果一覧.Add(項目を構築(検証段階.代表チェーン,ソース種別,シード値,番号,メソッド群,理想コード));
        }
        return 結果一覧;
    }

    /// <summary>段階3: 全組み合わせ自動生成(3段: 中間1 -> 中間2 -> 終端)を重複なく順次生成する。</summary>
    public static IEnumerable<チェーン検証項目>段階3全組み合わせを生成(入力ソース種別 ソース種別,ulong シード値=0x3030UL){
        var 中間一覧=拡張メソッドカタログ.中間演算子一覧;
        var 終端一覧=拡張メソッドカタログ.終端演算子一覧;
        var 試行番号=0;

        for(var i=0;i<中間一覧.Count;i++){
            var 第1=中間一覧[i];
            for(var j=0;j<中間一覧.Count;j++){
                var 第2=中間一覧[j];
                // SelectMany の2重連続は要素数が爆発するため1段のみに制限
                if(第1.メソッド名=="SelectMany"&&第2.メソッド名=="SelectMany"){
                    continue;
                }
                for(var k=0;k<終端一覧.Count;k++){
                    var 終端=終端一覧[k];
                    yield return 項目を構築(
                        検証段階.全組み合わせ自動生成,
                        ソース種別,
                        シード値,
                        試行番号,
                        [第1.メソッド名,第2.メソッド名,終端.メソッド名],
                        null);
                    試行番号++;
                }
            }
        }
    }

    /// <summary>段階4: シード値と試行番号から、より長い(4〜7段の)メソッドチェーンを決定論的にランダム生成する。</summary>
    public static チェーン検証項目 段階4ランダムチェーンを生成(
        入力ソース種別 ソース種別,
        ulong 基準シード,
        int 試行番号,
        int 最小段数=4,
        int 最大段数=7){
        var 派生シード=シードを派生(基準シード,(ulong)試行番号);
        var 状態=派生シード;
        var 中間一覧=拡張メソッドカタログ.中間演算子一覧;
        var 終端一覧=拡張メソッドカタログ.終端演算子一覧;

        var 段数幅=Math.Max(1,最大段数-最小段数+1);
        var 総段数=最小段数+(int)(次の乱数(ref 状態)%(ulong)段数幅);
        var 中間段数=Math.Max(1,総段数-1);

        var 選択メソッド=new List<string>(総段数);
        var すでにSelectManyありか=false;
        for(var 段=0;段<中間段数;段++){
            var 候補番号=(int)(次の乱数(ref 状態)%(ulong)中間一覧.Count);
            var 候補=中間一覧[候補番号];
            if(候補.メソッド名=="SelectMany"){
                if(すでにSelectManyありか){
                    候補=中間一覧[0];
                }else{
                    すでにSelectManyありか=true;
                }
            }
            選択メソッド.Add(候補.メソッド名);
        }

        var 終端番号=(int)(次の乱数(ref 状態)%(ulong)終端一覧.Count);
        選択メソッド.Add(終端一覧[終端番号].メソッド名);

        return 項目を構築(
            検証段階.ランダム無限探索,
            ソース種別,
            派生シード,
            試行番号,
            選択メソッド,
            null);
    }

    public static チェーン検証項目 項目を構築(
        検証段階 段階,
        入力ソース種別 ソース種別,
        ulong シード値,
        int 試行番号,
        IReadOnlyList<string>メソッド名一覧,
        string? 手書き理想コード){
        var クエリ式=拡張メソッドカタログ.クエリ式を構築("source",メソッド名一覧);
        var 表示式=$"{ソース種別}: {string.Join(" → ",メソッド名一覧)}";
        var 推定特性=理想特性推定と原因診断器.理想特性を推定(ソース種別,メソッド名一覧);
        var 識別子=$"S{(int)段階}_{ソース種別}_{試行番号:D4}_{string.Join("_",メソッド名一覧)}";
        return new チェーン検証項目(
            識別子,
            段階,
            ソース種別,
            シード値,
            試行番号,
            メソッド名一覧,
            表示式,
            クエリ式,
            手書き理想コード,
            推定特性);
    }

    public static ulong シードを派生(ulong 基準シード,ulong 番号){
        var z=基準シード+(番号+1UL)*0x9E3779B97F4A7C15UL;
        z=(z^(z>>30))*0xBF58476D1CE4E5B9UL;
        z=(z^(z>>27))*0x94D049BB133111EBUL;
        return z^(z>>31);
    }

    private static ulong 次の乱数(ref ulong 状態){
        if(状態==0UL){
            状態=0x9E3779B97F4A7C15UL;
        }
        状態^=状態>>12;
        状態^=状態<<25;
        状態^=状態>>27;
        return 状態*0x2545F4914F6CDD1DUL;
    }
}
