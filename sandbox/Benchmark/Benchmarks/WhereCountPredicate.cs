using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Cathei.LinqGen;
using SpanLinq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZLinq;
using ZLinq.Linq;

namespace Benchmark;

// https://github.com/Cysharp/ZLinq/issues/156

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WhereCountPredicate
{
    private static readonly List<User> _users = new List<User>() {
        new User() { Name = "John", Email = "john@example.com", IsActive = true },
        new User() { Name = "Jane", Email = "jane@example.com", IsActive = false },
        new User() { Name = "Jim", Email = "jim@example.com", IsActive = true },
        new User() { Name = "Jill", Email = "jill@example.com", IsActive = false },
        new User() { Name = "Jack", Email = "jack@example.com", IsActive = true },
        new User() { Name = "Jill", Email = "jill@example.com", IsActive = false },
        new User() { Name = "Jack", Email = "jack@example.com", IsActive = true },
        new User() { Name = "Jill", Email = "jill@example.com", IsActive = false },
        new User() { Name = "Jack", Email = "jack@example.com", IsActive = true },
    };

    //[Benchmark]
    //public string StringJoinLinq() => string.Join(",", _users.Where(x => x.IsActive).Select(x => x.Name));

    //[Benchmark]
    //public string StringJoinZLinq() => _users.AsValueEnumerable().Where(x => x.IsActive).Select(x => x.Name).JoinToString(',');

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public int LinqWhereCount() => _users.Where(x => x.IsActive).Count();

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int ZLinqWhereCount() => _users.AsValueEnumerable().Where(x => x.IsActive).Count();

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public int LinqCountPredicate() => _users.Count(x => x.IsActive);

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public int ZLinqCountPredicate() => _users.AsValueEnumerable().Count(x => x.IsActive);

    public class User
    {
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public bool IsActive { get; set; } = default!;
    }
}
