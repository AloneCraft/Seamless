using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Seamless.Linq;

/// <summary>融合したグループ集計のオーバーフローを、その集計結果が評価される時点で通知する。</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DeferredGroupSum<T> where T:unmanaged,INumber<T>{
    private T value;
    private bool overflow;

    public readonly T Value{
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get{
            if(this.overflow){ThrowOverflow();}
            return this.value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T value){
        if(this.overflow){return;}
        if(typeof(T)==typeof(int)){
            var a=Unsafe.As<T,int>(ref this.value);
            var b=Unsafe.As<T,int>(ref value);
            var r=unchecked(a+b);
            if(((a^r)&(b^r))<0){
                this.overflow=true;
            }else{
                Unsafe.As<T,int>(ref this.value)=r;
            }
            return;
        }
        if(typeof(T)==typeof(long)){
            var a=Unsafe.As<T,long>(ref this.value);
            var b=Unsafe.As<T,long>(ref value);
            var r=unchecked(a+b);
            if(((a^r)&(b^r))<0){
                this.overflow=true;
            }else{
                Unsafe.As<T,long>(ref this.value)=r;
            }
            return;
        }
        this.AddSlow(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddSlow(T value){
        try{
            this.value=checked(this.value+value);
        }catch(OverflowException){
            this.overflow=true;
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOverflow()=>throw new OverflowException();
}
