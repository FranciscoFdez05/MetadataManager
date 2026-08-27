using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MetadataManager.Services
{
    /// <summary>
    /// Eliminación de metadatos en JPEG y PNG sin recomprimir: se copian los datos de imagen
    /// tal cual y se descartan únicamente los bloques que transportan información añadida
    /// (EXIF, XMP, IPTC, comentarios, perfiles y marcas de tiempo).
    /// </summary>
    public static class LosslessImageStripper
    {
        private const int JpegSoi = 0xD8;
        private const int JpegEoi = 0xD9;
        private const int JpegSos = 0xDA;

        // APP0 mínimo (JFIF) que se vuelve a insertar para que cualquier decodificador
        // encuentre una cabecera estándar tras eliminar los originales.
        private static readonly byte[] MinimalJfif =
        {
            0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0x00,
            0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00
        };

        // Bloques PNG que afectan a cómo se ve la imagen y por tanto se conservan.
        private static readonly HashSet<string> PngChunksToKeep = new(StringComparer.Ordinal)
        {
            "IHDR", "PLTE", "IDAT", "IEND", "tRNS", "gAMA", "cHRM", "sRGB", "sBIT", "bKGD", "pHYs", "hIST", "sPLT"
        };

        /// <summary>
        /// Devuelve los bytes del fichero sin metadatos, o null si el contenido no es
        /// un JPEG/PNG reconocible (el llamante debe recurrir entonces a otra estrategia).
        /// </summary>
        /// <param name="orientation">
        /// Orientación EXIF original (1-8). Si es distinta de 1 se reinserta un bloque EXIF
        /// mínimo con esa única etiqueta, para que la imagen siga viéndose derecha.
        /// </param>
        public static byte[]? Strip(byte[] source, int orientation = 1)
        {
            if (IsJpeg(source)) return StripJpeg(source, orientation);
            if (IsPng(source)) return StripPng(source);
            return null;
        }

        public static bool IsJpeg(byte[] data) => data.Length > 3 && data[0] == 0xFF && data[1] == JpegSoi;

        public static bool IsPng(byte[] data) =>
            data.Length > 8 && data[0] == 0x89 && data[1] == (byte)'P' && data[2] == (byte)'N' && data[3] == (byte)'G';

        /// <summary>
        /// Bloque APP1/EXIF mínimo: cabecera TIFF little-endian con un IFD0 que contiene
        /// únicamente la etiqueta 0x0112 (Orientation).
        /// </summary>
        internal static byte[] BuildOrientationSegment(int orientation)
        {
            byte value = (byte)Math.Clamp(orientation, 1, 8);

            return new byte[]
            {
                0xFF, 0xE1, 0x00, 0x22,                          // APP1, longitud 34
                (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
                (byte)'I', (byte)'I', 0x2A, 0x00,                // TIFF little-endian
                0x08, 0x00, 0x00, 0x00,                          // desplazamiento del IFD0
                0x01, 0x00,                                      // una entrada
                0x12, 0x01,                                      // etiqueta 0x0112 (Orientation)
                0x03, 0x00,                                      // tipo SHORT
                0x01, 0x00, 0x00, 0x00,                          // un valor
                value, 0x00, 0x00, 0x00,                         // valor + relleno
                0x00, 0x00, 0x00, 0x00                           // no hay IFD siguiente
            };
        }

        private static byte[]? StripJpeg(byte[] data, int orientation)
        {
            using var output = new MemoryStream(data.Length);
            output.WriteByte(0xFF);
            output.WriteByte(JpegSoi);
            output.Write(MinimalJfif, 0, MinimalJfif.Length);

            if (orientation > 1)
            {
                byte[] segment = BuildOrientationSegment(orientation);
                output.Write(segment, 0, segment.Length);
            }

            int position = 2;

            while (position + 1 < data.Length)
            {
                if (data[position] != 0xFF)
                {
                    // Fichero mal formado: no se toca.
                    return null;
                }

                // Puede haber bytes de relleno 0xFF antes del identificador del marcador.
                int markerIndex = position + 1;
                while (markerIndex < data.Length && data[markerIndex] == 0xFF) markerIndex++;
                if (markerIndex >= data.Length) return null;

                byte marker = data[markerIndex];

                if (marker == JpegEoi)
                {
                    output.Write(data, markerIndex - 1, data.Length - (markerIndex - 1));
                    return output.ToArray();
                }

                if (marker == JpegSos)
                {
                    // A partir de aquí vienen los datos comprimidos: se copian sin analizar.
                    output.Write(data, markerIndex - 1, data.Length - (markerIndex - 1));
                    return output.ToArray();
                }

                int lengthIndex = markerIndex + 1;
                if (lengthIndex + 1 >= data.Length) return null;

                int segmentLength = (data[lengthIndex] << 8) | data[lengthIndex + 1];
                if (segmentLength < 2 || lengthIndex + segmentLength > data.Length) return null;

                bool isApplicationSegment = marker >= 0xE0 && marker <= 0xEF;
                bool isComment = marker == 0xFE;

                if (!isApplicationSegment && !isComment)
                {
                    output.WriteByte(0xFF);
                    output.WriteByte(marker);
                    output.Write(data, lengthIndex, segmentLength);
                }

                position = lengthIndex + segmentLength;
            }

            return null;
        }

        private static byte[]? StripPng(byte[] data)
        {
            using var output = new MemoryStream(data.Length);
            output.Write(data, 0, 8);

            int position = 8;

            while (position + 8 <= data.Length)
            {
                int length = ReadBigEndianInt32(data, position);
                if (length < 0 || position + 12L + length > data.Length) return null;

                string type = Encoding.ASCII.GetString(data, position + 4, 4);
                int totalLength = length + 12;

                if (PngChunksToKeep.Contains(type))
                {
                    output.Write(data, position, totalLength);
                }

                position += totalLength;

                if (type == "IEND") return output.ToArray();
            }

            return null;
        }

        private static int ReadBigEndianInt32(byte[] data, int offset) =>
            (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
    }
}
