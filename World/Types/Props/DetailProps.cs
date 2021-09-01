using System.Collections.Generic;
using System.Drawing;
using System.IO;

using RobloxFiles.DataTypes;
using Source2Roblox.Util;

namespace Source2Roblox.World.Types
{
    public enum DetailPropOrientation : byte
    {
        NORMAL,
        SCREEN_ALIGNED,
        SCREEN_ALIGNED_VERTICAL
    }

    public enum DetailPropType : byte
    {
        MODEL,
        SPRITE,
        SHAPE_CROSS,
        SHAPE_TRI
    }

    public class DetailModel
    {
        public readonly Vector3 Position;
        public readonly Vector3 Rotation;

        public readonly ushort ModelIndex;
        public readonly ushort LeafIndex;

        public readonly ColorRGBExp32 Lighting;

        public readonly int LightStyles;
        public readonly int NumLightStyles;

        public readonly byte SwayAmount;
        public readonly byte ShapeAngle;
        public readonly byte ShapeSize;

        public readonly DetailPropOrientation Orientation;
        public readonly DetailPropType Type;

        public readonly float Scale;

        public DetailModel(BinaryReader reader)
        {
            Position = reader.ReadVector3();
            Rotation = reader.ReadVector3();

            ModelIndex = reader.ReadUInt16();
            LeafIndex = reader.ReadUInt16();

            var argb = reader.ReadInt32();
            Lighting = Color.FromArgb(argb);

            LightStyles = reader.ReadInt32();
            NumLightStyles = reader.ReadByte();

            SwayAmount = reader.ReadByte();
            ShapeAngle = reader.ReadByte();
            ShapeSize = reader.ReadByte();

            Orientation = (DetailPropOrientation)reader.ReadByte();
            Type = (DetailPropType)reader.ReadByte();

            Scale = reader.ReadSingle();
        }
    }

    public class DetailSprite
    {
        public Vector2 Size;
        public Rect Crop;

        public DetailSprite(BinaryReader reader)
        {
            float x0 = reader.ReadSingle(),
                  y0 = reader.ReadSingle(),
                  x1 = reader.ReadSingle(),
                  y1 = reader.ReadSingle();

            float crop_x0 = reader.ReadSingle(),
                  crop_y0 = reader.ReadSingle(),
                  crop_x1 = reader.ReadSingle(),
                  crop_y1 = reader.ReadSingle();

            float width = x1 - x0,
                  height = y1 - y0;

            Size = new Vector2(width, height);
            Crop = new Rect(crop_x0, crop_y0, crop_x1, crop_y1);
        }
    }


    public class DetailProps
    {
        public List<string> Names = new List<string>();
        public List<DetailModel> DetailModels = new List<DetailModel>();
        public List<DetailSprite> DetailSprites = new List<DetailSprite>();

        public DetailProps(BinaryReader reader)
        {
            int numNames = reader.ReadInt32();

            for (int i = 0; i < numNames; i++)
            {
                string name = reader.ReadString(128);
                Names.Add(name);
            }

            var numSprites = reader.ReadInt32();

            for (int i = 0; i < numSprites; i++)
            {
                var sprite = new DetailSprite(reader);
                DetailSprites.Add(sprite);
            }

            var numDetailModels = reader.ReadInt32();

            for (int i = 0; i < numDetailModels; i++)
            {
                var model = new DetailModel(reader);
                DetailModels.Add(model);
            }
        }
    }
}
