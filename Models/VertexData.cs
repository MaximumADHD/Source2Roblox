using RobloxFiles.DataTypes;
using Source2Roblox.FileSystem;
using System;
using System.Diagnostics;
using System.IO;

namespace Source2Roblox.Models
{
    public class VertexFixup
    {
        public int LOD;
        public int Source;
        public int CopyTo;
        public int Count;
    }

    public class Tangent
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public readonly float W;

        public override string ToString()
        {
            return $"{X}, {Y}, {Z}, {W}";
        }

        public Tangent(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Tangent(Vector3 pos, float w)
        {
            X = pos.X;
            Y = pos.Y;
            Z = pos.Z;
            W = w;
        }

        public Tangent(BinaryReader reader)
        {
            X = reader.ReadSingle();
            Y = reader.ReadSingle();
            Z = reader.ReadSingle();
            W = reader.ReadSingle();
        }
    }

    public class StudioVertex
    {
        public readonly float[] Weights;
        public readonly byte[] Bones;
        public readonly byte NumBones;

        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly Vector2 UV;

        public override string ToString()
        {
            return $"[{Position}][{Normal}][{UV}]";
        }

        public StudioVertex(BinaryReader reader)
        {
            const int maxBones = ModelConstants.MaxBonesPerVert;
            Weights = new float[maxBones];
            Bones = new byte[maxBones];

            for (int i = 0; i < maxBones; i++)
                Weights[i] = reader.ReadSingle();

            for (int i = 0; i < maxBones; i++)
                Bones[i] = reader.ReadByte();

            NumBones = reader.ReadByte();
            Position = reader.ReadVector3();
            Normal = reader.ReadVector3();
            UV = reader.ReadVector2();
        }
    }

    public class VertexData
    {
        public readonly string ID;
        public readonly int Version;
        public readonly int Checksum;

        public readonly int NumLODs;
        public readonly int NumVertices;
        public readonly int[] NumVerticesByLOD;

        public readonly int NumFixups;
        public readonly int FixupTableStart;
        public readonly int VertexDataStart;
        public readonly int TangentDataStart;

        public readonly Tangent[] Tangents;
        public readonly VertexFixup[] Fixups;
        public readonly StudioVertex[] Vertices;
       
        public VertexData(ModelHeader mdl, string path, GameMount game = null)
        {
            using (var stream = GameMount.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                ID = reader.ReadString(4);
                Debug.Assert(ID == "IDSV", "Not a VVD file!");

                Version = reader.ReadInt32();
                Debug.Assert(Version == 4, $"Unsupported VVD version: {Version} (expected 4!)");

                Checksum = reader.ReadInt32();
                Debug.Assert(Checksum == mdl.Checksum, $"VVD checksum didn't match MDL checksum!");

                NumLODs = reader.ReadInt32();
                NumFixups = reader.ReadInt32();
                NumVerticesByLOD = new int[ModelConstants.MaxLODs];

                for (int i = 0; i < ModelConstants.MaxLODs; i++)
                    NumVerticesByLOD[i] = reader.ReadInt32();
                
                FixupTableStart = reader.ReadInt32();
                VertexDataStart = reader.ReadInt32();
                TangentDataStart = reader.ReadInt32();
                NumVertices = (TangentDataStart - VertexDataStart) / 48;

                Fixups = new VertexFixup[NumFixups];
                Tangents = new Tangent[NumVertices];
                Vertices = new StudioVertex[NumVertices];

                // Read Fixup Table.
                int copyTo = 0;
                reader.JumpTo(FixupTableStart);

                for (int i = 0; i < NumFixups; i++)
                {
                    int lod = reader.ReadInt32();
                    int src = reader.ReadInt32();
                    int count = reader.ReadInt32();

                    Fixups[i] = new VertexFixup
                    {
                        LOD = lod,
                        Count = count,

                        Source = src,
                        CopyTo = copyTo,
                    };

                    copyTo += count;
                }
                
                // Read Vertex Data.
                reader.JumpTo(VertexDataStart);

                for (int i = 0; i < NumVertices; i++)
                {
                    var vert = new StudioVertex(reader);
                    Vertices[i] = vert;
                }
                
                // Read Tangent Data.
                reader.JumpTo(TangentDataStart);

                for (int i = 0; i < NumVertices; i++)
                {
                    var tangent = new Tangent(reader);
                    Tangents[i] = tangent;
                }
            }
        }

        public int FixupSearch(int dest)
        {
            int len = Fixups.Length;

            foreach (var fixup in Fixups)
            {
                int index = dest - fixup.CopyTo;

                if (index < 0 || index >= len)
                    continue;

                return fixup.Source + index;
            }

            return dest;
        }
    }
}
