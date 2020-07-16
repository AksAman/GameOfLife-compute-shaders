using UnityEngine;

namespace AksAman.Extensions
{
    public static class AllExtensions
    {
        /// <summary>
        /// Extension method to convert Texture to external Texture2D
        /// <see cref="https://forum.unity.com/threads/converting-texture-to-texture2d.25991/#post-4073560"/>
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        public static Texture2D T2Texture2D(this Texture texture)
        {
            return Texture2D.CreateExternalTexture(
                texture.width,
                texture.height,
                TextureFormat.RGBA32,
                false, false,
                texture.GetNativeTexturePtr());
        }

        /// <summary>
        /// Extension method to convert rendertexture to Texture2D
        /// </summary>
        /// <param name="rTex">RenderTexture to be converted</param>
        /// <returns>Converted texture2D</returns>
        public static Texture2D RT2Texture2D(this RenderTexture rTex)
        {
            var sourceTex = new Texture2D(rTex.width, rTex.height);
            RenderTexture.active = rTex;
            sourceTex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            sourceTex.Apply();
            RenderTexture.active = null;
            rTex.Release();
            return sourceTex;
        }

        /// <summary>
        /// Extension method to delete all the childs of a transform
        /// </summary>
        /// <param name="transform">Parent transform</param>
        /// <param name="destroyImmediate">Should destroy immediately or not</param>
        public static void ClearTransformChildren(this Transform transform, bool destroyImmediate = false)
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (destroyImmediate)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                else
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }
    }
}
