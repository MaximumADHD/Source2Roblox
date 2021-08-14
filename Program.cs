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

            GameMount = new GameMount(gameDir);

            if (model != null)
            {
                var mdl = new ModelFile(model);
                string obj = ObjMesher.BakeMDL(mdl);

                var info = new FileInfo(model);
                string name = info.Name;

                string path = Path.Combine(@"C:\Users\clone\Desktop", $"{name}.obj");
                File.WriteAllText(path, obj);

                Debugger.Break();
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
