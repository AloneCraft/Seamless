// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
namespace Tsavorite.core;

// markerは操作固有で単調に変化し、受付の証明ではない。
// trusted adapterは外部APIや遅延所有者へ引き渡す前にmarkerを設定する。
internal interface IRecoverySubmission{
    void MarkMayHaveEscaped();
}

internal abstract class RecoverySubmissionAdapter(StorageDeviceBase device){
    internal static RecoverySubmissionAdapter TryCreate(IDevice inner){
        // exact typeは派生型のvirtual overrideを迂回せずprivate Core経路を選択する。
        // 拒否時の安全性は型判定ではなく操作のmarkerで判断する。
        if(inner.GetType()==typeof(NativeStorageDevice)){return new NativeAdapter((NativeStorageDevice)inner);}
        if(inner.GetType()==typeof(LocalStorageDevice)&&OperatingSystem.IsWindows()){return new LocalAdapter((LocalStorageDevice)inner);}
        if(inner.GetType()==typeof(RandomAccessLocalStorageDevice)){return new RandomAccessAdapter((RandomAccessLocalStorageDevice)inner);}
        return null;
    }
    internal abstract void ReadAsync(int segment,ulong address,IntPtr destination,uint length,DeviceIOCompletionCallback callback,object context,IRecoverySubmission submission);
    internal abstract void WriteAsync(IntPtr source,int segment,ulong address,uint length,DeviceIOCompletionCallback callback,object context,IRecoverySubmission submission);
    internal void ReadAsync(ulong address,IntPtr destination,uint length,DeviceIOCompletionCallback callback,object context,IRecoverySubmission submission){
        device.ResolveAddressForRecovery(address,out var segment,out var offset);
        this.ReadAsync(segment,offset,destination,length,callback,context,submission);
    }
    internal void WriteAsync(IntPtr source,ulong address,uint length,DeviceIOCompletionCallback callback,object context,IRecoverySubmission submission){
        device.ResolveAddressForRecovery(address,out var segment,out var offset);
        this.WriteAsync(source,segment,offset,length,callback,context,submission);
    }
    private sealed class NativeAdapter(NativeStorageDevice device):RecoverySubmissionAdapter(device){
        internal override void ReadAsync(int segment,ulong address,IntPtr destination,uint length,DeviceIOCompletionCallback callback,object context,IRecoverySubmission submission)
            =>device.ReadAsyncForRecovery(segment,address,destination,length,callback,context,submission);
        internal override void WriteAsync(IntPtr source,int segment,ulong address,uint length,DeviceIOCompletionCallback callback,object context,IRecoverySubmission submission)
            =>device.WriteAsyncForRecovery(source,segment,address,length,callback,context,submission);
    }
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private sealed class LocalAdapter:RecoverySubmissionAdapter{
        private readonly LocalStorageDevice device;
        internal LocalAdapter(LocalStorageDevice device):base(device){this.device=device;}
        internal override void ReadAsync(int segment,ulong address,IntPtr destination,uint length,DeviceIOCompletionCallback callback,object context,IRecoverySubmission submission)
            =>this.device.ReadAsyncForRecovery(segment,address,destination,length,callback,context,submission);
        internal override void WriteAsync(IntPtr source,int segment,ulong address,uint length,DeviceIOCompletionCallback callback,object context,IRecoverySubmission submission)
            =>this.device.WriteAsyncForRecovery(source,segment,address,length,callback,context,submission);
    }
    private sealed class RandomAccessAdapter:RecoverySubmissionAdapter{
        private readonly RandomAccessLocalStorageDevice device;
        internal RandomAccessAdapter(RandomAccessLocalStorageDevice device):base(device){this.device=device;}
        internal override void ReadAsync(int segment,ulong address,IntPtr destination,uint length,DeviceIOCompletionCallback callback,object context,IRecoverySubmission submission)
            =>this.device.ReadAsyncForRecovery(segment,address,destination,length,callback,context,submission);
        internal override void WriteAsync(IntPtr source,int segment,ulong address,uint length,DeviceIOCompletionCallback callback,object context,IRecoverySubmission submission)
            =>this.device.WriteAsyncForRecovery(source,segment,address,length,callback,context,submission);
    }
}
