using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;

using Source2Roblox.FileSystem;
using Source2Roblox.Geometry;
using Source2Roblox.Models;
using Source2Roblox.Textures;
using Source2Roblox.World;

using RobloxFiles;
using RobloxFiles.Enums;
using RobloxFiles.DataTypes;
using System.Linq;

using ValveKeyValue;

namespace Source2Roblox
{
    class Program
    {
        private static readonly Dictionary<string, string> argMap = new Dictionary<string, string>();
        private static readonly Dictionary<string, Image> handledFiles = new Dictionary<string, Image>();
        private static readonly KVSerializer VmtHelper = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        public static GameMount GameMount { get; private set; }
        
        public static string GetArg(string argName)
        {
            if (argMap.TryGetValue(argName, out string arg))
                return arg;

            return null;
        }

        public static string CleanPath(string path)
        {
            string cleaned = path
                .ToLowerInvariant()
                .Replace('\\', '/');

            return cleaned;
        }

        public static string GetFileName(string path)
        {
            var info = new FileInfo(path);
            return info.Name.Replace(info.Extension, "");
        }

        public static void SaveVTF(string path, bool noAlpha, string exportDir, GameMount game = null)
        {
            if (!string.IsNullOrEmpty(path))
            {
                string fileName = GetFileName(path);
                string filePath = Path.Combine(exportDir, fileName + ".png");

                if (handledFiles.ContainsKey(filePath))
                    return;

                if (!handledFiles.TryGetValue(path, out Image bitmap))
                {
                    Console.WriteLine($"\tReading {path}");

                    using (var vtfStream = GameMount.OpenRead(path, game))
                    using (var vtfReader = new BinaryReader(vtfStream))
                    {
                        var vtf = new VTFFile(vtfReader, noAlpha);
                        bitmap = vtf.HighResImage;

                        handledFiles.Add(path, bitmap);
                    }
                }

                bitmap.Save(filePath);
                handledFiles[filePath] = bitmap;

                Console.WriteLine($"\tWrote {filePath}");
            }
        }

        public static void ProcessVMT(KVObject entry, ref string diffusePath, ref string bumpPath, ref bool noAlpha)
        {
            string key = entry.Name.ToLowerInvariant();
            var value = entry.Value.ToString();

            if (key == ">=dx90_20b")
            {
                foreach (var child in entry.Children)
                    ProcessVMT(child, ref diffusePath, ref bumpPath, ref noAlpha);

                return;
            }

            if (key == "$basetexture")
            {
                diffusePath = $"materials/{value}.vtf";
                return;
            }

            if (key == "$bumpmap")
            {
                bumpPath = $"materials/{value}.vtf";
                return;
            }

            if (key == "$translucent")
            {
                noAlpha = (value != "1");
                return;
            }
        }

        public static void BakeRbxm(ModelFile model)
        {
            var modelInfo = new FileInfo(model.Name);
            
            string modelName = modelInfo.Name
                .Replace(".mdl", "")
                .ToLowerInvariant();

            string localAppData = Environment.GetEnvironmentVariable("localappdata");
            string exportDir = Path.Combine(localAppData, "Roblox Studio", "content", "models", modelName);

            Directory.CreateDirectory(exportDir);

            var meshBuffers = new List<MeshBuffer>();
            var exportBlob = new BinaryRobloxFile();

            var exportModel = new Model()
            {
                Name = modelName,
                Parent = exportBlob
            };

            for (int bodyPart = 0; bodyPart < model.BodyPartCount; bodyPart++)
            {
                var meshes = model.GetMeshes(bodyPart);
                meshBuffers.AddRange(meshes);
            }

            var allVerts = meshBuffers.SelectMany(buff => buff.Vertices);
            Region3 baseAABB = MeshBuffer.ComputeAABB(allVerts);
            Vector3 baseCenter = baseAABB.CFrame.Position;

            var handledFiles = new HashSet<string>();
            string lastBodyPart = "";
            var game = model.Game;

            foreach (var meshBuffer in meshBuffers)
            {
                Region3 aabb = MeshBuffer.ComputeAABB(meshBuffer.Vertices);
                Vector3 center = aabb.CFrame.Position;

                int numVerts = meshBuffer.NumVerts;
                int numIndices = meshBuffer.NumIndices;

                var verts = meshBuffer.Vertices;
                var indices = meshBuffer.Indices;

                var mesh = new RobloxMesh()
                {
                    NumVerts = numVerts,
                    NumFaces = numIndices / 3,

                    Verts = new List<RobloxVertex>(),
                    Faces = new List<int[]>()
                };

                mesh.NumVerts = meshBuffer.NumVerts;
                mesh.NumFaces = meshBuffer.NumIndices / 3;

                foreach (RobloxVertex vert in verts)
                {
                    vert.Position -= center;
                    mesh.Verts.Add(vert);
                }
                
                for (int i = 0; i < numIndices; i += 3)
                {
                    var face = new int[3]
                    {
                        indices[i + 2],
                        indices[i + 1],
                        indices[i + 0],
                    };

                    mesh.Faces.Add(face);
                }

                string matPath = meshBuffer.MaterialPath;
                var info = new FileInfo(matPath);

                string matName = info.Name.Replace(".vmt", "");
                string name = meshBuffer.BodyPart;

                if (name == lastBodyPart)
                    name = matName;
                else
                    lastBodyPart = name;
                
                if (!name.StartsWith(modelName))
                    name = $"{modelName}_{name}";

                string diffusePath = "",
                       bumpPath = "";

                bool noAlpha = true;

                if (GameMount.HasFile(matPath, game))
                {
                    handledFiles.Add(matPath);

                    using (var vmtStream = GameMount.OpenRead(matPath, game))
                    {
                        var vmt = VmtHelper.Deserialize(vmtStream);

                        foreach (var entry in vmt)
                            ProcessVMT(entry, ref diffusePath, ref bumpPath, ref noAlpha);

                        SaveVTF(diffusePath, noAlpha, exportDir, game);
                        SaveVTF(bumpPath, true, exportDir, game);
                    }
                }

                var meshPart = new MeshPart()
                {
                    MeshId = $"rbxasset://models/{modelName}/{name}.mesh",
                    InitialSize = aabb.Size,
                    CFrame = aabb.CFrame,
                    DoubleSided = true,
                    Size = aabb.Size,
                    Anchored = true,
                    Name = name,
                };

                if (string.IsNullOrEmpty(diffusePath))
                {
                    meshPart.Color3uint8 = new Color3(1, 1, 1);
                    meshPart.Material = Material.SmoothPlastic;
                }
                else
                {
                    string diffuseName = GetFileName(diffusePath);
                    meshPart.TextureID = $"rbxasset://models/{modelName}/{diffuseName}.png";

                    if (!string.IsNullOrEmpty(bumpPath) || !noAlpha)
                    {
                        var surface = new SurfaceAppearance()
                        {
                            AlphaMode = noAlpha ? AlphaMode.Overlay : AlphaMode.Transparency,
                            ColorMap = meshPart.TextureID,
                        };

                        if (!string.IsNullOrEmpty(bumpPath))
                        {
                            string bumpName = GetFileName(bumpPath);
                            surface.NormalMap = $"rbxasset://models/{modelName}/{bumpName}.png";
                        }
                        
                        surface.Parent = meshPart;
                    }
                }

                string meshPath = Path.Combine(exportDir, $"{name}.mesh");

                using (var stream = File.OpenWrite(meshPath))
                    mesh.Save(stream);

                meshPart.Parent = exportModel;
            }

            var parts = exportModel.GetChildrenOfType<BasePart>();
            BasePart largestPart = null;
            float largestMass = 0;

            foreach (var part in parts)
            {
                var size = part.Size;
                var mass = size.X * size.Y * size.Z;

                if (mass > largestMass)
                {
                    largestMass = mass;
                    largestPart = part;
                }
            }

            string exportPath = Path.Combine(exportDir, "..", $"{modelName}.rbxm");
            exportModel.WorldPivotData = new CFrame();
            exportModel.PrimaryPart = largestPart;
            exportBlob.Save(exportPath);
        }

