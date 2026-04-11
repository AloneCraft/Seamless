using Seamless.Stores;
namespace Test.Seamless.Collections;

public class UnmanagedDictionary {
    [Fact, Trait("Category", "Unit")]
    public void AddTryGet_Remove_動作する() {
        using var store = Factory.Create().Create<int, long>();
        using var subject = store.Create();

        subject.Add(1, 100);

        Assert.True(subject.TryGetValue(1, out var value));
        Assert.Equal(100, value);

        Assert.True(subject.Remove(1));
        Assert.False(subject.TryGetValue(1, out _));
    }

    [Fact, Trait("Category", "Unit")]
    public void Remove_KeyValuePair_値一致時のみ削除される() {
        using var store = Factory.Create().Create<int, int>();
        using var subject = store.Create();

        subject.Add(1, 10);

        Assert.False(subject.Remove(new KeyValuePair<int, int>(1, 20)));
        Assert.True(subject.ContainsKey(1));

        Assert.True(subject.Remove(new KeyValuePair<int, int>(1, 10)));
        Assert.False(subject.ContainsKey(1));
    }

    [Fact, Trait("Category", "Unit")]
    public void Add_int_int_同一件数追加時はUnmanagedのログ消費がManaged以下() {
        const int count = 200;

        var unmanagedUsed = UnmanagedUnmanaged(count);
        var managedUsed = ManagedManaged(count);
        //1.1.1 unmanagedUsed=3200,managedUsed=4800
        //2.0   unmanagedUsed=4864,managedUsed=4864
        Assert.True(
            unmanagedUsed <= managedUsed,
            $"unmanagedUsed: {unmanagedUsed}, managedUsed: {managedUsed}"
        );
    }
    [Fact, Trait("Category", "Unit")]
    public void Add_int_string_同一件数追加時はUnmanagedのログ消費がManaged以下() {
        const int count = 200;

        var unmanagedUsed = UnmanagedUnmanaged(count);
        var managedUsed = ManagedManaged(count);
        //1.1.1 unmanagedUsed=3200,managedUsed=4800
        //2.0   unmanagedUsed=4864,managedUsed=4864
        Assert.True(
            unmanagedUsed <= managedUsed,
            $"unmanagedUsed: {unmanagedUsed}, managedUsed: {managedUsed}"
        );
    }

    private static long UnmanagedUnmanaged(int count) {
        using var store = Factory.Create().Create<int, int>();
        using var subject = store.Create();
        var before = store.Address.TailAddress;
        for(var i = 0; i < count; i++) {
            subject.Add(i, i);
        }
        var after = store.Address.TailAddress;
        return after - before;
    }

    private static long ManagedManaged(int count) {
        using var store = Factory.Create().Create<int,int>(new ManagedTag(),new ManagedTag());
        using var subject = store.Create();
        var before = store.Address.TailAddress;
        for(var i = 0; i < count; i++) {
            subject.Add(i, i);
        }
        var after = store.Address.TailAddress;
        return after - before;
    }
}
