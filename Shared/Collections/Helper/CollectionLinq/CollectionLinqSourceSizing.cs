namespace Collection.Helper.CollectionLinq;

public static class CollectionLinqSourceSizing {
    public static long GetMemorySize(int count) {
        var required = Math.Max(8192L,count*128L);
        var result = 1L;
        while(result<required) {
            result<<=1;
        }
        return result;
    }
}
