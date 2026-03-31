
namespace Test.Seamless.Collections;


public partial class MemoryOnlyDevice {
    public partial void ctor_正常に生成できる(){
        using var device = new global::Seamless.Devices.MemoryOnlyDevice(1L<<22);
        Assert.NotNull(device);
    }
    public partial void ReadAsync_IOExceptionを投げる() {
        using var device = new global::Seamless.Devices.MemoryOnlyDevice(1L<<22);
        Assert.Throws<IOException>((Action)(() =>
            device.ReadAsync(0,0ul,0,0u,null!,null!)));
    }
    public partial void WriteAsync_IOExceptionを投げる() {
        using var device = new global::Seamless.Devices.MemoryOnlyDevice(1L<<22);
        Assert.Throws<IOException>((Action)(() =>
            device.WriteAsync(0,0,0ul,0u,null!,null!)));
    }
    public partial void RemoveSegmentAsync_例外を投げない() {
        using var device = new global::Seamless.Devices.MemoryOnlyDevice(1L<<22);
        var completed = false;
        device.RemoveSegmentAsync(0,_ => { completed=true; },null!);
        Assert.False(completed);
    }
    public partial void Dispose_安全に呼び出せる() {
        var device = new global::Seamless.Devices.MemoryOnlyDevice(1L<<22);
        device.Dispose();
        device.Dispose();
    }
}
