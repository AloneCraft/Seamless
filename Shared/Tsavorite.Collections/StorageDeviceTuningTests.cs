namespace Shared.Tsavorite.Collections;

public class StorageDeviceTuningTests {
    [Fact]
    public void CreatePair_RandomBuffered_ReturnsDevices() {
        var directoryPath = Path.Combine(global::Seamless.TestKit.TestPath.Root,nameof(StorageDeviceTuningTests));
        Directory.CreateDirectory(directoryPath);
        try {
            var tuning = new global::Seamless.Collections.StorageTuning(
                FileAccess.ReadWrite,
                global::Seamless.Collections.StorageAccessPattern.Random,
                global::Seamless.Collections.StorageBufferingMode.Buffered);
            using var devices = global::Seamless.Collections.Tsavorite.TsavoriteStorageDevices.CreatePair(
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
