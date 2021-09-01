using RobloxFiles.DataTypes;
using System.IO;

namespace Source2Roblox.World.Types
{
    public class Face
    {
        public int FaceIndex;
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

        public int FirstUV;
        public int FirstVert;
        public int FirstNorm;

        public string Material;
        public int BrushIndex;
        public int LeafIndex;

        public bool Skip;
        public Leaf Leaf;

        public int EntityId = -1;
        public Vector3 Center = new Vector3();

        public override string ToString()
        {
            return $"{Material ?? ""}";
        }
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

            LightmapTextureMinsInLuxels = reader.ReadVector2Int32();
            LightmapTextureSizeInLuxels = reader.ReadVector2Int32();
            
            OriginalFace = reader.ReadInt32();
            NumPrimitives = reader.ReadUInt16();
            FirstPrimitiveId = reader.ReadUInt16();
            SmoothingGroups = reader.ReadUInt32();
        }
    }
}
