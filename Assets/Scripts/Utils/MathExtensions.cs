using Unity.Mathematics;
using Unity.Mathematics.Geometry;

namespace Utils
{
    public static class MathExtensions
    {
        public static bool RaycastAABB(this MinMaxAABB aabb, float3 start, float3 end, out float3 entryPoint)
        {
            entryPoint = float3.zero;
            var dir = end - start;
            var length = math.length(dir);
            if (length < 1e-5f) return false;
    
            var invDir = math.rcp(dir);
            var t0 = (aabb.Min - start) * invDir;
            var t1 = (aabb.Max - start) * invDir;
    
            var tmin = math.min(t0, t1);
            var tmax = math.max(t0, t1);
    
            var tenter = math.cmax(tmin);
            var texit = math.cmin(tmax);
    
            var intersects = tenter < texit && texit > 0 && tenter < length;
            if (!intersects) return false;
        
            var intersectPoint = start + tenter * dir;
            if (tenter < 0)
            {
                intersectPoint = start + texit * dir;
            }
        
            if (math.length(intersectPoint - start) <= length)
            {
                entryPoint = intersectPoint;
                return true;
            }

            return false;
        }
    }
}