namespace Source2Roblox.World.Types
{
    public struct Edge
    {
        public ushort Index0;
        public ushort Index1;

        public override string ToString()
        {
            return $"{{{Index0}, {Index1}}}";
        }
    }
}
