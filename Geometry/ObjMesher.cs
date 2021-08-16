using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Source2Roblox.FileSystem;
using Source2Roblox.Models;
using Source2Roblox.Textures;
using Source2Roblox.World;
using Source2Roblox.World.Types;

using RobloxFiles.DataTypes;
using ValveKeyValue;
using System.Diagnostics;

namespace Source2Roblox
{
    public static class ObjMesher
    {
        private const TextureFlags IGNORE = TextureFlags.Sky | TextureFlags.Trans | TextureFlags.Hint | TextureFlags.Skip | TextureFlags.Trigger;
        private static readonly KVSerializer VmtHelper = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);

        private static string GetFileName(string path)
        {
            var info = new FileInfo(path);
            return info.Name.Replace(info.Extension, "");
        }

        public static void BakeMDL(ModelFile model, string exportDir, int skin = 0, int lod = 0, int subModel = 0)
        {
            if (!Directory.Exists(exportDir))
                Directory.CreateDirectory(exportDir);

            var game = model.Game;
            var info = new FileInfo(model.Name);

            string name = info.Name.Replace(".mdl", "");

            var objWriter = new StringBuilder();
            var mtlWriter = new StringBuilder();
            var meshBuffers = new List<MeshBuffer>();
            
            for (int bodyPart = 0; bodyPart < model.BodyPartCount; bodyPart++)
            {
                var meshes = model.GetMeshes(lod, skin, subModel, bodyPart);
                meshBuffers.AddRange(meshes);
            }

            // Write Vertices.
            objWriter.AppendLine("# Vertices\n");

            var allVerts = meshBuffers
                .SelectMany(mesh => mesh.Vertices)
                .ToArray();

            foreach (var vert in allVerts)
            {
                var pos = vert.Position / 10f;
                objWriter.AppendLine($"v  {pos.X} {pos.Z} {-pos.Y}");

                var norm = vert.Normal;
                objWriter.AppendLine($"vn {norm.X} {norm.Z} {-norm.Y}");

                var uv = vert.UV;
                objWriter.AppendLine($"vt {uv.X} {1f - uv.Y}\n");
            }

            // Write Faces.
            int faceIndexBase = 1;
            string lastBodyPart = "";
            objWriter.AppendLine("# Faces\n");
            
            foreach (MeshBuffer mesh in meshBuffers)
            {
                string matPath = mesh.MaterialPath;
                string bodyPart = mesh.BodyPart;

                if (bodyPart != lastBodyPart)
                {
                    objWriter.AppendLine($"o {bodyPart}\n");
                    lastBodyPart = bodyPart;
                }

                var matInfo = new FileInfo(matPath);
                string matName = matInfo.Name.Replace(".vmt", "");

                string diffusePath = "",
                       bumpPath = "";

                bool noAlpha = true;

                if (GameMount.HasFile(matPath, game))
                {
                    using (var vmtStream = GameMount.OpenRead(matPath, game))
                    {
                        var vmt = VmtHelper.Deserialize(vmtStream);
                       
                        foreach (var entry in vmt)
                        {
                            string key = entry.Name.ToLowerInvariant();
                            var value = entry.Value.ToString();

                            if (key == "$basetexture")
                            {
                                string path = $"materials/{value}.vtf";

                                if (!GameMount.HasFile(path, game))
                                {
                                    Console.WriteLine($"\tInvalid $basetexture: {path}");
                                    continue;
                                }

                                diffusePath = path;
                            }
                            else if (key == "$bumpmap")
                            {
                                string path = $"materials/{value}.vtf";

                                if (!GameMount.HasFile(path, game))
                                {
                                    Console.WriteLine($"\tInvalid $bumpmap: {path}");
                                    continue;
                                }

                                bumpPath = path;
                            }
                            else if (key == "$translucent")
                            {
                                bool alpha = (value == "1");
                                noAlpha = !alpha;
                            }
                        }

                        if (!string.IsNullOrEmpty(diffusePath))
                        {
                            string diffuseName = GetFileName(diffusePath);
                            string diffuseFile = Path.Combine(exportDir, diffuseName + ".png");

                            Console.WriteLine($"\tReading {diffusePath}");

                            using (var vtfStream = GameMount.OpenRead(diffusePath, game))
                            using (var vtfReader = new BinaryReader(vtfStream))
                            {
                                var vtf = new VTFFile(vtfReader, noAlpha);
                                vtf.HighResImage.Save(diffuseFile);

                                Console.WriteLine($"\tWrote {diffuseFile}");
                            }
                        }

                        if (!string.IsNullOrEmpty(bumpPath))
                        {
                            string bumpName = GetFileName(bumpPath);
                            string bumpFile = Path.Combine(exportDir, bumpName + ".png");

                            Console.WriteLine($"\tReading {bumpPath}");

                            using (var vtfStream = GameMount.OpenRead(bumpPath, game))
                            using (var vtfReader = new BinaryReader(vtfStream))
                            {
                                var vtf = new VTFFile(vtfReader, noAlpha);
                                vtf.HighResImage.Save(bumpFile);

                                Console.WriteLine($"\tWrote {bumpFile}");
                            }
                        }
                    }
                }
                
                objWriter.AppendLine($"\tg {matName}");

                if (!string.IsNullOrEmpty(diffusePath))
                {
                    string diffuseName = GetFileName(diffusePath);
                    objWriter.AppendLine($"\tusemtl {matName}\n");
                    mtlWriter.AppendLine($"newmtl {matName}");

                    if (!string.IsNullOrEmpty(bumpPath))
                    {
                        string bumpName = GetFileName(bumpPath);
                        mtlWriter.AppendLine($"bump {bumpName}.png");
                    }
                        
                    mtlWriter.AppendLine($"map_Kd {diffuseName}.png");
                    mtlWriter.AppendLine(noAlpha ? "" : $"map_d  {diffuseName}.png\n");
                }
                
                for (int i = 0; i < mesh.NumIndices; i += 3)
                {
                    objWriter.Append("\tf");

                    for (int j = 2; j >= 0; j--)
                    {
                        int f = faceIndexBase + mesh.Indices[i + j];
                        objWriter.Append($" {f}/{f}/{f}");
                    }

                    objWriter.AppendLine();
                }

                objWriter.AppendLine();
                faceIndexBase += mesh.NumVerts;
            }

            string obj = objWriter.ToString();
            string objPath = Path.Combine(exportDir, $"{name}.obj");

            string mtl = mtlWriter.ToString();
            string mtlPath = Path.Combine(exportDir, $"{name}.mtl");

            File.WriteAllText(objPath, obj);
            Console.WriteLine($"\tWrote: {objPath}");

            File.WriteAllText(mtlPath, mtl);
            Console.WriteLine($"\tWrote: {mtlPath}");
        }

