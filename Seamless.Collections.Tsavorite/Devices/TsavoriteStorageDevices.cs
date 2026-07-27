namespace Seamless.Collections.Tsavorite;

public readonly record struct TsavoriteStorageDevicePair(IDevice LogDevice,IDevice ObjectLogDevice):IDisposable {
    public void Dispose() {
        this.ObjectLogDevice.Dispose();
        this.LogDevice.Dispose();
    }
}

public static class TsavoriteStorageDevices {
    public static TsavoriteStorageDevicePair CreatePair(
        string logPath,
        string objectLogPath,
        StorageTuning? tuning = null,
        bool preallocateFile = false,
        bool deleteOnClose = false,
        long capacity = -1,
        bool recoverDevice = false,
        bool readOnly = false,
        int numCompletionThreads = 0) {
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectLogPath);

        var actualTuning = tuning??StorageTuning.Default;
        var logDevice = Create(logPath,actualTuning,preallocateFile,deleteOnClose,capacity,recoverDevice,readOnly,numCompletionThreads);
        try {
            var objectLogDevice = Create(objectLogPath,actualTuning,preallocateFile,deleteOnClose,capacity,recoverDevice,readOnly,numCompletionThreads);
            return new(logDevice,objectLogDevice);
        } catch {
            logDevice.Dispose();
            throw;
        }
    }

    public static IDevice Create(
        string path,
        StorageTuning tuning,
        bool preallocateFile = false,
        bool deleteOnClose = false,
        long capacity = -1,
        bool recoverDevice = false,
        bool readOnly = false,
        int numCompletionThreads = 0) {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var deviceType = tuning.AccessPattern==StorageAccessPattern.Random
            ? DeviceType.RandomAccess
            : DeviceType.FileStream;
        var disableFileBuffering = tuning.BufferingMode==StorageBufferingMode.Unbuffered;
        if(disableFileBuffering) {
            deviceType=DeviceType.Native;
        }

        return global::Tsavorite.core.Devices.CreateLogDevice(
            path,
            deviceType: deviceType,
            preallocateFile: preallocateFile,
            deleteOnClose: deleteOnClose,
            capacity: capacity,
            recoverDevice: recoverDevice,
            disableFileBuffering: disableFileBuffering,
            readOnly: readOnly,
            ioBackend: NativeStorageDevice.IoBackend.Default,
            numCompletionThreads: numCompletionThreads);
    }
}
