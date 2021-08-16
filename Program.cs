using System;
using System.Collections.Generic;
using System.Diagnostics;

using Source2Roblox.World;
using Source2Roblox.FileSystem;
using Source2Roblox.Models;
using System.IO;

namespace Source2Roblox
{
    class Program
    {
        private static Dictionary<string, string> argMap = new Dictionary<string, string>();
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
            
            if (gameDir == null)
                return;

            Console.WriteLine($"Loading game mount: {gameDir}...");
            GameMount = new GameMount(gameDir);

            Console.WriteLine("Ready!");

            if (model != null)
            {
                bool running = true;

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
                        Console.WriteLine("Enter a model path:");
                        path = Console.ReadLine();
                    }

                    try
                    {
                        var mdl = new ModelFile(path);
                        string obj = ObjMesher.BakeMDL(mdl);

                        var info = new FileInfo(mdl.Name);
                        string name = info.Name.Replace(info.Extension, "");

                        string export = Path.Combine(@"C:\Users\clone\Desktop", $"{name}.obj");
                        File.WriteAllText(export, obj);

                        Console.WriteLine($"Wrote {export}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"An error occurred: {e.Message}");
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
