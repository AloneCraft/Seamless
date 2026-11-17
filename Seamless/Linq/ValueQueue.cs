using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Seamless.Linq;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Seamless.Linq;

/// <summary>
/// 小ウィンドウ（capacity <= 16）は [InlineArray(16)] のスタック/構造体内バッファ、
/// それを超える場合は ArrayPool&lt;T&gt;.Shared から借りた配列で回すリングキュー。
/// SkipLast/TakeLast のような「直近 N 件のスライディングウィンドウ」向けで、
/// Count/Enqueue/Dequeue は System.Collections.Generic.Queue&lt;T&gt; と同義。
/// PooledArray&lt;T&gt; と同じく struct なので、コピーやボクシングをすると
/// 同じ配列を複数回 ArrayPool へ返す危険がある。単一の所有者が持ち、
/// 借りた後はコピーせずそのまま使って Dispose すること。
/// default(ValueQueue&lt;T&gt;) は items が null で、Dispose は no-op になる
/// （生成コードは「未初期化でも無条件に Dispose を呼べる」性質に依存している）。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct ValueQueue<T> : IDisposable {
    [InlineArray(16)]
    private struct InlineBuffer {
        public T _element0;
    }

    private InlineBuffer inlineBuffer;
    private T[]? poolItems;
    private int head;
    private int tail;
    private int size;
    private int capacity;

    public ValueQueue(int capacity) {
        this.head = 0;
        this.tail = 0;
        this.size = 0;
        this.capacity = capacity;
        if(capacity <= 16) {
            this.poolItems = null;
        } else {
            this.poolItems = ArrayPool<T>.Shared.Rent(capacity);
        }
    }

    public int Count => this.size;

    private Span<T> Buffer {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.poolItems is not null
            ? this.poolItems.AsSpan()
            : MemoryMarshal.CreateSpan(ref this.inlineBuffer._element0, 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(T item) {
        var buffer = this.Buffer;
        if(this.size == buffer.Length) {
            this.Grow();
            buffer = this.Buffer;
        }

        buffer[this.tail] = item;
        this.tail = (this.tail + 1) % buffer.Length;
        this.size++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Dequeue() {
        if(this.size == 0) Throw();

        var buffer = this.Buffer;
        T removed = buffer[this.head];
        if(RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
            buffer[this.head] = default!;
        }
        this.head = (this.head + 1) % buffer.Length;
        this.size--;
        return removed;
    }

    private static void Throw() => throw new InvalidOperationException("Queue is empty.");

    private void Grow() {
        var oldBuffer = this.Buffer;
        var newCap = Math.Max(32, oldBuffer.Length * 2);
        T[] newArray = ArrayPool<T>.Shared.Rent(newCap);
        if(this.size > 0) {
            if(this.head < this.tail) {
                oldBuffer.Slice(this.head, this.size).CopyTo(newArray.AsSpan());
            } else {
                var firstPart = oldBuffer.Length - this.head;
                oldBuffer.Slice(this.head, firstPart).CopyTo(newArray.AsSpan());
                oldBuffer.Slice(0, this.tail).CopyTo(newArray.AsSpan(firstPart));
            }
        }
        if(this.poolItems != null) {
            ArrayPool<T>.Shared.Return(this.poolItems, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
        this.poolItems = newArray;
        this.head = 0;
        this.tail = this.size;
    }

    public void Dispose() {
        if(this.poolItems != null) {
            ArrayPool<T>.Shared.Return(this.poolItems, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            this.poolItems = null;
        }
    }
}
