using Test.System.Linq.Intercept;

namespace Test.System.Linq.Intercept.Optimization;

public sealed class DirectSelectManySemantics {
    [Fact,Trait("Category","Unit")]
    public void IdentitySelectorPreservesEmptySingleAndMultipleOrder() {
        IEnumerable<IEnumerable<int>>[] cases = [
            [],
            [Array.Empty<int>()],
            [new[] { 1 }],
            [new[] { 1,2 },Array.Empty<int>(),new[] { 3,4 }],
        ];

        foreach(var source in cases) {
            var expected=IdentitySelectorBaseline(source);
            var actual=IdentitySelectorOptimized(source);
            Assert.Equal(expected,actual);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void IdentitySelectorDisposesInnerThenOuterWhenInnerThrows() {
        var expectedEvents=new List<string>();
        var expectedSource=CreateFaultingSource(expectedEvents);
        Assert.Throws<InvalidOperationException>(() => IdentitySelectorBaseline(expectedSource));

        var actualEvents=new List<string>();
        var actualSource=CreateFaultingSource(actualEvents);
        Assert.Throws<InvalidOperationException>(() => IdentitySelectorOptimized(actualSource));

        Assert.Equal(new[] { "inner disposed","outer disposed" },actualEvents);
        Assert.Equal(expectedEvents,actualEvents);
    }

    [Fact,Trait("Category","Unit")]
    public void IdentitySelectorUsesIEnumerableGetEnumeratorInsteadOfConcretePattern() {
        var expectedEvents=new List<string>();
        var expected=IdentitySelectorWithDivergentInnerBaseline(
            new[] { new DivergentEnumerable(expectedEvents) });

        var actualEvents=new List<string>();
        var actual=IdentitySelectorWithDivergentInnerOptimized(
            new[] { new DivergentEnumerable(actualEvents) });

        Assert.Equal(expected,actual);
        Assert.Equal(expectedEvents,actualEvents);
    }

    [Fact,Trait("Category","Unit")]
    public void IdentitySelectorPreservesReceiverAndArgumentEvaluationOrder() {
        foreach(var argumentForm in new[] { false,true }) {
            var expectedEvents=new List<string>();
            if(argumentForm) {
                IdentitySelectorArgumentOrderBaseline(expectedEvents);
            } else {
                IdentitySelectorReceiverOrderBaseline(expectedEvents);
            }

            var actualEvents=new List<string>();
            if(argumentForm) {
                IdentitySelectorArgumentOrderOptimized(actualEvents);
            } else {
                IdentitySelectorReceiverOrderOptimized(actualEvents);
            }

            Assert.Equal(expectedEvents,actualEvents);
            if(argumentForm) {
                expectedEvents.Clear();
                IdentitySelectorArgumentOrderStatementBaseline(expectedEvents);

                actualEvents.Clear();
                IdentitySelectorArgumentOrderStatementOptimized(actualEvents);

                Assert.Equal(expectedEvents,actualEvents);
            }
        }
    }

    [Fact,Trait("Category","Unit")]
    public void IdentitySelectorNullOuterUsesSourceParamNameAndPreservesEvaluationOrder() {
        foreach(var argumentForm in new[] { false,true }) {
            var expectedEvents=new List<string>();
            var expected=Record.Exception(() => IdentitySelectorNullBaseline(expectedEvents,argumentForm));

            var actualEvents=new List<string>();
            var actual=Record.Exception(() => IdentitySelectorNullOptimized(actualEvents,argumentForm));

            var expectedArgumentNull=Assert.IsType<ArgumentNullException>(expected);
            var actualArgumentNull=Assert.IsType<ArgumentNullException>(actual);
            Assert.Equal("source",expectedArgumentNull.ParamName);
            Assert.Equal(expectedArgumentNull.ParamName,actualArgumentNull.ParamName);
            Assert.Equal(expectedEvents,actualEvents);
        }
    }

    private static IEnumerable<IEnumerable<int>> CreateFaultingSource(List<string> events) {
        var inner=new TrackingEnumerable<int>([1],events,"inner",throwOnMoveNext: true);
        return new TrackingEnumerable<IEnumerable<int>>([inner],events,"outer",throwOnMoveNext: false);
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] IdentitySelectorBaseline(IEnumerable<IEnumerable<int>> source) {
        var consumer=new Consumer<int>();
        source.SelectMany(static item => item).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] IdentitySelectorOptimized(IEnumerable<IEnumerable<int>> source) {
        var consumer=new Consumer<int>();
        source.SelectMany(static item => item).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static int[] IdentitySelectorWithDivergentInnerBaseline(IEnumerable<DivergentEnumerable> source) {
        var consumer=new Consumer<int>();
        source.SelectMany(static item => item).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static int[] IdentitySelectorWithDivergentInnerOptimized(IEnumerable<DivergentEnumerable> source) {
        var consumer=new Consumer<int>();
        source.SelectMany(static item => item).Consume(consumer);
        return consumer.Items.ToArray();
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static void IdentitySelectorReceiverOrderBaseline(List<string> events) =>
        CreateOuter(events).SelectMany(static item => item).Consume(CreateRecordingConsumer(events));

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void IdentitySelectorReceiverOrderOptimized(List<string> events) =>
        CreateOuter(events).SelectMany(static item => item).Consume(CreateRecordingConsumer(events));

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static void IdentitySelectorArgumentOrderBaseline(List<string> events) =>
        CreateRecordingConsumer(events).Consume(CreateOuter(events).SelectMany(static item => item));

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void IdentitySelectorArgumentOrderOptimized(List<string> events) =>
        CreateRecordingConsumer(events).Consume(CreateOuter(events).SelectMany(static item => item));

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static void IdentitySelectorArgumentOrderStatementBaseline(List<string> events) {
        CreateRecordingConsumer(events).Consume(CreateOuter(events).SelectMany(static item => item));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void IdentitySelectorArgumentOrderStatementOptimized(List<string> events) {
        CreateRecordingConsumer(events).Consume(CreateOuter(events).SelectMany(static item => item));
    }

    [global::Seamless.FLinq.LinqIntercept(false)]
    private static void IdentitySelectorNullBaseline(List<string> events,bool argumentForm) {
        IEnumerable<IEnumerable<int>> source=null!;
        if(argumentForm) {
            CreateRecordingConsumer(events).Consume(source.SelectMany(static item => item));
        } else {
            source.SelectMany(static item => item).Consume(CreateRecordingConsumer(events));
        }
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void IdentitySelectorNullOptimized(List<string> events,bool argumentForm) {
        IEnumerable<IEnumerable<int>> source=null!;
        if(argumentForm) {
            CreateRecordingConsumer(events).Consume(source.SelectMany(static item => item));
        } else {
            source.SelectMany(static item => item).Consume(CreateRecordingConsumer(events));
        }
    }

    private static IEnumerable<IEnumerable<int>> CreateOuter(List<string> events) {
        events.Add("source.Create");
        return new[] { (IEnumerable<int>)new[] { 4,5 } };
    }

    private static SelectManyRecordingConsumer CreateRecordingConsumer(List<string> events) {
        events.Add("consumer.Create");
        return new SelectManyRecordingConsumer(events);
    }

    internal sealed class DivergentEnumerable(List<string> events):IEnumerable<int> {
        public IEnumerator<int> GetEnumerator() {
            events.Add("public.GetEnumerator");
            throw new InvalidOperationException("The concrete GetEnumerator must not be called.");
        }

        IEnumerator<int> IEnumerable<int>.GetEnumerator() {
            events.Add("interface.GetEnumerator");
            return ((IEnumerable<int>)new[] { 7,8 }).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<int>)this).GetEnumerator();
    }

    private sealed class TrackingEnumerable<T>(IEnumerable<T> source,List<string> events,string name,bool throwOnMoveNext):IEnumerable<T> {
        public IEnumerator<T> GetEnumerator() => new TrackingEnumerator(source.GetEnumerator(),events,name,throwOnMoveNext);
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private sealed class TrackingEnumerator(IEnumerator<T> source,List<string> events,string name,bool throwOnMoveNext):IEnumerator<T> {
            private bool hasThrown;
            public T Current => source.Current;
            object IEnumerator.Current => this.Current!;

            public bool MoveNext() {
                if(throwOnMoveNext&&!this.hasThrown) {
                    this.hasThrown=true;
                    throw new InvalidOperationException("inner failure");
                }
                return source.MoveNext();
            }

            public void Reset() => source.Reset();

            public void Dispose() {
                events.Add($"{name} disposed");
                source.Dispose();
            }
        }
    }
}

internal sealed class SelectManyRecordingConsumer(List<string> events) {
    public void Consume(int item) => events.Add($"consumer.Consume({item})");
}

internal static class DirectSelectManyConsumeSupport {
    public static void Consume(
        this IEnumerable<int> source,
        SelectManyRecordingConsumer consumer) {
        foreach(var item in source) {
            consumer.Consume(item);
        }
    }

    public static void Consume(
        this SelectManyRecordingConsumer consumer,
        IEnumerable<int> source) {
        foreach(var item in source) {
            consumer.Consume(item);
        }
    }
}
