using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MetadataManager.UI
{
    /// <summary>Iconos disponibles para la barra de herramientas.</summary>
    public enum Glyph
    {
        AddFile,
        AddFolder,
        Remove,
        Clean,
        Save,
        Connect,
        Options
    }

    /// <summary>
    /// Iconos de la barra de herramientas dibujados en tiempo de ejecución.
    /// </summary>
    /// <remarks>
    /// Cada icono se diseña sobre una rejilla de 16×16 unidades, centrado en (8, 8) y con el mismo
    /// margen, y se dibuja directamente al tamaño en píxeles de la pantalla. Así no hay reescalado
    /// posterior (que los deja borrosos con el escalado de Windows al 125 % o 150 %) y todos
    /// quedan alineados con el texto de su botón.
    /// </remarks>
    public static class Glyphs
    {
        /// <summary>Tamaño de diseño: el de un icono de barra de herramientas al 100 %.</summary>
        public const int DesignSize = 16;

        private static readonly Color Accent = Color.FromArgb(37, 99, 235);
        private static readonly Color Neutral = Color.FromArgb(71, 85, 105);
        private static readonly Color Positive = Color.FromArgb(22, 140, 75);
        private static readonly Color Negative = Color.FromArgb(200, 40, 40);
        private static readonly Color Folder = Color.FromArgb(226, 150, 30);
        private static readonly Color Paper = Color.White;

        private static readonly Dictionary<(Glyph, int), Bitmap> Cache = new();

        /// <summary>Devuelve el icono dibujado a <paramref name="size"/> píxeles de lado.</summary>
        public static Image Get(Glyph glyph, int size)
        {
            size = Math.Max(DesignSize, size);
            if (Cache.TryGetValue((glyph, size), out var cached)) return cached;

            var bitmap = new Bitmap(size, size);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.ScaleTransform(size / (float)DesignSize, size / (float)DesignSize);

                switch (glyph)
                {
                    case Glyph.AddFile: DrawAddFile(g); break;
                    case Glyph.AddFolder: DrawAddFolder(g); break;
                    case Glyph.Remove: DrawRemove(g); break;
                    case Glyph.Clean: DrawClean(g); break;
                    case Glyph.Save: DrawSave(g); break;
                    case Glyph.Connect: DrawConnect(g); break;
                    case Glyph.Options: DrawOptions(g); break;
                    default: throw new ArgumentOutOfRangeException(nameof(glyph));
                }
            }

            Cache[(glyph, size)] = bitmap;
            return bitmap;
        }

        private static Pen Stroke(Color color, float width) => new(color, width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        /// <summary>Hoja con la esquina doblada y una insignia verde con el signo más.</summary>
        private static void DrawAddFile(Graphics g)
        {
            var page = new[]
            {
                new PointF(2f, 1.5f), new PointF(8f, 1.5f), new PointF(11f, 4.5f),
                new PointF(11f, 14.5f), new PointF(2f, 14.5f)
            };

            using (var paper = new SolidBrush(Paper)) g.FillPolygon(paper, page);
            using (var pen = Stroke(Neutral, 1.3f))
            {
                g.DrawPolygon(pen, page);
                g.DrawLines(pen, new[] { new PointF(8f, 1.5f), new PointF(8f, 4.5f), new PointF(11f, 4.5f) });
            }

            DrawBadge(g, 11.2f, 11.2f, Positive, plus: true);
        }

        private static void DrawAddFolder(Graphics g)
        {
            using var path = new GraphicsPath();
            path.AddLines(new[]
            {
                new PointF(1.5f, 13f), new PointF(1.5f, 3.5f), new PointF(6f, 3.5f),
                new PointF(7.5f, 5f), new PointF(14.5f, 5f), new PointF(14.5f, 13f)
            });
            path.CloseFigure();

            using (var fill = new SolidBrush(Color.FromArgb(110, Folder))) g.FillPath(fill, path);
            using (var pen = Stroke(Folder, 1.3f)) g.DrawPath(pen, path);

            // Solapa delantera, algo más clara, para que se lea como carpeta y no como pestaña.
            using var flap = new SolidBrush(Color.FromArgb(200, Folder));
            g.FillRectangle(flap, 1.5f, 7f, 13f, 6f);
        }

        private static void DrawRemove(Graphics g)
        {
            using var pen = Stroke(Negative, 2.2f);
            g.DrawLine(pen, 4f, 4f, 12f, 12f);
            g.DrawLine(pen, 12f, 4f, 4f, 12f);
        }

        /// <summary>Escoba en diagonal, centrada en la rejilla.</summary>
        private static void DrawClean(Graphics g)
        {
            using (var handle = Stroke(Color.FromArgb(150, 100, 50), 1.8f))
            {
                g.DrawLine(handle, 14f, 2f, 8.6f, 7.4f);
            }

            var head = new[]
            {
                new PointF(5.6f, 6.2f), new PointF(9.8f, 10.4f), new PointF(6.2f, 14.5f), new PointF(1.5f, 9.8f)
            };

            using (var fill = new SolidBrush(Accent)) g.FillPolygon(fill, head);
            using (var outline = Stroke(Color.FromArgb(30, 64, 175), 1f)) g.DrawPolygon(outline, head);

            // Cerdas: dos líneas claras paralelas al borde inferior.
            using var bristles = Stroke(Color.FromArgb(190, Color.White), 0.9f);
            g.DrawLine(bristles, 4.4f, 9.8f, 7.6f, 13f);
            g.DrawLine(bristles, 6.4f, 8.2f, 9f, 10.8f);
        }

        /// <summary>Disquete con su pestaña metálica y la etiqueta.</summary>
        private static void DrawSave(Graphics g)
        {
            using var body = new GraphicsPath();
            body.AddLines(new[]
            {
                new PointF(2f, 2f), new PointF(11.5f, 2f), new PointF(14f, 4.5f),
                new PointF(14f, 14f), new PointF(2f, 14f)
            });
            body.CloseFigure();

            using (var fill = new SolidBrush(Accent)) g.FillPath(fill, body);

            using (var shutter = new SolidBrush(Color.FromArgb(220, 230, 250))) g.FillRectangle(shutter, 4.5f, 2f, 6f, 4f);
            using (var slot = new SolidBrush(Accent)) g.FillRectangle(slot, 8.5f, 2.8f, 1.2f, 2.4f);
            using (var label = new SolidBrush(Paper)) g.FillRectangle(label, 4f, 8.5f, 8f, 5.5f);
        }

        /// <summary>Dos eslabones de cadena enlazados.</summary>
        private static void DrawConnect(Graphics g)
        {
            var state = g.Save();
            g.TranslateTransform(8f, 8f);
            g.RotateTransform(-45f);

            using var pen = Stroke(Positive, 1.8f);
            DrawRoundedRectangle(g, pen, -7f, -2.6f, 8.2f, 5.2f, 2.6f);
            DrawRoundedRectangle(g, pen, -1.2f, -2.6f, 8.2f, 5.2f, 2.6f);

            g.Restore(state);
        }

        /// <summary>Rueda dentada con ocho dientes y un hueco central.</summary>
        private static void DrawOptions(Graphics g)
        {
            const int teeth = 8;
            const float outer = 6.6f;
            const float inner = 4.9f;

            var points = new List<PointF>();
            for (int i = 0; i < teeth; i++)
            {
                double center = i * 2 * Math.PI / teeth;
                double half = Math.PI / teeth;

                // Cada diente: base ancha en el radio interior y cara algo más estrecha en el exterior.
                points.Add(Polar(inner, center - half * 0.95));
                points.Add(Polar(outer, center - half * 0.45));
                points.Add(Polar(outer, center + half * 0.45));
                points.Add(Polar(inner, center + half * 0.95));
            }

            using var gear = new GraphicsPath(FillMode.Alternate);
            gear.AddPolygon(points.ToArray());
            gear.AddEllipse(8f - 2.1f, 8f - 2.1f, 4.2f, 4.2f);

            using var fill = new SolidBrush(Neutral);
            g.FillPath(fill, gear);
        }

        private static PointF Polar(float radius, double angle) =>
            new(8f + radius * (float)Math.Cos(angle), 8f + radius * (float)Math.Sin(angle));

        /// <summary>Círculo de color con un signo más blanco, para las acciones de añadir.</summary>
        private static void DrawBadge(Graphics g, float cx, float cy, Color color, bool plus)
        {
            const float radius = 3.5f;

            using (var ring = new SolidBrush(Color.White)) g.FillEllipse(ring, cx - radius - 0.8f, cy - radius - 0.8f, (radius + 0.8f) * 2, (radius + 0.8f) * 2);
            using (var fill = new SolidBrush(color)) g.FillEllipse(fill, cx - radius, cy - radius, radius * 2, radius * 2);

            if (!plus) return;

            using var pen = Stroke(Color.White, 1.4f);
            g.DrawLine(pen, cx - 2f, cy, cx + 2f, cy);
            g.DrawLine(pen, cx, cy - 2f, cx, cy + 2f);
        }

        private static void DrawRoundedRectangle(Graphics g, Pen pen, float x, float y, float width, float height, float radius)
        {
            using var path = new GraphicsPath();
            float diameter = radius * 2;
            path.AddArc(x, y, diameter, diameter, 180, 90);
            path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
            path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
            path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            g.DrawPath(pen, path);
        }
    }
}
