using RobloxFiles.DataTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Source2Roblox.World.Types
{
    [Flags]
    public enum DispTriTags
    {
        None = 0,
        Surface = 1,
        Walkable = 2,
        Buildable = 4,
        SurfProp1 = 8,
        SurfProp2 = 16,
    }

    public struct DispVert
    {
        public readonly Vector3 Vector;
        public readonly float Dist;
        public readonly float Alpha;

        public DispVert(BinaryReader reader)
        {
            Vector = reader.ReadVector3();
            Dist = reader.ReadSingle();
            Alpha = reader.ReadSingle();
        }

        public override string ToString()
        {
            return $"[{Vector}] {Dist} {Alpha}";
        }
    }

    public class DispInfo
    {
        public Vector3 StartPosition;

        public int DispVertStart;
        public int DispTriStart;

        public int Power;
        public int MinTess;

        public float SmoothingAngle;
        public int Contents;

        public ushort MapFace;
        public int LightmapAlphaStart;
        public int LightmapSamplePositionStart;

        public DispInfo(BinaryReader reader)
        {
            StartPosition = reader.ReadVector3();

            DispVertStart = reader.ReadInt32();
            DispTriStart = reader.ReadInt32();

            Power = reader.ReadInt32();
            MinTess = reader.ReadInt32();

            SmoothingAngle = reader.ReadSingle();
            Contents = reader.ReadInt32();

            MapFace = reader.ReadUInt16();
            LightmapAlphaStart = reader.ReadInt32();
            LightmapSamplePositionStart = reader.ReadInt32();

            // Skip neighbor rules and allowed verts.
            reader.Skip(130);
        }
    }
}
