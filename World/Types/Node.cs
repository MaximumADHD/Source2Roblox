using RobloxFiles.DataTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Source2Roblox.World.Types
{
    public class Node
    {
        public readonly int PlaneNum;
        public readonly int[] Children;

        public readonly Vector3int16 BoundsMin;
        public readonly Vector3int16 BoundsMax;

        public readonly ushort FirstFace;
        public readonly ushort NumFaces;

        public readonly short Area;
        public readonly short Padding;

        public Node(BinaryReader reader)
        {
            PlaneNum = reader.ReadInt32();
            Children = new int[2];

            for (int i = 0; i < 2; i++)
                Children[i] = reader.ReadInt32();

            BoundsMin = reader.ReadVector3int16();
            BoundsMax = reader.ReadVector3int16();

            FirstFace = reader.ReadUInt16();
            NumFaces = reader.ReadUInt16();

            Area = reader.ReadInt16();
            Padding = reader.ReadInt16();
        }
    }
}
