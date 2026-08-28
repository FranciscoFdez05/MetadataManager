using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace MetadataManager.Services
{
    /// <summary>
    /// Miniaturas del propio Explorador de Windows (IShellItemImageFactory).
    /// Sirve para formatos que GDI+ no sabe abrir (PDF, Office, vídeo, HEIC…) y,
    /// como último recurso, devuelve el icono asociado al tipo de archivo.
    /// </summary>
    internal static class ShellThumbnail
    {
        /// <summary>Devuelve solo la miniatura real del contenido; falla si no hay controlador.</summary>
        private const int ThumbnailOnly = 0x08;

        /// <summary>Devuelve el icono del tipo de archivo, sin leer el contenido.</summary>
        private const int IconOnly = 0x04;

        /// <summary>Escala la imagen para que quepa en el tamaño pedido.</summary>
        private const int ResizeToFit = 0x00;

        /// <summary>Los controladores de miniaturas son código de terceros: no se les espera indefinidamente.</summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

        /// <summary>Miniatura generada a partir del contenido del archivo.</summary>
        public static Bitmap? TryGetThumbnail(string path, int size) => Run(path, size, ThumbnailOnly);

        /// <summary>Icono asociado al tipo de archivo; no depende del contenido.</summary>
        public static Bitmap? TryGetIcon(string path, int size) => Run(path, size, IconOnly);

        /// <summary>
        /// El shell exige apartamento STA y los hilos del pool son MTA,
        /// así que la llamada se hace en un hilo propio y con tiempo límite.
        /// </summary>
        private static Bitmap? Run(string path, int size, int flags)
        {
            Bitmap? result = null;

            var thread = new Thread(() => result = Extract(path, size, flags))
            {
                IsBackground = true,
                Name = "ShellThumbnail"
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return thread.Join(Timeout) ? result : null;
        }

        private static Bitmap? Extract(string path, int size, int flags)
        {
            IShellItemImageFactory? factory = null;
            IntPtr handle = IntPtr.Zero;

            try
            {
                Guid interfaceId = typeof(IShellItemImageFactory).GUID;

                if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref interfaceId, out factory) != 0 || factory is null)
                {
                    return null;
                }

                if (factory.GetImage(new SIZE(size, size), flags | ResizeToFit, out handle) != 0 || handle == IntPtr.Zero)
                {
                    return null;
                }

                return FromHandle(handle);
            }
            catch (Exception ex) when (ex is COMException or ArgumentException or ExternalException)
            {
                return null;
            }
            finally
            {
                if (handle != IntPtr.Zero) DeleteObject(handle);
                if (factory is not null) Marshal.ReleaseComObject(factory);
            }
        }

        /// <summary>
        /// Copia el HBITMAP a un mapa de bits conservando la transparencia:
        /// Image.FromHbitmap la pierde y deja fondos negros en los iconos.
        /// </summary>
        private static Bitmap FromHandle(IntPtr handle)
        {
            var section = default(DIBSECTION);
            int expected = Marshal.SizeOf<DIBSECTION>();

            if (GetObject(handle, expected, ref section) != expected ||
                section.dsBmih.biBitCount != 32 ||
                section.dsBm.bmBits == IntPtr.Zero)
            {
                return Image.FromHbitmap(handle);
            }

            int width = section.dsBmih.biWidth;
            int height = Math.Abs(section.dsBmih.biHeight);
            int stride = section.dsBm.bmWidthBytes;

            // Un alto negativo indica que las filas ya vienen ordenadas de arriba abajo.
            bool topDown = section.dsBmih.biHeight < 0;

            byte[] pixels = new byte[stride * height];
            bool hasAlpha = false;

            for (int row = 0; row < height; row++)
            {
                int source = topDown ? row : height - 1 - row;
                Marshal.Copy(section.dsBm.bmBits + (source * stride), pixels, row * stride, stride);
            }

            for (int index = 3; index < pixels.Length && !hasAlpha; index += 4)
            {
                hasAlpha = pixels[index] != 0;
            }

            // Algunos controladores devuelven 32 bits con el canal alfa a cero: sin esto la imagen saldría vacía.
            if (!hasAlpha)
            {
                for (int index = 3; index < pixels.Length; index += 4) pixels[index] = 255;
            }

            // El shell entrega el color ya multiplicado por el alfa.
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);

            try
            {
                int copy = Math.Min(stride, data.Stride);
                for (int row = 0; row < height; row++)
                {
                    Marshal.Copy(pixels, row * stride, data.Scan0 + (row * data.Stride), copy);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(
            string path,
            IntPtr bindingContext,
            ref Guid interfaceId,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? factory);

        [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
        private static extern int GetObject(IntPtr handle, int size, ref DIBSECTION target);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr handle);

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, int flags, out IntPtr bitmap);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int Width;
            public int Height;

            public SIZE(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DIBSECTION
        {
            public BITMAP dsBm;
            public BITMAPINFOHEADER dsBmih;
            public uint dsBitfield0;
            public uint dsBitfield1;
            public uint dsBitfield2;
            public IntPtr dshSection;
            public uint dsOffset;
        }
    }
}
