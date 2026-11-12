namespace Test.System.Linq;

// FLinq の生成コード（SkipLast 段）が依存する ValueQueue<T> の契約を固定する。
// 特に「default のまま Dispose してよい」「Dispose を2回呼んでも壊れない」は、
// 生成 struct が構築済み判定なしで無条件に Dispose を呼ぶ前提そのもの。
// ここが崩れると ArrayPool へ同じ配列を二重返却してサイレントなデータ破壊になる。
[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ValueQueueTests {
    [Fact, Trait("Category","Unit")]
    public void Dispose() {
        {
            //未初期化（default）のまま Dispose しても例外にならないテスト
            var queue = default(global::Seamless.Linq.ValueQueue<int>);
            queue.Dispose();
            queue.Dispose();
        }
        {
            //借りたあと Dispose を2回呼んでも二重返却にならないテスト
            var queue = new global::Seamless.Linq.ValueQueue<int>(8);
            queue.Enqueue(1);
            queue.Dispose();
            queue.Dispose();

            //直後に別のキューを借りて内容が壊れていないことを確認する
            var next = new global::Seamless.Linq.ValueQueue<int>(8);
            try {
                next.Enqueue(42);
                Assert.Equal(1,next.Count);
                Assert.Equal(42,next.Dequeue());
            } finally {
                next.Dispose();
            }
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Enqueue() {
        {
            //容量ちょうどまで詰めても成長しないテスト
            var queue = new global::Seamless.Linq.ValueQueue<int>(4);
            try {
                for(var i = 0;i<4;i++) {
                    queue.Enqueue(i);
                }
                Assert.Equal(4,queue.Count);
                for(var i = 0;i<4;i++) {
                    Assert.Equal(i,queue.Dequeue());
                }
                Assert.Equal(0,queue.Count);
            } finally {
                queue.Dispose();
            }
        }
        {
            //容量を超えたら成長し、順序が保たれるテスト
            var queue = new global::Seamless.Linq.ValueQueue<int>(2);
            try {
                for(var i = 0;i<64;i++) {
                    queue.Enqueue(i);
                }
                Assert.Equal(64,queue.Count);
                for(var i = 0;i<64;i++) {
                    Assert.Equal(i,queue.Dequeue());
                }
            } finally {
                queue.Dispose();
            }
        }
        {
            //Enqueue と Dequeue を交互に繰り返してリングが折り返しても順序が保たれるテスト
            var queue = new global::Seamless.Linq.ValueQueue<int>(4);
            try {
                for(var i = 0;i<4;i++) {
                    queue.Enqueue(i);
                }
                for(var i = 4;i<1024;i++) {
                    Assert.Equal(i-4,queue.Dequeue());
                    queue.Enqueue(i);
                }
                Assert.Equal(4,queue.Count);
            } finally {
                queue.Dispose();
            }
        }
    }
}
