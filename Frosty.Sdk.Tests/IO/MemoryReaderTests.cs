using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.Tests.IO;

/// <summary>
///     Tests for <see cref="MemoryReader" />, reading the test process's own memory (no extra rights needed).
/// </summary>
internal sealed class MemoryReaderTests
{
    /// <summary>
    ///     Verify that the platform interop reads back exactly what is in memory.
    /// </summary>
    [Test]
    public void ReadExactly_ReadsOwnProcessMemory()
    {
        Assume.That(OperatingSystem.IsWindows() || OperatingSystem.IsLinux(), "MemoryReader only support Windows or Linux right now.");

        byte[] expected = GC.AllocateArray<byte>(64, true);
        RandomNumberGenerator.Fill(expected);

        using Process process = Process.GetCurrentProcess();
        using MemoryReader reader = new(process);
        reader.Position = Marshal.UnsafeAddrOfPinnedArrayElement(expected, 0);

        byte[] actual = new byte[expected.Length];
        reader.ReadExactly(actual);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(reader.Position, Is.EqualTo(Marshal.UnsafeAddrOfPinnedArrayElement(expected, 0) + 64));
        }
    }
}
