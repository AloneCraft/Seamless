using System.Text;

namespace Sample.FLinqChainVerifier.Core;

/// <summary>1つの拡張メソッド演算子の記述子。</summary>
public sealed record 拡張メソッド記述子(
    string メソッド名,
    メソッド分類 分類,
    string 呼び出しコード,
    bool 終端か,
    bool 第2ソースを使うか,
    bool 順序を変更または非決定にするか,
    string 理想処理説明);

/// <summary>
/// 入力ソースから呼び出せる全拡張メソッドのカタログと、
/// 隣接2メソッドペア(A.B, B.C, C.D)の組み合わせ特性知識ベース。
/// </summary>
public static class 拡張メソッドカタログ{
    private static readonly 拡張メソッド記述子[]中間演算子配列=[
        new("Where",メソッド分類.フィルタ射影,".Where(x=>(x&1)==0)",false,false,false,"if((x&1)!=0)continue;"),
        new("Select",メソッド分類.フィルタ射影,".Select(x=>x*3+1)",false,false,false,"var y=x*3+1;"),
        new("SelectMany",メソッド分類.平坦化結合,".SelectMany(x=>second)",false,true,false,"foreach(var sub in second)"),
        new("Join",メソッド分類.平坦化結合,".Join(second,x=>x&15,y=>y&15,(x,y)=>x+y)",false,true,false,"Lookup構築+内側走査"),
        new("GroupJoin",メソッド分類.平坦化結合,".GroupJoin(second,x=>x&15,y=>y&15,(x,g)=>x+g.Count())",false,true,false,"Lookup構築+グループ件数結合"),
        new("Take",メソッド分類.状態ストリーム,".Take(12)",false,false,false,"if(taken>=12)break;"),
        new("Skip",メソッド分類.状態ストリーム,".Skip(4)",false,false,false,"if(skipped<4){skipped++;continue;}"),
        new("TakeLast",メソッド分類.状態ストリーム,".TakeLast(8)",false,false,false,"末尾8件スライスまたはリングバッファ"),
        new("SkipLast",メソッド分類.状態ストリーム,".SkipLast(4)",false,false,false,"末尾4件除外スライスまたはキュー"),
        new("TakeWhile",メソッド分類.状態ストリーム,".TakeWhile(x=>x<200)",false,false,false,"if(!(x<200))break;"),
        new("SkipWhile",メソッド分類.状態ストリーム,".SkipWhile(x=>x<20)",false,false,false,"条件成立中は読み飛ばし"),
        new("Distinct",メソッド分類.状態ストリーム,".Distinct()",false,false,false,"if(!set.Add(x))continue;"),
        new("DistinctBy",メソッド分類.状態ストリーム,".DistinctBy(x=>x&31)",false,false,false,"if(!set.Add(x&31))continue;"),
        new("Union",メソッド分類.状態ストリーム,".Union(second)",false,true,true,"HashSetで第1・第2ソース重複排除"),
        new("Intersect",メソッド分類.状態ストリーム,".Intersect(second)",false,true,true,"第2ソースHashSet照合"),
        new("Except",メソッド分類.状態ストリーム,".Except(second)",false,true,true,"第2ソースHashSet除外"),
        new("UnionBy",メソッド分類.状態ストリーム,".UnionBy(second,x=>x&31)",false,true,true,"キーHashSetで重複排除連結"),
        new("IntersectBy",メソッド分類.状態ストリーム,".IntersectBy(second,x=>x&31)",false,true,true,"キーHashSetで積集合"),
        new("ExceptBy",メソッド分類.状態ストリーム,".ExceptBy(second,x=>x&31)",false,true,true,"キーHashSetで差集合"),
        new("Concat",メソッド分類.平坦化結合,".Concat(second)",false,true,false,"第1ループ後に第2ループ直結"),
        new("Append",メソッド分類.状態ストリーム,".Append(77)",false,false,false,"ループ完了後に末尾1件追加"),
        new("Prepend",メソッド分類.状態ストリーム,".Prepend(11)",false,false,false,"ループ開始前に先頭1件出力"),
        new("DefaultIfEmpty",メソッド分類.状態ストリーム,".DefaultIfEmpty(0)",false,false,false,"空のときのみ既定値0を1件出力"),
        new("Reverse",メソッド分類.順序変更,".Reverse()",false,false,false,"Span逆順ループまたはバッファ逆走査"),
        new("OrderBy",メソッド分類.順序変更,".OrderBy(x=>x&31)",false,false,false,"インデックス収集+SortIndexes"),
        new("OrderByDescending",メソッド分類.順序変更,".OrderByDescending(x=>x&31)",false,false,false,"インデックス収集+降順SortIndexes"),
        new("GroupBy_Sum",メソッド分類.グループ集約,".GroupBy(x=>x&15,x=>x).Select(g=>g.Key+g.Sum())",false,false,true,"DictionarySlimによる1パス直接集計"),
        new("GroupBy_Count",メソッド分類.グループ集約,".GroupBy(x=>x&15).Select(g=>g.Key*10+g.Count())",false,false,true,"DictionarySlimによる1パス件数集計"),
        new("GroupBy_KeyOnly",メソッド分類.グループ集約,".GroupBy(x=>x&15).Select(g=>g.Key)",false,false,true,"HashSetによる1パスDistinct降格"),
        new("Zip",メソッド分類.平坦化結合,".Zip(second,(x,y)=>x+y)",false,true,false,"2ソース同時インデックス走査"),
    ];

