using Seamless.Collections;

namespace Test.Seamless.Collections;

public class ManagedDictionary {
    [Fact, Trait("Category", Unit)]
    public void AddTryGet_Remove_動作する() {
        using var store = Store.Create<int, long>();
        using var subject = store.Create();

        subject.Add(1, 100);

        Assert.True(subject.TryGetValue(1, out var value));
        Assert.Equal(100, value);

        Assert.True(subject.Remove(1));
        Assert.False(subject.TryGetValue(1, out _));
    }

    [Fact, Trait("Category", Unit)]
    public void Remove_KeyValuePair_値一致時のみ削除される() {
        using var store = Store.Create<int, int>();
        using var subject = store.Create();

        subject.Add(1, 10);

        Assert.False(subject.Remove(new KeyValuePair<int, int>(1, 20)));
        Assert.True(subject.ContainsKey(1));

        Assert.True(subject.Remove(new KeyValuePair<int, int>(1, 10)));
        Assert.False(subject.ContainsKey(1));
    }

    [Fact, Trait("Category", Unit)]
    public void Add_int_int_同一件数追加時はManagedのログ消費がUnmanaged以上() {
        const int count = 200;

        var managedUsed = Managed(count);
        var unmanagedUsed = Unmanaged(count);

        Assert.True(
            managedUsed >= unmanagedUsed,
            $"managedUsed: {managedUsed}, unmanagedUsed: {unmanagedUsed}"
        );
    }

    private static long Managed(int count) {
        using var store = Store.Create<int, int>();
        using var subject = store.Create();

        var before = store.Address.TailAddress;
        for(var i = 0; i < count; i++) {
            subject.Add(i, i);
        }
        var after = store.Address.TailAddress;
        return after - before;
    }

    private static long Unmanaged(int count) {
        using var store = Store.CreateUnmanaged<int, int>();
        using var subject = store.Create();

        var before = store.Address.TailAddress;
        for(var i = 0; i < count; i++) {
            subject.Add(i, i);
        }
        var after = store.Address.TailAddress;
        return after - before;
    }
}
