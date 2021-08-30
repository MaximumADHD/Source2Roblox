using RobloxFiles.DataTypes;

namespace Source2Roblox.World.Types
{
    public struct Ambient
    {
        public readonly Color3 Color;
        public readonly int Brightness;

        public Ambient(Color3 color, int brightness)
        {
            Color = color;
            Brightness = brightness;
        }

        public override string ToString()
        {
            return $"{Color} [{Brightness}]";
        }
    }
}