    private static readonly 拡張メソッド記述子[]終端演算子配列=[
        new("ToArray",メソッド分類.終端コレクション,".ToArray()",true,false,false,"結果配列への格納"),
        new("ToList",メソッド分類.終端コレクション,".ToList()",true,false,false,"結果Listへの格納"),
        new("ToHashSet",メソッド分類.終端コレクション,".ToHashSet()",true,false,true,"結果HashSetへの格納"),
        new("Count",メソッド分類.終端スカラー,".Count()",true,false,false,"件数カウントまたはO(1)長取得"),
        new("Count_Pred",メソッド分類.終端スカラー,".Count(x=>(x&1)==0)",true,false,false,"条件一致件数のインライン加算"),
        new("LongCount",メソッド分類.終端スカラー,".LongCount()",true,false,false,"long件数カウント"),
        new("Sum",メソッド分類.終端スカラー,".Sum(x=>(long)x)",true,false,false,"longアキュムレータへの直接加算"),
        new("Min",メソッド分類.終端スカラー,".Min()",true,false,false,"最小値の1パス比較更新"),
        new("Max",メソッド分類.終端スカラー,".Max()",true,false,false,"最大値の1パス比較更新"),
        new("MinBy",メソッド分類.終端スカラー,".MinBy(x=>x&31)",true,false,false,"キー最小要素の1パス保持"),
        new("MaxBy",メソッド分類.終端スカラー,".MaxBy(x=>x&31)",true,false,false,"キー最大要素の1パス保持"),
        new("Average",メソッド分類.終端スカラー,".Average(x=>(long)x)",true,false,false,"合計と件数の1パス同時集計"),
        new("Aggregate",メソッド分類.終端スカラー,".Aggregate(0L,(acc,x)=>acc+x)",true,false,false,"シード値からの1パス畳み込み"),
        new("Any",メソッド分類.終端スカラー,".Any()",true,false,false,"1件見つかった時点で即時true短絡"),
        new("Any_Pred",メソッド分類.終端スカラー,".Any(x=>x%7==0)",true,false,false,"条件成立時点で即時true短絡"),
        new("All",メソッド分類.終端スカラー,".All(x=>x>=-10000)",true,false,false,"条件不成立時点で即時false短絡"),
        new("Contains",メソッド分類.終端スカラー,".Contains(10)",true,false,false,"対象値一致時点で即時true短絡"),
        new("First",メソッド分類.終端スカラー,".First()",true,false,false,"先頭1件取得で即時break短絡"),
        new("FirstOrDefault",メソッド分類.終端スカラー,".FirstOrDefault()",true,false,false,"先頭1件取得または既定値0"),
        new("Last",メソッド分類.終端スカラー,".Last()",true,false,false,"末尾要素取得または1パス最終値保持"),
        new("LastOrDefault",メソッド分類.終端スカラー,".LastOrDefault()",true,false,false,"末尾要素取得または既定値0"),
        new("ElementAt",メソッド分類.終端スカラー,".ElementAt(2)",true,false,false,"添字2の直接参照またはカウンタ短絡"),
        new("ElementAtOrDefault",メソッド分類.終端スカラー,".ElementAtOrDefault(2)",true,false,false,"添字2の直接参照または範囲外0"),
        new("SequenceEqual",メソッド分類.終端スカラー,".SequenceEqual(second)",true,true,false,"2シーケンスの要素逐次比較"),
        new("CountBy_ToArray",メソッド分類.終端コレクション,".CountBy(x=>x&15).Select(kv=>kv.Key+kv.Value).ToArray()",true,false,true,"挿入順ハッシュ表での件数集計"),
        new("AggregateBy_ToArray",メソッド分類.終端コレクション,".AggregateBy(x=>x&15,0,(acc,x)=>acc+x).Select(kv=>kv.Key+kv.Value).ToArray()",true,false,true,"挿入順ハッシュ表での畳み込み集計"),
    ];

