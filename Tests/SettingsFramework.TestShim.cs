namespace UnityEngine
{
    public struct Rect
    {
    }

    public struct Color
    {
    }

    internal static class Mathf
    {
        internal static float Min(float left, float right)
        {
            return left < right ? left : right;
        }

        internal static float Max(float left, float right)
        {
            return left > right ? left : right;
        }

        internal static int RoundToInt(float value)
        {
            return (int)System.Math.Round(value);
        }
    }
}
