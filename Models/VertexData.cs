using RobloxFiles.DataTypes;
using Source2Roblox.FileSystem;
using System.IO;

namespace Source2Roblox.Models
{
    public struct VertexFixup
    {
        public readonly int LOD;
        public readonly int SourceVertexID;
        public readonly int NumVertexes;

        public VertexFixup(BinaryReader reader)
        {
            LOD = reader.ReadInt32();
            SourceVertexID = reader.ReadInt32();
            NumVertexes = reader.ReadInt32();
        }
    }

    public struct Tangent
    {
        public readonly Vector3 Position;
        public readonly float Length;

        public override string ToString()
        {
            return $"[{Length}] {{{Position}}}";
        }

        public Tangent(Vector3 pos, float length)
        {
            Position = pos;
            Length = length;
        }

        public Tangent(BinaryReader reader)
        {
            Position = reader.ReadVector3();
            Length = reader.ReadSingle();
        }
    }

    public struct StudioBoneWeights
    {
        private const int MAX_NUM_BONES_PER_VERT = 3;

        public readonly float[] Weights;
        public readonly byte[] Bones;
        public readonly byte NumBones;

        public StudioBoneWeights(BinaryReader reader)
        {
            Weights = new float[MAX_NUM_BONES_PER_VERT];
            Bones = new byte[MAX_NUM_BONES_PER_VERT];

            for (int i = 0; i < MAX_NUM_BONES_PER_VERT; i++)
                Weights[i] = reader.ReadSingle();

            for (int i = 0; i < MAX_NUM_BONES_PER_VERT; i++)
                Bones[i] = reader.ReadByte();

            NumBones = reader.ReadByte();
        }
    }

    public struct StudioVertex
    {
        public readonly StudioBoneWeights BoneWeights;
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly Vector2 UV;

        public override string ToString()
        {
            return $"[{Position}][{Normal}][{UV}]";
        }

        public StudioVertex(BinaryReader reader)
        {
            BoneWeights = new StudioBoneWeights(reader);
            Position = reader.ReadVector3();
            Normal = reader.ReadVector3();
            UV = reader.ReadVector2();
        }
    }

    public struct VertexData
    {
        private const int MAX_NUM_LODS = 8;

        public readonly int ID;
        public readonly int Version;
        public readonly int Checksum;

        public readonly int NumLODs;
        public readonly int[] NumVertices;

        public readonly int NumFixups;
        public readonly int FixupTableStart;
        public readonly int VertexDataStart;
        public readonly int TangentDataStart;

        public readonly Tangent[][] Tangents;
        public readonly VertexFixup[] FixupTable;
        public readonly StudioVertex[][] Vertices;
       
        public VertexData(string path, GameMount game = null)
        {
            using (var stream = GameMount.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                ID = reader.ReadInt32();
                Version = reader.ReadInt32();
                Checksum = reader.ReadInt32();

                NumLODs = reader.ReadInt32();
                NumVertices = new int[MAX_NUM_LODS];

                for (int i = 0; i < MAX_NUM_LODS; i++)
                {
                    int numVerts = reader.ReadInt32();

                    if (i >= NumLODs)
                        numVerts = 0;

                    NumVertices[i] = numVerts;
                }
                
                NumFixups = reader.ReadInt32();
                FixupTableStart = reader.ReadInt32();
                VertexDataStart = reader.ReadInt32();
                TangentDataStart = reader.ReadInt32();

                Tangents = new Tangent[MAX_NUM_LODS][];
                FixupTable = new VertexFixup[NumFixups];
                Vertices = new StudioVertex[MAX_NUM_LODS][];
                
                // Read Fixup Table.
                reader.JumpTo(FixupTableStart);

                for (int i = 0; i < NumFixups; i++)
                    FixupTable[i] = new VertexFixup(reader);

                // Read Vertex Data.
                reader.JumpTo(VertexDataStart);

                for (int i = 0; i < 1/*NumLODs*/; i++)
                {
                    int numVerts = NumVertices[i];
                    var vertices = new StudioVertex[numVerts];

                    for (int j = 0; j < numVerts; j++)
                        vertices[j] = new StudioVertex(reader);

                    Vertices[i] = vertices;
                }

                // Read Tangent Data.
                reader.JumpTo(TangentDataStart);

                for (int i = 0; i < 1/*NumLODs*/; i++)
                {
                    int numVerts = NumVertices[i];
                    var tangents = new Tangent[numVerts];

                    for (int j = 0; j < numVerts; j++)
                        tangents[j] = new Tangent(reader);

                    Tangents[i] = tangents;
                }
            }
        }

        public StudioVertex[] GetVertices(int lod = 0)
        {
            if (lod < 0)
                lod = 0;
            else if (lod >= NumLODs)
                lod = NumLODs - 1;

            return Vertices[lod];
        }

        public Tangent[] GetTangents(int lod = 0)
        {
            if (lod < 0)
                lod = 0;
            else if (lod >= NumLODs)
                lod = NumLODs - 1;

            return Tangents[lod];
        }
    }
}
