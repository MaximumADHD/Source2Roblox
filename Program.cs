using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Source2Roblox.FileSystem;
using Source2Roblox.Geometry;
using Source2Roblox.Models;
using Source2Roblox.Textures;
using Source2Roblox.World;

namespace Source2Roblox
{
    class Program
    {
        private static readonly Dictionary<string, string> argMap = new Dictionary<string, string>();
        public static GameMount GameMount { get; private set; }

        public const int STUDS_TO_VMF = 12;
        public static bool LOCAL_ONLY = true;
        public static bool NO_PROMPT = false;
        
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
                .Replace('\\', '/')
                .Replace("//", "/");

            return cleaned;
        }

        [STAThread]
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

            string noPrompt = GetArg("-noPrompt");
            string upload   = GetArg("-upload");

            string gameDir = GetArg("-game");
            string model = GetArg("-model");
            string mesh = GetArg("-mesh");

            string mapName = GetArg("-map");
            string vtfName = GetArg("-vtf");

            if (gameDir == null)
                return;

            if (upload != null)
                LOCAL_ONLY = false;

            if (noPrompt != null)
                NO_PROMPT = true;

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            GameMount = new GameMount(gameDir);

            if (mesh != null)
            {
                var load = RobloxMeshFile.Open(mesh);
                
                using (var stream = File.OpenWrite(mesh + "_PATCH"))
                    load.Save(stream);

                var reopen = RobloxMeshFile.Open(mesh + "_PATCH");
                
                var oldBones = load.Bones
                    .Select(bone => bone.CFrame.ToString())
                    .ToList();

                var newBones = reopen.Bones
                    .Select(bone => bone.CFrame.ToString())
                    .ToList();

                Debugger.Break();
            }

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
                    var highRes = file.HighResImage;

                    if (lowRes != null)
                    {
                        string lowResPath = Path.Combine(dir, $"{name}_LOW_RES.png");
                        lowRes.Save(lowResPath);
                    }

                    var normalMap = SSBump.ToNormalMap(highRes);
                    string normalPath = Path.Combine(dir, $"{name}_NORMAL_MAP.png");

                    normalMap.Save(normalPath);
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
                        MeshBuilder.BakeMDL_RBXM(mdl);
                        MeshBuilder.BakeMDL_OBJ(mdl, exportDir);
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
                MeshBuilder.BakeBSP_RBXL(bsp);
            }

            Console.WriteLine("Press any key to continue...");
            Console.Read();
        }
    }
}
