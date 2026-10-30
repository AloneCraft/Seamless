using System.ComponentModel;
using System.Numerics;

namespace Seamless.Linq;

/// <summary>融合したグループ集計のオーバーフローを、その集計結果が評価される時点で通知する。</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct DeferredGroupSum<T> where T:struct,INumber<T>{
    private T value;
    private bool overflow;

    public readonly T Value{
        get{
            if(this.overflow){throw new OverflowException();}
            return this.value;
        }
    }

    public void Add(T value){
        if(this.overflow){return;}
        try{
            this.value=checked(this.value+value);
        }catch(OverflowException){
            this.overflow=true;
        }
    }
}
