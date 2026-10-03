using UnityEngine;

namespace WaveSurvival.Tools.SpriteLibraryBuilder
{
    public static class SpriteOverlayMath
    {
        public static Rect FitTextureRect(float textureWidth, float textureHeight, Rect area)
        {
            float scale = Mathf.Min(area.width / textureWidth, area.height / textureHeight);
            float w = textureWidth * scale;
            float h = textureHeight * scale;
            return new Rect(
                area.x + (area.width - w) * 0.5f,
                area.y + (area.height - h) * 0.5f,
                w, h);
        }

        public static Rect SpriteToScreenRect(Rect spriteRect, float texWidth, float texHeight, Rect texDrawRect)
        {
            float scaleX = texDrawRect.width / texWidth;
            float scaleY = texDrawRect.height / texHeight;
            float x = texDrawRect.x + spriteRect.x * scaleX;
            float y = texDrawRect.y + (texHeight - spriteRect.y - spriteRect.height) * scaleY;
            return new Rect(x, y, spriteRect.width * scaleX, spriteRect.height * scaleY);
        }

        public static Vector2 ScreenToTexturePoint(Vector2 screenPoint, Rect texDrawRect, float texWidth, float texHeight)
        {
            float u = (screenPoint.x - texDrawRect.x) / texDrawRect.width * texWidth;
            float v = (1f - (screenPoint.y - texDrawRect.y) / texDrawRect.height) * texHeight;
            return new Vector2(u, v);
        }

        public static int HitTestRect(Vector2 point, Rect[] rects)
        {
            for (int i = 0; i < rects.Length; i++)
                if (rects[i].Contains(point)) return i;
            return -1;
        }
    }
}