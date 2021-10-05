using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using RobloxFiles;
using RobloxFiles.DataTypes;

namespace Source2Roblox.Geometry
{
    public class RobloxVertex
    {
        public Vector3 Position = new Vector3();
        public Vector3 Normal = new Vector3(1, 0, 0);

        public Vector2 UV = new Vector2();
        public Tangent Tangent = new Tangent(0, 0, -1, 1);

        public Color? Color;
        public byte NumBones;
        public Dictionary<Bone, byte> Bones;
    }

    public class MeshSubset
    {
        public int FacesIndex = 0;
        public int FacesLength = 0;

        public int VertsIndex = 0;
        public int VertsLength = 0;

        public int NumBones = 0;
        public ushort[] BoneIndices;

        public override string ToString()
        {
            return string.Join(", ", BoneIndices.Take(NumBones));
        }
    }

    public struct Envelope
    {
        public byte[] Bones;
        public byte[] Weights;
    }

    public class RobloxMesh
    {
        public int NumFaces => Faces.Count;
        public readonly List<int[]> Faces = new List<int[]>();

        public ushort NumSubsets => (ushort)Subsets.Count;
        public readonly List<MeshSubset> Subsets = new List<MeshSubset>();
    }

    public class RobloxMeshFile
    {
        public List<RobloxVertex> Verts = new List<RobloxVertex>();
        public List<RobloxMesh> Meshes = new List<RobloxMesh>();
        public List<Bone> Bones = new List<Bone>();
        
