using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq.Optimization;

// ルール §3: Join/GroupJoin のキーセレクタ・ホイストとハッシュ結合分解。
public sealed class JoinHashing:OptimizationTestBase{
    [Fact,Trait("Category","Unit")]
    public static void ComputedOuterKey() => ComputedOuterKeyImpl();

    private static void ComputedOuterKeyImpl(){
        // o.Key+GetOffset() の GetOffset() は要素非依存 → キーセレクタからホイスト可能。
        var expected=SystemEnumerable.Join(Outers(),Inners(),
            o => o.Key+GetOffset(),i => i.Key,(o,i) => o.Id+i.Id).ToList();
        var actual=new Consumer<int>();
        Outers().Join(Inners(),o => o.Key+GetOffset(),i => i.Key,(o,i) => o.Id+i.Id).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }

    [Fact,Trait("Category","Unit")]
    public static void DuplicateInnerKeysMultiBucket() => DuplicateInnerKeysMultiBucketImpl();

    private static void DuplicateInnerKeysMultiBucketImpl(){
        {
            // 内側に同一キー複数（x,y がともに Key=1）。各 outer に対し全件展開されること。
            var expected=SystemEnumerable.Join(Outers(),Inners(),
                o => o.Key,i => i.Key,(o,i) => o.Id*100+i.Id).ToList();
            var actual=new Consumer<int>();
            Outers().Join(Inners(),o => o.Key,i => i.Key,(o,i) => o.Id*100+i.Id).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
        {
            // 一致ゼロ（空結果）。
            var expected=SystemEnumerable.Join(Outers(),Inners(),
                o => o.Key+1000,i => i.Key,(o,i) => o.Id*100+i.Id).ToList();
            var actual=new Consumer<int>();
            Outers().Join(Inners(),o => o.Key+1000,i => i.Key,(o,i) => o.Id*100+i.Id).Consume(actual);
            AssertSequenceEqual(expected,actual.Items);
        }
    }

    [Fact,Trait("Category","Unit")]
    public static void JoinAnonymousResult() => JoinAnonymousResultImpl();

    private static void JoinAnonymousResultImpl(){
        // 匿名型を出力 → record struct 中間表現の経路を通す。遅延列挙のまま構造比較する。
        var expected=SystemEnumerable.Join(Outers(),Inners(),o => o.Key,i => i.Key,(o,i) => new{ O=o.Id,I=i.Id });
        var actual=Outers().Join(Inners(),o => o.Key,i => i.Key,(o,i) => new{ O=o.Id,I=i.Id });
        AssertSequenceEqual(expected,actual);
    }

    [Fact,Trait("Category","Unit")]
    public static void GroupJoinWithEmptyGroups() => GroupJoinWithEmptyGroupsImpl();

    private static void GroupJoinWithEmptyGroupsImpl(){
        // a(Key0),d(Key0) は内側一致なし（空グループ）→ Sum 0。b,c は一致あり。
        var expected=SystemEnumerable.GroupJoin(Outers(),Inners(),
            o => o.Key,i => i.Key,(o,g) => o.Id*1000+SystemEnumerable.Sum(SystemEnumerable.Select(g,x => x.Id))).ToList();
        var actual=new Consumer<int>();
        Outers().GroupJoin(Inners(),o => o.Key,i => i.Key,(o,g) => o.Id*1000+g.Sum(x => x.Id)).Consume(actual);
        AssertSequenceEqual(expected,actual.Items);
    }
}
