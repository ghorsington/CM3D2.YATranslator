#region License

//	Copyright (c) 2012 Calvin Rien
//        http://the.darktable.com
//
//	This software is provided 'as-is', without any express or implied warranty. In
//	no event will the authors be held liable for any damages arising from the use
//	of this software.
//
//	Permission is granted to anyone to use this software for any purpose,
//	including commercial applications, and to alter it and redistribute it freely,
//	subject to the following restrictions:
//
//	1. The origin of this software must not be misrepresented; you must not claim
//	that you wrote the original software. If you use this software in a product,
//	an acknowledgment in the product documentation would be appreciated but is not
//	required.
//
//	2. Altered source versions must be plainly marked as such, and must not be
//	misrepresented as being the original software.
//
//	3. This notice may not be removed or altered from any source distribution.
//
//  =============================================================================
//
//  derived from Gregorio Zanon's script
//  http://forum.unity3d.com/threads/119295-Writing-AudioListener.GetOutputData-to-wav-problem?p=806734&viewfull=1#post806734
//  Originally from https://gist.github.com/darktable/2317063
//  Modified for use in YATranslator (https://github.com/denikson/CM3D2.YATranslator)

#endregion

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CM3D2.YATranslator.Plugin.Utils
{
    public static class WavUtils
    {
        private const int HEADER_SIZE = 44;

        public static byte[] ToWavData(this AudioClip clip)
        {
            using (var memoryStream = CreateEmpty())
            {
                ConvertAndWrite(memoryStream, clip);

                WriteHeader(memoryStream, clip);

                return memoryStream.ToArray();
            }
        }

        private static MemoryStream CreateEmpty()
        {
            var memoryStream = new MemoryStream();
            const byte emptyByte = new byte();

            for (int i = 0; i < HEADER_SIZE; i++) //preparing the header
                memoryStream.WriteByte(emptyByte);

            return memoryStream;
        }

        private static void ConvertAndWrite(Stream fileStream, AudioClip clip)
        {
            var samples = new float[clip.samples];

            clip.GetData(samples, 0);

            var intData = new short[samples.Length];
            //converting in 2 float[] steps to Int16[], //then Int16[] to Byte[]

            var bytesData = new byte[samples.Length * 2];
            //bytesData array is twice the size of
            //dataSource array because a float converted in Int16 is 2 bytes.

            const int rescaleFactor = 32767; //to convert float to Int16

            for (int i = 0; i < samples.Length; i++)
            {
                intData[i] = (short) (samples[i] * rescaleFactor);
                var byteArr = BitConverter.GetBytes(intData[i]);
                byteArr.CopyTo(bytesData, i * 2);
            }

            fileStream.Write(bytesData, 0, bytesData.Length);
        }

        private static void WriteHeader(Stream fileStream, AudioClip clip)
        {
            int hz = clip.frequency;
            int channels = clip.channels;
            int samples = clip.samples;

            fileStream.Seek(0, SeekOrigin.Begin);

            var riff = Encoding.UTF8.GetBytes("RIFF");
            fileStream.Write(riff, 0, 4);

            var chunkSize = BitConverter.GetBytes(fileStream.Length - 8);
            fileStream.Write(chunkSize, 0, 4);

            var wave = Encoding.UTF8.GetBytes("WAVE");
            fileStream.Write(wave, 0, 4);

            var fmt = Encoding.UTF8.GetBytes("fmt ");
            fileStream.Write(fmt, 0, 4);

            var subChunk1 = BitConverter.GetBytes(16);
            fileStream.Write(subChunk1, 0, 4);

            var audioFormat = BitConverter.GetBytes(1);
            fileStream.Write(audioFormat, 0, 2);

            var numChannels = BitConverter.GetBytes(channels);
            fileStream.Write(numChannels, 0, 2);

            var sampleRate = BitConverter.GetBytes(hz);
            fileStream.Write(sampleRate, 0, 4);

            var byteRate = BitConverter.GetBytes(hz * channels * 2); // sampleRate * bytesPerSample*number of channels, here 44100*2*2
            fileStream.Write(byteRate, 0, 4);

            ushort blockAlign = (ushort) (channels * 2);
            fileStream.Write(BitConverter.GetBytes(blockAlign), 0, 2);

            const ushort bps = 16;
            var bitsPerSample = BitConverter.GetBytes(bps);
            fileStream.Write(bitsPerSample, 0, 2);

            var datastring = Encoding.UTF8.GetBytes("data");
            fileStream.Write(datastring, 0, 4);

            var subChunk2 = BitConverter.GetBytes(samples * channels * 2);
            fileStream.Write(subChunk2, 0, 4);

            //		fileStream.Close();
        }
    }
}