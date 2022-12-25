using FluentAssertions.Common;
using g3;
using MathNet.Numerics.Distributions;
using UnityEngine;

namespace f3
{
    public static class g3ToUnityConvert
    {
        public static Vector3f ToVector3f(this Vector3 v) => new Vector3f(v.x, v.y, v.z);

        public static Vector3 ToVector3(this Vector3f v) => new Vector3(v.x, v.y, v.z);

        public static Vector3 ToVector3(this Vector3d v) => new Vector3((float)v.x, (float)v.y, (float)v.z);

        public static Color ToColor(this Vector3f v) => new Color(v.x, v.y, v.z, 1.0f);

        public static Vector3f ToVector3f(this Color c) => new Vector3f(c.r, c.g, c.b);

        public static Colorf ToColorf(this Color c) => new Colorf(c.r, c.g, c.b, c.a);

        public static Color ToColor(this Colorf c) => new Color(c.r, c.g, c.b, c.a);

        public static Color32 ToColor32(this Colorf c)
        {
            Colorb cb = c.ToBytes();
            return new Color32(cb.r, cb.g, cb.b, cb.a);
        }

        public static Vector2f ToVector2f(this Vector2 v) => new Vector2f(v.x, v.y);

        public static Vector2 ToVector2(this Vector2f v) => new Vector2(v.x, v.y);

        public static Vector4f ToVector4f(this Vector4 v) => new Vector4f(v.x, v.y, v.z, v.w);

        public static Vector4 ToVector4(this Vector4f v) => new Vector4(v.x, v.y, v.z, v.w);

        public static Quaternionf ToQuaternionf(this Quaternion q) => new Quaternionf(q.x, q.y, q.z, q.w);

        public static Quaternion ToQuaternion(this Quaternionf q) => new Quaternion(q.x, q.y, q.z, q.w);

        public static Ray3f ToRay3f(this Ray r) => new Ray3f(r.origin.ToVector3f(), r.direction.ToVector3f());

        public static Ray ToRay(this Ray3f r) => new Ray(r.Origin.ToVector3(), r.Direction.ToVector3());

        public static AxisAlignedBox2f ToAxisAlignedBox2f(this Rect rect) => new AxisAlignedBox2f(rect.min.ToVector2f(), rect.max.ToVector2f());
        public static Rect ToRect(this AxisAlignedBox2f axisAlignedBox)
        {
            Rect ub = new Rect();
            ub.min = axisAlignedBox.Min.ToVector2();
            ub.max = axisAlignedBox.Max.ToVector2();
            return ub;
        }

        public static AxisAlignedBox3f ToAxisAlignedBox3f(this Bounds bounds) => new AxisAlignedBox3f(bounds.min.ToVector3f(), bounds.max.ToVector3f());
        public static Bounds ToBounds(this AxisAlignedBox3f axisAlignedBox)
        {
            Bounds ub = new Bounds();
            ub.SetMinMax(axisAlignedBox.Min.ToVector3(), axisAlignedBox.Max.ToVector3());
            return ub;
        }
    }
}
