using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using RobloxFiles;
using RobloxFiles.DataTypes;

using Source2Roblox.Models;

namespace Source2Roblox.Geometry
{
    public class RobloxVertex
    {
        public Vector3 Position = new Vector3();
        public Vector3 Normal = new Vector3(1, 0, 0);

        public Vector2 UV = new Vector2();
        public Tangent Tangent = new Tangent(0, 0, -1, 1);

        public Color? Color;
        public Envelope? Envelope;

        public static implicit operator StudioVertex(RobloxVertex vertex)
        {
            var oldPos = vertex.Position * Program.STUDS_TO_VMF;
            var newPos = new Vector3(oldPos.X, -oldPos.Z, oldPos.Y);

            var oldNorm = vertex.Normal;
            var newNorm = new Vector3(oldNorm.X, -oldNorm.Z, oldNorm.Y);

            var oldUV = vertex.UV;
            var newUV = new Vector2(oldUV.X, oldUV.Y);

            var newVertex = new StudioVertex
            {
                NumBones = 0,
                Bones = new byte[3] { 0, 0, 0 },
                Weights = new float[3] { 0f, 0f, 0f },

                Position = newPos,
                Normal = newNorm,
                UV = newUV,
            };

            Envelope? envelopePtr = vertex.Envelope;

            if (envelopePtr.HasValue)
            {
                var envelope = envelopePtr.Value;

                for (int i = 0; i < 3; i++)
                {
                    byte bone = envelope.Bones[i];
                    byte weight = envelope.Weights[i];

                    if (bone == 0xFF)
                        break;

                    newVertex.NumBones++;
                    newVertex.Bones[i] = bone;
                    newVertex.Weights[i] = weight / 255f;
                }
            }

            return newVertex;
        }
    }

    public class MeshSubset
    {
        public int FacesIndex;
        public int FacesLength;

        public int VertsIndex;
        public int VertsLength;

        public int NumBones;
        public ushort[] BoneIndices;
    }

    public struct Envelope
    {
        public byte[] Bones;
        public byte[] Weights;
    }

    public class RobloxMesh
    {
        public uint Version;
        public ushort NumMeshes = 0;

        public int NumVerts = 0;
        public List<RobloxVertex> Verts = new List<RobloxVertex>();

        public int NumFaces = 0;
        public List<int[]> Faces = new List<int[]>();

        public ushort NumLODs;
        public List<uint> LODs;

        public uint NumBones = 0;
        public List<Bone> Bones;

        public ushort NumSubsets = 0;
        public List<MeshSubset> Subsets;

        public int NameTableSize = 0;
        public byte[] NameTable;

        public bool HasLODs => (Version >= 3);
        public bool HasDeformation => (Version >= 4);
        public bool HasVertexColors { get; private set; }

        private static void LoadGeometry_Ascii(StringReader reader, RobloxMesh mesh)
        {
            string header = reader.ReadLine();
            mesh.NumMeshes = 1;

            if (!header.StartsWith("version 1"))
                throw new Exception("Expected version 1 header, got: " + header);

            string version = header.Substring(8);
            float vertScale = (version == "1.00" ? 0.5f : 1);

            if (int.TryParse(reader.ReadLine(), out mesh.NumFaces))
                mesh.NumVerts = mesh.NumFaces * 3;
            else
                throw new Exception("Expected 2nd line to be the polygon count.");

            mesh.Faces = new List<int[]>();
            mesh.Verts = new List<RobloxVertex>();

            string polyBuffer = reader.ReadLine();
            MatchCollection matches = Regex.Matches(polyBuffer, @"\[(.*?)\]");

            int face = 0;
            int index = 0;
            int target = 0;

            var vertex = new RobloxVertex();

            foreach (Match m in matches)
            {
                string vectorStr = m.Groups[1].ToString();

                float[] coords = vectorStr.Split(',')
                    .Select(coord => Format.ParseFloat(coord))
                    .ToArray();

                if (target == 0)
                    vertex.Position = new Vector3(coords) * vertScale;
                else if (target == 1)
                    vertex.Normal = new Vector3(coords);
                else if (target == 2)
                    vertex.UV = new Vector2(coords[0], 1 - coords[1]);

                target = (target + 1) % 3;

                if (target == 0)
                {
                    mesh.Verts.Add(vertex);
                    vertex = new RobloxVertex();

                    if (index % 3 == 0)
                    {
                        int v = face * 3;
                        var faceDef = new int[3] { v, v + 1, v + 2 };
                        mesh.Faces.Add(faceDef);
                    }
                }
            }
        }

