using System;
using Sandbox.Graphics;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace SirHolomap
{
    // Drawing in screen pixels, over the game's image: sprites, lines, text.
    // The map's interface is drawn after the 3D scene, like the game's own
    // menus, so it stays sharp.
    internal static class Gfx
    {
        public const string Font = "White";

        public static float Width = 1920;
        public static float Height = 1080;

        // 1 at 1080 pixels high: the interface keeps its proportions.
        public static float Scale = 1;

        public static void BeginFrame()
        {
            var screen = MyGuiManager.GetFullscreenRectangle();
            Width = Math.Max(screen.Width, 1);
            Height = Math.Max(screen.Height, 1);
            Scale = Height / 1080f;
        }

        public static Vector2 Mouse
        {
            get { return MyGuiManager.GetScreenCoordinateFromNormalizedCoordinate(MyGuiManager.MouseCursorPosition); }
        }

        public static Color Alpha(Color color, float alpha)
        {
            var a = MathHelper.Clamp(alpha, 0, 1) * color.A / 255f;
            return new Color(color.R, color.G, color.B, (byte)(a * 255));
        }

        public static void Sprite(string texture, float cx, float cy, float w, float h, Color color)
        {
            var dest = new RectangleF(cx - w / 2, cy - h / 2, w, h);
            MyRenderProxy.DrawSprite(texture, ref dest, null, color, 0f, true, true);
        }

        public static void Sprite(string texture, Vector2 centre, float size, Color color)
        {
            Sprite(texture, centre.X, centre.Y, size, size, color);
        }

        public static void Rect(float x, float y, float w, float h, Color color)
        {
            var dest = new RectangleF(x, y, w, h);
            MyRenderProxy.DrawSprite(GameTextures.White, ref dest, null, color, 0f, true, true);
        }

        public static void Frame(float x, float y, float w, float h, float thickness, Color color)
        {
            Rect(x, y, w, thickness, color);
            Rect(x, y + h - thickness, w, thickness, color);
            Rect(x, y, thickness, h, color);
            Rect(x + w - thickness, y, thickness, h, color);
        }

        // A sprite turned along a direction: lines and arrows.
        public static void Line(Vector2 a, Vector2 b, float thickness, Color color)
        {
            Line(a, b, thickness, color, GameTextures.White);
        }

        public static void Line(Vector2 a, Vector2 b, float thickness, Color color, string texture)
        {
            var delta = b - a;
            var length = delta.Length();
            if (length < 0.5f || !IsFinite(a) || !IsFinite(b))
                return;
            var right = delta / length;
            var origin = a;
            var dest = new RectangleF(a.X, a.Y - thickness / 2, length, thickness);
            MyRenderProxy.DrawSpriteExt(texture, ref dest, null, color, ref right, ref origin, true, true);
        }

        public static void Rotated(string texture, Vector2 centre, float size, Vector2 direction, Color color)
        {
            if (direction.LengthSquared() < 1e-6f)
                direction = Vector2.UnitX;
            direction.Normalize();
            var origin = centre;
            var dest = new RectangleF(centre.X - size / 2, centre.Y - size / 2, size, size);
            MyRenderProxy.DrawSpriteExt(texture, ref dest, null, color, ref direction, ref origin, true, true);
        }

        public static void DashedLine(Vector2 a, Vector2 b, float thickness, float dash, Color color)
        {
            var delta = b - a;
            var length = delta.Length();
            if (length < 1 || dash < 1)
                return;
            var step = delta / length;
            for (var d = 0f; d < length; d += dash * 2)
                Line(a + step * d, a + step * Math.Min(d + dash, length), thickness, color);
        }

        public static bool IsFinite(Vector2 v)
        {
            return !float.IsNaN(v.X) && !float.IsNaN(v.Y) && !float.IsInfinity(v.X) && !float.IsInfinity(v.Y)
                && Math.Abs(v.X) < 1e6f && Math.Abs(v.Y) < 1e6f;
        }

        public static float TextScale(float size)
        {
            return size;
        }

        public static void Text(string text, float x, float y, float scale, Color color,
            MyGuiDrawAlignEnum align = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var at = MyGuiManager.GetNormalizedCoordinateFromScreenCoordinate(new Vector2(x, y));
            MyGuiManager.DrawString(Font, text, at, scale, color, align, false, float.PositiveInfinity, true);
        }

        public static Vector2 Measure(string text, float scale)
        {
            if (string.IsNullOrEmpty(text))
                return Vector2.Zero;
            return MyGuiManager.GetScreenSizeFromNormalizedSize(MyGuiManager.MeasureString(Font, text, scale));
        }

        // The text, cut with "..." to fit a width in pixels.
        public static string Fit(string text, float scale, float width)
        {
            if (string.IsNullOrEmpty(text) || Measure(text, scale).X <= width)
                return text;
            // The longest start that fits, found by halving.
            var low = 0;
            var high = text.Length - 1;
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                if (Measure(text.Substring(0, middle) + "...", scale).X <= width)
                    low = middle;
                else
                    high = middle - 1;
            }
            return text.Substring(0, Math.Max(low, 1)) + "...";
        }
    }
}
