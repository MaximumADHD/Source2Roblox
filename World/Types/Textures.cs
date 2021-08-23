using System;
using System.IO;
using RobloxFiles.DataTypes;

namespace Source2Roblox.World.Types
{
    [Flags]
    public enum TextureFlags
    {
        None = 0x0,
        Light = 0x1,
        Sky2D = 0x2,
        Sky = 0x4,
        Warp = 0x8,
        Trans = 0x10,
        NoPortal = 0x20,
        Trigger = 0x40,
        NoDraw = 0x80,
        Hint = 0x100,
        Skip = 0x200,
        NoLight = 0x400,
        BumpLight = 0x800,
        NoShadows = 0x1000,
        NoDecals = 0x2000,
        NoChop = 0x4000,
        Hitbox = 0x8000
    }

    public class TextureVector : Vector3
    {
        public readonly float Offset;
        public override string ToString() => $"{X}, {Y}, {Z}, {Offset}";

        public TextureVector(float x, float y, float z, float offset) : base(x, y, z)
        {
            Offset = offset;
        }

        public TextureVector(Vector3 vec, float offset) : base(vec.X, vec.Y, vec.Z)
        {
            Offset = offset;
        }
    }

    public class Texel
    {
        public readonly TextureVector S;
        public readonly TextureVector T;

        public override string ToString() => $"{{{S}}}, {{{T}}}";

        public Texel(TextureVector s, TextureVector t)
        {
            S = s;
            T = t;
        }

        public Texel(BinaryReader reader)
        {
            Vector3 vec0 = reader.ReadVector3();
            float off0 = reader.ReadSingle();

            Vector3 vec1 = reader.ReadVector3();
            float off1 = reader.ReadSingle();

            S = new TextureVector(vec0, off0);
            T = new TextureVector(vec1, off1);
        }

        public Vector2 CalcTexCoord(Vector3 v, Vector2 size = null)
        {
            if (size == null)
                size = new Vector2(1, 1);

            float x = v.Dot(S) + S.Offset,
                  y = v.Dot(T) + T.Offset;

            return new Vector2(x, y) / size;
        }
    }

    public class TextureInfo
    {
        public readonly Texel TextureVecs;
        public readonly Texel LightmapVecs;

        public readonly TextureFlags Flags;
        public readonly int TextureData;

        public TextureInfo(BinaryReader reader)
        {
            TextureVecs = new Texel(reader);
            LightmapVecs = new Texel(reader);

            Flags = (TextureFlags)reader.ReadInt32();
            TextureData = reader.ReadInt32();
        }
    }

    public class TextureData
    {
        public readonly Vector3 Reflectivity;
        public readonly int StringTableIndex;

        public readonly Vector2 Size;
        public readonly Vector2 ViewSize;
        
        public TextureData(BinaryReader reader)
        {
            Reflectivity = reader.ReadVector3();
            StringTableIndex = reader.ReadInt32();

            Size = reader.ReadVector2Int32();
            ViewSize = reader.ReadVector2Int32();
        }
    }
}
