using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using RobloxFiles.DataTypes;
using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;

public static class Extensions
{
    private static readonly Type BinaryReader = typeof(BinaryReader);
    
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

    public static ConstructorInfo GetConstructor(this Type type, params Type[] args)
    {
        return type.GetConstructor(args);
    }

    public static T Invoke<T>(this ConstructorInfo constructor, params object[] args)
    {
        return (T)constructor.Invoke(args);
    }

    public static void ReadToEnd<T>(this BinaryReader reader, List<T> list, Func<T> readNext = null)
    {
        var type = typeof(T);
        var stream = reader.BaseStream;

        if (readNext == null)
        {
            // this variable is fine, don't know 
            // what intellisense is on about...

            #pragma warning disable IDE0059 
            var construct = type.GetConstructor(BinaryReader);

            #pragma warning restore IDE0059
            readNext = new Func<T>(() => construct.Invoke<T>(reader));
        }

        Debug.Assert(readNext != null, $"No constructor for {type.Name} takes a BinaryReader!");

        while (stream.Position < stream.Length)
        {
            var inst = readNext();
            list.Add(inst);
        }
    }

    public static Vector3 ReadVector3(this BinaryReader reader)
    {
        float x = reader.ReadSingle(),
              y = reader.ReadSingle(),
              z = reader.ReadSingle();

        return new Vector3(x, y, z);
    }

    public static Vector2 ReadVector2(this BinaryReader reader)
    {
        float x = reader.ReadSingle(),
              y = reader.ReadSingle();

        return new Vector2(x, y);
    }

    public static Vector2 ReadVector2Int32(this BinaryReader reader)
    {
        int x = reader.ReadInt32(),
            y = reader.ReadInt32();

        return new Vector2(x, y);
    }

    public static string ReadString(this BinaryReader reader, int? length, Encoding encoding = null)
    {
        var stream = reader.BaseStream;
        long len = length ?? -1;
        
        if (len <= 0)
        {
            var start = stream.Position;

            while (reader.PeekChar() > 0)
                stream.Position++;

            len = stream.Position - start + 1;
            stream.Position = start;
        }

        byte[] buffer = reader.ReadBytes((int)len);

        if (encoding == null)
            encoding = Encoding.UTF8;

        string result = encoding
            .GetString(buffer)
            .TrimEnd('\0');

        return result;
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

