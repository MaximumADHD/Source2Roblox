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
using Source2Roblox.Util;
using System.Diagnostics;

namespace Source2Roblox.Geometry
{
    public static class ObjMesher
    {
        private const TextureFlags IGNORE = TextureFlags.Sky | TextureFlags.Trans | TextureFlags.Hint | TextureFlags.Skip | TextureFlags.Trigger;
        
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
                ValveMaterial vmt = null;
                
                if (GameMount.HasFile(matPath, game))
                {
                    vmt = new ValveMaterial(matPath, game);
                    vmt.SaveVTF(vmt.DiffusePath, exportDir);
                    vmt.SaveVTF(vmt.BumpPath, exportDir, true);
                }

                string diffusePath = vmt?.DiffusePath;
                objWriter.AppendLine($"\tg {matName}");

                if (!string.IsNullOrEmpty(diffusePath))
                {
                    string diffuse = diffusePath.Replace(".vtf", ".png");
                    diffuse = Program.CleanPath(diffuse);

                    objWriter.AppendLine($"\tusemtl {matName}\n");
                    mtlWriter.AppendLine($"newmtl {matName}");

                    string bumpPath = vmt.BumpPath;
                    bool noAlpha = vmt.NoAlpha;

                    if (!string.IsNullOrEmpty(bumpPath))
                    {
                        string bump = bumpPath.Replace(".vtf", ".png");
                        bump = Program.CleanPath(bump);
                        mtlWriter.AppendLine($"bump {bump}");
                    }
                    
                    mtlWriter.AppendLine($"map_Kd {diffuse}");
                    mtlWriter.AppendLine(noAlpha ? "" : $"map_d {diffuse}\n");
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

        public static void BakeBSP(BSPFile bsp, string exportDir, GameMount game = null)
        {
            string gameName = GameMount.GetGameName(game);
            string mapName = bsp.Name;

            var objWriter = new StringBuilder();
            var mtlWriter = new StringBuilder();

            foreach (Vector3 vert in bsp.Vertices)
            {
                var v = vert / 10f;
                objWriter.AppendLine($"v {v.X} {v.Z} {-v.Y}");
            }

            foreach (Vector3 norm in bsp.VertNormals)
                objWriter.AppendLine($"vn {norm.X} {norm.Z} {-norm.Y}");

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

            int normBaseId = 0;
            var materialSets = new Dictionary<string, ValveMaterial>();
            var facesByMaterial = new Dictionary<string, List<Face>>();

            foreach (Face face in bsp.Faces)
            {
                int planeId = face.PlaneNum;
                int dispInfo = face.DispInfo;
                int numEdges = face.NumEdges;
                int firstEdge = face.FirstEdge;

                face.FirstNorm = normBaseId;
                normBaseId += numEdges;

                var texInfo = bsp.TexInfo[face.TexInfo];
                var flags = texInfo.Flags;

                if ((flags & IGNORE) != TextureFlags.None)
                    continue;

                var texData = bsp.TexData[texInfo.TextureData];
                int stringIndex = texData.StringTableIndex;

                var matIndex = bsp.TexDataStringTable[stringIndex];
                var material = bsp.TexDataStringData[matIndex];

                if (material.StartsWith($"maps/{mapName}"))
                {
                    while (true)
                    {
                        var lastUnderscore = material.LastIndexOf('_');

                        if (lastUnderscore < 0)
                            break;

                        string number = material.Substring(lastUnderscore + 1);

                        if (int.TryParse(number, out int _))
                        {
                            material = material.Substring(0, lastUnderscore);
                            continue;
                        }

                        material = material.Replace($"maps/{mapName}/", "");
                        break;
                    }
                }

                if (!materialSets.ContainsKey(material))
                {
                    var saveInfo = new FileInfo(material);
                    string saveDir = saveInfo.DirectoryName;

                    Directory.CreateDirectory(saveDir);
                    Console.WriteLine($"Fetching material {material}");

                    var vmt = new ValveMaterial(material, game);
                    materialSets[material] = vmt;

                    if (!string.IsNullOrEmpty(vmt.BumpPath))
                        vmt.SaveVTF(vmt.BumpPath, exportDir);

                    if (!string.IsNullOrEmpty(vmt.DiffusePath))
                        vmt.SaveVTF(vmt.DiffusePath, exportDir);

                    facesByMaterial[material] = new List<Face>();
                }

                facesByMaterial[material].Add(face);
            }

            int numFaces = 0;
            string objPath = Path.Combine(exportDir, $"{mapName}.obj");
            string mtlPath = Path.Combine(exportDir, $"{mapName}.mtl");

            foreach (string material in facesByMaterial.Keys)
            {
                var faces = facesByMaterial[material];
                objWriter.AppendLine($"\nusemtl {material}");
                
                foreach (var face in faces)
                {
                    int numEdges  = face.NumEdges,
                        firstEdge = face.FirstEdge,
                        firstNorm = face.FirstNorm;

                    // objWriter.AppendLine($"  g face_{numFaces++}");
                    objWriter.Append("  f");

                    for (int i = 0; i < numEdges; i++)
                    {
                        var vertIndex = 1 + vertIndices[firstEdge + i];
                        var normIndex = 1 + normIndices[firstNorm + i];
                        objWriter.Append($" {vertIndex}//{normIndex}");
                    }

                    objWriter.AppendLine();
                }

                mtlWriter.AppendLine($"newmtl {material}");
                mtlWriter.AppendLine($"map_Kd materials/{material}.png\n");
            }

            string obj = objWriter.ToString();
            File.WriteAllText(objPath, obj);

            string mtl = mtlWriter.ToString();
            File.WriteAllText(mtlPath, mtl);
        }
    }
}
