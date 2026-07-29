using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class DynamicQueryComparerTests{
    [Fact,Trait("Category","Unit")]
    public void Materialize上限を超えるSequenceを実行時に拒否する(){
        var values=Enumerable.Range(0,DynamicQueryValue.MaximumMaterializedItemCount+1);
        var exception=Assert.Throws<InvalidOperationException>(()=>DynamicQueryValue.FromObject(
            values,
            QueryValueType.Int,
            QueryCardinality.Sequence,
            QueryOrderSemantics.Ordered));
        Assert.Contains("100,000",exception.Message,StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void IntGroupMaterializerはunordered入力をcanonicalizeする(){
        var first=DynamicQueryMaterializer.CreateCanonicalIntGroup(7,[3,-1,3,2]);
        var second=DynamicQueryMaterializer.CreateCanonicalIntGroup(7,[2,3,-1,3]);

        Assert.Equal([-1,2,3,3],first.Values);
        Assert.True(Core.DynamicQueryComparer.Compare(
            DynamicQueryValue.Scalar(new IntGroupQueryAtom(first)),
            DynamicQueryValue.Scalar(new IntGroupQueryAtom(second))).IsMatch);
    }

    [Fact,Trait("Category","Unit")]public void Compare(){
        {
            var expected=DynamicQueryValue.Sequence(
                QueryValueType.Int,
                QueryOrderSemantics.Ordered,
                [new IntQueryAtom(1),new IntQueryAtom(2)]);
            var actual=DynamicQueryValue.Sequence(
                QueryValueType.Int,
                QueryOrderSemantics.Ordered,
                [new IntQueryAtom(2),new IntQueryAtom(1)]);
            var comparison=Core.DynamicQueryComparer.Compare(expected,actual);
            Assert.False(comparison.IsMatch);
            Assert.Equal(0,comparison.DifferenceIndex);
        }
        {
            var expected=DynamicQueryValue.Sequence(
                QueryValueType.Int,
                QueryOrderSemantics.Unordered,
                [new IntQueryAtom(1),new IntQueryAtom(1),new IntQueryAtom(2)]);
            var matching=DynamicQueryValue.Sequence(
                QueryValueType.Int,
                QueryOrderSemantics.Unordered,
                [new IntQueryAtom(2),new IntQueryAtom(1),new IntQueryAtom(1)]);
            var missingDuplicate=DynamicQueryValue.Sequence(
                QueryValueType.Int,
                QueryOrderSemantics.Unordered,
                [new IntQueryAtom(2),new IntQueryAtom(2),new IntQueryAtom(1)]);
            Assert.True(Core.DynamicQueryComparer.Compare(expected,matching).IsMatch);
            Assert.False(Core.DynamicQueryComparer.Compare(expected,missingDuplicate).IsMatch);
        }
        {
            var expected=DynamicQueryValue.Scalar(new IntGroupQueryAtom(new IntGroup(3,[1,2,2])));
            var matching=DynamicQueryValue.Scalar(new IntGroupQueryAtom(new IntGroup(3,[1,2,2])));
            var different=DynamicQueryValue.Scalar(new IntGroupQueryAtom(new IntGroup(3,[1,2])));
            Assert.True(Core.DynamicQueryComparer.Compare(expected,matching).IsMatch);
            Assert.False(Core.DynamicQueryComparer.Compare(expected,different).IsMatch);
        }
    }
}
