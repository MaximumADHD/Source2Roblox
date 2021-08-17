using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using RobloxFiles.DataTypes;
using Microsoft.Win32;

public static class Extensions
{
    public static void Skip(this BinaryReader reader, int bytes)
    {
        var stream = reader.BaseStream;
        stream.Position += bytes;
    }

    public static void JumpTo(this BinaryReader reader, long position)
    {
        var stream = reader.BaseStream;
        stream.Position = position;
    }

    public static Action SetWaypoint(this BinaryReader reader)
    {
        long pos = reader.BaseStream.Position;
        return new Action(() => JumpTo(reader, pos));
    }

    public static Vector3 ReadVector3(this BinaryReader reader)
    {
        float x = reader.ReadSingle(),
              y = reader.ReadSingle(),
              z = reader.ReadSingle();

        return new Vector3(x, y, z);
    }

    public static Vector2 ReadIntVector2(this BinaryReader reader)
    {
        int x = reader.ReadInt32(),
            y = reader.ReadInt32();

        return new Vector2(x, y);
    }

    public static Vector2 ReadVector2(this BinaryReader reader)
    {
        float x = reader.ReadSingle(),
              y = reader.ReadSingle();

        return new Vector2(x, y);
    }

    public static string ReadString(this BinaryReader reader, int? length, Encoding encoding = null)
    {
        var stream = reader.BaseStream;
        var buffer = new List<byte>();
        int len = length ?? -1;

        while (stream.Position < stream.Length)
        {
            if (len >= 0 && buffer.Count >= len)
                break;

            byte next = reader.ReadByte();

            if (next == 0 && length == null)
                break;

            buffer.Add(next);
        }

        // Remove trailing null bytes

        for (int i = buffer.Count - 1; i >= 0; i--)
        {
            byte last = buffer[i];

            if (last != 0)
                break;

            buffer.RemoveAt(i);
        }

        // Convert to UTF-8
        var bytes = buffer.ToArray();

        if (encoding == null)
            encoding = Encoding.UTF8;
        
        return encoding.GetString(bytes);
    }

    public static RegistryKey GetSubKey(this RegistryKey key, params string[] path)
    {
        string constructedPath = Path.Combine(path);
        return key.CreateSubKey(constructedPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.None);
    }

    public static string GetString(this RegistryKey key, string name)
    {
        var result = key.GetValue(name, "");
        return result.ToString();
    }
}

