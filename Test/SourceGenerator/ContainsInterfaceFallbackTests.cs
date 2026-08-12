using Seamless.FLinq;

namespace Test.SourceGenerator;

public sealed class ContainsInterfaceFallbackTests {
    [Fact]
    public void UnknownIEnumerableFallbackPreservesResultDisposeExceptionAndNullContracts() {
        var container=new Container();

        var hit=new TrackingEnumerable([1,2,3]);
        Assert.True(container.Contains(hit,2));
        Assert.Equal(1,hit.GetEnumeratorCount);
        Assert.Equal(1,hit.DisposeCount);

        var miss=new TrackingEnumerable([1,2,3]);
        Assert.False(container.Contains(miss,9));
        Assert.Equal(1,miss.GetEnumeratorCount);
        Assert.Equal(1,miss.DisposeCount);

        var throwing=new TrackingEnumerable([1,2,3],throwOnMoveNext:2);
        Assert.Throws<InvalidOperationException>(() => container.Contains(throwing,9));
        Assert.Equal(1,throwing.DisposeCount);

        Assert.Throws<ArgumentNullException>(() => container.Contains(null!,1));
    }

    private sealed class Container {
        [LinqIntercept(true)]
        public bool Contains(IEnumerable<int> source,int value) => global::System.Linq.Enumerable.Contains(source,value);
    }

    private sealed class TrackingEnumerable(int[] values,int throwOnMoveNext=0):IEnumerable<int> {
        private readonly int[] values=values;
        private readonly int throwOnMoveNext=throwOnMoveNext;
        public int GetEnumeratorCount { get; private set; }
        public int DisposeCount { get; private set; }

        public IEnumerator<int> GetEnumerator() {
            this.GetEnumeratorCount++;
            return new Enumerator(this);
        }

        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

        private sealed class Enumerator(TrackingEnumerable owner):IEnumerator<int> {
            private int index=-1;
            public int Current => owner.values[index];
            object global::System.Collections.IEnumerator.Current => this.Current;

            public bool MoveNext() {
                this.index++;
                if(owner.throwOnMoveNext!=0&&this.index+1==owner.throwOnMoveNext) {
                    throw new InvalidOperationException("MoveNext failure");
                }
                return this.index<owner.values.Length;
            }

            public void Reset() => throw new NotSupportedException();
            public void Dispose() => owner.DisposeCount++;
        }
    }
}