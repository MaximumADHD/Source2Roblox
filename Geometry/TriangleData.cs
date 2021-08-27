using System;
using System.Diagnostics;
using System.IO;

using RobloxFiles.DataTypes;
using Source2Roblox.Models;

namespace Source2Roblox.Geometry
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

    public class BoneStateChange
    {
        public int HardwareId;
        public int NewBoneId;
    }

    public class Vertex
    {
        public StripGroup Group;

        public byte[] BoneWeightIndex;
        public byte NumBones;

        public ushort OrigMeshVertId;
        public byte[] BoneIds;

        public override string ToString()
        {
            return $"{OrigMeshVertId}";
        }
    }

    public class Strip
    {
        public int NumIndices;
        public int IndexOffset;

        public int NumVerts;
        public int VertOffset;

        public short NumBones;
        public StripFlags Flags;

        public StripGroup Group;
        public BoneStateChange[] BoneStateChanges;
    }

    public class StripGroup
    {
        public Vertex[] Vertices;
        public ushort[] Indices;
        public Strip[] Strips;

        public StripGroupFlags Flags;
        public StudioMesh Mesh;

        public override string ToString()
        {
            return $"StripGroup (Flags: {Flags})";
        }

        public ushort GetMeshIndex(int i)
        {
            var index = Indices[i];
            var vert = Vertices[index];
            return vert.OrigMeshVertId;
        }
    }

    public class StudioMesh
    {
        public StudioMeshFlags Flags;
        public StripGroup[] StripGroups;

        public StudioLOD LOD;
        public string[] Materials;

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

    public class StudioLOD
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
        public StudioLOD[] LODs;
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
        public readonly int MaterialReplacementListOffset;

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
            MaterialReplacementListOffset = reader.ReadInt32();

            NumBodyParts = reader.ReadInt32();
            Debug.Assert(NumBodyParts == mdl.BodyPartCount, "TriangleData.NumBodyParts != ModelHeader.BodyPartCount!");

            BodyPartOffset = reader.ReadInt32();
            BodyParts = new StudioBodyPart[NumBodyParts];
        }
    }
}
