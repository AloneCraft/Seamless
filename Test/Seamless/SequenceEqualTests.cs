namespace Test;

public sealed class SequenceEqualTests {
    [Fact]
    public void SequenceEqualSpanSkipsComparisonForAliasedSpan() {
        var value = new Probe();
        var source = new[] { value };

        Assert.True(global::Seamless.Linq.Enumerable.SequenceEqualSpan(source,source));
        Assert.Equal(0,value.EqualsCallCount);

        var separate = new[] { value };
        Assert.True(global::Seamless.Linq.Enumerable.SequenceEqualSpan(source,separate));
        Assert.Equal(1,value.EqualsCallCount);
    }

    private sealed class Probe:IEquatable<Probe> {
        public int EqualsCallCount { get; private set; }

        public bool Equals(Probe? other) {
            this.EqualsCallCount++;
            return ReferenceEquals(this,other);
        }

        public override bool Equals(object? obj) => obj is Probe other&&this.Equals(other);
        public override int GetHashCode() => 0;
    }
}
