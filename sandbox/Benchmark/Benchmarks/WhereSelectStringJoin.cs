using Benchmark;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZLinq;
using ZLinq.Linq;

namespace Benchmark;

// https://github.com/Cysharp/ZLinq/issues/156

[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WhereSelectStringJoin
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

    static readonly User[] usersArray = _users.ToArray();

    [Benchmark]
    [BenchmarkCategory(Categories.LINQ)]
    public string LinqWhereSelectStringJoin() => //this.LinqWhereSelectStringJoinImpl();
    string.Join(",", LinqWhereSelectStringJoinImpl());
    private IEnumerable<string> LinqWhereSelectStringJoinImpl() => _users.Where(x => x.IsActive).Select(x => x.Name);

    [Benchmark]
    [BenchmarkCategory(Categories.ZLinq)]
    public string ZLinqWhereSelectStringJoin()
    {
        var query = _users.AsValueEnumerable().Where(x => x.IsActive).Select(x => x.Name);
        // query.JoinToString
        var a = query.JoinToString(",");
        return a;
    }

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
