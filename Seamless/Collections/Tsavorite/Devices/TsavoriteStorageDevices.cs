using EqualityEmpty = Seamless.Collections.Tsavorite.EqualityEmpty;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpanByteStoreAllocator = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordTriggers
    >
>;
using SpanByteStoreFunctions = Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordTriggers
>;
using Garnet.common;
using Seamless.Collections;
using Tsavorite.core;
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
            numCompletionThreads: numCompletionThreads);
    }
}
