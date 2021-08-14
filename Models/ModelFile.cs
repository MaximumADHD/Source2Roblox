using Source2Roblox.FileSystem;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Source2Roblox.Models
{
    // Largely a port of this implementation from noclip.website on GitHub.
    // Special thanks for their research into this spec!
    // https://github.com/magcius/noclip.website/blob/master/src/SourceEngine/Studio.ts

    public class ModelFile
    {
        public readonly ModelHeader Header;
        public readonly VertexData VertexData;
        public readonly TriangleData TriangleData;

        public string Name => Header.Name;
        public ModelFlags Flags => Header.Flags;

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

                            int groupPos = vtxMeshPos + groupOffset,
                                meshIndexBase = 0;

                            vtxStream.Position = groupPos;

                            for (int GROUP = 0; GROUP < numGroups; GROUP++)
                            {
                                int numVerts = vtxReader.ReadInt32(),
                                    vertOffset = vtxReader.ReadInt32(),

                                    numIndices = vtxReader.ReadInt32(),
                                    indexOffset = vtxReader.ReadInt32();

                                vtxReader.Skip(8);

                                var group = new StripGroup()
                                {
                                    Flags = (StripGroupFlags)vtxReader.ReadByte(),
                                    Verts = new StripVertex[numVerts],
                                    Indices = new ushort[numIndices],
                                    Mesh = mesh
                                };

                                if ((group.Flags & StripGroupFlags.IsHardwareSkinned) == StripGroupFlags.None)
                                    continue;

                                // Read Verts
                                vtxStream.Position = groupPos + vertOffset;

                                for (int VERT = 0; VERT < numVerts; VERT++)
                                {
                                    var boneWeightIndex = vtxReader.ReadBytes(3);
                                    var numBones = vtxReader.ReadByte();

                                    var vertId = vtxReader.ReadUInt16();
                                    var boneIds = vtxReader.ReadBytes(3);

                                    var mdlVertIndex = mesh.VertexOffset + vertId;
                                    var vvdVertIndex = vvd.FixupSearch(mdlVertIndex);

                                    var vvdVertex = vvd.Vertices[vvdVertIndex];
                                    var vvdTangent = vvd.Tangents[vvdVertIndex];

                                    // Tangent sanity check.
                                    Debug.Assert(vvdTangent.W == 0 || Math.Abs(vvdTangent.W) == 1);

                                    var boneWeights = new float[4];
                                    var totalBoneWeight = 0.0f;

                                    for (int i = 0; i < numBones; i++)
                                    {
                                        var index = boneWeightIndex[i];
                                        boneWeights[i] = vvdVertex.Weights[index];
                                        totalBoneWeight += boneWeights[i];
                                    }

                                    for (int i = 0; i < numBones; i++)
                                        boneWeights[i] /= totalBoneWeight;

                                    group.Verts[VERT] = new StripVertex()
                                    {
                                        BoneWeights = boneWeights,
                                        Index = vvdVertIndex,

                                        BoneIds = boneIds,
                                        Group = group,
                                    };
                                }

                                // Read Indices
                                vtxStream.Position = groupPos + indexOffset;

                                for (int INDEX = 0; INDEX < numIndices; INDEX++)
                                {
                                    int index = meshIndexBase + vtxReader.ReadUInt16();
                                    group.Indices[INDEX] = (ushort)index;
                                }

                                groupPos += 0x19;
                                meshIndexBase += numVerts;

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

        public StripGroup[] GetStripGroups(int lod = 0, int model = 0, int group = 0, int bodyPart = 0)
        {
            var meshes = TriangleData
                .BodyParts[bodyPart]
                .Models[model]
                .LODs[lod]
                .Meshes;

            var groups = meshes
                .Select(vmesh => vmesh.StripGroups[group])
                .ToArray();

            return groups;
        }
    }
}
