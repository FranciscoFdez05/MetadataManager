using System;

namespace MetadataManager.Services
{
    /// <summary>
    /// Reparto de anchos de las tres columnas de la lista de archivos (nombre, tipo y tamaño).
    /// Los anchos no se ajustan a mano: mientras quepan los mínimos, la suma de las tres columnas
    /// es exactamente el ancho visible, de modo que no aparece barra horizontal y ninguna
    /// columna puede quedarse fuera de la vista.
    /// </summary>
    public static class FileColumnLayout
    {
        public const int MinName = 90;
        public const int MinType = 64;
        public const int MinSize = 56;

        /// <summary>Ancho por debajo del cual ni siquiera caben las tres columnas en su mínimo.</summary>
        public const int Minimum = MinName + MinType + MinSize;

        private const int MaxType = 160;
        private const int MaxSize = 110;

        /// <summary>Anchos por defecto para un ancho visible dado, como {nombre, tipo, tamaño}.</summary>
        public static int[] Distribute(int available)
        {
            if (available < Minimum) return new[] { MinName, MinType, MinSize };

            int size = Math.Clamp(available / 5, MinSize, MaxSize);
            int type = Math.Clamp(available / 4, MinType, MaxType);
            int name = available - type - size;

            if (name < MinName)
            {
                // El nombre manda: las columnas auxiliares ceden lo que haga falta, hasta su mínimo.
                int excess = MinName - name;
                int fromType = Math.Min(excess, type - MinType);

                type -= fromType;
                size -= Math.Min(excess - fromType, size - MinSize);
                name = available - type - size;
            }

            return new[] { name, type, size };
        }
    }
}
