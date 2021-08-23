using System;
using System.IO;

using RobloxFiles.DataTypes;

namespace Source2Roblox.World.Types
{
    [Flags]
    public enum BrushFlags
    {
        Empty        = 0x0,
        Solid        = 0x1,
        Window       = 0x2,
        Aux          = 0x4,
        Grate        = 0x8,
        Slime        = 0x10,
        Water        = 0x20,
        Mist         = 0x40,
        Opaque       = 0x80,
        TestFog      = 0x100,
        Unused       = 0x200,
        Unused6      = 0x400,
        Team1        = 0x800,
        Team2        = 0x1000,
        IgnoreNodraw = 0x2000,
        Moveable     = 0x4000,
        AreaPortal   = 0x8000,
        PlayerClip   = 0x10000,
        MonsterClip  = 0x20000,
        Current_0    = 0x40000,
        Current_90   = 0x80000,
        Current_180  = 0x100000,
        Current_270  = 0x200000,
        Current_Up   = 0x400000,
        Current_Down = 0x800000,
        Origin       = 0x1000000,
        Monster      = 0x2000000,
        Debris       = 0x4000000,
        Detail       = 0x8000000,
        Translucent  = 0x10000000,
        Ladder       = 0x20000000,
        Hitbox       = 0x40000000
    }

    public struct Brush
    {
        public readonly BrushFlags Contents;
        public readonly int FirstSide;
        public readonly int NumSides;

        public Brush(BinaryReader reader)
        {
            FirstSide = reader.ReadInt32();
            NumSides = reader.ReadInt32();
            Contents = (BrushFlags)reader.ReadInt32();
        }
    }

    public struct BrushSide
    {
        public readonly ushort PlaneNum;
        public readonly short TexInfo;
        public readonly short DispInfo;
        public readonly short Bevel;

        public BrushSide(BinaryReader reader)
        {
            PlaneNum = reader.ReadUInt16();
            TexInfo = reader.ReadInt16();
            DispInfo = reader.ReadInt16();
            Bevel = reader.ReadInt16();
        }
    }

    public class BrushModel
    {
        public Vector3 BoundsMin;
        public Vector3 BoundsMax;
        public Vector3 Origin;

        public int HeadNode;
        public int FirstFace;
        public int NumFaces;

        public BrushModel(BinaryReader reader)
        {
            BoundsMin = reader.ReadVector3();
            BoundsMax = reader.ReadVector3();
            Origin = reader.ReadVector3();

            HeadNode = reader.ReadInt32();
            FirstFace = reader.ReadInt32();
            NumFaces = reader.ReadInt32();
        }
    }
}
