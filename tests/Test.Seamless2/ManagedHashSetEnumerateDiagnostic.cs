using Seamless;
using Seamless.Stores;

namespace Test.Seamless.Diagnostics;

public class ManagedHashSetEnumerateDiagnostic {
    [Fact]
    public void ManagedHashSet_AfterFlushAndEvict_PublicEnumeration_Diagnostic() {
        var folder = Path.Combine(Environment.CurrentDirectory,"ManagedHashSetEnumerateDiagnostic",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var store = Factory.Create().Create<string>(logDevice,objDevice,new ManagedTag());
            using var set = store.Create();
            var expected = Enumerable.Range(0,128).Select(i => "s"+i).ToArray();
            foreach(var value in expected)
                set.Add(value);

            store.Store.Log.FlushAndEvict(wait: true);

            var enumerated = new global::System.Collections.Generic.List<string>();
            foreach(var value in set)
                enumerated.Add(value);

            var distinct = enumerated.Distinct(StringComparer.Ordinal).OrderBy(static value => value,StringComparer.Ordinal).ToArray();
            var missingFromEnumeration = expected.Where(value => !distinct.Contains(value,StringComparer.Ordinal)).ToArray();
            var missingFromContains = expected.Where(value => !set.Contains(value)).ToArray();
            Assert.Equal(expected.Length,enumerated.Count);
            Assert.Equal(expected.Length,distinct.Length);
            Assert.Empty(missingFromEnumeration);
            Assert.Empty(missingFromContains);
        }
        finally {
            try {
                Directory.Delete(folder,true);
            }
            catch {
            }
            try {
                Directory.Delete(folder+"_obj",true);
            }
            catch {
            }
        }
    }
}