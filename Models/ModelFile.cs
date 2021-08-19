using RobloxFiles.DataTypes;
using Source2Roblox.FileSystem;
using Source2Roblox.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Source2Roblox.Models
{
    // Largely a port of this implementation from noclip.website on GitHub.
    // Special thanks for their research into this spec!
    // https://github.com/magcius/noclip.website/blob/master/src/SourceEngine/Studio.ts

    public class MeshBuffer
    {
        public string BodyPart;
        public string MaterialPath;

        public int NumVerts => Vertices.Count;
        public List<StudioVertex> Vertices;

        public int NumIndices => Indices.Count;
        public List<ushort> Indices;

        public static Region3 ComputeAABB(IEnumerable<StudioVertex> vertices)
        {
            float min_X = float.MaxValue,
                  min_Y = float.MaxValue,
                  min_Z = float.MaxValue,

                  max_X = float.MinValue,
                  max_Y = float.MinValue,
                  max_Z = float.MinValue;

            foreach (StudioVertex studioVertex in vertices)
            {
                RobloxVertex rbxVertex = studioVertex;
                Vector3 pos = rbxVertex.Position;

                min_X = Math.Min(min_X, pos.X);
                min_Y = Math.Min(min_Y, pos.Y);
                min_Z = Math.Min(min_Z, pos.Z);

                max_X = Math.Max(max_X, pos.X);
                max_Y = Math.Max(max_Y, pos.Y);
                max_Z = Math.Max(max_Z, pos.Z);
            }

            var min = new Vector3(min_X, min_Y, min_Z);
            var max = new Vector3(max_X, max_Y, max_Z);

            return new Region3(min, max);
        }
    }

    public class ModelFile
    {
        public readonly GameMount Game;
        public readonly string Location;
        public readonly ModelHeader Header;
        public readonly VertexData VertexData;
        public readonly TriangleData TriangleData;
        public readonly IReadOnlyList<string> Materials;

        public string Name => Header.Name;
        public ModelFlags Flags => Header.Flags;
        public int BodyPartCount => Header.BodyPartCount;

        public StudioVertex[] Vertices => VertexData.Vertices;
        public StudioBodyPart[] BodyParts => TriangleData.BodyParts;

        public override string ToString() => Name;
        
        public ModelFile(string path, GameMount game = null)
        {
            if (!path.StartsWith("models/"))
                path = $"models/{path}";

            if (!path.EndsWith(".mdl"))
                path += ".mdl";

            string vvdPath = path.Replace(".mdl", ".vvd");
            string vtxPath = path.Replace(".mdl", ".dx90.vtx");

            var mdlStream = GameMount.OpenRead(path, game);
            var mdlReader = new BinaryReader(mdlStream);
            
            var vtxStream = GameMount.OpenRead(vtxPath, game);
            var vtxReader = new BinaryReader(vtxStream);

            var mdl = new ModelHeader(mdlReader);
            var vtx = new TriangleData(mdl, vtxReader);
            var vvd = new VertexData(mdl, vvdPath, game);

            int numTextures = mdl.TextureCount;
            var textureIndex = mdl.TextureIndex;

            int numTextureDirs = mdl.TextureDirCount;
            int textureDirIndex = mdl.TextureDirIndex;

            var materialDirs = new List<string>();
            mdlStream.Position = textureDirIndex;

            for (int i = 0; i < numTextureDirs; i++)
            {
                int dirIndex = mdlReader.ReadInt32();

                long restore = mdlStream.Position;
                mdlStream.Position = dirIndex;

                string textureDir = mdlReader.ReadString(null);
                string materialDir = Path.Combine("materials", textureDir);

                materialDir = Program.CleanPath(materialDir);
                materialDirs.Add(materialDir);

                mdlStream.Position = restore;
            }

            var materialPaths = new List<string>();
            mdlStream.Position = textureIndex;

            for (int i = 0; i < numTextures; i++)
            {
                int nameIndex = mdlReader.ReadInt32();
                mdlStream.Position += (nameIndex - 4);

                string name = mdlReader.ReadString(null);
                name = Program.CleanPath(name);
                textureIndex += 0x40;

                if (name.Contains('/'))
                    if (!name.StartsWith("materials"))
                        name = "materials/" + name;

                foreach (string dir in materialDirs)
                {
                    string mtlPath = Program.CleanPath(name + ".vmt");
                    
                    if (!mtlPath.StartsWith(dir))
                        mtlPath = dir + mtlPath;

                    if (GameMount.HasFile(mtlPath, game))
                    {
                        materialPaths.Add(mtlPath);
                        break;
                    }
                }

                mdlStream.Position = textureIndex;
            }

            int numSkinRef = mdl.SkinReferenceCount;
            int numSkinFamilies = mdl.SkinFamilyCount;

            int skinIndex = mdl.SkinRefIndex;
            var skinArray = new List<ushort>();

            int skinArraySize = numSkinRef * numSkinFamilies;
            mdlStream.Position = skinIndex;

            for (int i = 0; i < skinArraySize; i++)
            {
                ushort value = mdlReader.ReadUInt16();
                skinArray.Add(value);
            }

            int vtxNumLODs = vtx.NumLODs;
            var lodMaterialPaths = new string[vtxNumLODs][];

            int vtxReplaceIndex = vtx.MaterialReplacementListOffset;
            vtxStream.Position = vtxReplaceIndex;

            for (int i = 0; i < vtxNumLODs; i++)
            {
                int numReplaces = vtxReader.ReadInt32(),
                    replaceOffset = vtxReader.ReadInt32(),
                    replaceIndex = vtxReplaceIndex + replaceOffset;

                string[] lodMaterials = materialPaths.ToArray();
                vtxStream.Position = replaceIndex;

                for (int j = 0; j < numReplaces; j++)
                {
                    var materialId = vtxReader.ReadUInt16();
                    Debug.Assert(materialId < materialPaths.Count);

                    int nameOffset = vtxReader.ReadInt32();
                    vtxStream.Position += (nameOffset - 6);

                    string newName = vtxReader.ReadString(null) + ".vmt";
                    string oldPath = lodMaterials[j];

                    var oldInfo = new FileInfo(oldPath);
                    var oldName = oldInfo.Name;

                    string newPath = oldPath.Replace(oldName, newName);

                    if (GameMount.HasFile(newPath, game))
                        lodMaterials[materialId] = newPath;
                    else
                        Console.WriteLine($"\tMissing replacement material {oldName}->{newName}?");

                    replaceIndex += 0x06;
                    vtxStream.Position = replaceIndex;
                }

                vtxReplaceIndex += 0x08;
                vtxStream.Position = vtxReplaceIndex;
                lodMaterialPaths[i] = lodMaterials;
            }

            int numBodyParts = vtx.NumBodyParts;
            var bodyParts = vtx.BodyParts;

            int mdlBodyPos = mdl.BodyPartIndex;
            int vtxBodyPos = vtx.BodyPartOffset;

            mdlStream.Position = mdlBodyPos;
            vtxStream.Position = vtxBodyPos;

            for (int BODY_PART = 0; BODY_PART < numBodyParts; BODY_PART++)
            {
                int bodyNameOffset = mdlReader.ReadInt32(),
                    mdlNumModels = mdlReader.ReadInt32(),
                    mdlBase = mdlReader.ReadInt32(),
                    mdlIndex = mdlReader.ReadInt32();

                int vtxNumModels = vtxReader.ReadInt32(),
                    modelOffset = vtxReader.ReadInt32();

                Debug.Assert(mdlNumModels == vtxNumModels);
                mdlStream.Position = mdlBodyPos + bodyNameOffset;

                var bodyPart = new StudioBodyPart()
                {
                    Root = vtx,
                    Base = mdlBase,
                    
                    Name = mdlReader.ReadString(null),
                    Models = new StudioModel[vtxNumModels]
                };

                int vtxModelPos = vtxBodyPos + modelOffset;
                vtxStream.Position = vtxModelPos;

                int mdlDataPos = mdlBodyPos + mdlIndex;
                mdlStream.Position = mdlDataPos;

                for (int MODEL = 0; MODEL < vtxNumModels; MODEL++)
                {
                    int numLODs = vtxReader.ReadInt32(),
                        lodOffset = vtxReader.ReadInt32();

                    string name = mdlReader.ReadString(64);
                    mdlReader.Skip(4);

                    var model = new StudioModel()
                    {
                        Name = name,
                        BoundingRadius = mdlReader.ReadSingle(),

                        NumMeshes = mdlReader.ReadInt32(),
                        MeshIndex = mdlReader.ReadInt32(),

                        NumVertices = mdlReader.ReadInt32(),
                        VertexIndex = mdlReader.ReadInt32(),
                        TangentIndex = mdlReader.ReadInt32(),

                        NumAttachments = mdlReader.ReadInt32(),
                        AttachmentIndex = mdlReader.ReadInt32(),

                        NumEyeballs = mdlReader.ReadInt32(),
                        EyeballIndex = mdlReader.ReadInt32(),

                        LODs = new StudioLOD[numLODs],
                        BodyPart = bodyPart
                    };
                    
                    var lodPos = vtxModelPos + lodOffset;
                    vtxStream.Position = lodPos;

                    for (int LOD = 0; LOD < numLODs; LOD++)
                    {
                        int vtxNumMeshes = vtxReader.ReadInt32(),
                            vtxMeshOffset = vtxReader.ReadInt32();

                        var lod = new StudioLOD()
                        {
                            SwitchPoint = vtxReader.ReadSingle(),
                            Meshes = new StudioMesh[vtxNumMeshes],
                            Model = model
                        };

                        int vtxMeshPos = lodPos + vtxMeshOffset;
                        vtxStream.Position = vtxMeshPos;

                        int mdlMeshPos = mdlDataPos + model.MeshIndex;
                        mdlStream.Position = mdlMeshPos;

                        for (int MESH = 0; MESH < vtxNumMeshes; MESH++)
                        {
                            int numGroups = vtxReader.ReadInt32(),
                                groupOffset = vtxReader.ReadInt32(),
                                skinRefIndex = mdlReader.ReadInt32();

                            var matPaths = new string[numSkinFamilies];

                            for (int i = 0; i < numSkinFamilies; i++)
                            {
                                int matIndex = skinArray[i * numSkinRef + skinRefIndex];

                                try
                                {
                                    matPaths[i] = lodMaterialPaths[LOD][matIndex];
                                }
                                catch
                                {
                                    matPaths[i] = "";
                                }
                            }

                            var mesh = new StudioMesh()
                            {
                                Flags = (StudioMeshFlags)vtxReader.ReadByte(),
                                StripGroups = new StripGroup[numGroups],

                                SkinRefIndex = skinRefIndex,
                                ModelIndex = mdlReader.ReadInt32(),

                                NumVertices = mdlReader.ReadInt32(),
                                VertexOffset = mdlReader.ReadInt32(),

                                NumFlexes = mdlReader.ReadInt32(),
                                FlexIndex = mdlReader.ReadInt32(),

                                MaterialType = mdlReader.ReadInt32(),
                                MaterialParam = mdlReader.ReadInt32(),

                                MeshId = mdlReader.ReadInt32(),
                                Center = mdlReader.ReadVector3(),

                                Materials = matPaths,
                                LOD = lod
                            };

                            int groupPos = vtxMeshPos + groupOffset;
                            vtxStream.Position = groupPos;

                            for (int GROUP = 0; GROUP < numGroups; GROUP++)
                            {
                                int numVerts = vtxReader.ReadInt32(),
                                    vertOffset = vtxReader.ReadInt32(),

                                    numIndices = vtxReader.ReadInt32(),
                                    indexOffset = vtxReader.ReadInt32(),

                                    numStrips = vtxReader.ReadInt32(),
                                    stripOffset = vtxReader.ReadInt32();

                                if (mdl.Version >= 49)
                                    vtxReader.Skip(8);

                                var group = new StripGroup()
                                {
                                    Flags = (StripGroupFlags)vtxReader.ReadByte(),

                                    Indices = new ushort[numIndices],
                                    Vertices = new Vertex[numVerts],
                                    Strips = new Strip[numStrips],

                                    Mesh = mesh
                                };

                                // Read Verts
                                vtxStream.Position = groupPos + vertOffset;

                                for (int VERT = 0; VERT < numVerts; VERT++)
                                {
                                    group.Vertices[VERT] = new Vertex()
                                    {
                                        BoneWeightIndex  = vtxReader.ReadBytes(3),
                                        NumBones = vtxReader.ReadByte(),

                                        OrigMeshVertId = vtxReader.ReadUInt16(),
                                        BoneIds = vtxReader.ReadBytes(3),

                                        Group = group,
                                    };
                                }

                                // Read Indices
                                vtxStream.Position = groupPos + indexOffset;

                                for (int INDEX = 0; INDEX < numIndices; INDEX++)
                                    group.Indices[INDEX] = vtxReader.ReadUInt16();

                                // Read Strips
                                var stripPos = groupPos + stripOffset;
                                vtxStream.Position = stripPos;

                                for (int STRIP = 0; STRIP < numStrips; STRIP++)
                                {
                                    var strip = new Strip()
                                    {
                                        NumIndices = vtxReader.ReadInt32(),
                                        IndexOffset = vtxReader.ReadInt32(),

                                        NumVerts = vtxReader.ReadInt32(),
                                        VertOffset = vtxReader.ReadInt32(),

                                        NumBones = vtxReader.ReadInt16(),
                                        Flags = (StripFlags)vtxReader.ReadByte(),
                                    };

                                    int numBoneStateChanges = vtxReader.ReadInt32(),
                                        boneStateChangeOffset = vtxReader.ReadInt32();

                                    var boneStateChanges = new BoneStateChange[numBoneStateChanges];
                                    vtxStream.Position = stripPos + boneStateChangeOffset;
                                    strip.BoneStateChanges = boneStateChanges;

                                    for (int CHANGE = 0; CHANGE < numBoneStateChanges; CHANGE++)
                                    {
                                        boneStateChanges[CHANGE] = new BoneStateChange()
                                        {
                                            HardwareId = vtxReader.ReadInt32(),
                                            NewBoneId = vtxReader.ReadInt32()
                                        };
                                    }

                                    stripPos += 0x1B;
                                    vtxStream.Position = stripPos;

                                    group.Strips[STRIP] = strip;
                                }

                                groupPos += 0x19;
                                vtxStream.Position = groupPos;

                                mesh.StripGroups[GROUP] = group;
                            }

                            mdlMeshPos += 0x74;
                            vtxMeshPos += 0x09;

                            mdlStream.Position = mdlMeshPos;
                            vtxStream.Position = vtxMeshPos;

                            lod.Meshes[MESH] = mesh;
                        }

                        lodPos += 0x0C;
                        vtxStream.Position = lodPos;

                        model.LODs[LOD] = lod;
                    }

                    mdlDataPos += 0x94;
                    vtxModelPos += 0x08;

                    mdlStream.Position = mdlDataPos;
                    vtxStream.Position = vtxModelPos;

                    bodyPart.Models[MODEL] = model;
                }

                mdlBodyPos += 0x10;
                vtxBodyPos += 0x08;

                mdlStream.Position = mdlBodyPos;
                vtxStream.Position = vtxBodyPos;

                bodyParts[BODY_PART] = bodyPart;
            }

            Game = game;
            Header = mdl;
            Location = path;
            VertexData = vvd;
            TriangleData = vtx;
            Materials = materialPaths;

            mdlStream.Dispose();
            mdlReader.Dispose();

            vtxStream.Dispose();
            vtxReader.Dispose();
        }

        public List<MeshBuffer> GetMeshes(int bodyPartId = 0, int modelId = 0, int lodId = 0, int skinId = 0)
        {
            StudioBodyPart bodyPart = TriangleData.BodyParts[bodyPartId];
            StudioModel model = bodyPart.Models[modelId];

            StudioLOD lod = model.LODs[lodId];
            StudioMesh[] meshes = lod.Meshes;
            
            var lodData = new VertexData(VertexData, lodId);
            var modelData = new ModelVertexData(model, lodData);
            var meshBuffers = new List<MeshBuffer>();

            foreach (var mesh in meshes)
            {
                var meshData = new MeshVertexData(mesh, modelData);
                var matPath = mesh.Materials[skinId];

                var meshBuffer = new MeshBuffer()
                {
                    BodyPart = bodyPart.Name,
                    MaterialPath = matPath,

                    Indices = new List<ushort>(),
                    Vertices = new List<StudioVertex>()
                };

                for (int vertId = 0; vertId < mesh.NumVertices; vertId++)
                {
                    var vertex = meshData.GetVertex(vertId);
                    meshBuffer.Vertices.Add(vertex);
                }

                foreach (var group in mesh.StripGroups)
                {
                    foreach (var strip in group.Strips)
                    {
                        var flags = strip.Flags;
                        
                        if ((flags & StripFlags.IsTriList) != StripFlags.None)
                        {
                            for (int i = 0; i < strip.NumIndices; i += 3)
                            {
                                int index = strip.IndexOffset + i;

                                for (int j = 0; j < 3; j++)
                                {
                                    var meshIndex = group.GetMeshIndex(index + j);
                                    meshBuffer.Indices.Add(meshIndex);
                                }
                            }
                        }
                        else
                        {
                            Debug.Assert((flags & StripFlags.IsTriStrip) != StripFlags.None);

                            for (int i = 0; i < strip.NumIndices - 2; ++i)
                            {
                                int index = strip.IndexOffset + i;
                                int ccw = 1 - (i & 1);

                                meshBuffer.Indices.AddRange(new ushort[3]
                                {
                                    group.GetMeshIndex(index),
                                    group.GetMeshIndex(index + 1 + ccw),
                                    group.GetMeshIndex(index + 2 - ccw),
                                });
                            }
                        }
                    }
                }

                meshBuffers.Add(meshBuffer);
            }

            return meshBuffers;
        }
    }
}
