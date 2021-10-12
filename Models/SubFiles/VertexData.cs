using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

using Source2Roblox.FileSystem;
using Source2Roblox.Geometry;

using RobloxFiles.DataTypes;

namespace Source2Roblox.Models
{
    public class VertexFixup
    {
        public int LOD;
        public int Count;
        public int CopySrc;
        public int CopyDst;
    }

    public class StudioVertex
    {
        public float[] Weights;
        public byte[] Bones;
        public byte NumBones;

        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 UV;

        public override string ToString()
        {
            return $"[{Position}][{Normal}][{UV}]";
        }

        public StudioVertex()
        {
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

        public static implicit operator RobloxVertex(StudioVertex vertex)
        {
            var oldPos = vertex.Position;
            var newPos = new Vector3(oldPos.X, oldPos.Z, -oldPos.Y) / Program.STUDS_TO_VMF;

            var oldNorm = vertex.Normal;
            var newNorm = new Vector3(oldNorm.X, oldNorm.Z, -oldNorm.Y);

            var oldUV = vertex.UV;
            var newUV = new Vector2(oldUV.X, oldUV.Y);

            var newVertex = new RobloxVertex()
            {
                Color = Color.FromArgb(255, 255, 255, 255),
                NumBones = vertex.NumBones,
                Position = newPos,
                Normal = newNorm,
                UV = newUV,
            };

            return newVertex;
        }
    }

    public class VertexData
    {
        public readonly string ID;
        public readonly int Version;
        public readonly int Checksum;

        public readonly int NumLODs;
        public readonly int NumFixups;
        public readonly int NumVertices;
        public readonly int[] NumVertsByLOD;

        public readonly Tangent[] Tangents;
        public readonly VertexFixup[] Fixups;
        public readonly StudioVertex[] Vertices;

        public VertexData(ModelHeader mdl, string path, GameMount game = null)
        {
            using (var stream = GameMount.OpenRead(path, game))
            using (var reader = new BinaryReader(stream))
            {
                ID = reader.ReadString(4);
                Debug.Assert(ID == "IDSV", "Not a VVD file!");

                Version = reader.ReadInt32();
                Debug.Assert(Version == 4, $"Unsupported VVD version: {Version} (expected 4!)");

                Checksum = reader.ReadInt32();
                Debug.Assert(Checksum == mdl.Checksum, $"VVD checksum didn't match MDL checksum!");

                NumLODs = reader.ReadInt32();
                NumVertsByLOD = new int[ModelConstants.MaxLODs];

                for (int i = 0; i < ModelConstants.MaxLODs; i++)
                    NumVertsByLOD[i] = reader.ReadInt32();

                NumFixups = reader.ReadInt32();
                
                int fixupTableStart = reader.ReadInt32(),
                    vertexDataStart = reader.ReadInt32(),
                    tangentDataStart = reader.ReadInt32();

                NumVertices = (tangentDataStart - vertexDataStart) / 48;
                Vertices = new StudioVertex[NumVertices];

                Fixups = new VertexFixup[NumFixups];
                Tangents = new Tangent[NumVertices];

                // Read Fixup Table.
                int copyDst = 0;
                reader.JumpTo(fixupTableStart);

                for (int i = 0; i < NumFixups; i++)
                {
                    var fixup = new VertexFixup
                    {
                        LOD = reader.ReadInt32(),
                        CopySrc = reader.ReadInt32(),

                        Count = reader.ReadInt32(),
                        CopyDst = copyDst
                    };

                    Fixups[i] = fixup;
                    copyDst += fixup.Count;
                }

                // Read Vertex Data.
                reader.JumpTo(vertexDataStart);

                for (int i = 0; i < NumVertices; i++)
                {
                    var vert = new StudioVertex(reader);
                    Vertices[i] = vert;
                }

                // Read Tangent Data.
                reader.JumpTo(tangentDataStart);

                for (int i = 0; i < NumVertices; i++)
                {
                    var tangent = new Tangent(reader);
                    Tangents[i] = tangent;
                }
            }
        }

        // Loads the minimum quantity of verts and runs fixups
        public VertexData(VertexData vvd, int rootLOD)
        {
            ID = vvd.ID;
            Version = vvd.Version;
            Checksum = vvd.Checksum;

            NumLODs = vvd.NumLODs;
            NumVertsByLOD = vvd.NumVertsByLOD;
            NumVertices = vvd.NumVertsByLOD[rootLOD];

            for (int i = 0; i < rootLOD; i++)
                NumVertsByLOD[i] = NumVertsByLOD[rootLOD];

            if (vvd.NumFixups == 0)
            {
                Vertices = vvd.Vertices;
                Tangents = vvd.Tangents;

                return;
            }

            var oldVerts = vvd.Vertices;
            var oldTangents = vvd.Tangents;

            var newVerts = new List<StudioVertex>();
            var newTangents = new List<Tangent>();

            for (int i = 0; i < vvd.NumFixups; i++)
            {
                var fixup = vvd.Fixups[i];

                if (fixup.LOD < rootLOD)
                    continue;

                var vertRange = oldVerts
                    .Skip(fixup.CopySrc)
                    .Take(fixup.Count);

                var tangentRange = oldTangents
                    .Skip(fixup.CopySrc)
                    .Take(fixup.Count);

                newVerts.AddRange(vertRange);
                newTangents.AddRange(tangentRange);
            }

            Vertices = newVerts.ToArray();
            Tangents = newTangents.ToArray();

            NumFixups = 0;
            Fixups = new VertexFixup[0];
        }
    }

    public class ModelVertexData
    {
        public readonly StudioModel Model;
        public readonly VertexData VertexData;

        public ModelVertexData(StudioModel model, VertexData data)
        {
            Model = model;
            VertexData = data;
        }

        public int GetGlobalVertexIndex(int i)
        {
            return i + (Model.VertexIndex / 48);
        }

        public int GetGlobalTangentIndex(int i)
        {
            return i + (Model.TangentIndex / 16);
        }

        public StudioVertex GetVertex(int i)
        {
            int index = GetGlobalVertexIndex(i);
            return VertexData.Vertices[index];
        }
    }

    public class MeshVertexData
    {
        public readonly StudioMesh Mesh;
        public readonly ModelVertexData VertexData;

        public MeshVertexData(StudioMesh mesh, ModelVertexData data)
        {
            Mesh = mesh;
            VertexData = data;
        }

        public int GetModelVertexIndex(int i)
        {
            return Mesh.VertexOffset + i;
        }

        public int GetGlobalVertexIndex(int i)
        {
            int index = GetModelVertexIndex(i);
            return VertexData.GetGlobalVertexIndex(index);
        }

        public StudioVertex GetVertex(int i)
        {
            int index = GetModelVertexIndex(i);
            return VertexData.GetVertex(index);
        }
    }
}