        static void Main(string[] args)
        {
            #region Process Launch Options
            string argKey = "";

            foreach (string arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    if (!string.IsNullOrEmpty(argKey))
                        argMap.Add(argKey, "");

                    argKey = arg;
                }
                else if (!string.IsNullOrEmpty(argKey))
                {
                    argMap.Add(argKey, arg);
                    argKey = "";
                }
            }

            if (!string.IsNullOrEmpty(argKey))
                argMap.Add(argKey, "");
            #endregion

            string model   = GetArg("-model");
            string gameDir = GetArg("-game");
            string mapName = GetArg("-map");
            string vtfName = GetArg("-vtf");
            
            if (gameDir == null)
                return;

            Console.Write($"Loading game mount: {gameDir}... ");
            GameMount = new GameMount(gameDir);

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Console.WriteLine("Ready!");

            if (vtfName != null)
            {
                var info = new FileInfo(vtfName);
                string name = info.Name.Replace(".vtf", "");

                string dir = Path.Combine(desktop, "ExamineVTF", name);
                Directory.CreateDirectory(dir);

                using (var stream = GameMount.OpenRead(vtfName))
                using (var reader = new BinaryReader(stream))
                {
                    var file = new VTFFile(reader, true);

                    for (int i = 0; i < file.NumFrames; i++)
                    {
                        var frame = file.Frames[i];

                        for (int j = 0; j < frame.Count; j++)
                        {
                            var mipmap = frame[j];

                            for (int k = 0; k < mipmap.Count; k++)
                            {
                                var image = mipmap[k];
                                string savePath = Path.Combine(dir, $"{name}_{i}_{j}_{k}.png");
                                image.Save(savePath);
                            }
                        }
                    }

                    var lowRes = file.LowResImage;

                    if (lowRes != null)
                    {
                        string lowResPath = Path.Combine(dir, $"{name}_LOW_RES.png");
                        lowRes.Save(lowResPath);
                    }
                }
            }

            if (model != null)
            {
                bool running = true;
                string exportDir = Path.Combine(desktop, "SourceModels");
                Directory.CreateDirectory(exportDir);

                while (running)
                {
                    string path;

                    if (!string.IsNullOrEmpty(model))
                    {
                        path = model;
                        running = false;
                        Console.WriteLine($"Processing -model: {model}");
                    }
                    else
                    {
                        Console.Write("Enter a model path: ");
                        path = Console.ReadLine();
                    }

                    var mdl = new ModelFile(path);
                    // ObjMesher.BakeMDL(mdl, desktop);

                    try
                    {
                        BakeRbxm(mdl);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error! {e.Message} {e.StackTrace}");
                    }
                }
            }

            if (mapName != null)
            {
                var bsp = new BSPFile($"maps/{mapName}.bsp");
                string obj = ObjMesher.BakeBSP(bsp);

                Console.WriteLine(obj);
                Debugger.Break();
            }

            Console.WriteLine("Press any key to continue...");
            Console.Read();
        }
    }
}
