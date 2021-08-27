using System;
using System.Collections.Generic;
using RobloxFiles.DataTypes;

namespace Source2Roblox.Octree
{
    public class OctreeNode<T> : IDisposable
    {
        public readonly Octree<T> Octree;
        public readonly T Object;

        private OctreeRegion<T> LowRegion = null;
        private Vector3 RawPosition = new Vector3();

        public OctreeNode(Octree<T> octree, T obj)
        {
            Octree = octree;
            Object = obj;
        }

        public Vector3 Position
        {
            get => RawPosition;

            set
            {
                RawPosition = value;

                if (LowRegion?.InRegionBounds(value) ?? false)
                    return;

                var newLowRegion = Octree.GetOrCreateLowestSubRegion(value);

                if (LowRegion != null)
                    LowRegion.MoveNode(newLowRegion, this);
                else
                    newLowRegion.AddNode(this);

                LowRegion = newLowRegion;
            }
        }

        public List<T> RadiusSearch(float radius)
        {
            return Octree.RadiusSearch(Position, radius);
        }

        public void Dispose()
        {
            if (LowRegion != null)
                return;

            LowRegion.RemoveNode(this);
        }
    }
}
