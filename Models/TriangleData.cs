using System;
using System.Diagnostics;
using System.IO;
using RobloxFiles.DataTypes;

namespace Source2Roblox.Models
{
    [Flags]
    public enum StripFlags : byte
    {
        None,
        IsTriList,
        IsTriStrip
    }

    [Flags]
    public enum StripGroupFlags : byte
    {
        None,
        IsFlexed = 0x01,
        IsHardwareSkinned = 0x02,
        IsDeltaFlexed = 0x04,
        SuppressHardwareMorph = 0x08
    }

    [Flags]
    public enum StudioMeshFlags : byte
    {
        None,
        IsTeeth,
        IsEyes
    }

    public class StripVertex
    {
        public float[] BoneWeights;
        public byte[] BoneIds;

        public StripGroup Group;
        public int Index;

        public override string ToString()
        {
            return $"{Index}";
        }

        public static implicit operator int(StripVertex vertex)
        {
            return vertex.Index;
        }
    }

    public class StripGroup
    {
        public StripGroupFlags Flags;
        public StripVertex[] Verts;
        public StudioMesh Mesh;

        public override string ToString()
        {
            return $"StripGroup (Flags: {Flags})";
        }
    }

    public class StudioMesh
    {
        public StudioMeshFlags Flags;
        public StripGroup[] StripGroups;
        public ushort[] Indices;

        public StudioModelLOD LOD;
        public string[] MaterialNames;

        public int SkinRefIndex;
        public int ModelIndex;

        public int NumVertices;
        public int VertexOffset;

        public int NumFlexes;
        public int FlexIndex;

        public int MaterialType;
        public int MaterialParam;

        public int MeshId;
        public Vector3 Center;

        public override string ToString()
        {
            return $"StudioMesh (Flags: {Flags})";
        }
    }

    public class StudioModelLOD
    {
        public float SwitchPoint;
        public StudioMesh[] Meshes;
        public StudioModel Model;

        public override string ToString()
        {
            return $"LOD (SwitchPoint: {SwitchPoint})";
        }
    }

    public class StudioModel
    {
        public StudioModelLOD[] LODs;
        public StudioBodyPart BodyPart;
        
        public string Name;
        public float BoundingRadius;

        public int NumMeshes;
        public int MeshIndex;

        public int NumVertices;
        public int VertexIndex;
        public int TangentIndex;

        public int NumAttachments;
        public int AttachmentIndex;

        public int NumEyeballs;
        public int EyeballIndex;

        public override string ToString()
        {
            return $"StudioModel ({Name})";
        }
    }

    public class StudioBodyPart
    {
        public StudioModel[] Models;
        public TriangleData Root;

        public string Name;
        public int Base;

        public override string ToString()
        {
            return $"BodyPart ({Name})";
        }
    }

    public class TriangleData
    {
        public readonly int Version;
        public readonly int VertCacheSize;

        public readonly ushort MaxBonesPerStrip;
        public readonly ushort MaxBonesPerTri;
        public readonly int MaxBonesPerVert;

        public readonly int Checksum;
        public readonly int NumLODs;

        public readonly int NumBodyParts;
        public readonly int BodyPartOffset;

        public readonly StudioBodyPart[] BodyParts;
        public readonly uint MaterialReplacementListOffset;

        public TriangleData(ModelHeader mdl, BinaryReader reader)
        {
            Version = reader.ReadInt32();
            Debug.Assert(Version == 7, $"Unsupported VTX version: {Version} (expected 7!)");

            VertCacheSize = reader.ReadInt32();
            MaxBonesPerStrip = reader.ReadUInt16();
            MaxBonesPerTri = reader.ReadUInt16();
            MaxBonesPerVert = reader.ReadInt32();

            Checksum = reader.ReadInt32();
            Debug.Assert(Checksum == mdl.Checksum, "VTX checksum didn't match MDL checksum!");

            NumLODs = reader.ReadInt32();
            MaterialReplacementListOffset = reader.ReadUInt32();

            NumBodyParts = reader.ReadInt32();
            Debug.Assert(NumBodyParts == mdl.BodyPartCount, "TriangleData.NumBodyParts != ModelHeader.BodyPartCount!");

            BodyPartOffset = reader.ReadInt32();
            BodyParts = new StudioBodyPart[NumBodyParts];
        }
    }
}
