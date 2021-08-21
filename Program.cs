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
using Source2Roblox.Util;

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

        public static string CleanPath(string path)
        {
            string cleaned = path
                .ToLowerInvariant()
                .Replace('\\', '/');

            return cleaned;
        }

        public static void BakeRbxm(ModelFile model)
        {
            var modelPath = model.Location;
            var modelInfo = new FileInfo(modelPath);

            var game = model.Game ?? GameMount;
            var gameName = game.GameName;
            
            string modelName = modelInfo.Name
                .Replace(".mdl", "")
                .ToLowerInvariant();

            string localAppData = Environment.GetEnvironmentVariable("localappdata");
            string rootWorkDir = Path.Combine(localAppData, "Roblox Studio", "content", "source", gameName);

            string rbxAssetDir = $"rbxasset://source/{gameName}";
            string modelDir = modelPath.Replace(modelInfo.Name, "");
            string meshDir = Path.Combine(modelDir, modelName);
            
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

                ValveMaterial vmt = null;

                if (GameMount.HasFile(matPath, game))
                {
                    vmt = new ValveMaterial(matPath, game);
                    vmt.SaveVTF(vmt.DiffusePath, rootWorkDir);
                    vmt.SaveVTF(vmt.BumpPath, rootWorkDir, true);
                    vmt.SaveVTF(vmt.IrisPath, rootWorkDir, false);
                }

                string meshWorkDir = Path.Combine(rootWorkDir, meshDir);
                Directory.CreateDirectory(meshWorkDir);

                var meshPart = new MeshPart()
                {
                    MeshId = $"{rbxAssetDir}/{meshDir}/{name}.mesh",
                    InitialSize = aabb.Size,
                    CFrame = aabb.CFrame,
                    DoubleSided = true,
                    Size = aabb.Size,
                    Anchored = true,
                    Name = name,
                };

                string diffusePath = vmt?.DiffusePath;

                if (string.IsNullOrEmpty(diffusePath))
                {
                    meshPart.Color3uint8 = new Color3(1, 1, 1);
                    meshPart.Material = Material.SmoothPlastic;
                }
                else
                {
                    string diffuseRoblox = diffusePath.Replace(".vtf", ".png");
                    meshPart.TextureID = $"{rbxAssetDir}/{diffuseRoblox}";

                    bool noAlpha = vmt.NoAlpha;
                    string bumpPath = vmt.BumpPath;
                    string irisPath = vmt.IrisPath;

                    if (!string.IsNullOrEmpty(bumpPath) || !noAlpha)
                    {
                        var surface = new SurfaceAppearance()
                        {
                            AlphaMode = noAlpha ? AlphaMode.Overlay : AlphaMode.Transparency,
                            ColorMap = meshPart.TextureID,
                        };

                        if (!string.IsNullOrEmpty(bumpPath))
                        {
                            string bumpRoblox = bumpPath.Replace(".vtf", ".png");
                            surface.NormalMap = $"{rbxAssetDir}/{bumpRoblox}";
                        }

                        if (!string.IsNullOrEmpty(irisPath))
                        {
                            string irisRoblox = irisPath.Replace(".vtf", ".png");

                            var eye = new BillboardGui
                            {
                                ExtentsOffsetWorldSpace = new Vector3(0, 0, -1.5f),
                                Size = new UDim2(.06f, 0, .06f, 0),
                                LightInfluence = 1.1f,
                                Adornee = meshPart,
                                Parent = meshPart,
                                Name = "Eye"
                            };

                            var eyeball = new ImageLabel
                            {
                                AnchorPoint = new Vector2(.5f, .5f),
                                Position = new UDim2(.5f, 0, .5f, 0),
                                Size = new UDim2(2f, 0, 2f, 0),
                                Image = meshPart.TextureID,
                                Name = "Eyeball",
                                Parent = eye,
                            };

                            var corner = new UICorner
                            {
                                CornerRadius = new UDim(1),
                                Parent = eyeball
                            };

                            var iris = new ImageLabel
                            {
                                Image = $"{rbxAssetDir}/{irisRoblox}",
                                Position = new UDim2(.5f, 0, .5f, 0),
                                AnchorPoint = new Vector2(.5f, .5f),
                                Size = new UDim2(1.1f, 0, 1.1f, 0),
                                BackgroundTransparency = 1,
                                Name = "Iris",
                                Parent = eye,
                                ZIndex = 2,
                            };

                            meshPart.Transparency = 1;
                        }
                        
                        surface.Parent = meshPart;
                    }
                }

                string meshPath = Path.Combine(meshWorkDir, $"{name}.mesh");

                using (var stream = File.OpenWrite(meshPath))
                {
                    mesh.Save(stream);
                    Console.WriteLine($"\tWrote {meshPath}");
                }

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

            string exportPath = Path.Combine(rootWorkDir, modelDir, $"{modelName}.rbxm");
            exportModel.WorldPivotData = new CFrame();
            exportModel.PrimaryPart = largestPart;

            exportBlob.Save(exportPath);
            Console.WriteLine($"\tWrote {exportPath}");
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

                    try
                    {
                        var mdl = new ModelFile(path);
                        BakeRbxm(mdl);

                        ObjMesher.BakeMDL(mdl, exportDir);
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\n{e}\n\n{e.StackTrace}\n");
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                }
            }

            if (mapName != null)
            {
                string exportDir = Path.Combine(desktop, "SourceMaps", GameMount.GameName);
                Directory.CreateDirectory(exportDir);

                var bsp = new BSPFile($"maps/{mapName}.bsp");
                ObjMesher.BakeBSP(bsp, exportDir);
            }

            Console.WriteLine("Press any key to continue...");
            Console.Read();
        }
    }
}