        private static void LoadGeometry_Binary(BinaryReader reader, RobloxMesh mesh)
        {
            _ = reader.ReadBytes(13); // version x.xx\n
            _ = reader.ReadUInt16();  // Header size

            if (mesh.HasDeformation)
            {
                mesh.NumMeshes = reader.ReadUInt16();

                mesh.NumVerts = reader.ReadInt32();
                mesh.NumFaces = reader.ReadInt32();

                mesh.NumLODs = reader.ReadUInt16();
                mesh.NumBones = reader.ReadUInt16();

                mesh.NameTableSize = reader.ReadInt32();
                mesh.NumSubsets = reader.ReadUInt16();

                reader.Skip(2);
                mesh.HasVertexColors = true;
            }
            else
            {
                var sizeof_Vertex = reader.ReadByte();
                mesh.HasVertexColors = sizeof_Vertex > 36;
                reader.Skip(1);

                if (mesh.HasLODs)
                {
                    reader.Skip(2);
                    mesh.NumLODs = reader.ReadUInt16();
                }

                if (mesh.NumLODs > 0)
                    mesh.NumMeshes = (ushort)(mesh.NumLODs - 1);
                else
                    mesh.NumMeshes = 1;

                mesh.NumVerts = reader.ReadInt32();
                mesh.NumFaces = reader.ReadInt32();

                mesh.NameTable = new byte[0];
            }

            mesh.LODs = new List<uint>();
            mesh.Bones = new List<Bone>();
            mesh.Faces = new List<int[]>();
            mesh.Verts = new List<RobloxVertex>();
            mesh.Subsets = new List<MeshSubset>();

            // Read Vertices
            for (int i = 0; i < mesh.NumVerts; i++)
            {
                var vert = new RobloxVertex()
                {
                    Position = reader.ReadVector3(),
                    Normal = reader.ReadVector3(),

                    UV = reader.ReadVector2(),
                    Tangent = reader.ReadUInt32()
                };

                Color? color = null;

                if (mesh.HasVertexColors)
                {
                    int rgba = reader.ReadInt32();
                    color = Color.FromArgb(rgba << 24 | rgba >> 8);
                }

                vert.Color = color;
                mesh.Verts.Add(vert);
            }

            if (mesh.HasDeformation && mesh.NumBones > 0)
            {
                // Read Envelopes
                for (int i = 0; i < mesh.NumVerts; i++)
                {
                    var vert = mesh.Verts[i];

                    var envelope = new Envelope()
                    {
                        Bones = reader.ReadBytes(4),
                        Weights = reader.ReadBytes(4)
                    };

                    vert.Envelope = envelope;
                }
            }

            // Read Faces
            for (int i = 0; i < mesh.NumFaces; i++)
            {
                var face = new int[3];

                for (int f = 0; f < 3; f++)
                    face[f] = reader.ReadInt32();

                mesh.Faces.Add(face);
            }

            if (mesh.HasLODs && mesh.NumLODs > 0)
            {
                // Read LOD ranges
                for (int i = 0; i < mesh.NumLODs; i++)
                {
                    uint lod = reader.ReadUInt32();
                    mesh.LODs.Add(lod);
                }
            }

            var nameIndices = new Dictionary<Bone, int>();
            var parentIds = new Dictionary<Bone, ushort>();

            if (mesh.HasDeformation)
            {
                // Read Bones
                for (int i = 0; i < mesh.NumBones; i++)
                {
                    float[] cf = new float[12];
                    var bone = new Bone();

                    nameIndices[bone] = reader.ReadInt32();
                    parentIds[bone] = reader.ReadUInt16();

                    // LOD Parent (ignored for now)
                    reader.Skip(2);

                    float culling = reader.ReadSingle();
                    bone.SetAttribute("Culling", culling);

                    for (int m = 0; m < 12; m++)
                    {
                        int index = (m + 3) % 12;
                        cf[index] = reader.ReadSingle();
                    }

                    bone.CFrame = new CFrame(cf);
                    mesh.Bones.Add(bone);
                    reader.Skip(4);
                }

                // Read Bone Names & Parents
                var nameTable = reader.ReadBytes(mesh.NameTableSize);
                mesh.NameTable = nameTable;

                foreach (Bone bone in mesh.Bones)
                {
                    int index = nameIndices[bone];
                    int parentId = parentIds[bone];
                    var buffer = new List<byte>();

                    while (true)
                    {
                        byte next = nameTable[index];

                        if (next > 0)
                            index++;
                        else
                            break;

                        buffer.Add(next);
                    }

                    var result = buffer.ToArray();
                    bone.Name = Encoding.UTF8.GetString(result);

                    if (parentId >= 0)
                    {
                        var parent = mesh.Bones[parentId];
                        bone.Parent = parent;
                    }
                }

                // Read Subsets
                for (int p = 0; p < mesh.NumSubsets; p++)
                {
                    var subset = new MeshSubset()
                    {
                        FacesIndex = reader.ReadInt32(),
                        FacesLength = reader.ReadInt32(),

                        VertsIndex = reader.ReadInt32(),
                        VertsLength = reader.ReadInt32(),

                        NumBones = reader.ReadInt32(),
                        BoneIndices = new ushort[26]
                    };

                    for (int i = 0; i < 26; i++)
                        subset.BoneIndices[i] = reader.ReadUInt16();

                    mesh.Subsets.Add(subset);
                }
            }
        }

