using System;
using System.IO;
using System.Linq;
using System.Drawing;
using RobloxFiles.DataTypes;
using System.Collections.Generic;
using System.Collections;

namespace Source2Roblox.World.Types
{
    [Flags]
    public enum StaticPropFlags
    {
        None = 0x0,
        UseLightingOrigin = 0x2,
        IgnoreNormals = 0x8,
        NoShadow = 0x10,
        ScreenSpaceFade = 0x20,
        NoPerVertexLighting = 0x40,
        NoPerTexelLighting = 0x100,
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

        public StaticProp(BSPFile bsp, GameLump lump, BinaryReader reader, long size)
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

                if (version >= 10 && size >= 76)
                {
                    uint rgba = reader.ReadUInt32();
                    uint argb = (rgba << 24) | (rgba >> 8);

                    if (size >= 80)
                        ExtraFlags = reader.ReadUInt32();

                    Color = Color.FromArgb((int)argb);
                }
            }

            if ((Flags & StaticPropFlags.UseLightingOrigin) != StaticPropFlags.None)
                return;

            LightingOrigin = null;
        }
    }

    public class StaticProps : IEnumerable<StaticProp>
    {
        public List<StaticProp> Props;
        public string[] Strings;
        public ushort[] Leaves;

        public StaticProps(BSPFile bsp, GameLump sprp, BinaryReader reader)
        {
            int numStrings = reader.ReadInt32();
            var strings = new string[numStrings];

            for (int i = 0; i < numStrings; i++)
                strings[i] = reader.ReadString(128);

            int numLeaves = reader.ReadInt32();
            var leaves = new ushort[numLeaves];

            for (int i = 0; i < numLeaves; i++)
                leaves[i] = reader.ReadUInt16();

            var numProps = reader.ReadInt32();
            var props = new StaticProp[numProps];

            var stream = reader.BaseStream;
            var remaining = stream.Length - stream.Position;

            if (remaining > 0 && numProps > 0)
            {
                var staticPropSize = remaining / numProps;

                for (int i = 0; i < numProps; i++)
                {
                    var prop = new StaticProp(bsp, sprp, reader, staticPropSize);
                    prop.Name = strings[prop.PropType];
                    props[i] = prop;
                }
            }
            
            Props = props.ToList();
            Strings = strings;
            Leaves = leaves;
        }

        public IEnumerator<StaticProp> GetEnumerator()
        {
            return Props.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Props.GetEnumerator();
        }
    }
}
