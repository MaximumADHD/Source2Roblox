using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;

using RobloxFiles.DataTypes;
using Microsoft.Win32;
using RobloxFiles;

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

    public static Vector3int16 ReadVector3int16(this BinaryReader reader)
    {
        short x = reader.ReadInt16(),
              y = reader.ReadInt16(),
              z = reader.ReadInt16();

        return new Vector3int16(x, y, z);
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

    public static Color Lerp(this Color c0, Color c1, float t)
    {
        int a = c0.A + (int)((c1.A - c0.A) * t),
            r = c0.R + (int)((c1.R - c0.R) * t),
            g = c0.G + (int)((c1.G - c0.G) * t),
            b = c0.B + (int)((c1.B - c0.B) * t);
        
        return Color.FromArgb(a, r, g, b);
    }

    public static Region3 GetBoundingBox(this Model model)
    {
        Func<float, float> abs = Math.Abs;

        float min_X = float.MaxValue,
              min_Y = float.MaxValue,
              min_Z = float.MaxValue;

        float max_X = float.MinValue,
              max_Y = float.MinValue,
              max_Z = float.MinValue;

        foreach (var part in model.GetDescendantsOfType<BasePart>())
        {
            var cf = part.CFrame;
            var size = part.Size;

            float sx = size.X,
                  sy = size.Y,
                  sz = size.Z;

            var matrix = cf.GetComponents();

            float x   = matrix[0], y   = matrix[1],  z   = matrix[2],
                  R00 = matrix[3], R01 = matrix[4],  R02 = matrix[5],
                  R10 = matrix[6], R11 = matrix[7],  R12 = matrix[8],
                  R20 = matrix[9], R21 = matrix[10], R22 = matrix[11];

            // https://zeuxcg.org/2010/10/17/aabb-from-obb-with-component-wise-abs/

            float ws_X = (abs(R00) * sx + abs(R01) * sy + abs(R02) * sz) / 2f,
                  ws_Y = (abs(R10) * sx + abs(R11) * sy + abs(R12) * sz) / 2f,
                  ws_Z = (abs(R20) * sx + abs(R21) * sy + abs(R22) * sz) / 2f;

            min_X = Math.Min(min_X, x - ws_X);
            min_Y = Math.Min(min_Y, y - ws_Y);
            min_Z = Math.Min(min_Z, z - ws_Z);

            max_X = Math.Max(max_X, x + ws_X);
            max_Y = Math.Max(max_Y, y + ws_Y);
            max_Z = Math.Max(max_Z, z + ws_Z);
        }

        var min = new Vector3(min_X, min_Y, min_Z);
        var max = new Vector3(max_X, max_Y, max_Z);

        return new Region3(min, max);
    }

    public static void SetPrimaryPartCFrame(this Model model, CFrame cf)
    {
        var primary = model.PrimaryPart;

        if (primary == null)
            throw new Exception("Model:SetPrimaryPartCFrame() failed because no PrimaryPart has been set, or the PrimaryPart no longer exists. Please set Model.PrimaryPart before using this.");

        var reference = primary.CFrame;
        primary.CFrame = cf;

        foreach (var part in model.GetDescendantsOfType<BasePart>())
        {
            if (part != primary)
            {
                var offset = reference.ToObjectSpace(part.CFrame);
                part.CFrame = cf * offset;
            }
        }
    }

    public static void PivotTo(this PVInstance pv, CFrame cf)
    {
        if (pv is BasePart basePart)
        {
            var pivotCFrame = cf * basePart.PivotOffset;
            basePart.CFrame = pivotCFrame;
        }
        else if (pv is Model model)
        {
            var primary = model.PrimaryPart;
            CFrame worldPivot;

            if (primary != null)
            {
                var offset = primary.PivotOffset;
                worldPivot = primary.CFrame * offset;
            }
            else
            {
                Optional<CFrame> optional = model.WorldPivotData;
                worldPivot = optional.Value;

                if (worldPivot == null)
                {
                    Region3 aabb = model.GetBoundingBox();
                    worldPivot = aabb.CFrame;
                }
            }

            foreach (var part in model.GetDescendantsOfType<BasePart>())
            {
                var offset = worldPivot.ToObjectSpace(part.CFrame);
                part.CFrame = cf * offset;
            }
        }
    }
}