        public void SaveV1(Stream stream)
        {
            using (StringWriter writer = new StringWriter())
            {
                writer.WriteLine("version 1.00");
                writer.WriteLine(NumFaces);

                for (int i = 0; i < NumFaces; i++)
                {
                    var face = Faces[i];

                    for (int j = 0; j < 3; j++)
                    {
                        var index = (int)face[j];
                        var vert = Verts[index];

                        writer.Write('[');
                        writer.Write(vert.Position * 2f);

                        writer.Write("][");
                        writer.Write(vert.Normal);

                        writer.Write("][");
                        writer.Write(vert.UV.X);

                        writer.Write(", ");
                        writer.Write(vert.UV.Y);

                        writer.Write(", 0]");
                    }
                }

                using (BinaryWriter bin = new BinaryWriter(stream))
                {
                    string file = writer.ToString();
                    byte[] mesh = Encoding.ASCII.GetBytes(file);
                    stream.Write(mesh, 0, mesh.Length);
                }
            }
        }

        public void Save(Stream stream)
        {
            ushort HeaderSize = 12;
            Version = 2;

            const byte VertSize = 40;
            const byte FaceSize = 12;
            const ushort LODSize = 4;

            if (NumLODs > 0)
            {
                Version = 3;
                HeaderSize = 16;
            }

            byte[] VersionHeader = Encoding.UTF8.GetBytes($"version {Version}.00\n");
            stream.SetLength(0);
            stream.Position = 0;

            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(VersionHeader);
                writer.Write(HeaderSize);

                writer.Write(VertSize);
                writer.Write(FaceSize);

                if (NumLODs > 0)
                {
                    writer.Write(LODSize);
                    writer.Write(NumLODs);
                }

                writer.Write(NumVerts);
                writer.Write(NumFaces);

                for (int i = 0; i < NumVerts; i++)
                {
                    RobloxVertex vertex = Verts[i];

                    Vector3 pos = vertex.Position;
                    writer.Write(pos.X);
                    writer.Write(pos.Y);
                    writer.Write(pos.Z);

                    Vector3 norm = vertex.Normal;
                    writer.Write(norm.X);
                    writer.Write(norm.Y);
                    writer.Write(norm.Z);

                    Vector2 uv = vertex.UV;
                    writer.Write(uv.X);
                    writer.Write(uv.Y);

                    Tangent tangent = vertex.Tangent;
                    writer.Write(tangent.ToUInt32());
                    
                    if (vertex.Color.HasValue)
                    {
                        var color = vertex.Color.Value;
                        int argb = color.ToArgb();

                        int rgba = (argb << 8 | argb >> 24);
                        writer.Write(rgba);
                    }
                    else
                    {
                        writer.Write(-1);
                    }
                }

                for (int i = 0; i < NumFaces; i++)
                {
                    var faces = Faces[i];

                    for (int f = 0; f < 3; f++)
                    {
                        int face = faces[f];
                        writer.Write(face);
                    }
                }

                for (int i = 0; i < NumLODs; i++)
                {
                    uint lod = LODs[i];
                    writer.Write(lod);
                }
            }
        }

