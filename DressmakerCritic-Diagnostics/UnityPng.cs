using System;
using System.Reflection;
using UnityEngine;

namespace DressmakerCritic.Diagnostics
{
    // Runtime bridge to UnityEngine.ImageConversion without a compile-time module ref
    // (that DLL targets netstandard 2.1 and breaks net472 builds under modern SDKs).
    internal static class UnityPng
    {
        private static readonly MethodInfo loadImage;
        private static readonly MethodInfo encodeToPng;

        static UnityPng()
        {
            Type type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "UnityEngine.ImageConversionModule") continue;
                type = asm.GetType("UnityEngine.ImageConversion");
                break;
            }
            if (type == null)
                type = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            loadImage = type?.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);
            encodeToPng = type?.GetMethod("EncodeToPNG", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Texture2D) }, null);
        }

        public static bool LoadImage(Texture2D texture, byte[] data, bool markNonReadable = true)
        {
            if (loadImage == null || texture == null || data == null) return false;
            return (bool)loadImage.Invoke(null, new object[] { texture, data, markNonReadable });
        }

        public static byte[] EncodeToPNG(Texture2D texture)
        {
            if (encodeToPng == null || texture == null) return null;
            return (byte[])encodeToPng.Invoke(null, new object[] { texture });
        }
    }
}