    private static readonly Dictionary<string,拡張メソッド記述子>名前辞書=構築する();

    private static Dictionary<string,拡張メソッド記述子>構築する(){
        var 辞書=new Dictionary<string,拡張メソッド記述子>(StringComparer.Ordinal);
        foreach(var 項目 in 中間演算子配列){
            辞書[項目.メソッド名]=項目;
        }
        foreach(var 項目 in 終端演算子配列){
            辞書[項目.メソッド名]=項目;
        }
        return 辞書;
    }

    public static IReadOnlyList<拡張メソッド記述子>中間演算子一覧=>中間演算子配列;
    public static IReadOnlyList<拡張メソッド記述子>終端演算子一覧=>終端演算子配列;

    public static bool 登録済みか(string メソッド名){
        return 名前辞書.ContainsKey(メソッド名);
    }

    public static 拡張メソッド記述子 取得(string メソッド名){
        if(名前辞書.TryGetValue(メソッド名,out var 記述子)){
            return 記述子;
        }else{
            throw new KeyNotFoundException($"未登録の拡張メソッド名です: {メソッド名}");
        }
    }

    /// <summary>
    /// 隣接する2つのメソッド(A.B または B.C)の組み合わせから、理想コードの遷移特性を導出する。
    /// </summary>
    public static ペア遷移特性 ペア遷移を取得(string 前段名,string 後段名){
        var 前段=取得(前段名);
        var 後段=取得(後段名);

        // 1. Stateless (Where / Select) 同士の組み合わせ
        if(前段.分類==メソッド分類.フィルタ射影&&後段.分類==メソッド分類.フィルタ射影){
            return new ペア遷移特性(
                前段名,
                後段名,
                "Stateless関数合成・1パスインライン融合",
                0,
                0,
                true,
                "同一forループ内でif分岐と変数変換を直列実行(中間構造体・デリゲート呼び出しなし)",
                $"{前段名}と{後段名}の間で列挙子構造体(TryGetNext)が入れ子になっている可能性があります。",
                "PipelineEnumerable.Loop.cs の直接Span/Indexループ条件でStateless段の連続を1本のforループへ展開してください。");
        }

        // 2. バリア(OrderBy / Reverse / Distinct) -> Where (Predicate Pushdown)
        if((前段.分類==メソッド分類.順序変更||前段名.StartsWith("Distinct",StringComparison.Ordinal))&&後段名=="Where"){
            return new ペア遷移特性(
                前段名,
                後段名,
                "バリア前Predicate Pushdown(前倒しフィルタ)",
                0,
                0,
                true,
                $"{前段名}のバッファリング・ソート前にWhereの条件判定を押し下げ、対象要素数を事前に削減",
                $"{前段名}で全件をバッファ・ソートした後に{後段名}でフィルタリングしています。",
                "PipelineBuilder.Stages.cs の Predicate Pushdown 規則で、順序・一意性を壊さないWhereをバリア前へ移動してください。");
        }

        // 3. OrderBy / OrderByDescending -> Take / First / ElementAt (Top-K 部分ソート融合)
        if((前段名=="OrderBy"||前段名=="OrderByDescending")&&(後段名=="Take"||後段名=="First"||後段名=="FirstOrDefault"||後段名=="ElementAt"||後段名=="ElementAtOrDefault")){
            return new ペア遷移特性(
                前段名,
                後段名,
                "Top-K部分ソート融合(PartialSortIndexes)",
                0,
                8,
                true,
                $"全N件の完全ソートを省き、上位K件のみをPartialSortIndexes / O(N)選択で抽出して即時出力",
                $"{前段名}で全要素を完全ソートした後に{後段名}で先頭要素のみを取り出しています。",
                "OrderBy.cs および PipelineEnumerable.OrderLoop.cs で後続のTake/First/ElementAtを検出し、Top-K部分ソート経路を有効化してください。");
        }

        // 4. Join / GroupJoin -> 後段
        if(前段名=="Join"||前段名=="GroupJoin"){
            var ループ数 = 2;
            var 追加メモリ = 6400L;
            var 説明 = $"{前段名}の内側ソースLookup探索表を構築し、外側走査ループ内で結合要素を後段へ供給";
            return new ペア遷移特性(
                前段名,
                後段名,
                $"{前段名}内側Lookup構築+走査融合",
                ループ数,
                追加メモリ,
                true,
                説明,
                $"{前段名}と{後段名}の間で中間列挙子または不要バッファが生成されている疑い。",
                "Join.cs のインライン化および直接Spanループを活用してください。");
        }

        // 5. GroupBy -> 集計 / OrderBy / Take
        if(前段名.StartsWith("GroupBy",StringComparison.Ordinal)){
            if(前段名=="GroupBy_KeyOnly"){
                return new ペア遷移特性(
                    前段名,
                    後段名,
                    "GroupByキーのみ参照のDistinctストリーミング降格",
                    0,
                    16,
                    true,
                    "GroupByの全件バリア(ToLookup)を作らず、HashSet<TKey>.Addによる1パスストリーミングで後段へ直結",
                    "グループ要素列を使わないGroupByでToLookupのリスト構築が発生しています。",
                    "PipelineBuilder.Stages.cs でグループ要素未使用のGroupByを検出し、Distinct相当の1パス処理へ降格させてください。");
            }
            if(後段.分類==メソッド分類.順序変更){
                return new ペア遷移特性(
                    前段名,
                    後段名,
                    "GroupBy1パス集計+ソートバッファ直接転送",
                    1,
                    24,
                    false,
                    "第1ループでDictionarySlimへ1パス集計し、確定したエントリ配列を中間Listコピーなしで直接ソート",
                    $"{前段名}の結果を一度List<T>へコピーしてから{後段名}のソートバッファへ詰め直しています。",
                    "PipelineEnumerable.StageLoops.cs の BuildGroupByOrderLoop で集計済みスロット配列を直接ソート対象へ渡してください。");
            }
            return new ペア遷移特性(
                前段名,
                後段名,
                "GroupBy 1パス直接集計融合(GroupByAggregateFusion)",
                1,
                24,
                false,
                "要素リスト(List<TElement>)を確保せず、DictionarySlimのスロット上で直接Sum/Countを更新して次段へ流す",
                $"{前段名}でToLookupによる要素リスト確保が発生しているか、{後段名}との接続で中間オブジェクトが生成されています。",
                "GroupByAggregateFusion.cs で対象セレクタを集計スロット更新へ融合し、第2ループで直接後段処理を実行してください。");
        }

        // 5. ストリーミング前段 -> 終端スカラー(Sum / Count / Any / First / Min / Max 等)
        if(前段.分類!=メソッド分類.順序変更&&前段.分類!=メソッド分類.グループ集約&&後段.分類==メソッド分類.終端スカラー){
            return new ペア遷移特性(
                前段名,
                後段名,
                "終端アキュムレータ・短絡インライン融合",
                0,
                0,
                true,
                $"前段({前段名})のループ最深部に{後段名}の集計変数更新またはbreak短絡を直接埋め込み(確保メモリ0)",
                $"{前段名}のパイプライン列挙子を生成してから外側の{後段名}ループでMoveNext/TryGetNextを呼んでいます。",
                "PipelineEnumerable.Loop.cs および 각スカラーFragmentのCompute生成で、前段ループ内部へアキュムレータをインライン展開してください。");
        }

        // 6. ストリーミング前段 -> ToArray / ToList
        if(前段.分類!=メソッド分類.順序変更&&前段.分類!=メソッド分類.グループ集約&&後段.分類==メソッド分類.終端コレクション){
            return new ペア遷移特性(
                前段名,
                後段名,
                "単一パス直接マテリアライズ融合",
                0,
                8,
                true,
                $"前段({前段名})の1パスループから直接ArrayPoolバッファまたは事前容量指定コレクションへ書き込む",
                $"{前段名}と{後段名}の間で複数回のバッファ再確保や列挙子ラッパー経由のコピーが発生しています。",
                "ToArray/ToListの消費経路でソース長上限を活用した事前確保またはArrayPool直接書き込みを適用してください。");
        }

        // 7. 順序変更バリア(OrderBy / Reverse) -> 後段
        if(前段.分類==メソッド分類.順序変更){
            return new ペア遷移特性(
                前段名,
                後段名,
                "ソート/反転バリア後の要素継続インラインループ",
                1,
                8,
                false,
                $"第1フェーズで{前段名}のインデックスを整列し、第2フェーズのループ内で{後段名}をインライン実行",
                $"{前段名}のソート結果を一度配列化してから別ループで{後段名}を回しています。",
                "PipelineEnumerable.OrderLoop.cs でソート済みインデックス走査ループの内部に後続ステージを直接展開してください。");
        }

        // 8. 既定のストリーミングまたはバリア接続
        var ループ増分=0;
        var 追加バイト=0L;
        var 完全インライン=true;
        if(後段.分類==メソッド分類.順序変更||後段.分類==メソッド分類.グループ集約){
            ループ増分=1;
            追加バイト=16;
            完全インライン=false;
        }else if(前段.メソッド名.Contains("Union",StringComparison.Ordinal)||前段.メソッド名.Contains("Intersect",StringComparison.Ordinal)||前段.メソッド名.Contains("Except",StringComparison.Ordinal)||前段.メソッド名.Contains("Distinct",StringComparison.Ordinal)){
            追加バイト=12;
        }
        return new ペア遷移特性(
            前段名,
            後段名,
            $"{前段名}→{後段名} パイプライン融合",
            ループ増分,
            追加バイト,
            完全インライン,
            $"{前段.理想処理説明} の直後に {後段.理想処理説明} を連結",
            $"{前段名}と{後段名}の境界で中間列挙子または一時バッファが生成されています。",
            "PipelineEnumerable.StageLoops.cs で両ステージ間の状態遷移を同一ループ内へ統合してください。");
    }

    /// <summary>
    /// メソッド名リストからC#のメソッドチェーン呼び出し式を組み立てる。
    /// </summary>
    public static string クエリ式を構築(string ソース変数名,IReadOnlyList<string>メソッド名一覧){
        var ビルダー=new StringBuilder(ソース変数名);
        foreach(var 名前 in メソッド名一覧){
            var 記述子=取得(名前);
            ビルダー.Append(記述子.呼び出しコード);
        }
        return ビルダー.ToString();
    }
}
