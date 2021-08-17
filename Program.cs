using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Source2Roblox.FileSystem;
using Source2Roblox.Geometry;
using Source2Roblox.Models;
using Source2Roblox.Textures;
using Source2Roblox.World;

using RobloxFiles;
using RobloxFiles.DataTypes;
using System.Linq;

namespace Source2Roblox
{
    class Program
    {
        private static readonly Dictionary<string, string> argMap = new Dictionary<string, string>();
        public static GameMount GameMount { get; private set; }
        
        public static string GetArg(string argName)
        {
            if (argMap.TryGetValue(argName, out string arg))
                return arg;

            return null;
        }

        public static void BakeRbxm(ModelFile model)
        {
            string modelName = model.Name
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

                var info = new FileInfo(meshBuffer.MaterialPath);
                var name = info.Name.Replace(".vmt", "");

                if (!name.StartsWith(modelName))
                    name = $"{modelName}_{name}";

                var meshPart = new MeshPart()
                {
                    MeshId = $"rbxasset://models/{modelName}/{name}.mesh",
                    BrickColor = BrickColor.Random(),
                    InitialSize = aabb.Size,
                    CFrame = aabb.CFrame,
                    DoubleSided = true,
                    Size = aabb.Size,
                    Name = name,
                };

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

            foreach (var otherPart in parts)
            {
                if (otherPart == largestPart)
                    continue;

                var weld = new WeldConstraint()
                {
                    Name = otherPart.Name,
                    Part0Internal = otherPart,
                    Part1Internal = largestPart,
                    CFrame0 = otherPart.CFrame.ToObjectSpace(largestPart.CFrame)
                };

                weld.Parent = otherPart;
            }

            string exportPath = Path.Combine(exportDir, $"{modelName}.rbxm");
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
                    BakeRbxm(mdl);
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
