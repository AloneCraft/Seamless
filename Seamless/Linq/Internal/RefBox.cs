using System;
using System.Buffers;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Seamless.Linq;
namespace Seamless.Linq;

// store struct to heap
internal sealed class RefBox<T>(T value):IDisposable
    where T : struct, IDisposable {
    private T value = value;
    private bool isDisposed;

    public ref T GetValueRef() => ref this.value;

    public void Dispose() {
        if(!this.isDisposed) {
            this.isDisposed=true;
            this.value.Dispose();
        }
    }
}