        private void LoadGeometry_Ascii(StringReader reader)
        {
            string header = reader.ReadLine();
            
            if (!header.StartsWith("version 1"))
                throw new Exception("Expected version 1 header, got: " + header);

            string version = header.Substring(8);
            float vertScale = (version == "1.00" ? 0.5f : 1);

            if (!int.TryParse(reader.ReadLine(), out int numFaces))
                throw new Exception("Expected 2nd line to be the polygon count.");

            string polyBuffer = reader.ReadLine();
            MatchCollection matches = Regex.Matches(polyBuffer, @"\[(.*?)\]");

            int face = 0;
            int index = 0;
            int target = 0;

            int numVerts = numFaces * 3;
            var vertex = new RobloxVertex();

            var mesh = new RobloxMesh();
            Meshes.Add(mesh);

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
                    Verts.Add(vertex);
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

        private void LoadGeometry_Binary(BinaryReader reader)
        {
            string versionTag = reader.ReadString(13);
            reader.Skip(2);

            if (!int.TryParse(versionTag.Substring(8, 1), out int version))
                throw new Exception("Invalid version!");

            int numMeshes;
            int numVerts;
            int numFaces;
            
            bool hasColor;
            int numLODs = 0;
            int numBones = 0;
            int numSubsets = 0;
            int nameTableSize = 0;

            if (version >= 4)
            {
                reader.Skip(2);

                numVerts = reader.ReadInt32();
                numFaces = reader.ReadInt32();

                numLODs = reader.ReadUInt16();
                numBones = reader.ReadUInt16();

                nameTableSize = reader.ReadInt32();
                numSubsets = reader.ReadUInt16();

                reader.Skip(2);
                hasColor = true;
            }
            else
            {
                var vertSize = reader.ReadByte();
                hasColor = (vertSize > 36);
                reader.Skip(1);

                if (version >= 3)
                {
                    reader.Skip(2);
                    numLODs = reader.ReadUInt16();
                }

                numVerts = reader.ReadInt32();
                numFaces = reader.ReadInt32();
            }

            var faces = new List<int[]>();
            var subsets = new List<MeshSubset>();
            var envelopes = new List<Envelope>();

            // Read Vertices
            for (int i = 0; i < numVerts; i++)
            {
                var vert = new RobloxVertex()
                {
                    Position = reader.ReadVector3(),
                    Normal = reader.ReadVector3(),

                    UV = reader.ReadVector2(),
                    Tangent = reader.ReadInt32()
                };

                if (hasColor)
                {
                    int rgba = reader.ReadInt32();
                    vert.Color = Color.FromArgb(rgba << 24 | rgba >> 8);
                }

                Verts.Add(vert);
            }

            if (numBones > 0)
            {
                // Read Envelopes
                for (int i = 0; i < numVerts; i++)
                {
                    var envelope = new Envelope()
                    {
                        Bones = reader.ReadBytes(4),
                        Weights = reader.ReadBytes(4)
                    };

                    envelopes.Add(envelope);
                }
            }

            // Read Faces
            for (int i = 0; i < numFaces; i++)
            {
                var face = new int[3];

                for (int f = 0; f < 3; f++)
                    face[f] = reader.ReadInt32();

                faces.Add(face);
            }

            // Read LOD ranges
            var lodStride = new List<int>();

            if (numLODs > 0)
            {
                for (int i = 0; i < numLODs; i++)
                {
                    int lod = reader.ReadInt32();
                    lodStride.Add(lod);
                }
            }

            // Read Bones
            var nameIndices = new Dictionary<Bone, int>();

            for (int i = 0; i < numBones; i++)
            {
                float[] cf = new float[12];
                var bone = new Bone();

                var nameIndex = reader.ReadInt32();
                int parentId = reader.ReadUInt16();

                if (parentId != 0xFFFF)
                    bone.Parent = Bones[parentId];

                nameIndices[bone] = nameIndex;
                reader.Skip(6);

                for (int m = 0; m < 12; m++)
                {
                    int index = (m + 3) % 12;
                    cf[index] = reader.ReadSingle();
                }

                bone.CFrame = new CFrame(cf);
                Bones.Add(bone);

                if (bone.Parent is Bone parent)
                {
                    var parentCF = parent.CFrame;
                    bone.CFrame = parentCF.ToObjectSpace(bone.CFrame);
                }
            }

            // Read Name Table
            var nameBuffer = reader.ReadBytes(nameTableSize);

            using (var nameTable = new MemoryStream(nameBuffer))
            using (var nameReader = new BinaryReader(nameTable))
            {
                foreach (Bone bone in Bones)
                {
                    nameTable.Position = nameIndices[bone];
                    bone.Name = nameReader.ReadString(null);
                }
            }
                
            // Read Mesh Subsets
            for (ushort set = 0; set < numSubsets; set++)
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

                for (int i = 0; i < subset.VertsLength; i++)
                {
                    var vertex = Verts[i];
                    var envelope = envelopes[i];
                    var boneWeights = vertex.Bones;

                    if (boneWeights == null)
                    {
                        boneWeights = new Dictionary<Bone, byte>();
                        vertex.Bones = boneWeights;
                    }

                    for (int j = 0; j < 4; j++)
                    {
                        int setIndex = envelope.Bones[j];
                        int boneIndex = subset.BoneIndices[setIndex];

                        if (boneIndex == 0xFFFF)
                            continue;

                        Bone bone = Bones[boneIndex];
                        byte weight = envelope.Weights[j];

                        if (weight > 0)
                        {
                            boneWeights[bone] = weight;
                            vertex.NumBones++;
                        }
                    }
                }

                subsets.Add(subset);
            }

            // Create meshes from the LOD strides.
            int subsetAt = 0;
            
            for (int i = 1; i < numLODs; i++)
            {
                int facesEnd = lodStride[i];
                int facesBegin = lodStride[i - 1];

                if (facesEnd == 0)
                    facesEnd = numFaces;

                var faceSet = faces
                    .Skip(facesBegin)
                    .Take(facesEnd - facesBegin);

                var mesh = new RobloxMesh();
                mesh.Faces.AddRange(faceSet);

                while (subsetAt < numSubsets)
                {
                    var subset = subsets[subsetAt];

                    if (subset.FacesIndex >= facesEnd)
                        break;

                    subset.FacesIndex -= facesBegin;
                    mesh.Subsets.Add(subset);
                    subsetAt++;
                }

                Meshes.Add(mesh);
            }
        }

