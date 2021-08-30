using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;

using RobloxFiles.DataTypes;
using Source2Roblox.Util;

namespace Source2Roblox.World.Types
{
    public enum DetailPropOrientation : byte
    {
        NORMAL,
        SCREEN_ALIGNED,
        SCREEN_ALIGNED_VERTICAL
    }

    public enum DetailPropType : byte
    {
        MODEL,
        SPRITE,
        SHAPE_CROSS,
        SHAPE_TRI
    }

    [Flags]
    public enum StaticPropFlags
    {
        NONE                   = 0x0000,
        USE_LIGHTING_ORIGIN    = 0x0002,
        IGNORE_NORMALS         = 0x0008,
        NO_SHADOW              = 0x0010,
        SCREEN_SPACE_FADE      = 0x0020,
        NO_PER_VERTEX_LIGHTING = 0x0040,
        NO_PER_TEXEL_LIGHTING  = 0x0100,
    }

    public class DetailModel
    {
        public readonly Vector3 Position;
        public readonly Vector3 Rotation;

        public readonly ushort ModelIndex;
        public readonly ushort LeafIndex;

        public readonly ColorRGBExp32 Lighting;

        public readonly int LightStyles;
        public readonly int NumLightStyles;

        public readonly byte SwayAmount;
        public readonly byte ShapeAngle;
        public readonly byte ShapeSize;

        public readonly DetailPropOrientation Orientation;
        public readonly DetailPropType Type;

        public readonly float Scale;

        public DetailModel(BinaryReader reader)
        {
            Position = reader.ReadVector3();
            Rotation = reader.ReadVector3();

            ModelIndex = reader.ReadUInt16();
            LeafIndex = reader.ReadUInt16();

            var argb = reader.ReadInt32();
            Lighting = Color.FromArgb(argb);

            LightStyles = reader.ReadInt32();
            NumLightStyles = reader.ReadByte();

            SwayAmount = reader.ReadByte();
            ShapeAngle = reader.ReadByte();
            ShapeSize = reader.ReadByte();

            Orientation = (DetailPropOrientation)reader.ReadByte();
            Type = (DetailPropType)reader.ReadByte();

            Scale = reader.ReadSingle();
        }
    }

    public class DetailSprite
    {
        public Vector2 Size;
        public Rect Crop;

        public DetailSprite(BinaryReader reader)
        {
            float x0 = reader.ReadSingle(),
                  y0 = reader.ReadSingle(),
                  x1 = reader.ReadSingle(),
                  y1 = reader.ReadSingle();

            float crop_x0 = reader.ReadSingle(),
                  crop_y0 = reader.ReadSingle(),
                  crop_x1 = reader.ReadSingle(),
                  crop_y1 = reader.ReadSingle();

            float width = x1 - x0,
                  height = y1 - y0;

            Size = new Vector2(width, height);
            Crop = new Rect(crop_x0, crop_y0, crop_x1, crop_y1);
        }
    }

    public class StaticProp
    {
        public Vector3 Position;
        public Vector3 Rotation;

        public ushort PropType;
        public ushort FirstLeaf;
        public ushort LeafCount;

        public byte Solidity;
        public StaticPropFlags Flags;

        public int Skin;
        public string Name;

        public float FadeMinDist;
        public float FadeMaxDist;

        public Vector3 LightingOrigin;
        public float? ForcedFadeScale;

        public ushort? MinDXLevel;
        public ushort? MaxDXLevel;

        public ushort? LightmapResX;
        public ushort? LightmapResY;

        public byte? MinCPULevel;
        public byte? MaxCPULevel;

        public byte? MinGPULevel;
        public byte? MaxGPULevel;

        public Color Color;
        public int? DisableX360;

        public uint? ExtraFlags;
        public float Scale = 1f;

        public StaticProp(BSPFile bsp, GameLump lump, BinaryReader reader)
        {
            var version = lump.Version;
            var bspVersion = bsp.Version;

            Position = reader.ReadVector3();
            Rotation = reader.ReadVector3();

            PropType = reader.ReadUInt16();
            FirstLeaf = reader.ReadUInt16();
            LeafCount = reader.ReadUInt16();

            Solidity = reader.ReadByte();
            Flags = (StaticPropFlags)reader.ReadByte();

            Skin = reader.ReadInt32();
            FadeMinDist = reader.ReadSingle();
            FadeMaxDist = reader.ReadSingle();
            LightingOrigin = reader.ReadVector3();

            if (version >= 5)
                ForcedFadeScale = reader.ReadSingle();
            
            if (version >= 6 && version <= 7)
            {
                MinDXLevel = reader.ReadUInt16();
                MaxDXLevel = reader.ReadUInt16();
            }

            if (version >= 8)
            {
                MinCPULevel = reader.ReadByte();
                MaxCPULevel = reader.ReadByte();

                MinGPULevel = reader.ReadByte();
                MaxGPULevel = reader.ReadByte();
            }

            if (bspVersion == 21)
            {
                if (version >= 7)
                {
                    int argb = reader.ReadInt32();
                    Color = Color.FromArgb(argb);
                }

                if (version >= 9)  DisableX360 = reader.ReadInt32();
                if (version >= 10) ExtraFlags = reader.ReadUInt32();
                if (version >= 11) Scale = reader.ReadSingle();
            }
            else if (bspVersion == 19 || bspVersion == 20)
            {
                if (version >= 7)
                {
                    Flags = (StaticPropFlags)reader.ReadUInt32();
                    LightmapResX = reader.ReadUInt16();
                    LightmapResY = reader.ReadUInt16();
                }
            }

            if ((Flags & StaticPropFlags.USE_LIGHTING_ORIGIN) != StaticPropFlags.NONE)
                return;

            LightingOrigin = null;
        }
    }

    public class DetailProps
    {
        public List<string> Names = new List<string>();
        public List<DetailModel> DetailModels = new List<DetailModel>();
        public List<DetailSprite> DetailSprites = new List<DetailSprite>();

        public DetailProps(BinaryReader reader)
        {
            int numNames = reader.ReadInt32();

            for (int i = 0; i < numNames; i++)
            {
                string name = reader.ReadString(128);
                Names.Add(name);
            }

            var numSprites = reader.ReadInt32();

            for (int i = 0; i < numSprites; i++)
            {
                var sprite = new DetailSprite(reader);
                DetailSprites.Add(sprite);
            }

            var numDetailModels = reader.ReadInt32();

            for (int i = 0; i < numDetailModels; i++)
            {
                var model = new DetailModel(reader);
                DetailModels.Add(model);
            }
        }
    }
}
