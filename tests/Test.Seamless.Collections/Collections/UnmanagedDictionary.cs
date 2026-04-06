using Seamless.Collections;

namespace Test.Seamless.Collections;

public class UnmanagedDictionary {
    [Fact, Trait("Category", Unit)]
    public void AddTryGet_Remove_動作する() {
        using var store = Store.CreateUnmanaged<int, long>();
        using var subject = store.Create();

        subject.Add(1, 100);

        Assert.True(subject.TryGetValue(1, out var value));
        Assert.Equal(100, value);

        Assert.True(subject.Remove(1));
        Assert.False(subject.TryGetValue(1, out _));
    }

    [Fact, Trait("Category", Unit)]
    public void Remove_KeyValuePair_値一致時のみ削除される() {
        using var store = Store.CreateUnmanaged<int, int>();
        using var subject = store.Create();

        subject.Add(1, 10);

        Assert.False(subject.Remove(new KeyValuePair<int, int>(1, 20)));
        Assert.True(subject.ContainsKey(1));

        Assert.True(subject.Remove(new KeyValuePair<int, int>(1, 10)));
        Assert.False(subject.ContainsKey(1));
    }
}
