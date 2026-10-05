using System.Numerics;

namespace Frosty.Core.Extensions;

public static class Vector3Extensions
{
    extension(Vector3)
    {
        /* The following implementations are based on SharpDX's `TransformCoordinate` implementation:
         * https://github.com/sharpdx/SharpDX/blob/master/Source/SharpDX.Mathematics/Vector3.cs
         */
        public static void TransformCoordinate(ref Vector3 coordinate, ref Matrix4x4 transform, out Vector3 result)
        {
            Vector4 vector = new();
            vector.X = (coordinate.X * transform.M11) + (coordinate.Y * transform.M21) + (coordinate.Z * transform.M31) + transform.M41;
            vector.Y = (coordinate.X * transform.M12) + (coordinate.Y * transform.M22) + (coordinate.Z * transform.M32) + transform.M42;
            vector.Z = (coordinate.X * transform.M13) + (coordinate.Y * transform.M23) + (coordinate.Z * transform.M33) + transform.M43;
            vector.W = 1f / ((coordinate.X * transform.M14) + (coordinate.Y * transform.M24) + (coordinate.Z * transform.M34) + transform.M44);

            result = new Vector3(vector.X * vector.W, vector.Y * vector.W, vector.Z * vector.W);
        }

        public static Vector3 TransformCoordinate(Vector3 coordinate, Matrix4x4 transform)
        {
            TransformCoordinate(ref coordinate, ref transform, out Vector3 result);
            return result;
        }
    }
}
