using System.Collections;

namespace Test.System.Linq.Intercept.Optimization;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class DirectMaterializeConcatSemantics:OptimizationTestBase {
    [Fact, Trait("Category","Unit")]
    public void ArrayToListConsumeMatchesSystemLinq() {
        foreach(var source in new[] { Array.Empty<int>(),[7],[3,1,4,1,5] }) {
            var expected=new RecordingConsumer<int>([]);
            var actual=new RecordingConsumer<int>([]);

            ArrayToListConsumeSystem(source,expected);
            ArrayToListConsumeOptimized(source,actual);

            Assert.Equal(expected.Items,actual.Items);
        }

        {
            var expectedEvents=new List<string>();
            var actualEvents=new List<string>();
            var expected=new RecordingConsumer<int>(expectedEvents,2);
            var actual=new RecordingConsumer<int>(actualEvents,2);

            var expectedException=RecordException(() => ArrayToListConsumeSystem([1,2,3],expected));
            var actualException=RecordException(() => ArrayToListConsumeOptimized([1,2,3],actual));

            Assert.Equal(expectedException,actualException);
            Assert.Equal(expected.Items,actual.Items);
            Assert.Equal(expectedEvents,actualEvents);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void ArrayToListConsumeEvaluatesConsumerOnlyAfterMaterialization() {
        {
            var expectedEvents=new List<string>();
            ArrayToListEvaluationOrderSystem(expectedEvents);

            var actualEvents=new List<string>();
            ArrayToListEvaluationOrderOptimized(actualEvents);

            Assert.Equal(expectedEvents,actualEvents);
        }
        {
            var expectedEvents=new List<string>();
            ArrayToListArgumentEvaluationOrderSystem(expectedEvents);

            var actualEvents=new List<string>();
            ArrayToListArgumentEvaluationOrderOptimized(actualEvents);

            Assert.Equal(expectedEvents,actualEvents);

            expectedEvents.Clear();
            ArrayToListArgumentEvaluationOrderStatementSystem(expectedEvents);

            actualEvents.Clear();
            ArrayToListArgumentEvaluationOrderStatementOptimized(actualEvents);
            Assert.Equal(expectedEvents,actualEvents);
        }

        {
            var expectedEvents=new List<string>();
            var expectedException=Record.Exception(() => ArrayToListNullSystem(null!,expectedEvents));

            var actualEvents=new List<string>();
            var actualException=Record.Exception(() => ArrayToListNullOptimized(null!,actualEvents));

            var expectedArgumentNull=Assert.IsType<ArgumentNullException>(expectedException);
            var actualArgumentNull=Assert.IsType<ArgumentNullException>(actualException);
            Assert.Equal(expectedArgumentNull.ParamName,actualArgumentNull.ParamName);
            Assert.Equal(expectedEvents,actualEvents);
            Assert.DoesNotContain("consumer.Create",actualEvents);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void IEnumerableConcatConsumeMatchesSystemLinqOrderSideEffectsExceptionsAndDispose() {
        foreach(var values in new[] {
            (First:Array.Empty<int>(),Second:Array.Empty<int>()),
            (First:new[] { 7 },Second:new[] { 9 }),
            (First:new[] { 1,2,3 },Second:new[] { 4,5 })
        }) {
            var expectedEvents=new List<string>();
            var actualEvents=new List<string>();
            var expected=new RecordingConsumer<int>(expectedEvents);
            var actual=new RecordingConsumer<int>(actualEvents);

            ConcatExpressionsSystem(expectedEvents,values.First,values.Second,expected);
            ConcatExpressionsOptimized(actualEvents,values.First,values.Second,actual);

            Assert.Equal(expected.Items,actual.Items);
            Assert.Equal(expectedEvents,actualEvents);
        }

        {
            var expectedEvents=new List<string>();
            var actualEvents=new List<string>();
            var expected=new RecordingConsumer<int>(expectedEvents);
            var actual=new RecordingConsumer<int>(actualEvents);
            var expectedFirst=new TrackingEnumerable<int>("first",[1,2,3],expectedEvents,throwMoveNextAt:1);
            var expectedSecond=new TrackingEnumerable<int>("second",[4,5],expectedEvents);
            var actualFirst=new TrackingEnumerable<int>("first",[1,2,3],actualEvents,throwMoveNextAt:1);
            var actualSecond=new TrackingEnumerable<int>("second",[4,5],actualEvents);

            var expectedException=RecordException(() => ConcatSystem(expectedFirst,expectedSecond,expected));
            var actualException=RecordException(() => ConcatOptimized(actualFirst,actualSecond,actual));

            Assert.Equal(expectedException,actualException);
            Assert.Equal(expected.Items,actual.Items);
            Assert.Equal(expectedEvents,actualEvents);
        }

        {
            var expectedEvents=new List<string>();
            var actualEvents=new List<string>();
            var expected=new RecordingConsumer<int>(expectedEvents,3);
            var actual=new RecordingConsumer<int>(actualEvents,3);
            var expectedFirst=new TrackingEnumerable<int>("first",[1,2],expectedEvents);
            var expectedSecond=new TrackingEnumerable<int>("second",[3,4],expectedEvents);
            var actualFirst=new TrackingEnumerable<int>("first",[1,2],actualEvents);
            var actualSecond=new TrackingEnumerable<int>("second",[3,4],actualEvents);

            var expectedException=RecordException(() => ConcatSystem(expectedFirst,expectedSecond,expected));
            var actualException=RecordException(() => ConcatOptimized(actualFirst,actualSecond,actual));

            Assert.Equal(expectedException,actualException);
            Assert.Equal(expected.Items,actual.Items);
            Assert.Equal(expectedEvents,actualEvents);
        }

        {
            var expectedEvents=new List<string>();
            var actualEvents=new List<string>();
            var expected=new RecordingConsumer<int>(expectedEvents);
            var actual=new RecordingConsumer<int>(actualEvents);
            var expectedFirst=new TrackingEnumerable<int>("first",[1,2],expectedEvents);
            var expectedSecond=new TrackingEnumerable<int>("second",[3,4],expectedEvents,throwOnGetEnumerator:true);
            var actualFirst=new TrackingEnumerable<int>("first",[1,2],actualEvents);
            var actualSecond=new TrackingEnumerable<int>("second",[3,4],actualEvents,throwOnGetEnumerator:true);

            var expectedException=RecordException(() => ConcatSystem(expectedFirst,expectedSecond,expected));
            var actualException=RecordException(() => ConcatOptimized(actualFirst,actualSecond,actual));

            Assert.Equal(expectedException,actualException);
            Assert.Equal(expected.Items,actual.Items);
            Assert.Equal(expectedEvents,actualEvents);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void IEnumerableConcatReceiverConsumePreparesBothSourcesBeforeConsumer() {
        AssertConcatEvaluationMatches(
            events => ConcatReceiverEvaluationSystem(events,nullFirst:false,nullSecond:false,throwConsumer:false),
            events => ConcatReceiverEvaluationOptimized(events,nullFirst:false,nullSecond:false,throwConsumer:false));
        AssertConcatEvaluationMatches(
            events => ConcatReceiverEvaluationSystem(events,nullFirst:true,nullSecond:false,throwConsumer:false),
            events => ConcatReceiverEvaluationOptimized(events,nullFirst:true,nullSecond:false,throwConsumer:false));
        AssertConcatEvaluationMatches(
            events => ConcatReceiverEvaluationSystem(events,nullFirst:false,nullSecond:true,throwConsumer:false),
            events => ConcatReceiverEvaluationOptimized(events,nullFirst:false,nullSecond:true,throwConsumer:false));
        AssertConcatEvaluationMatches(
            events => ConcatReceiverEvaluationSystem(events,nullFirst:false,nullSecond:false,throwConsumer:true),
            events => ConcatReceiverEvaluationOptimized(events,nullFirst:false,nullSecond:false,throwConsumer:true));
    }

    [Fact, Trait("Category","Unit")]
    public void IEnumerableConcatArgumentConsumeEvaluatesConsumerBeforePreparingSources() {
        AssertConcatEvaluationMatches(
            events => ConcatArgumentEvaluationSystem(events,nullFirst:false,nullSecond:false,throwConsumer:false),
            events => ConcatArgumentEvaluationOptimized(events,nullFirst:false,nullSecond:false,throwConsumer:false));
        AssertConcatEvaluationMatches(
            events => ConcatArgumentEvaluationSystem(events,nullFirst:true,nullSecond:false,throwConsumer:false),
            events => ConcatArgumentEvaluationOptimized(events,nullFirst:true,nullSecond:false,throwConsumer:false));
        AssertConcatEvaluationMatches(
            events => ConcatArgumentEvaluationSystem(events,nullFirst:false,nullSecond:true,throwConsumer:false),
            events => ConcatArgumentEvaluationOptimized(events,nullFirst:false,nullSecond:true,throwConsumer:false));
        AssertConcatEvaluationMatches(
            events => ConcatArgumentEvaluationSystem(events,nullFirst:false,nullSecond:false,throwConsumer:true),
            events => ConcatArgumentEvaluationOptimized(events,nullFirst:false,nullSecond:false,throwConsumer:true));
    }

    [Fact, Trait("Category","Unit")]
    public void RangeConcatPreparationEvaluatesEachArgumentExactlyOnce() {
        AssertConcatEvaluationMatches(
            RangeConcatReceiverEvaluationSystem,
            RangeConcatReceiverEvaluationOptimized);
        AssertConcatEvaluationMatches(
            RangeConcatArgumentEvaluationSystem,
            RangeConcatArgumentEvaluationOptimized);
    }

    private static void ArrayToListConsumeSystem(int[] source,RecordingConsumer<int> consumer) =>
        source.ToList().Consume(consumer);

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ArrayToListConsumeOptimized(int[] source,RecordingConsumer<int> consumer) =>
        source.ToList().Consume(consumer);

    private static void ArrayToListEvaluationOrderSystem(List<string> events) =>
        CreateArray(events).ToList().Consume(CreateConsumer(events));

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ArrayToListEvaluationOrderOptimized(List<string> events) =>
        CreateArray(events).ToList().Consume(CreateConsumer(events));

    private static void ArrayToListArgumentEvaluationOrderSystem(List<string> events) =>
        CreateConsumer(events).Consume(CreateArray(events).ToList());

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ArrayToListArgumentEvaluationOrderOptimized(List<string> events) =>
        CreateConsumer(events).Consume(CreateArray(events).ToList());

    private static void ArrayToListArgumentEvaluationOrderStatementSystem(List<string> events) {
        CreateConsumer(events).Consume(CreateArray(events).ToList());
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ArrayToListArgumentEvaluationOrderStatementOptimized(List<string> events) {
        CreateConsumer(events).Consume(CreateArray(events).ToList());
    }

    private static void ArrayToListNullSystem(int[] source,List<string> events) =>
        source.ToList().Consume(CreateConsumer(events));

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ArrayToListNullOptimized(int[] source,List<string> events) =>
        source.ToList().Consume(CreateConsumer(events));

    private static void ConcatExpressionsSystem(
        List<string> events,
        int[] first,
        int[] second,
        RecordingConsumer<int> consumer) =>
        Create("first",first,events).Concat(Create("second",second,events)).Consume(consumer);

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ConcatExpressionsOptimized(
        List<string> events,
        int[] first,
        int[] second,
        RecordingConsumer<int> consumer) =>
        Create("first",first,events).Concat(Create("second",second,events)).Consume(consumer);

    private static void ConcatSystem(
        IEnumerable<int> first,
        IEnumerable<int> second,
        RecordingConsumer<int> consumer) =>
        first.Concat(second).Consume(consumer);

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ConcatOptimized(
        IEnumerable<int> first,
        IEnumerable<int> second,
        RecordingConsumer<int> consumer) =>
        first.Concat(second).Consume(consumer);

    private static void ConcatReceiverEvaluationSystem(
        List<string> events,
        bool nullFirst,
        bool nullSecond,
        bool throwConsumer) =>
        CreateNullable("first",nullFirst,events)
            .Concat(CreateNullable("second",nullSecond,events))
            .Consume(CreateConsumer(events,throwConsumer));

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ConcatReceiverEvaluationOptimized(
        List<string> events,
        bool nullFirst,
        bool nullSecond,
        bool throwConsumer) =>
        CreateNullable("first",nullFirst,events)
            .Concat(CreateNullable("second",nullSecond,events))
            .Consume(CreateConsumer(events,throwConsumer));

    private static void ConcatArgumentEvaluationSystem(
        List<string> events,
        bool nullFirst,
        bool nullSecond,
        bool throwConsumer) =>
        CreateConsumer(events,throwConsumer).Consume(
            CreateNullable("first",nullFirst,events)
                .Concat(CreateNullable("second",nullSecond,events)));

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ConcatArgumentEvaluationOptimized(
        List<string> events,
        bool nullFirst,
        bool nullSecond,
        bool throwConsumer) =>
        CreateConsumer(events,throwConsumer).Consume(
            CreateNullable("first",nullFirst,events)
                .Concat(CreateNullable("second",nullSecond,events)));

    private static void RangeConcatReceiverEvaluationSystem(List<string> events) =>
        Enumerable.Range(
                CreateRangeArgument("first.Start",3,events),
                CreateRangeArgument("first.Count",2,events))
            .Concat(Enumerable.Range(
                CreateRangeArgument("second.Start",7,events),
                CreateRangeArgument("second.Count",2,events)))
            .Consume(CreateConsumer(events));

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void RangeConcatReceiverEvaluationOptimized(List<string> events) =>
        Enumerable.Range(
                CreateRangeArgument("first.Start",3,events),
                CreateRangeArgument("first.Count",2,events))
            .Concat(Enumerable.Range(
                CreateRangeArgument("second.Start",7,events),
                CreateRangeArgument("second.Count",2,events)))
            .Consume(CreateConsumer(events));

    private static void RangeConcatArgumentEvaluationSystem(List<string> events) =>
        CreateConsumer(events).Consume(
            Enumerable.Range(
                    CreateRangeArgument("first.Start",3,events),
                    CreateRangeArgument("first.Count",2,events))
                .Concat(Enumerable.Range(
                    CreateRangeArgument("second.Start",7,events),
                    CreateRangeArgument("second.Count",2,events))));

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void RangeConcatArgumentEvaluationOptimized(List<string> events) =>
        CreateConsumer(events).Consume(
            Enumerable.Range(
                    CreateRangeArgument("first.Start",3,events),
                    CreateRangeArgument("first.Count",2,events))
                .Concat(Enumerable.Range(
                    CreateRangeArgument("second.Start",7,events),
                    CreateRangeArgument("second.Count",2,events))));

    private static IEnumerable<int> Create(string name,int[] values,List<string> events) {
        events.Add(name+".Create");
        return new TrackingEnumerable<int>(name,values,events);
    }

    private static IEnumerable<int> CreateNullable(string name,bool returnNull,List<string> events) {
        events.Add(name+".Create");
        return returnNull ? null! : new TrackingEnumerable<int>(name,[1,2],events);
    }

    private static int CreateRangeArgument(string name,int value,List<string> events) {
        events.Add(name);
        return value;
    }

    private static int[] CreateArray(List<string> events) {
        events.Add("source.Create");
        return [2,3];
    }

    private static RecordingConsumer<int> CreateConsumer(List<string> events,bool throwOnCreate = false) {
        events.Add("consumer.Create");
        if(throwOnCreate) {
            throw new IntentionalException("consumer.Create");
        }
        return new RecordingConsumer<int>(events);
    }

    private static void AssertConcatEvaluationMatches(
        Action<List<string>> system,
        Action<List<string>> optimized) {
        var expectedEvents=new List<string>();
        var actualEvents=new List<string>();

        var expectedException=Record.Exception(() => system(expectedEvents));
        var actualException=Record.Exception(() => optimized(actualEvents));

        Assert.Equal(expectedException?.GetType(),actualException?.GetType());
        Assert.Equal(expectedException?.Message,actualException?.Message);
        Assert.Equal(expectedEvents,actualEvents);
    }

    private static string? RecordException(Action action) {
        try {
            action();
            return null;
        } catch(IntentionalException exception) {
            return exception.Message;
        }
    }

    internal sealed class IntentionalException(string message):Exception(message);
}

internal sealed class RecordingConsumer<T>(List<string> events,int? throwAt = null) {
    private int consumeCount;
    public List<T> Items { get; }=[];

    public void Consume(T item) {
        events.Add("consumer.Consume("+item+")");
        this.consumeCount++;
        if(this.consumeCount==throwAt) {
            throw new DirectMaterializeConcatSemantics.IntentionalException("consumer");
        }
        this.Items.Add(item);
    }
}

internal sealed class TrackingEnumerable<T>(
    string name,
    IReadOnlyList<T> values,
    List<string> events,
    int? throwMoveNextAt = null,
    bool throwOnGetEnumerator = false):IEnumerable<T> {
    public IEnumerator<T> GetEnumerator() {
        events.Add(name+".GetEnumerator");
        if(throwOnGetEnumerator) {
            throw new DirectMaterializeConcatSemantics.IntentionalException(name+".GetEnumerator");
        }
        return new Enumerator(name,values,events,throwMoveNextAt);
    }

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

    private sealed class Enumerator(
        string name,
        IReadOnlyList<T> values,
        List<string> events,
        int? throwMoveNextAt):IEnumerator<T> {
        private int index=-1;
        private int moveNextCount;

        public T Current {
            get {
                events.Add(name+".Current("+this.index+")");
                return values[this.index];
            }
        }

        object? IEnumerator.Current => this.Current;

        public bool MoveNext() {
            events.Add(name+".MoveNext("+this.moveNextCount+")");
            if(this.moveNextCount++==throwMoveNextAt) {
                throw new DirectMaterializeConcatSemantics.IntentionalException(name+".MoveNext");
            }
            return ++this.index<values.Count;
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose() => events.Add(name+".Dispose");
    }
}

internal static class DirectMaterializeConcatConsumeSupport {
    public static void Consume<T>(this IEnumerable<T> source,RecordingConsumer<T> consumer) {
        foreach(var item in source) {
            consumer.Consume(item);
        }
    }

    public static void Consume<T>(this RecordingConsumer<T> consumer,IEnumerable<T> source) {
        foreach(var item in source) {
            consumer.Consume(item);
        }
    }
}
