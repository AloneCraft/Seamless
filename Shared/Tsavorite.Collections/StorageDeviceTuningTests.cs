#if BENCHMARK_PROJECT
namespace Benchmark.Tsavorite.Collections;
#else
namespace Tsavorite.Collections;
#endif

public class StorageDeviceTuningTests {
    [Fact]
    public void CreatePair_RandomBuffered_ReturnsDevices() {
        var directoryPath = Path.Combine(Environment.CurrentDirectory,nameof(StorageDeviceTuningTests));
        Directory.CreateDirectory(directoryPath);
        try {
            var tuning = new global::Seamless.Persisted.Collections.StorageTuning(
                FileAccess.ReadWrite,
                global::Seamless.Persisted.Collections.StorageAccessPattern.Random,
                global::Seamless.Persisted.Collections.StorageBufferingMode.Buffered);
            using var devices = global::Seamless.Tsavorite.Devices.TsavoriteStorageDevices.CreatePair(
                Path.Combine(directoryPath,"log.dat"),
                Path.Combine(directoryPath,"object-log.dat"),
                tuning);

            Assert.True(devices.LogDevice.SectorSize>0);
            Assert.True(devices.ObjectLogDevice.SectorSize>0);
        } finally {
            try {
                Directory.Delete(directoryPath,recursive: true);
            } catch {
            }
        }
    }
}
