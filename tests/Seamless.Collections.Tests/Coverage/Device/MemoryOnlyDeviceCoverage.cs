using Seamless.Collections;
using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class MemoryOnlyDevice {
    [Fact, Trait("Category","Coverage")]
    public void ctor_正常に生成できる() {
        using var device = new global::Seamless.Collections.MemoryOnlyDevice(1L<<22);
        Assert.NotNull(device);
    }
    [Fact, Trait("Category","Coverage")]
    public void ReadAsync_IOExceptionを投げる() {
        using var device = new global::Seamless.Collections.MemoryOnlyDevice(1L<<22);
        Assert.Throws<IOException>((Action)(() =>
            device.ReadAsync(0,0ul,default(IntPtr),0u,(DeviceIOCompletionCallback)null!,null!)));
    }
    [Fact, Trait("Category","Coverage")]
    public void WriteAsync_IOExceptionを投げる() {
        using var device = new global::Seamless.Collections.MemoryOnlyDevice(1L<<22);
        Assert.Throws<IOException>((Action)(() =>
            device.WriteAsync(default(IntPtr),0,0ul,0u,(DeviceIOCompletionCallback)null!,null!)));
    }
    [Fact, Trait("Category","Coverage")]
    public void RemoveSegmentAsync_例外を投げない() {
        using var device = new global::Seamless.Collections.MemoryOnlyDevice(1L<<22);
        var completed = false;
        device.RemoveSegmentAsync(0,_ => { completed=true; },null!);
        Assert.False(completed);
    }
    [Fact, Trait("Category","Coverage")]
    public void Dispose_安全に呼び出せる() {
        var device = new global::Seamless.Collections.MemoryOnlyDevice(1L<<22);
        device.Dispose();
        device.Dispose();
    }
}