        public void SaveV1(Stream stream)
        {
            var mesh = Meshes.First();

            using (StringWriter writer = new StringWriter())
            {
                writer.WriteLine("version 1.00");
                writer.WriteLine(mesh.NumFaces);

                for (int i = 0; i < mesh.NumFaces; i++)
                {
                    var face = mesh.Faces[i];

                    for (int j = 0; j < 3; j++)
                    {
                        var index = face[j];
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

                using (var bin = new BinaryWriter(stream))
                {
                    string file = writer.ToString();
                    byte[] content = Encoding.ASCII.GetBytes(file);
                    bin.Write(content);
                }
            }
        }

        public void Save(Stream stream)
        {
            ushort headerSize = 12;
            int version = 2;

            const byte VertSize = 40;
            const byte FaceSize = 12;
            const ushort LODSize = 4;

            if (Meshes.Count > 1)
            {
                version = 3;
                headerSize = 16;
            }

            var boneInWorld = new Dictionary<Bone, CFrame>();
            var boneCulling = new Dictionary<Bone, float>();
            var nameTable = new Dictionary<string, int>();

            var numBones = (uint)(Bones?.Count ?? 0);
            var rawNameTable = new byte[0];

            if (numBones > 0)
            {
                version = 4;
                headerSize = 24;

                using (var nameBuffer = new MemoryStream())
                using (var nameWriter = new BinaryWriter(nameBuffer))
                {
                    var boneNames = Bones
                        .Select(bone => bone.Name)
                        .Distinct()
                        .ToList();

                    foreach (var name in boneNames)
                    {
                        var utf8 = Encoding.UTF8.GetBytes(name);
                        nameTable[name] = (int)nameBuffer.Position;

                        nameWriter.Write(utf8);
                        nameWriter.Write((byte)0);
                    }

                    rawNameTable = nameBuffer.ToArray();
                }

                foreach (var bone in Bones)
                {
                    var parent = bone.Parent;
                    CFrame parentCF = null;

                    if (parent is Bone otherBone)
                        boneInWorld.TryGetValue(otherBone, out parentCF);
                    
                    if (parentCF == null)
                        parentCF = new CFrame();

                    var cf = parentCF * bone.CFrame;
                    boneInWorld[bone] = cf;
                }

                for (byte i = 0; i < Bones.Count; i++)
                {
                    var bone = Bones[i];
                    var cf = boneInWorld[bone];

                    var origin = cf.Position;
                    var bestDist = -1f;

                    foreach (var vert in Verts)
                    {
                        var bones = vert.Bones;
                        Debug.Assert(bones != null);

                        if (!bones.ContainsKey(bone))
                            continue;

                        var pos = vert.Position;
                        var dist = (pos - origin).Magnitude;
                        bestDist = Math.Max(bestDist, dist);
                    }

                    boneCulling[bone] = bestDist;
                }
            }

            var lods = new List<int>();
            var faces = new List<int[]>();

            var subsets = new List<MeshSubset>();
            var envelopes = new List<Envelope>();

            byte[] versionHeader = Encoding.UTF8.GetBytes($"version {version}.00\n");
            stream.Position = 0;
            stream.SetLength(0);
            lods.Add(0);

            if (Meshes.Count < 2)
                lods.Add(0);

            foreach (var mesh in Meshes)
            {
                int facesBegin = faces.Count;

                foreach (var face in faces)
                {
                    var newFace = new int[3];

                    for (int i = 0; i < 3; i++)
                        newFace[i] = facesBegin + face[i];

                    faces.Add(newFace);
                }

                var allIndices = faces.SelectMany(face => face);
                var vertsBegin = allIndices.Min();
                var vertsEnd = allIndices.Max();

                if (Meshes.Count < 2)
                    continue;

                lods.Add(faces.Count);
            }

            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(versionHeader);
                writer.Write(headerSize);

                if (version > 4)
                {
                    writer.Write((ushort)Meshes.Count);

                    writer.Write(Verts.Count);
                    writer.Write(faces.Count);

                    writer.Write((ushort)lods.Count);
                    writer.Write((ushort)Bones.Count);

                    writer.Write(rawNameTable.Length);
                    writer.Write(subsets.Count);
                }
                else
                {
                    writer.Write(VertSize);
                    writer.Write(FaceSize);

                    if (Meshes.Count > 1)
                    {
                        writer.Write(LODSize);
                        writer.Write(lods.Count);
                    }

                    writer.Write(Verts.Count);
                    writer.Write(faces.Count);
                }
                
                foreach (var vertex in Verts)
                {
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
                    writer.Write(tangent.ToInt32());

                    Color? color = vertex.Color;
                    int rgba = -1;

                    if (color.HasValue)
                    {
                        int argb = color.Value.ToArgb();
                        rgba = argb << 8 | argb >> 24;
                    }

                    writer.Write(rgba);
                }

                if (numBones > 0)
                {
                    foreach (var envelope in envelopes)
                    {
                        writer.Write(envelope.Bones);
                        writer.Write(envelope.Weights);
                    }
                }

                foreach (var face in faces)
                    foreach (var index in face)
                        writer.Write(index);

                foreach (var lod in lods)
                    writer.Write(lod);

                if (numBones > 0)
                {
                    foreach (var bone in Bones)
                    {
                        int nameIndex = nameTable[bone.Name];
                        writer.Write(nameIndex);

                        ushort parentIndex = (ushort)Bones.IndexOf(bone);
                        writer.Write(parentIndex);
                        writer.Write(parentIndex);

                        var culling = boneCulling[bone];
                        writer.Write(culling);

                        var cf = boneInWorld[bone];
                        var components = cf.GetComponents();

                        for (int i = 0; i < 12; i++)
                        {
                            int index = (i + 3) % 12;
                            writer.Write(components[index]);
                        }
                    }

                    writer.Write(rawNameTable);

                    foreach (var subset in subsets)
                    {
                        writer.Write(subset.FacesIndex);
                        writer.Write(subset.FacesLength);

                        writer.Write(subset.VertsIndex);
                        writer.Write(subset.VertsLength);

                        for (int i = 0; i < 26; i++)
                        {
                            var index = subset.BoneIndices[i];
                            writer.Write(index);
                        }
                    }
                }
            }
        }

        public RobloxMeshFile()
        {
        }

        public static RobloxMeshFile FromObjFile(string filePath)
        {
            string contents = File.ReadAllText(filePath);
            var file = new RobloxMeshFile();
            
            var mesh = new RobloxMesh();
            file.Meshes.Add(mesh);

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
                                    int faceId = file.Verts.Count;
                                    vertexLookup.Add(key, faceId);

                                    var vert = new RobloxVertex()
                                    {
                                        Position = posTable[posId],
                                        Normal = normTable[normId],
                                        UV = uvTable[uvId]
                                    };

                                    file.Verts.Add(vert);
                                }

                                face[i] = vertexLookup[key];
                            }

                            mesh.Faces.Add(face);
                            break;
                        }
                    }
                }
            }

            return file;
        }

        public RobloxMeshFile(byte[] data)
        {
            string file = Encoding.ASCII.GetString(data);

            if (!file.StartsWith("version "))
                throw new Exception("Invalid .mesh header!");

            string versionStr = file.Substring(8, 4);
            uint version = (uint)Format.ParseDouble(versionStr);

            if (version == 1)
            {
                var buffer = new StringReader(file);
                LoadGeometry_Ascii(buffer);

                buffer.Dispose();
            }
            else
            {
                var stream = new MemoryStream(data);

                using (var reader = new BinaryReader(stream))
                    LoadGeometry_Binary(reader);

                stream.Dispose();
            }
        }

        public static RobloxMeshFile Open(Stream stream)
        {
            byte[] data;

            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                data = buffer.ToArray();
            }

            return new RobloxMeshFile(data);
        }

        public static RobloxMeshFile Open(string path)
        {
            RobloxMeshFile result;

            using (FileStream meshStream = File.OpenRead(path))
                result = Open(meshStream);

            return result;
        }
    }
}