using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using wowzer.fs.Extensions;

namespace wowzer.fs.Formats
{
    public class BLP
    {
        public enum ColorEncoding
        {
            JPEG = 0,
            Palette = 1,
            DXT = 2,
            ARGB8888 = 3,
            ARGB8888_2 = 4,
        }

        public enum PixelFormat
        {
            DXT1 = 0,
            DXT3 = 1,
            ARGB8888 = 2,
            ARGB1555 = 3,
            ARGB4444 = 4,
            RGB565 = 5,
            A8 = 6,
            DXT5 = 7,
            Unspecified = 8,
            ARGB2565 = 9,
            BC5 = 11,
        }

        private readonly byte[] _pixelData;

        public BLP(Stream dataStream)
        {
            var magic = dataStream.ReadUInt32LE();
            uint alphaSize, formatVersion;
            int width, height;
            bool hasMips;

            ColorEncoding colorEncoding;
            PixelFormat preferredFormat;
            switch (magic)
            {
                case 0x30504C42: // BLP0
                case 0x31504C42: // BLP1
                    {
                        formatVersion = 1;
                        colorEncoding = (ColorEncoding)dataStream.ReadUInt32LE();
                        alphaSize = dataStream.ReadUInt32LE();
                        width = dataStream.ReadInt32LE();
                        height = dataStream.ReadInt32LE();
                        preferredFormat = (PixelFormat)dataStream.ReadUInt32LE();
                        hasMips = dataStream.ReadUInt32LE() != 0;
                        break;
                    }
                case 0x32504C42: // BLP2
                    {
                        formatVersion = dataStream.ReadUInt32LE();
                        colorEncoding = (ColorEncoding)dataStream.ReadUInt8();
                        alphaSize = dataStream.ReadUInt8();
                        preferredFormat = (PixelFormat)dataStream.ReadUInt8();
                        hasMips = dataStream.ReadUInt8() != 0;
                        width = dataStream.ReadInt32LE();
                        height = dataStream.ReadInt32LE();
                        break;
                    }
                default:
                    throw new NotImplementedException();
            }

            var mipOffsets = dataStream.ReadInt32LE(16);
            var mipSizes = dataStream.ReadInt32LE(16);

            var colorSource = default(IColorSource);
            switch (colorEncoding)
            {
                case ColorEncoding.Palette:
                    {
                        colorSource = new PaletteColorSource(dataStream.ReadUInt8(256 * 4));
                        break;
                    }
                case ColorEncoding.JPEG:
                    {
                        var headerSize = dataStream.ReadUInt32LE();
                        var jpegHeader = new byte[headerSize];
                        dataStream.ReadExactly(jpegHeader);

                        // What do we do with this header?
                        break;
                    }
                case ColorEncoding.DXT:
                    colorSource = new DXTColorSource(alphaSize, preferredFormat);
                    break;
                default:
                    break;
            }

            var dataSize = (int) (dataStream.Length - dataStream.Position);
            var dataBuffer = ArrayPool<byte>.Shared.Rent(dataSize);
            dataStream.ReadExactly(dataBuffer);

            // Load the pixels now
            for (var level = 0; level < mipOffsets.Length; ++level)
            {
                if (mipOffsets[level] == 0)
                    continue;

                var scale = (int) Math.Pow(2, level);
                var levelWidth = width / scale;
                var levelHeight = height / scale;

                var levelData = dataBuffer.AsSpan().Slice(mipOffsets[level], mipSizes[level]);
                _pixelData = colorEncoding switch
                {
                    ColorEncoding.Palette or ColorEncoding.DXT
                        => colorSource.GetPixelData(levelWidth, levelHeight, levelData),
                    ColorEncoding.ARGB8888
                        => levelData.ToArray(),
                    _ => [],
                };
            }
        }
    }

    internal interface IColorSource {
        public byte[] GetPixelData(int width, int height, Span<byte> data);
    }

    internal struct DXTColorSource(uint alphaSize, BLP.PixelFormat format) : IColorSource
    {
        private readonly BLP.PixelFormat _pixelFormat = alphaSize > 1
            ? (format == BLP.PixelFormat.DXT5 ? BLP.PixelFormat.DXT5 : BLP.PixelFormat.DXT3)
            : BLP.PixelFormat.DXT1;

        public readonly byte[] GetPixelData(int width, int height, Span<byte> data)
        {
            return [];
        }
    }

    internal record struct PaletteColorSource(byte[] _palette) : IColorSource
    {
        public readonly byte[] GetPixelData(int width, int height, Span<byte> data)
        {
            return [];
        }
    }
}
