using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OneDriveServerTransfer.Destination;

/// <summary>
/// Test seam over the filesystem facts the destination layer depends on. The default
/// implementation uses <see cref="System.IO" /> and, on Windows only, a kernel32 call
/// for hard-link counts. Hard-link counting is Windows-runtime behavior; off Windows it
/// conservatively reports a single link (no link information available).
/// </summary>
internal interface IFileSystemProbe
{
    DriveType GetDriveType(string driveRoot);

    bool FileOrDirectoryExists(string fullPath);

    bool IsReparsePoint(string fullPath);

    /// <summary>
    /// Returns the number of hard links for an existing file. Returns 1 when the count
    /// cannot be determined (non-Windows runtime or unreadable metadata).
    /// </summary>
    int GetHardLinkCount(string fullPath);
}

internal sealed class SystemIOFileSystemProbe : IFileSystemProbe
{
    public DriveType GetDriveType(string driveRoot) => new DriveInfo(driveRoot).DriveType;

    public bool FileOrDirectoryExists(string fullPath) => File.Exists(fullPath) || Directory.Exists(fullPath);

    public bool IsReparsePoint(string fullPath)
    {
        if (!FileOrDirectoryExists(fullPath))
        {
            return false;
        }

        return (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0;
    }

    public int GetHardLinkCount(string fullPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return 1;
        }

        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return NativeMethods.GetFileInformationByHandle(stream.SafeFileHandle, out var information)
                ? (int)information.NumberOfLinks
                : 1;
        }
        catch (IOException)
        {
            return 1;
        }
        catch (UnauthorizedAccessException)
        {
            return 1;
        }
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandle(
            SafeFileHandle hFile,
            out ByHandleFileInformation lpFileInformation);

        [StructLayout(LayoutKind.Sequential)]
        internal struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }
    }
}
