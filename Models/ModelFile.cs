using Source2Roblox.FileSystem;
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

    public class MaterialBatch
    {
        public int NumVerts => Vertices.Count;
        public List<StudioVertex> Vertices;

        public int NumIndices => Indices.Count;
        public List<ushort> Indices;
    }

    public class ModelFile
    {
        public readonly ModelHeader Header;
        public readonly VertexData VertexData;
        public readonly TriangleData TriangleData;

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

                        LODs = new StudioModelLOD[numLODs],
                        BodyPart = bodyPart
                    };
                    
                    var lodPos = vtxModelPos + lodOffset;
                    vtxStream.Position = lodPos;

                    for (int LOD = 0; LOD < numLODs; LOD++)
                    {
                        int vtxNumMeshes = vtxReader.ReadInt32(),
                            vtxMeshOffset = vtxReader.ReadInt32();

                        var lod = new StudioModelLOD()
                        {
                            SwitchPoint = vtxReader.ReadSingle(),
                            Meshes = new StudioMesh[vtxNumMeshes],
                            Model = model
                        };

                        int vtxMeshPos = lodPos + vtxMeshOffset;
                        vtxStream.Position = vtxMeshPos;

                        int mdlMeshPos = mdlDataPos + model.MeshIndex;
                        mdlStream.Position = mdlMeshPos;

                        var vvdLod = new VertexData(vvd, LOD);

                        for (int MESH = 0; MESH < vtxNumMeshes; MESH++)
                        {
                            int numGroups = vtxReader.ReadInt32(),
                                groupOffset = vtxReader.ReadInt32();

                            var mesh = new StudioMesh()
                            {
                                Flags = (StudioMeshFlags)vtxReader.ReadByte(),
                                StripGroups = new StripGroup[numGroups],

                                SkinRefIndex = mdlReader.ReadInt32(),
                                ModelIndex = mdlReader.ReadInt32(),

                                NumVertices = mdlReader.ReadInt32(),
                                VertexOffset = mdlReader.ReadInt32(),

                                NumFlexes = mdlReader.ReadInt32(),
                                FlexIndex = mdlReader.ReadInt32(),

                                MaterialType = mdlReader.ReadInt32(),
                                MaterialParam = mdlReader.ReadInt32(),

                                MeshId = mdlReader.ReadInt32(),
                                Center = mdlReader.ReadVector3(),

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

            Header = mdl;
            VertexData = vvd;
            TriangleData = vtx;

            mdlStream.Dispose();
            mdlReader.Dispose();

            vtxStream.Dispose();
            vtxReader.Dispose();
        }

        public List<MaterialBatch> GetTriangles(int lod = 0, int subModel = 0, int bodyPart = 0)
        {
            StudioModel model = TriangleData
                .BodyParts[bodyPart]
                .Models[subModel];

            StudioMesh[] meshes = model
                .LODs[lod]
                .Meshes;
            
            var lodData = new VertexData(VertexData, lod);
            var modelData = new ModelVertexData(model, lodData);
            var materialBatches = new List<MaterialBatch>();

            foreach (var mesh in meshes)
            {
                var meshData = new MeshVertexData(mesh, modelData);

                var materialBatch = new MaterialBatch()
                {
                    Vertices = new List<StudioVertex>(),
                    Indices = new List<ushort>()
                };

                for (int vertId = 0; vertId < mesh.NumVertices; vertId++)
                {
                    var vertex = meshData.GetVertex(vertId);
                    materialBatch.Vertices.Add(vertex);
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
                                    materialBatch.Indices.Add(meshIndex);
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

                                var indices = new ushort[3]
                                {
                                    group.GetMeshIndex(index),
                                    group.GetMeshIndex(index + 1 + ccw),
                                    group.GetMeshIndex(index + 2 - ccw)
                                };

                                materialBatch.Indices.AddRange(indices);
                            }
                        }
                    }
                }

                materialBatches.Add(materialBatch);
            }

            return materialBatches;
        }
    }
}
