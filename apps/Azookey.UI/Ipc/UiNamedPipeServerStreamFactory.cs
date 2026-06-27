using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Microsoft.Win32.SafeHandles;

namespace Azookey.UI.Ipc;

public static class UiNamedPipeServerStreamFactory
{
    private const uint SddlRevision = 1;
    private const uint PipeAccessDuplex = 0x00000003;
    private const uint PipeTypeByte = 0x00000000;
    private const uint PipeReadModeByte = 0x00000000;
    private const uint PipeWait = 0x00000000;
    private const uint PipeUnlimitedInstances = 255;
    private const uint FileFlagFirstPipeInstance = 0x00080000;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint FileFlagWriteThrough = 0x80000000;

    public static NamedPipeServerStream Create(CreateNamedPipeServerStreamContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string pipePath = $@"\\.\pipe\{context.NamedPipeEndPoint.PipeName}";
        IntPtr securityDescriptor = IntPtr.Zero;

        if (!ConvertStringSecurityDescriptorToSecurityDescriptor(
                UiNamedPipeSecurity.SecurityDescriptorSddl,
                SddlRevision,
                out securityDescriptor,
                out _))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            SecurityAttributes securityAttributes = new()
            {
                Length = Marshal.SizeOf<SecurityAttributes>(),
                SecurityDescriptor = securityDescriptor,
                InheritHandle = false,
            };

            uint openMode = PipeAccessDuplex | GetOpenModeFlags(context.PipeOptions);
            SafePipeHandle handle = CreateNamedPipe(
                pipePath,
                openMode,
                PipeTypeByte | PipeReadModeByte | PipeWait,
                PipeUnlimitedInstances,
                outBufferSize: 0,
                inBufferSize: 0,
                defaultTimeout: 0,
                ref securityAttributes);

            if (handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            bool isAsync = context.PipeOptions.HasFlag(PipeOptions.Asynchronous);
            return new NamedPipeServerStream(PipeDirection.InOut, isAsync, isConnected: false, handle);
        }
        finally
        {
            if (securityDescriptor != IntPtr.Zero)
            {
                _ = LocalFree(securityDescriptor);
            }
        }
    }

    private static uint GetOpenModeFlags(PipeOptions pipeOptions)
    {
        uint flags = 0;

        if (pipeOptions.HasFlag(PipeOptions.Asynchronous))
        {
            flags |= FileFlagOverlapped;
        }

        if (pipeOptions.HasFlag(PipeOptions.WriteThrough))
        {
            flags |= FileFlagWriteThrough;
        }

        if (pipeOptions.HasFlag(PipeOptions.FirstPipeInstance))
        {
            flags |= FileFlagFirstPipeInstance;
        }

        return flags;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
        string stringSecurityDescriptor,
        uint stringSdRevision,
        out IntPtr securityDescriptor,
        out uint securityDescriptorSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafePipeHandle CreateNamedPipe(
        string pipeName,
        uint openMode,
        uint pipeMode,
        uint maxInstances,
        uint outBufferSize,
        uint inBufferSize,
        uint defaultTimeout,
        ref SecurityAttributes securityAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;

        [MarshalAs(UnmanagedType.Bool)]
        public bool InheritHandle;
    }
}