        public static string BakeBSP(BSPFile bsp)
        {
            var writer = new StringBuilder();

            foreach (Vector3 vert in bsp.Vertices)
            {
                var v = vert / 10f;
                writer.AppendLine($"v {v.X} {v.Z} {-v.Y}");
            }

            foreach (Vector3 norm in bsp.VertNormals)
                writer.AppendLine($"vn {norm.X} {norm.Z} {-norm.Y}");

            var edges = bsp.Edges;
            var surfEdges = bsp.SurfEdges;

            var vertIndices = new List<uint>();
            var normIndices = bsp.VertNormalIndices;

            foreach (int surf in surfEdges)
            {
                uint edge;

                if (surf >= 0)
                    edge = edges[surf * 2 + 0];
                else
                    edge = edges[-surf * 2 + 1];

                vertIndices.Add(edge);
            }

            int normBaseIndex = 0;

            foreach (Face face in bsp.Faces)
            {
                int planeId = face.PlaneNum;
                int dispInfo = face.DispInfo;
                int numEdges = face.NumEdges;
                int firstEdge = face.FirstEdge;

                int firstNorm = normBaseIndex;
                normBaseIndex += numEdges;

                var texInfo = bsp.TexInfo[face.TexInfo];
                var flags = texInfo.Flags;

                if ((flags & IGNORE) != TextureFlags.None)
                    continue;

                writer.Append("f");

                for (int i = 0; i < numEdges; i++)
                {
                    var vertIndex = 1 + vertIndices[firstEdge + i];
                    var normIndex = 1 + normIndices[firstNorm + i];
                    writer.Append($" {vertIndex}//{normIndex}");
                }

                writer.AppendLine();
            }

            return writer.ToString();
        }
    }
}
