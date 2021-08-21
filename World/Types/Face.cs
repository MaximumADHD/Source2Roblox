using RobloxFiles.DataTypes;
using System.IO;

namespace Source2Roblox.World.Types
{
    public class Face
    {
        public ushort PlaneNum;
        public byte Side;
        public bool OnNode;

        public int FirstEdge;
        public short NumEdges;
        public short TexInfo;
        public short DispInfo;
        public short SurfaceFogVolumeID;
        public byte[] Styles;

        public int LightOffset;
        public float Area;

        public Vector2 LightmapTextureMinsInLuxels;
        public Vector2 LightmapTextureSizeInLuxels;
        
        public int OriginalFace;
        public ushort NumPrimitives;
        public ushort FirstPrimitiveId;
        public uint SmoothingGroups;

        public int FirstNorm;
        public string Material;

        public Face(BinaryReader reader)
        {
            PlaneNum = reader.ReadUInt16();
            Side = reader.ReadByte();

            OnNode = reader.ReadBoolean();
            FirstEdge = reader.ReadInt32();
            NumEdges = reader.ReadInt16();
            TexInfo = reader.ReadInt16();
            DispInfo = reader.ReadInt16();

            SurfaceFogVolumeID = reader.ReadInt16();
            Styles = reader.ReadBytes(4);

            LightOffset = reader.ReadInt32();
            Area = reader.ReadSingle();

            LightmapTextureMinsInLuxels = reader.ReadIntVector2();
            LightmapTextureSizeInLuxels = reader.ReadIntVector2();
            
            OriginalFace = reader.ReadInt32();
            NumPrimitives = reader.ReadUInt16();
            FirstPrimitiveId = reader.ReadUInt16();
            SmoothingGroups = reader.ReadUInt32();
        }
    }
}
