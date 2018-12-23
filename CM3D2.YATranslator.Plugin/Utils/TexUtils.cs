using System.IO;
using System.Text;
using UnityEngine;

namespace CM3D2.YATranslator.Plugin.Utils
{
    public static class TexUtils
    {
        public static TextureResource ReadTexture(byte[] data, string name)
        {
            var binaryReader = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
            string text = binaryReader.ReadString();
            if (text != "CM3D2_TEX")
                Logger.WriteLine(LogLevel.Warning, $"Texture {name}.tex is not a valid CM3D2 1000 or 1010 texture.");
            int num = binaryReader.ReadInt32();
            string fileName = binaryReader.ReadString();
            int width = 0;
            int height = 0;
            var textureFormat = TextureFormat.ARGB32;
            if (1010 <= num)
            {
                width = binaryReader.ReadInt32();
                height = binaryReader.ReadInt32();
                textureFormat = (TextureFormat) binaryReader.ReadInt32();
            }

            int bufferSize = binaryReader.ReadInt32();
            var pixelData = new byte[bufferSize];
            binaryReader.Read(pixelData, 0, bufferSize);
            if (num == 1000)
            {
                width = (pixelData[16] << 24) | (pixelData[17] << 16) | (pixelData[18] << 8) | pixelData[19];
                height = (pixelData[20] << 24) | (pixelData[21] << 16) | (pixelData[22] << 8) | pixelData[23];
            }

            binaryReader.Close();
            return new TextureResource(width, height, textureFormat, pixelData);
        }
    }
}