using System.Runtime.InteropServices;

namespace Seamless.Collections;

internal struct PinnedSpanByteOwner:IDisposable {
    private readonly byte[] bytes;
    private GCHandle handle;
    public SpanByte SpanByte { get; }

    public PinnedSpanByteOwner(ReadOnlySpan<byte> bytes) {
        this.bytes=bytes.ToArray();
        this.handle=GCHandle.Alloc(this.bytes,GCHandleType.Pinned);
        this.SpanByte=SpanByte.FromPinnedSpan(this.bytes);
    }

    public void Dispose() {
        if(this.handle.IsAllocated)
            this.handle.Free();
    }
}