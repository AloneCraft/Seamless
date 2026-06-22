namespace Test.Seamless.Devices;


public class MemoryOnlyDevice {

    [Fact, Trait("Category","Unit")]
    public void ctor_正常に生成できる() {
        using var device = new global::Seamless.Collections.Tsavorite.Devices.MemoryOnlyDevice(1L<<22);
        Assert.NotNull(device);
    }
    [Fact, Trait("Category","Unit")]
    public void ReadAsync_IOExceptionを投げる() {
        using var device = new global::Seamless.Collections.Tsavorite.Devices.MemoryOnlyDevice(1L<<22);
        Assert.Throws<IOException>((Action)(() =>
            device.ReadAsync(0,0ul,0,0u,null!,null!)));
    }
    [Fact, Trait("Category","Unit")]
    public void WriteAsync_OutOfMemoryExceptionを投げる() {
        using var device = new global::Seamless.Collections.Tsavorite.Devices.MemoryOnlyDevice(1L<<22);
        Assert.Throws<OutOfMemoryException>((Action)(() =>
            device.WriteAsync(0,0,0ul,0u,null!,null!)));
    }
    [Fact, Trait("Category","Unit")]
    public void RemoveSegmentAsync_例外を投げない() {
        using var device = new global::Seamless.Collections.Tsavorite.Devices.MemoryOnlyDevice(1L<<22);
        var completed = false;
        device.RemoveSegmentAsync(0,_ => { completed=true; },null!);
        Assert.False(completed);
    }
    [Fact, Trait("Category","Unit")]
    public void Dispose_安全に呼び出せる() {
        var device = new global::Seamless.Collections.Tsavorite.Devices.MemoryOnlyDevice(1L<<22);
        device.Dispose();
        device.Dispose();
    }
}

