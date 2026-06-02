using Benchmark;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZLinq;
using ZLinq.Linq;

namespace Benchmark;

// 参考: https://github.com/Cysharp/ZLinq/issues/156
//
// List<User> を対象にした Where → Select → ToArray の3ライブラリ比較。
// 文字列・オブジェクト操作の特性を評価するための実用的なクエリ例。
//
// ─ FLinq (Seamless) の private メソッドパターン ──────────────────────────────
//  SourceGenerator は「LINQ呼び出しを含むメソッドへの呼び出し」をインターセプトする。
//  [Benchmark] メソッドから直接 LINQ を書いても展開されるが、private ヘルパーに
//  切り出すことで「このメソッドが SG の展開対象」と明示的に表現できる。
//    FLinqWhereSelectStringJoin()     ← BDN の計測対象（[Benchmark]）
//    FLinqWhereSelectStringJoinImpl() ← SG がインターセプトして struct 展開
//
// ─ 配列 vs リスト ───────────────────────────────────────────────────────────
//  usersArray: Source が T[] の場合は TryGetSpan 最適化パスが使われる。
//  _users    : List<T> の場合は IEnumerator<T> 経由になる。
//  → 同一クエリで両方計測することでソース型の影響を分離できる。

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WhereSelectStringJoin
{
    private static readonly List<User> _users =
    [
        new() { Name = "John", Email = "john@example.com", IsActive = true  },
        new() { Name = "Jane", Email = "jane@example.com", IsActive = false },
        new() { Name = "Jim",  Email = "jim@example.com",  IsActive = true  },
        new() { Name = "Jill", Email = "jill@example.com", IsActive = false },
        new() { Name = "Jack", Email = "jack@example.com", IsActive = true  },
        new() { Name = "Jill", Email = "jill@example.com", IsActive = false },
        new() { Name = "Jack", Email = "jack@example.com", IsActive = true  },
        new() { Name = "Jill", Email = "jill@example.com", IsActive = false },
        new() { Name = "Jack", Email = "jack@example.com", IsActive = true  },
    ];

    static readonly User[] usersArray = _users.ToArray();

    // ── List<User> ソース ──────────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(Categories.LINQ)]
    public string[] LinqWhereSelectStringJoin()
    {
        return _users.Where(x => x.IsActive).Select(x => x.Name).ToArray();
    }

    // FLinq: public メソッドは BDN 計測のエントリ、private が SG インターセプト対象
    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public string[] FLinqWhereSelectStringJoin()
    {
        return this.FLinqWhereSelectStringJoinImpl();
    }
    private string[] FLinqWhereSelectStringJoinImpl() =>
        _users.Where(x => x.IsActive).Select(x => x.Name).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public string[] ZLinqWhereSelectStringJoin() =>
        _users.AsValueEnumerable().Where(x => x.IsActive).Select(x => x.Name).ToArray();

    // ── User[] ソース（TryGetSpan 最適化パスを測定）─────────────────────────

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public string[] LinqWhereSelectArray() =>
        usersArray.Where(x => x.IsActive).Select(x => x.Name).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.Seamless)]
    public string[] FLinqWhereSelectArray() => FLinqArrayImpl();
    private static string[] FLinqArrayImpl() =>
        usersArray.Where(x => x.IsActive).Select(x => x.Name).ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public string[] ZLinqWhereSelectArray() =>
        usersArray.AsValueEnumerable().Where(x => x.IsActive).Select(x => x.Name).ToArray();

    //[Benchmark]
    //[BenchmarkCategory(Categories.LINQ)]
    //public string ArrayLinqWhereSelectStringJoin() => string.Join(",", usersArray.Where(x => x.IsActive).Select(x => x.Name));

    //[Benchmark]
    //[BenchmarkCategory(Categories.ZLinq)]
    //public string ArrayZLinqWhereSelectStringJoin() => usersArray.AsValueEnumerable().Where(x => x.IsActive).Select(x => x.Name).JoinToString(',');

    //[Benchmark]
    //[BenchmarkCategory(Categories.LINQ)]
    //public void LinqWhereSelect()
    //{
    //    foreach (var item in _users.Where(x => x.IsActive).Select(x => x.Name))
    //    {
    //        Do(item);
    //    }
    //}
    //[Benchmark]
    //[BenchmarkCategory(Categories.ZLinq)]
    //public void ZLinqWhereSelect()
    //{
    //    foreach (var item in _users.AsValueEnumerable().Where(x => x.IsActive).Select(x => x.Name))
    //    {
    //        Do(item);
    //    }
    //}


    [MethodImpl(MethodImplOptions.NoInlining)]
    void Do(string _)
    {
    }

    public class User
    {
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public bool IsActive { get; set; } = default!;
    }
}
