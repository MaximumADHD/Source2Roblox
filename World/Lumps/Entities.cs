using System.Collections.Generic;
using System.IO;
using System.Text;

using Source2Roblox.World.Types;

namespace Source2Roblox.World.Lumps
{
    public class Entities : HashSet<Entity>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            byte[] buffer = reader.ReadBytes((int)stream.Length);
            string content = Encoding.UTF8.GetString(buffer);

            using (var entReader = new StringReader(content))
            {
                Entity entity = null;
                string line;

                while ((line = entReader.ReadLine()) != null)
                {
                    if (line == null || line == "\0")
                        break;

                    if (line == "{")
                    {
                        entity = new Entity();
                        continue;
                    }
                    else if (line == "}")
                    {
                        Add(entity);
                        continue;
                    }

                    var keyStart = line.IndexOf('"');
                    var keyEnd = line.IndexOf('"', keyStart + 1);

                    var valueStart = line.IndexOf('"', keyEnd + 1);
                    var valueEnd = line.IndexOf('"', valueStart + 1);

                    string key = line.Substring(keyStart + 1, keyEnd - keyStart - 1);
                    string value = line.Substring(valueStart + 1, valueEnd - valueStart - 1);

                    entity.AddField(key, value);
                }
            }
        }
    }
}
