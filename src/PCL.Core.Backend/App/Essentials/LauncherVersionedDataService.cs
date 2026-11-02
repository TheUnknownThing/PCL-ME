using System;
using System.Buffers.Binary;

namespace PCL.Core.App.Essentials;

public static class LauncherVersionedDataService
{
    private const uint MagicNumber = 0x454E4321;

    public static LauncherVersionedData Parse(ReadOnlySpan<byte> bytes)
    {
        // 0 - 4 magic number | 4 - 8 version | 8 - 12 payload length | n bytes payload
        if (bytes.Length < 12)
        {
            throw new ArgumentException("No enough data for LauncherVersionedData", nameof(bytes));
        }

        if (BinaryPrimitives.ReadUInt32BigEndian(bytes[..4]) != MagicNumber)
        {
            throw new ArgumentException("Unknown data for LauncherVersionedData", nameof(bytes));
        }

        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(bytes[8..12]);
        if (payloadLength > bytes.Length - 12)
        {
            throw new ArgumentException("No enough data for LauncherVersionedData", nameof(bytes));
        }

        if (payloadLength < 0)
        {
            throw new ArgumentException("Invalid data length for LauncherVersionedData", nameof(bytes));
        }

        return new LauncherVersionedData(
            Version: BinaryPrimitives.ReadUInt32BigEndian(bytes[4..8]),
            Data: bytes[12..(12 + payloadLength)].ToArray());
    }

    public static LauncherVersionedData ParseBase64(string base64)
    {
        return Parse(Convert.FromBase64String(base64));
    }

    public static byte[] Serialize(LauncherVersionedData data)
    {
        ArgumentNullException.ThrowIfNull(data.Data);

        var bytes = new byte[12 + data.Data.Length];
        var bytesSpan = bytes.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(bytesSpan[..4], MagicNumber);
        BinaryPrimitives.WriteUInt32BigEndian(bytesSpan[4..8], data.Version);
        BinaryPrimitives.WriteInt32BigEndian(bytesSpan[8..12], data.Data.Length);
        data.Data.CopyTo(bytesSpan[12..]);
        return bytes;
    }

    public static bool IsValid(ReadOnlySpan<byte> data)
    {
        try
        {
            return data.Length >= 12 && BinaryPrimitives.ReadUInt32BigEndian(data[..4]) == MagicNumber;
        }
        catch
        {
            return false;
        }
    }
}

public readonly record struct LauncherVersionedData(
    uint Version,
    byte[] Data);
