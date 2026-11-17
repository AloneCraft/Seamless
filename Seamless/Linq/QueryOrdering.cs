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
using Seamless.Execution;
namespace Seamless.Linq;

/// <summary>生成queryが通常LINQへ委ねる整列の配列とキーpayloadを、構築前に共通予算へ計上する。</summary>
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public static class QueryOrdering{
    public interface IKeyBudget{void ReserveRows(long count);}
    public sealed class InputBudget<T>:IDisposable,IKeyBudget{
        private readonly ExecutionScope scope;
        private readonly ResourceReservation memory;
        private readonly IKeyBudget[] keys;
        private readonly global::Seamless.Execution.Codecs.IAdapter<T>? codec;
        private readonly long metadata;
        private long count,payload,reserved;
        private bool disposed;
        public InputBudget(ExecutionScope scope,IKeyBudget[] keys){
            ArgumentNullException.ThrowIfNull(scope);ArgumentNullException.ThrowIfNull(keys);scope.ThrowIfUnavailable();this.scope=scope;this.keys=keys;
            if(RuntimeHelpers.IsReferenceOrContainsReferences<T>()&&scope.Codecs.CanEncode<T>()){this.codec=scope.Codecs.Get<T>();}
            this.metadata=checked(128L+keys.Length*32L);this.memory=scope.Budget.ReserveMemory(this.metadata);
            try{scope.Own(this);}catch{this.memory.Dispose();throw;}
        }
        public IEnumerable<T> Read(IEnumerable<T> source){
            ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();ArgumentNullException.ThrowIfNull(source);
            foreach(var value in source){
                ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();
                var count=checked(this.count+1);var payload=this.payload;
                if(RuntimeHelpers.IsReferenceOrContainsReferences<T>()){payload=checked(payload+64L);if(this.codec is not null){payload=checked(payload+4L*this.codec.GetEncodedLength(value));}}
                // BCLの有界な拡大用配列・入力コピー・位置mapを、要素を渡す前に覆う。
                var reserved=Math.Max(this.reserved,count);
                this.memory.Resize(this.RequiredBytes(reserved,payload));
                foreach(var key in this.keys){key.ReserveRows(count);}
                this.count=count;this.reserved=reserved;this.payload=payload;yield return value;
            }
        }
        public void ReserveRows(long count){
            ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();ArgumentOutOfRangeException.ThrowIfNegative(count);
            if(count<=this.reserved){return;}this.memory.Resize(this.RequiredBytes(count,this.payload));this.reserved=count;
        }
        private long RequiredBytes(long count,long payload)=>checked(this.metadata+(4L*count+32)*Unsafe.SizeOf<T>()+count*sizeof(int)+payload);
        public void Dispose(){if(this.disposed){return;}this.disposed=true;try{this.memory.Dispose();}finally{this.scope.Release(this);}}
    }
    public sealed class KeyBudget<TKey>:IDisposable,IKeyBudget{
        private static readonly bool references=RuntimeHelpers.IsReferenceOrContainsReferences<TKey>();
        private readonly ExecutionScope scope;
        private readonly ResourceReservation memory;
        private readonly global::Seamless.Execution.Codecs.IAdapter<TKey>? codec;
        private readonly bool materializedOutput;
        private long slots,payload;
        private bool disposed;
        public KeyBudget(ExecutionScope scope,int count):this(scope,(long)count){}
        public KeyBudget(ExecutionScope scope,long count):this(scope,count,false){}
        public KeyBudget(ExecutionScope scope,long count,bool materializedOutput){
            ArgumentNullException.ThrowIfNull(scope);ArgumentOutOfRangeException.ThrowIfNegative(count);scope.ThrowIfUnavailable();this.scope=scope;
            this.materializedOutput=materializedOutput;this.slots=count;
            if(references&&scope.Codecs.CanEncode<TKey>()){this.codec=scope.Codecs.Get<TKey>();}
            this.memory=scope.Budget.ReserveMemory(checked(96L+count*(long)Unsafe.SizeOf<TKey>()));
            try{scope.Own(this);}catch{this.memory.Dispose();throw;}
        }
        public void ReserveRows(long count){
            ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();ArgumentOutOfRangeException.ThrowIfNegative(count);
            if(this.materializedOutput&&count>0){count=checked(4L*count+32);}
            var slots=Math.Max(this.slots,count);if(slots==this.slots){return;}
            this.memory.Resize(checked(96L+slots*Unsafe.SizeOf<TKey>()+this.payload));this.slots=slots;
        }
        public TKey Capture(TKey key){
            ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();
            if(references){var payload=checked(this.payload+64L);if(this.codec is not null){payload=checked(payload+4L*this.codec.GetEncodedLength(key));}this.memory.Resize(checked(96L+this.slots*Unsafe.SizeOf<TKey>()+payload));this.payload=payload;}
            return key;
        }
        public void Dispose(){if(this.disposed){return;}this.disposed=true;try{this.memory.Dispose();}finally{this.scope.Release(this);}}
    }
}
