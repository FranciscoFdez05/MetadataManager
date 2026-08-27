using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MetadataManager.Services
{
    /// <summary>
    /// Iconos de la barra de herramientas dibujados en tiempo de ejecución: evita
    /// depender de archivos de imagen y se adapta a cualquier escala de pantalla.
    /// </summary>
    public static class Glyphs
    {
        private static readonly Color Accent = Color.FromArgb(37, 99, 235);
        private static readonly Color Neutral = Color.FromArgb(75, 85, 99);
        private static readonly Color Positive = Color.FromArgb(22, 128, 71);
        private static readonly Color Negative = Color.FromArgb(185, 40, 40);
        private static readonly Color Warning = Color.FromArgb(200, 130, 20);

        private static readonly Dictionary<string, Image> Cache = new(StringComparer.Ordinal);

        public static Image AddFile => Get(nameof(AddFile), DrawAddFile);
        public static Image AddFolder => Get(nameof(AddFolder), DrawAddFolder);
        public static Image Remove => Get(nameof(Remove), DrawRemove);
        public static Image Clean => Get(nameof(Clean), DrawClean);
        public static Image Save => Get(nameof(Save), DrawSave);
        public static Image Options => Get(nameof(Options), DrawOptions);
        public static Image Connect => Get(nameof(Connect), DrawConnect);

        private static Image Get(string key, Action<Graphics> draw)
        {
            if (Cache.TryGetValue(key, out Image? cached)) return cached;

            var bitmap = new Bitmap(16, 16);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                draw(g);
            }

            Cache[key] = bitmap;
            return bitmap;
        }

        private static void DrawAddFile(Graphics g)
        {
            using var pen = new Pen(Neutral, 1.4f);
            var page = new[]
            {
                new PointF(3, 1.5f), new PointF(9, 1.5f), new PointF(12, 4.5f),
                new PointF(12, 9), new PointF(3, 9)
            };

            g.DrawPolygon(pen, page);
            g.DrawLine(pen, 9, 1.5f, 9, 4.5f);
            g.DrawLine(pen, 9, 4.5f, 12, 4.5f);

            using var plus = new Pen(Positive, 2f);
            g.DrawLine(plus, 11, 12.5f, 15, 12.5f);
            g.DrawLine(plus, 13, 10.5f, 13, 14.5f);
        }

        private static void DrawAddFolder(Graphics g)
        {
            using var pen = new Pen(Warning, 1.4f);
            using var fill = new SolidBrush(Color.FromArgb(40, Warning));

            var folder = new[]
            {
                new PointF(1.5f, 12.5f), new PointF(1.5f, 3.5f), new PointF(6, 3.5f),
                new PointF(7.5f, 5.5f), new PointF(13.5f, 5.5f), new PointF(13.5f, 12.5f)
            };

            g.FillPolygon(fill, folder);
            g.DrawPolygon(pen, folder);
        }

        private static void DrawRemove(Graphics g)
        {
            using var pen = new Pen(Negative, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, 4, 4, 12, 12);
            g.DrawLine(pen, 12, 4, 4, 12);
        }

        private static void DrawClean(Graphics g)
        {
            using var pen = new Pen(Accent, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var fill = new SolidBrush(Color.FromArgb(60, Accent));

            // Mango de la escoba.
            g.DrawLine(pen, 12.5f, 2.5f, 7.5f, 7.5f);

            // Cepillo.
            var head = new[]
            {
                new PointF(4f, 7f), new PointF(9f, 12f), new PointF(6.5f, 14.5f), new PointF(1.5f, 9.5f)
            };

            g.FillPolygon(fill, head);
            g.DrawPolygon(pen, head);
            g.DrawLine(pen, 2.8f, 8.2f, 7.8f, 13.2f);
        }

        private static void DrawSave(Graphics g)
        {
            using var pen = new Pen(Accent, 1.4f);
            using var fill = new SolidBrush(Color.FromArgb(40, Accent));

            var body = new RectangleF(2.5f, 2.5f, 11, 11);
            g.FillRectangle(fill, body);
            g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);

            g.DrawRectangle(pen, 5.5f, 2.5f, 5, 4);       // pestaña superior
            g.DrawRectangle(pen, 4.5f, 9f, 7, 4.5f);      // etiqueta inferior
        }

        /// <summary>Dos eslabones enlazados: conectar una herramienta externa.</summary>
        private static void DrawConnect(Graphics g)
        {
            using var pen = new Pen(Positive, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

            using var left = new GraphicsPath();
            left.AddArc(1.5f, 4.5f, 7f, 7f, 90, 180);
            g.DrawPath(pen, left);

            using var right = new GraphicsPath();
            right.AddArc(7.5f, 4.5f, 7f, 7f, 270, 180);
            g.DrawPath(pen, right);

            g.DrawLine(pen, 5f, 8f, 11f, 8f);
        }

        private static void DrawOptions(Graphics g)
        {
            using var pen = new Pen(Neutral, 1.5f);

            g.DrawEllipse(pen, 5.5f, 5.5f, 5, 5);

            for (int i = 0; i < 4; i++)
            {
                double angle = Math.PI / 4 + i * Math.PI / 2;
                float dx = (float)Math.Cos(angle);
                float dy = (float)Math.Sin(angle);

                g.DrawLine(pen, 8 + dx * 5.2f, 8 + dy * 5.2f, 8 + dx * 7f, 8 + dy * 7f);
            }

            g.DrawLine(pen, 8, 1.5f, 8, 3.5f);
            g.DrawLine(pen, 8, 12.5f, 8, 14.5f);
            g.DrawLine(pen, 1.5f, 8, 3.5f, 8);
            g.DrawLine(pen, 12.5f, 8, 14.5f, 8);
        }
    }
}
