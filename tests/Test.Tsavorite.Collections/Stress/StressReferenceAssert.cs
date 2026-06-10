namespace Test.Tsavorite.Stress;

internal static class StressReferenceAssert {
    public static void DictionaryEquals(IEnumerable<int> probeKeys,Dictionary<int,int> expected,Func<int,(bool Found,int Value)> actualTryGet) {
        foreach(var key in probeKeys) {
            var expectedFound = expected.TryGetValue(key,out var expectedValue);
            var (actualFound,actualValue)=actualTryGet(key);
            Assert.Equal(expectedFound,actualFound);
            if(expectedFound)
                Assert.Equal(expectedValue,actualValue);
        }
    }

    public static void DictionaryEquals(IEnumerable<string> probeKeys,Dictionary<string,string> expected,Func<string,(bool Found,string Value)> actualTryGet) {
        foreach(var key in probeKeys) {
            var expectedFound = expected.TryGetValue(key,out var expectedValue);
            var (actualFound,actualValue)=actualTryGet(key);
            Assert.Equal(expectedFound,actualFound);
            if(expectedFound)
                Assert.Equal(expectedValue,actualValue);
        }
    }

    public static void SetEquals(IEnumerable<int> probeKeys,HashSet<int> expected,Func<int,bool> actualContains) {
        foreach(var key in probeKeys)
            Assert.Equal(expected.Contains(key),actualContains(key));
    }

    public static void SetEquals(IEnumerable<string> probeKeys,HashSet<string> expected,Func<string,bool> actualContains) {
        foreach(var key in probeKeys)
            Assert.Equal(expected.Contains(key),actualContains(key));
    }
}
