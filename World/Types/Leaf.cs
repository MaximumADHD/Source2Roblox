using System.IO;
using Source2Roblox.Util;
using RobloxFiles.DataTypes;

namespace Source2Roblox.World.Types
{
    public class Leaf
    {
        public readonly int Contents;
        public readonly ushort Cluster;

        public readonly ushort AreaFlags;
        public readonly short LeafWaterDataId;

        public readonly Vector3int16 BoundsMin;
        public readonly Vector3int16 BoundsMax;

        public readonly ushort FirstLeafFace;
        public readonly ushort NumLeafFaces;

        public readonly ushort FirstLeafBrush;
        public readonly ushort NumLeafBrushes;

        public readonly bool HasAmbientLighting;
        public readonly ColorRGBExp32[] AmbientLighting;

        public Leaf(BinaryReader reader)
        {
            Contents = reader.ReadInt32();
            Cluster = reader.ReadUInt16();

            AreaFlags = reader.ReadUInt16();
            BoundsMin = reader.ReadVector3int16();
            BoundsMax = reader.ReadVector3int16();

            FirstLeafFace = reader.ReadUInt16();
            NumLeafFaces = reader.ReadUInt16();

            FirstLeafBrush = reader.ReadUInt16();
            NumLeafBrushes = reader.ReadUInt16();
            LeafWaterDataId = reader.ReadInt16();
        }

        public Leaf(BinaryReader reader, BSPFile bsp) : this(reader)
        {
            if (bsp.Version <= 19)
            {
                HasAmbientLighting = true;
                AmbientLighting = new ColorRGBExp32[6];

                for (int i = 0; i < 6; i++)
                {
                    var color = new ColorRGBExp32(reader);
                    AmbientLighting[i] = color;
                }

                reader.Skip(2);
            }
        }
    }
}
