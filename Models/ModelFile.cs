using Source2Roblox.FileSystem;
using System.IO;

namespace Source2Roblox.Models
{
    public class ModelFile
    {
        public readonly StudioHDR Header;
        public readonly VertexData VertexData;
        public readonly TriangleData TriangleData;

        public string Name => Header.Name;
        public StudioHDRFlags Flags => Header.Flags;
        public override string ToString() => Name;
        
        public ModelFile(string path, GameMount game = null)
        {
            if (!path.StartsWith("models/"))
                path = $"models/{path}";

            if (!path.EndsWith(".mdl"))
                path += ".mdl";

            using (var mdlStream = GameMount.OpenRead(path, game))
            using (var mdlReader = new BinaryReader(mdlStream))
            {
                Header = new StudioHDR(mdlReader);
                // TODO: Read data described by header offsets.
            }

            string vvd = path.Replace(".mdl", ".vvd");
            VertexData = new VertexData(vvd, game);

            string vtx = path.Replace(".mdl", ".dx90.vtx");
            TriangleData = new TriangleData(vtx, game);
        }
    }
}