        public static RobloxMesh FromObjFile(string filePath)
        {
            string contents = File.ReadAllText(filePath);

            var mesh = new RobloxMesh()
            {
                Version = 3,
                Faces = new List<int[]>(),
                Verts = new List<RobloxVertex>()
            };

            var uvTable = new List<Vector2>();
            var posTable = new List<Vector3>();
            var normTable = new List<Vector3>();
            var vertexLookup = new Dictionary<string, int>();

            using (var reader = new StringReader(contents))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0)
                        continue;

                    string[] buffer = line
                        .Split(' ')
                        .Where(word => word.Length > 0)
                        .ToArray();

                    string action = buffer[0];

                    switch (action)
                    {
                        case "v":
                        case "vn":
                        case "vt":
                        {
                            float[] input = buffer.Skip(1)
                                .Where(str => !string.IsNullOrEmpty(str))
                                .Select(float.Parse)
                                .ToArray();

                            object value = null;
                            IList target = null;

                            switch (action)
                            {
                                case "v":
                                {
                                    value = new Vector3(input);
                                    target = posTable;
                                    break;
                                }
                                case "vn":
                                {
                                    value = new Vector3(input);
                                    target = normTable;
                                    break;
                                }
                                case "vt":
                                {
                                    value = new Vector2(input);
                                    target = uvTable;
                                    break;
                                }
                            }

                            if (target != null)
                                target.Add(value);

                            break;
                        }
                        case "f":
                        {
                            var face = new int[3];

                            for (int i = 0; i < 3; i++)
                            {
                                string faceDef = buffer[i + 1];
                                string[] indices = faceDef.Split('/');

                                int uvId = int.Parse(indices[1]) - 1;
                                int posId = int.Parse(indices[0]) - 1;
                                int normId = int.Parse(indices[2]) - 1;

                                string key = $"{posId}/{uvId}/{normId}";

                                if (!vertexLookup.ContainsKey(key))
                                {
                                    int faceId = mesh.NumVerts++;
                                    vertexLookup.Add(key, faceId);

                                    var vert = new RobloxVertex()
                                    {
                                        Position = posTable[posId],
                                        Normal = normTable[normId],
                                        UV = uvTable[uvId]
                                    };

                                    mesh.Verts.Add(vert);
                                }

                                face[i] = vertexLookup[key];
                            }

                            mesh.Faces.Add(face);
                            mesh.NumFaces++;

                            break;
                        }
                    }
                }
            }

            return mesh;
        }

        public static RobloxMesh FromBuffer(byte[] data)
        {
            string file = Encoding.ASCII.GetString(data);

            if (!file.StartsWith("version "))
                throw new Exception("Invalid .mesh header!");

            string versionStr = file.Substring(8, 4);
            double version = Format.ParseDouble(versionStr);
            var mesh = new RobloxMesh() { Version = (uint)version };

            if (mesh.Version == 1)
            {
                var buffer = new StringReader(file);
                LoadGeometry_Ascii(buffer, mesh);

                buffer.Dispose();
            }
            else
            {
                var stream = new MemoryStream(data);

                using (var reader = new BinaryReader(stream))
                    LoadGeometry_Binary(reader, mesh);

                stream.Dispose();
            }

            return mesh;
        }

        public static RobloxMesh FromStream(Stream stream)
        {
            byte[] data;

            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                data = buffer.ToArray();
            }

            return FromBuffer(data);
        }

        public static RobloxMesh FromFile(string path)
        {
            RobloxMesh result;

            using (FileStream meshStream = File.OpenRead(path))
                result = FromStream(meshStream);

            return result;
        }
    }
}