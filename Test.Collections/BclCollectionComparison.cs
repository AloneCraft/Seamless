namespace Test.Collections;

public static class BclCollectionComparison{
    public static void SequenceEqual<T>(IEnumerable<T> expected,IEnumerable<T> actual){
        var expectedItems=expected.ToArray();
        var actualItems=actual.ToArray();
        if(!expectedItems.SequenceEqual(actualItems)){
            throw new InvalidOperationException($"Sequence mismatch. Expected [{string.Join(", ",expectedItems)}], actual [{string.Join(", ",actualItems)}].");
        }
    }

    public static void SetEqual<T>(IEnumerable<T> expected,IEnumerable<T> actual){
        var expectedSet=new HashSet<T>(expected);
        var actualSet=new HashSet<T>(actual);
        if(!expectedSet.SetEquals(actualSet)){
            throw new InvalidOperationException($"Set mismatch. Expected [{string.Join(", ",expectedSet)}], actual [{string.Join(", ",actualSet)}].");
        }
    }

    public static void DictionaryEqual<TKey,TValue>(
        IReadOnlyDictionary<TKey,TValue> expected,
        IReadOnlyDictionary<TKey,TValue> actual)
        where TKey : notnull{
        if(expected.Count!=actual.Count){
            throw new InvalidOperationException($"Dictionary count mismatch. Expected {expected.Count}, actual {actual.Count}.");
        }

        foreach(var pair in expected){
            if(!actual.TryGetValue(pair.Key,out var actualValue)){
                throw new InvalidOperationException($"Dictionary key was missing: {pair.Key}.");
            }

            if(!EqualityComparer<TValue>.Default.Equals(pair.Value,actualValue)){
                throw new InvalidOperationException($"Dictionary value mismatch for {pair.Key}. Expected {pair.Value}, actual {actualValue}.");
            }
        }
    }
}
