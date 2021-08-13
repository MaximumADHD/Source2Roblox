using System;
using System.IO;

using SevenZip;
using SevenZip.Compression.LZMA;

namespace Source2Roblox
{
    public static class LZMA
    {
        private const int dictionary = 1 << 23;
        private const bool eos = false;

        private static readonly CoderPropID[] propIDs =
        {
            CoderPropID.DictionarySize,
            CoderPropID.PosStateBits,
            CoderPropID.LitContextBits,
            CoderPropID.LitPosBits,
            CoderPropID.Algorithm,
            CoderPropID.NumFastBytes,
            CoderPropID.MatchFinder,
            CoderPropID.EndMarker
        };

        // these are the default properties, keeping it simple for now:
        private static readonly object[] properties =
        {
            dictionary,
            2,
            3,
            0,
            2,
            128,
            "bt4",
            eos
        };

        public static byte[] Compress(byte[] inputBytes)
        {
            using (var outStream = new MemoryStream())
            {
                var encoder = new Encoder();
                encoder.SetCoderProperties(propIDs, properties);
                encoder.WriteCoderProperties(outStream);

                using (var inStream = new MemoryStream(inputBytes))
                {
                    long fileSize = inStream.Length;

                    for (int i = 0; i < 8; i++)
                        outStream.WriteByte((byte)(fileSize >> (8 * i)));

                    encoder.Code(inStream, outStream, -1, -1, null);
                }
                
                return outStream.ToArray();
            }
        }

        public static byte[] Decompress(byte[] inputBytes, byte[] props = null, long outSize = -1)
        {
            using (var newInStream = new MemoryStream(inputBytes))
            {
                var decoder = new Decoder();
                newInStream.Seek(0, 0);

                if (props == null)
                {
                    props = new byte[5];

                    if (newInStream.Read(props, 0, 5) != 5)
                    {
                        var ex = new Exception("input .lzma is too short");
                        throw ex;
                    }
                }

                if (outSize < 0)
                {
                    outSize = 0;

                    for (int i = 0; i < 8; i++)
                    {
                        int v = newInStream.ReadByte();

                        if (v < 0)
                            throw new Exception("Can't Read 1");

                        outSize |= ((long)(byte)v) << (8 * i);
                    }
                }

                long compressedSize = newInStream.Length - newInStream.Position;
                decoder.SetDecoderProperties(props);

                using (var newOutStream = new MemoryStream())
                {
                    decoder.Code(newInStream, newOutStream, compressedSize, outSize, null);
                    return newOutStream.ToArray();
                }
            }
        }
    }
}