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

namespace Source2Roblox.Geometry
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
            var game = model.Game;
            var info = new FileInfo(model.Name);

            string name = info.Name.Replace(".mdl", "");
            exportDir = Path.Combine(exportDir, "SourceModels", name);

            if (!Directory.Exists(exportDir))
                Directory.CreateDirectory(exportDir);

            var objWriter = new StringBuilder();
            var mtlWriter = new StringBuilder();
            var meshBuffers = new List<MeshBuffer>();
            var handledFiles = new HashSet<string>();
            
            for (int bodyPart = 0; bodyPart < model.BodyPartCount; bodyPart++)
            {
                var meshes = model.GetMeshes(bodyPart, subModel, lod, skin);
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
                            Program.ProcessVMT(entry, ref diffusePath, ref bumpPath, ref noAlpha);

                        Program.SaveVTF(diffusePath, noAlpha, exportDir, game);
                        Program.SaveVTF(bumpPath, true, exportDir, game);
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
