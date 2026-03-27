using Common;

using Tsavorite.core;

using Xunit;
namespace Seamless.Collections.Tests;

public class TestMemoryOnlyDevice {
    [Fact]
    public void ReadAsync() {
        using var device = new MemoryOnlyDevice(-1);
        DeviceIOCompletionCallback callback = (errorCode,numBytes,context) => {};
        Assert.Throws<IOException>(() => device.ReadAsync(0,0,IntPtr.Zero,0,callback,null!));
    }
    [Fact]
    public void WriteAsync() {
        using var device = new MemoryOnlyDevice(-1);
        DeviceIOCompletionCallback callback = (errorCode,numBytes,context) => {};
        Assert.Throws<IOException>(() => device.WriteAsync(IntPtr.Zero,-1,0,0,callback,null!));
    }
    [Fact]
    public void RemoveSegmentAsync_NullCallback_DoesNotThrow() {
        // Arrange
        using var device = new MemoryOnlyDevice(-1);
        // Act & Assert (no exception)
        device.RemoveSegmentAsync(0,null!,null!);
    }
    [Fact]
    public void Dispose_DoesNotThrow() {
        // Arrange
        var device = new MemoryOnlyDevice(-1);
        // Act & Assert
        device.Dispose();
    }
}
