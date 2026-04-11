using Seamless.Store;
using Tsavorite.core;
 
namespace Test.Seamless.Collections;
 
public class ManagedDictionary {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = global::Seamless.Const.MinObjectLogCapacity;

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
        using var store = Factory.Create().Create<int, int>(new ManagedTag(), new ManagedTag());
        using var subject = store.Create();

        var before = store.Address.TailAddress;
        for(var i = 0; i < count; i++) {
            subject.Add(i, i);
        }
        var after = store.Address.TailAddress;
        return after - before;
    }

    private static long Unmanaged(int count) {
        using var store = Factory.Create().Create<int, int>();
        using var subject = store.Create();

        var before = store.Address.TailAddress;
        for(var i = 0; i < count; i++) {
            subject.Add(i, i);
        }
        var after = store.Address.TailAddress;
        return after - before;
    }

    [Fact, Trait("Category", "Unit")]
    public void CreateUnmanagedKey_AddTryGet_Remove_動作する() {
        using var store = Factory.Create().Create<int, string>();
        using var subject = store.Create();

        subject.Add(1, "a");

        Assert.True(subject.TryGetValue(1, out var value));
        Assert.Equal("a", value);

        Assert.True(subject.Remove(1));
        Assert.False(subject.TryGetValue(1, out _));
    }

    [Fact, Trait("Category", "Unit")]
    public void CreateUnmanagedValue_AddTryGet_Remove_動作する() {
        using var store = Factory.Create().Create<string, int>();
        using var subject = store.Create();

        subject.Add("a", 1);

        Assert.True(subject.TryGetValue("a", out var value));
        Assert.Equal(1, value);

        Assert.True(subject.Remove("a"));
        Assert.False(subject.TryGetValue("a", out _));
    }

    [Fact, Trait("Category", "Unit")]
    public void CreateUnmanagedKey_Storage_AddTryGet_Remove_動作する() {
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder, "log.data"), deleteOnClose: true, capacity: TestMemorySize);
        using var objectLogDevice = Devices.CreateLogDevice(Path.Combine(folder + "_obj", "obj.data"), deleteOnClose: true, capacity: global::Seamless.Const.MinObjectLogCapacity);
        using var store = Factory.Create(MemorySize: TestMemorySize, CheckpointDir: folder).Create<int, string>(logDevice,objectLogDevice);
        using var subject = store.Create();

        subject.Add(1, "a");

        Assert.True(subject.TryGetValue(1, out var value));
        Assert.Equal("a", value);

        Assert.True(subject.Remove(1));
        Assert.False(subject.TryGetValue(1, out _));
    }

    [Fact, Trait("Category", "Unit")]
    public void CreateUnmanagedValue_Storage_AddTryGet_Remove_動作する() {
        var folder = CreateFolder();
        using var logDevice = Devices.CreateLogDevice(Path.Combine(folder, "log.data"), deleteOnClose: true, capacity: TestMemorySize);
        using var objectLogDevice = Devices.CreateLogDevice(Path.Combine(folder + "_obj", "obj.data"), deleteOnClose: true, capacity: global::Seamless.Const.MinObjectLogCapacity);
        using var store = Factory.Create(MemorySize: TestMemorySize, CheckpointDir: folder).Create<string, int>(logDevice,objectLogDevice);
        using var subject = store.Create();

        subject.Add("a", 1);

        Assert.True(subject.TryGetValue("a", out var value));
        Assert.Equal(1, value);

        Assert.True(subject.Remove("a"));
        Assert.False(subject.TryGetValue("a", out _));
    }
}
