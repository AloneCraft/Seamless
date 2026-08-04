namespace Seamless.Linq;

/// <summary>
/// ArrayPool&lt;T&gt;.Shared から借りた配列で回すリングキュー。
/// SkipLast のような「直近 N 件のスライディングウィンドウ」向けで、
/// Count/Enqueue/Dequeue は System.Collections.Generic.Queue&lt;T&gt; と同義。
/// PooledArray&lt;T&gt; と同じく struct なので、コピーやボクシングをすると
/// 同じ配列を複数回 ArrayPool へ返す危険がある。単一の所有者が持ち、
/// 借りた後はコピーせずそのまま使って Dispose すること。
/// default(ValueQueue&lt;T&gt;) は items が null で、Dispose は no-op になる
/// （生成コードは「未初期化でも無条件に Dispose を呼べる」性質に依存している）。
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public struct ValueQueue<T>(int capacity):IDisposable {
    private T[] items = ArrayPool<T>.Shared.Rent(capacity);
    private int head = 0;
    private int tail = 0;
    private int size = 0;

    public int Count => this.size;

    public void Enqueue(T item) {
        if(this.items.Length==this.size) {
            this.Grow();
        }

        this.items[this.tail]=item;
        this.tail=(this.tail+1)%this.items.Length;
        this.size++;
    }

    public T Dequeue() {
        if(this.size==0) Throw();

        T removed = this.items[this.head];
        this.items[this.head]=default!;
        this.head=(this.head+1)%this.items.Length;
        this.size--;
        return removed;
    }
    private static void Throw() => throw new InvalidOperationException("Queue is empty.");
    private void Grow() {
        T[] newArray = ArrayPool<T>.Shared.Rent(this.items.Length*2);
        if(this.size>0) {
            if(this.head<this.tail) {
                Array.Copy(this.items,this.head,newArray,0,this.size);
            } else {
                Array.Copy(this.items,this.head,newArray,0,this.items.Length-this.head);
                Array.Copy(this.items,0,newArray,this.items.Length-this.head,this.tail);
            }
        }
        ArrayPool<T>.Shared.Return(this.items,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        this.items=newArray;
        this.head=0;
        this.tail=this.size;
    }

    public void Dispose() {
        if(this.items!=null) {
            ArrayPool<T>.Shared.Return(this.items,clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            this.items=null!;
        }
    }
}
