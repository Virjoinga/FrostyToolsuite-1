using System.Numerics;

namespace Frosty.Core.Extensions;

public static class Matrix4x4Extensions
{
    extension(Matrix4x4 extended)
    {
        public Vector3 Backward
        {
            get
            {
                Vector3 vector3;
                vector3.X = extended.M31;
                vector3.Y = extended.M32;
                vector3.Z = extended.M33;
                return vector3;
            }
            set
            {
                extended.M31 = value.X;
                extended.M32 = value.Y;
                extended.M33 = value.Z;
            }
        }

        public Vector3 Forward
        {
            get
            {
                Vector3 vector3;
                vector3.X = -extended.M31;
                vector3.Y = -extended.M32;
                vector3.Z = -extended.M33;
                return vector3;
            }
            set
            {
                extended.M31 = -value.X;
                extended.M32 = -value.Y;
                extended.M33 = -value.Z;
            }
        }

        public Vector3 Left
        {
            get
            {
                Vector3 vector3;
                vector3.X = -extended.M11;
                vector3.Y = -extended.M12;
                vector3.Z = -extended.M13;
                return vector3;
            }
            set
            {
                extended.M11 = -value.X;
                extended.M12 = -value.Y;
                extended.M13 = -value.Z;
            }
        }

        public Vector3 Right
        {
            get
            {
                Vector3 vector3;
                vector3.X = extended.M11;
                vector3.Y = extended.M12;
                vector3.Z = extended.M13;
                return vector3;
            }
            set
            {
                extended.M11 = value.X;
                extended.M12 = value.Y;
                extended.M13 = value.Z;
            }
        }

        public Vector3 Scale
        {
            get
            {
                return new Vector3(extended.M11, extended.M22, extended.M33);
            }
            set
            {
                extended.M11 = value.X;
                extended.M22 = value.Y;
                extended.M33 = value.Z;
            }
        }

        public Vector3 Up
        {
            get
            {
                Vector3 vector3;
                vector3.X = extended.M21;
                vector3.Y = extended.M22;
                vector3.Z = extended.M23;
                return vector3;
            }
            set
            {
                extended.M21 = value.X;
                extended.M22 = value.Y;
                extended.M23 = value.Z;
            }
        }
    }
}
