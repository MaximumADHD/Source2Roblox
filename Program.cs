using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Source2Roblox.FileSystem;
using Source2Roblox.Models;
using Source2Roblox.Textures;
using Source2Roblox.World;

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
                    var info = new FileInfo(mdl.Name);

                    string name = info.Name.Replace(info.Extension, "");
                    string exportTo = Path.Combine(exportDir, name);
                        
                    ObjMesher.BakeMDL(mdl, exportTo);
                    Console.WriteLine($"\tWrote files to {exportTo}!");
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
