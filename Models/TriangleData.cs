using System;
using System.Collections.Generic;
using System.IO;

using Source2Roblox.FileSystem;

namespace Source2Roblox.Models
{
    [Flags]
    public enum StripFlags
    {
        IsTriList = 0x01,
        IsTriStrip = 0x02
    }
    
    [Flags]
    public enum StripGroupFlags
    {
        IsFlexed = 0x01,
        IsHardwareSkinned = 0x02,
        IsDeltaFlexed = 0x04,
        SuppressHardwareMorph = 0x08
    }

    public struct StripGroup
    {
        public int NumVerts;
        public int VertOffset;

        public int NumIndices;
        public int IndexOffset;

        public int NumStrips;
        public int StripOffset;

        public StripFlags Flags;
    }

    public struct StudioMesh
    {
        public int NumStripGroups;
        public int StripGroupOffset;

        public byte Flags;
        public List<StripGroup> StripGroups;
    }

    public struct StudioModelLOD
    {
        public float SwitchPoint;
        public List<StudioMesh> Meshes;

        public int NumMeshes;
        public int MeshOffset;
    }

    public struct StudioModel
    {
        public int NumLODs;
        public int LODOffset;
        public List<StudioModelLOD> LODs;
    }

    public struct StudioBodyPart
    {
        public int NumModels;
        public int ModelOffset;
        public List<StudioModel> Models;
    }

    public struct TriangleData
    {
        public readonly uint Version;
        public readonly uint VertCacheSize;

        public readonly ushort MaxBonesPerStrip;
        public readonly ushort MaxBonesPerTri;
        public readonly uint MaxBonesPerVert;

        public readonly uint Checksum;
        public readonly uint NumLODs;

        public readonly uint NumBodyParts;
        public readonly uint BodyPartOffset;
        public readonly List<StudioBodyPart> BodyParts;

        public readonly uint MaterialReplacementListOffset;

        public TriangleData(string path, GameMount game = null)
        {
            using (var stream = GameMount.OpenRead(path, game))
            using (var reader = new BinaryReader(stream))
            {
                Version = reader.ReadUInt32();
                VertCacheSize = reader.ReadUInt32();

                MaxBonesPerStrip = reader.ReadUInt16();
                MaxBonesPerTri = reader.ReadUInt16();
                MaxBonesPerVert = reader.ReadUInt32();

                Checksum = reader.ReadUInt32();
                NumLODs = reader.ReadUInt32();

                MaterialReplacementListOffset = reader.ReadUInt32();

                NumBodyParts = reader.ReadUInt32();
                BodyPartOffset = reader.ReadUInt32();

                BodyParts = new List<StudioBodyPart>();
                stream.Position = BodyPartOffset;

                for (int i = 0; i < NumBodyParts; i++)
                {
                    var bodyPart = new StudioBodyPart()
                    {
                        NumModels = reader.ReadInt32(),
                        ModelOffset = reader.ReadInt32(),
                        Models = new List<StudioModel>()
                    };

                    int modelIndex = (int)(BodyPartOffset + bodyPart.ModelOffset);
                    stream.Position = modelIndex;

                    for (int j = 0; j < bodyPart.NumModels; j++)
                    {
                        var model = new StudioModel()
                        {
                            NumLODs = reader.ReadInt32(),
                            LODOffset = reader.ReadInt32(),
                            LODs = new List<StudioModelLOD>()
                        };

                        var lodIndex = modelIndex + model.LODOffset;
                        stream.Position = lodIndex;
                        bodyPart.Models.Add(model);

                        for (int lod = 0; lod < model.NumLODs; lod++)
                        {
                            var modelLOD = new StudioModelLOD()
                            {
                                NumMeshes = reader.ReadInt32(),
                                MeshOffset = reader.ReadInt32(),
                                SwitchPoint = reader.ReadSingle(),
                                Meshes = new List<StudioMesh>()
                            };

                            int meshIndex = lodIndex + modelLOD.MeshOffset;
                            stream.Position = meshIndex;
                            model.LODs.Add(modelLOD);

                            for (int m = 0; m < modelLOD.NumMeshes; m++)
                            {
                                var mesh = new StudioMesh()
                                {
                                    NumStripGroups = reader.ReadInt32(),
                                    StripGroupOffset = reader.ReadInt32(),
                                    StripGroups = new List<StripGroup>(),
                                    Flags = reader.ReadByte()
                                };

                                int stripGroupIndex = meshIndex + mesh.StripGroupOffset;
                                stream.Position = stripGroupIndex;
                                modelLOD.Meshes.Add(mesh);

                                for (int g = 0; g < mesh.NumStripGroups; g++)
                                {
                                    var stripGroup = new StripGroup()
                                    {
                                        NumVerts = reader.ReadInt32(),
                                        VertOffset = reader.ReadInt32(),

                                        NumIndices = reader.ReadInt32(),
                                        IndexOffset = reader.ReadInt32(),

                                        NumStrips = reader.ReadInt32(),
                                        StripOffset = reader.ReadInt32(),

                                        Flags = (StripFlags)reader.ReadByte()
                                    };

                                    int stripIndex = stripGroupIndex + stripGroup.StripOffset;
                                    mesh.StripGroups.Add(stripGroup);
                                    stream.Position = stripIndex;

                                    stripGroupIndex += 0x19;
                                    stream.Position = stripGroupIndex;
                                }
                            }

                            lodIndex += 0x0C;
                            stream.Position = lodIndex;
                        }

                        modelIndex += 0x08;
                        stream.Position = modelIndex;
                    }

                    BodyParts.Add(bodyPart);
                }
            }
        }
    }
}
