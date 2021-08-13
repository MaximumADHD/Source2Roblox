using Source2Roblox.World.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Source2Roblox.World.Lumps
{
    public class TexInfo : List<TextureInfo>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                TextureInfo info = new TextureInfo(reader);
                Add(info);
            }
        }
    }

    public class TexData : List<TextureData>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                TextureData data = new TextureData(reader);
                Add(data);
            }
        }
    }

    public class TexDataStringTable : List<int>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                int offset = reader.ReadInt32();
                Add(offset);
            }
        }
    }

    public class TexDataStringData : Dictionary<int, string>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            var buffer = new List<byte>(128);
            bool reading = true;
            int start = 0;

            while (stream.Position < stream.Length)
            {
                byte next = reader.ReadByte();

                if (next == 0)
                {
                    string result = Encoding.UTF8.GetString(buffer.ToArray());
                    Add(start, result);

                    reading = false;
                    buffer.Clear();
                }
                else
                {
                    if (!reading)
                    {
                        start = (int)stream.Position - 1;
                        reading = true;
                    }

                    buffer.Add(next);
                }
            }

            if (buffer.Count > 0)
            {
                string result = Encoding.UTF8.GetString(buffer.ToArray());
                Add(start, result);
            }
        }
    }
}
