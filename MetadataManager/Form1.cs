using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using IO = System.IO;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Tiff;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.FileType;
using System.IO.Compression;
using System.Xml.Linq;
using System.Text;
using System.Security.Cryptography;

namespace MetadataManager
{
    public partial class Form1 : Form
    {
        private int nextId = 1;

        // campo temporal para revertir cambios si falla la modificación
        private object editingOldValue = null;

        // fecha estándar solicitada
        private const string StandardDateIso = "2000:01:01T00:00:00";
        private static readonly DateTime StandardDateTime = DateTime.Parse("2000-01-01T00:00:00", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);

        public Form1()
        {
            InitializeComponent();

            // asegurar visibilidad y ajustes mínimos del DataGridView (por si el diseñador ocultó columnas)
            if (dataGridViewMetadata.Columns.Count >= 2)
            {
                dataGridViewMetadata.Columns[0].Visible = true;
                dataGridViewMetadata.Columns[1].Visible = true;
            }

            dataGridViewMetadata.AllowUserToAddRows = false;
            dataGridViewMetadata.AllowUserToDeleteRows = false;
            dataGridViewMetadata.RowHeadersVisible = false;
            dataGridViewMetadata.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewMetadata.MultiSelect = false;
            // permitir barra horizontal: no auto‑fill de columnas
            dataGridViewMetadata.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dataGridViewMetadata.ScrollBars = ScrollBars.Both;

            SuscribirEventos();
        }

        private void SuscribirEventos()
        {
            // asegurar que el ListView muestra solo el nombre (una única columna)
            if (listViewArchives.Columns.Count == 0)
            {
                listViewArchives.Columns.Add("Nombre", 400);
                listViewArchives.Columns[0].Width = 400;
            }

            listViewArchives.AllowDrop = true;
            listViewArchives.Scrollable = true;
            listViewArchives.DragEnter += listViewArchives_DragEnter;
            listViewArchives.DragDrop += listViewArchives_DragDrop;
            listViewArchives.SelectedIndexChanged += listViewArchives_SelectedIndexChanged;
            listViewArchives.DoubleClick += listViewArchives_DoubleClick;

            // dataGrid handlers (edición segura)
            dataGridViewMetadata.CellDoubleClick += dataGridViewMetadata_CellDoubleClick;
            dataGridViewMetadata.CellBeginEdit += dataGridViewMetadata_CellBeginEdit;
            dataGridViewMetadata.CellEndEdit += dataGridViewMetadata_CellEndEdit;
            dataGridViewMetadata.CellValidating += dataGridViewMetadata_CellValidating;

            // botones creados desde el diseñador
            if (this.Controls.ContainsKey("buttonAddFile")) buttonAddFile.Click += buttonAddFile_Click;
            if (this.Controls.ContainsKey("buttonclean")) buttonclean.Click += buttonclean_Click;

            // botón para limpiar metadatos de la imagen seleccionada
            if (this.Controls.ContainsKey("buttonCleanMetadata")) buttonCleanMetadata.Click += buttonCleanMetadata_Click;
        }

        private void listViewArchives_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void listViewArchives_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                string[] archivos = (string[])e.Data.GetData(DataFormats.FileDrop);
                AddFiles(archivos);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al procesar archivos: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // método reutilizable para añadir archivos/directorios al listView (solo nombre visible)
        private void AddFiles(IEnumerable<string> archivos)
        {
            foreach (string archivo in archivos)
            {
                try
                {
                    // comprobar duplicados por Tag (ruta completa)
                    bool existe = false;
                    foreach (ListViewItem it in listViewArchives.Items)
                    {
                        if (it.Tag is string existingPath && string.Equals(existingPath, archivo, StringComparison.OrdinalIgnoreCase))
                        {
                            existe = true;
                            break;
                        }
                    }
                    if (existe) continue;

                    string extension = Path.GetExtension(archivo).ToLowerInvariant();
                    string tipo = detectarTipo(extension, archivo);

                    // calcular nombre visible (archivo o carpeta)
                    string nombreVisible;
                    if (IO.Directory.Exists(archivo))
                        nombreVisible = new IO.DirectoryInfo(archivo).Name;
                    else
                        nombreVisible = Path.GetFileName(archivo);

                    // el ListView mostrará solo el nombre; la ruta completa queda en Tag
                    ListViewItem item = new ListViewItem(nombreVisible);
                    item.Tag = archivo;
                    // guardamos tipo en tooltip interno
                    item.ToolTipText = tipo;

                    listViewArchives.Items.Add(item);
                    nextId++;
                }
                catch
                {
                    // seguir con el siguiente archivo en caso de error individual
                }
            }

            // ajustar la columna al contenido para que aparezca barra horizontal si hace falta
            try
            {
                listViewArchives.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);
                // si la columna cabría totalmente dentro del control, se puede limitar su ancho máximo
                var maxWidth = Math.Max(listViewArchives.Width - SystemInformation.VerticalScrollBarWidth, 100);
                if (listViewArchives.Columns[0].Width > Math.Max(maxWidth, 400))
                    listViewArchives.Columns[0].Width = listViewArchives.Columns[0].Width; // permite deslizarse
            }
            catch { /* ignorar */ }
        }

        // click del botón para abrir explorador y añadir archivos manualmente
        private void buttonAddFile_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Multiselect = true;
                dlg.Filter = "Todos los archivos (*.*)|*.*";
                dlg.Title = "Seleccionar archivos para añadir";

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    AddFiles(dlg.FileNames);
                }
            }
        }

        // click del botón para limpiar/eliminar el fichero seleccionado
        private void buttonclean_Click(object sender, EventArgs e)
        {
            if (listViewArchives.SelectedItems.Count == 0)
            {
                MessageBox.Show("No hay ningún fichero seleccionado.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var sel = listViewArchives.SelectedItems[0];
            listViewArchives.Items.Remove(sel);

            // limpiar metadata si el eliminado era el actualmente mostrado
            dataGridViewMetadata.Rows.Clear();
        }

        private string detectarTipo(string extension, string ruta)
        {
            if (string.IsNullOrEmpty(extension))
            {
                if (IO.Directory.Exists(ruta)) return "Directory";
                return "Unknown";
            }

            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif":
                case ".tiff":
                    return "Image";
                case ".pdf":
                    return "PDF";
                case ".doc":
                case ".docx":
                    return "Word";
                case ".xls":
                case ".xlsx":
                    return "Excel";
                case ".txt":
                    return "Text";
                case ".zip":
                case ".rar":
                    return "Archive";
                default:
                    return extension.TrimStart('.');
            }
        }

        private void listViewArchives_SelectedIndexChanged(object sender, EventArgs e)
        {
            dataGridViewMetadata.Rows.Clear();

            if (listViewArchives.SelectedItems.Count == 0) return;

            var selectedItem = listViewArchives.SelectedItems[0];
            string ruta = selectedItem.Tag as string;
            if (string.IsNullOrEmpty(ruta)) return;

            if (File.Exists(ruta))
            {
                mostrarMetadatosFichero(ruta);
            }
            else if (IO.Directory.Exists(ruta))
            {
                mostrarMetadatosDirectorio(ruta);
            }
            else
            {
                addRow("Status", "No existe (posible ruta remota)");
            }
        }

        private void listViewArchives_DoubleClick(object sender, EventArgs e)
        {
            if (listViewArchives.SelectedItems.Count == 0) return;
            var sel = listViewArchives.SelectedItems[0];
            string ruta = sel.Tag as string;
            if (File.Exists(ruta))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = ruta, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo abrir el archivo: " + ex.Message);
                }
            }
        }

        private void mostrarMetadatosFichero(string ruta)
        {
            try
            {
                dataGridViewMetadata.Rows.Clear();

                IO.FileInfo fileInfo = new IO.FileInfo(ruta);
                addRow("Nombre", fileInfo.Name);
                addRow("Tamaño (bytes)", fileInfo.Length.ToString());
                addRow("Fecha creación", fileInfo.CreationTime.ToString("s"));
                addRow("Fecha modif.", fileInfo.LastWriteTime.ToString("s"));
                addRow("Extensión", fileInfo.Extension);
                addRow("Atributos", fileInfo.Attributes.ToString());
                addRow("Ruta", fileInfo.FullName); // ahora la ruta también se muestra en dataGridViewMetadata

                string ext = fileInfo.Extension.ToLowerInvariant();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif" || ext == ".tiff")
                {
                    try
                    {
                        using (var img = Image.FromFile(ruta))
                        {
                            addRow("Resolución", $"{img.Width} x {img.Height}");
                            addRow("PixelFormat", img.PixelFormat.ToString());
                        }
                    }
                    catch { /* ignorar error lectura imagen */ }
                }

                try
                {
                    var metadataDirectories = ImageMetadataReader.ReadMetadata(ruta);

                    var ifd0 = metadataDirectories.OfType<ExifIfd0Directory>().FirstOrDefault();
                    if (ifd0 != null)
                    {
                        string make = ifd0.GetDescription(ExifDirectoryBase.TagMake);
                        if (!string.IsNullOrEmpty(make)) addRow("Camara - Marca", make);

                        string model = ifd0.GetDescription(ExifDirectoryBase.TagModel);
                        if (!string.IsNullOrEmpty(model)) addRow("Camara - Modelo", model);
                    }

                    var subIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                    if (subIfd != null)
                    {
                        string fechaDesc = subIfd.GetDescription(ExifDirectoryBase.TagDateTimeOriginal)
                                           ?? subIfd.GetDescription(ExifDirectoryBase.TagDateTime);
                        if (!string.IsNullOrEmpty(fechaDesc))
                        {
                            if (DateTime.TryParse(fechaDesc, out DateTime dtOriginal))
                                addRow("Fecha original (EXIF)", dtOriginal.ToString("s"));
                            else
                                addRow("Fecha original (EXIF)", fechaDesc);
                        }

                        string isoDesc = subIfd.GetDescription(ExifDirectoryBase.TagIsoEquivalent);
                        if (!string.IsNullOrEmpty(isoDesc)) addRow("ISO", isoDesc);

                        string apertureDesc = subIfd.GetDescription(ExifDirectoryBase.TagAperture);
                        if (!string.IsNullOrEmpty(apertureDesc)) addRow("Apertura", apertureDesc);

                        string exposureDesc = subIfd.GetDescription(ExifDirectoryBase.TagExposureTime);
                        if (!string.IsNullOrEmpty(exposureDesc)) addRow("Tiempo exposicion", exposureDesc);

                        string fNumberDesc = subIfd.GetDescription(ExifDirectoryBase.TagFNumber);
                        if (!string.IsNullOrEmpty(fNumberDesc)) addRow("FNumber", fNumberDesc);
                    }

                    var gpsDirectory = metadataDirectories.OfType<GpsDirectory>().FirstOrDefault();
                    if (gpsDirectory != null)
                    {
                        string latDesc = gpsDirectory.GetDescription(GpsDirectory.TagLatitude);
                        string lonDesc = gpsDirectory.GetDescription(GpsDirectory.TagLongitude);
                        string altDesc = gpsDirectory.GetDescription(GpsDirectory.TagAltitude);

                        double? latDecimal = null;
                        double? lonDecimal = null;

                        if (!string.IsNullOrEmpty(latDesc) && !string.IsNullOrEmpty(lonDesc))
                        {
                            latDecimal = ParseDmsToDecimal(latDesc);
                            lonDecimal = ParseDmsToDecimal(lonDesc);
                        }

                        if (!latDecimal.HasValue || !lonDecimal.HasValue)
                        {
                            // fallback a racionales RAW si existen
                            try
                            {
                                var latRationals = gpsDirectory.GetObject(GpsDirectory.TagLatitude) as object[];
                                var lonRationals = gpsDirectory.GetObject(GpsDirectory.TagLongitude) as object[];

                                if (latRationals == null)
                                {
                                    var latArr = gpsDirectory.GetRationalArray(GpsDirectory.TagLatitude);
                                    if (latArr != null) latRationals = latArr.Cast<object>().ToArray();
                                }
                                if (lonRationals == null)
                                {
                                    var lonArr = gpsDirectory.GetRationalArray(GpsDirectory.TagLongitude);
                                    if (lonArr != null) lonRationals = lonArr.Cast<object>().ToArray();
                                }

                                var latRefObj = gpsDirectory.GetObject(GpsDirectory.TagLatitudeRef);
                                var lonRefObj = gpsDirectory.GetObject(GpsDirectory.TagLongitudeRef);

                                if (latRationals != null && lonRationals != null)
                                {
                                    latDecimal = ConvertRationalDmsToDecimal(latRationals, latRefObj);
                                    lonDecimal = ConvertRationalDmsToDecimal(lonRationals, lonRefObj);
                                }
                            }
                            catch { /* ignore */ }
                        }

                        if (latDecimal.HasValue && lonDecimal.HasValue)
                        {
                            string coordsDecimal = $"{latDecimal.Value.ToString(CultureInfo.InvariantCulture)}, {lonDecimal.Value.ToString(CultureInfo.InvariantCulture)}";
                            addRow("Coordinates", coordsDecimal);
                            if (!string.IsNullOrEmpty(latDesc)) addRow("GPS - Lat (DMS)", latDesc);
                            if (!string.IsNullOrEmpty(lonDesc)) addRow("GPS - Lon (DMS)", lonDesc);
                            if (!string.IsNullOrEmpty(altDesc)) addRow("GPS - Altitude", altDesc);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(latDesc) || !string.IsNullOrEmpty(lonDesc))
                            {
                                addMetaIfNotExists("GPS - Lat (raw)", latDesc);
                                addMetaIfNotExists("GPS - Lon (raw)", lonDesc);
                            }
                            else
                            {
                                addRow("GPS", "No disponible");
                            }
                        }
                    }
                    else
                    {
                        addRow("GPS", "No disponible");
                    }

                    foreach (var directory in metadataDirectories)
                    {
                        foreach (var tag in directory.Tags)
                        {
                            string key = $"{directory.Name} - {tag.Name}";
                            addMetaIfNotExists(key, tag.Description);
                        }
                    }

                    // calcular hash SHA256 y añadir al final
                    try
                    {
                        string sha = ComputeSha256(ruta);
                        if (!string.IsNullOrEmpty(sha))
                            addRow("Hash SHA256", sha);
                    }
                    catch (Exception exHash)
                    {
                        addRow("Hash SHA256", "Error: " + exHash.Message);
                    }
                }
                catch (Exception exExif)
                {
                    addRow("EXIF Error", exExif.Message);
                }
            }
            catch (Exception ex)
            {
                addRow("Error", ex.Message);
            }
        }

        private string ComputeSha256(string filePath)
        {
            // calcula SHA256 y devuelve en hexadecimal en minúsculas
            using (var stream = File.OpenRead(filePath))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private void mostrarMetadatosDirectorio(string ruta)
        {
            try
            {
                dataGridViewMetadata.Rows.Clear();

                IO.DirectoryInfo dirInfo = new IO.DirectoryInfo(ruta);
                addRow("Nombre", dirInfo.Name);
                addRow("Ruta", dirInfo.FullName);
                addRow("Fecha creación", dirInfo.CreationTime.ToString("s"));
                addRow("Atributos", dirInfo.Attributes.ToString());
                addRow("Items (count)", dirInfo.EnumerateFileSystemInfos().Count().ToString());
            }
            catch (Exception ex)
            {
                addRow("Error", ex.Message);
            }
        }

        private void addRow(string clave, string valor)
        {
            dataGridViewMetadata.Rows.Add(clave, valor ?? "");
        }

        private void addMetaIfNotExists(string clave, string valor)
        {
            foreach (DataGridViewRow row in dataGridViewMetadata.Rows)
            {
                if (string.Equals(Convert.ToString(row.Cells[0].Value), clave, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            addRow(clave, valor);
        }

        private void dataGridViewMetadata_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            // almacenar valor original para revertir si es necesario
            if (e.RowIndex >= 0 && e.ColumnIndex == 1)
                editingOldValue = dataGridViewMetadata.Rows[e.RowIndex].Cells[1].Value;
        }

        private void dataGridViewMetadata_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            // validaciones básicas antes de aceptar el cambio
            if (e.RowIndex < 0 || e.ColumnIndex != 1) return;

            string newValue = Convert.ToString(e.FormattedValue) ?? string.Empty;
            string key = Convert.ToString(dataGridViewMetadata.Rows[e.RowIndex].Cells[0].Value) ?? string.Empty;

            // evitar proyecto con nombre vacío
            if (string.Equals(key, "Nombre", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(newValue))
            {
                dataGridViewMetadata.Rows[e.RowIndex].ErrorText = "El nombre no puede estar vacío.";
                e.Cancel = true;
                return;
            }

            // validación básica para fechas
            if ((string.Equals(key, "Fecha creación", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(key, "Fecha modif.", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(newValue))
            {
                if (!DateTime.TryParse(newValue, out _))
                {
                    dataGridViewMetadata.Rows[e.RowIndex].ErrorText = "Formato de fecha inválido. Usa yyyy-MM-ddTHH:mm:ss u otro formato reconocible.";
                    e.Cancel = true;
                    return;
                }
            }

            dataGridViewMetadata.Rows[e.RowIndex].ErrorText = string.Empty;
        }

        private void dataGridViewMetadata_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1) return;

            var row = dataGridViewMetadata.Rows[e.RowIndex];
            string key = Convert.ToString(row.Cells[0].Value) ?? string.Empty;
            string newValue = Convert.ToString(row.Cells[1].Value) ?? string.Empty;

            try
            {
                // obtener ruta del fichero seleccionado en listview
                if (listViewArchives.SelectedItems.Count == 0)
                    throw new InvalidOperationException("No hay fichero seleccionado en la lista.");

                string path = listViewArchives.SelectedItems[0].Tag as string;
                if (string.IsNullOrEmpty(path))
                    throw new InvalidOperationException("Ruta del elemento no disponible.");

                bool applied = TryApplyMetadataChange(path, key, newValue);

                if (!applied)
                {
                    // aceptamos el cambio en la tabla pero no se pudo aplicar al fichero: avisar y dejar valor anterior
                    MessageBox.Show("No se pudo aplicar el cambio al fichero. Revirtiendo al valor anterior.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    row.Cells[1].Value = editingOldValue;
                }
                else
                {
                    // refrescar metadatos mostrados (por si algo cambió, p. ej. nombre o fechas)
                    if (File.Exists(path) || IO.Directory.Exists(path))
                    {
                        // si se renombró path puede haber cambiado; forzar recarga usando ruta actual en Tag
                        var sel = listViewArchives.SelectedItems[0];
                        string currentPath = sel.Tag as string;
                        if (File.Exists(currentPath))
                            mostrarMetadatosFichero(currentPath);
                        else if (IO.Directory.Exists(currentPath))
                            mostrarMetadatosDirectorio(currentPath);
                    }
                }
            }
            catch (Exception ex)
            {
                // revertir y mostrar error
                row.Cells[1].Value = editingOldValue;
                MessageBox.Show("Error al aplicar metadatos: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                editingOldValue = null;
            }
        }

        // intenta aplicar cambios 'simples' a metadatos de ficheros/directorios
        // devuelve true si se aplicó correctamente (o si no aplica a fichero pero no hay error),
        // false si no se pudo aplicar (por ejemplo un rename fallido)
        private bool TryApplyMetadataChange(string path, string key, string newValue)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return false;

                // solo se manejan cambios en ficheros normales o directorios
                bool isFile = File.Exists(path);
                bool isDir = IO.Directory.Exists(path);

                if (!isFile && !isDir) return false;

                // manejar renombrado
                if (string.Equals(key, "Nombre", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(newValue)) throw new ArgumentException("Nombre vacío.");

                    // validar nombre de fichero
                    foreach (char c in Path.GetInvalidFileNameChars())
                        if (newValue.Contains(c)) throw new ArgumentException("El nombre contiene caracteres inválidos.");

                    string parent = isFile ? Path.GetDirectoryName(path) : IO.Directory.GetParent(path)?.FullName;
                    if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("No se puede determinar la carpeta padre.");

                    string dest = Path.Combine(parent, newValue);

                    if (string.Equals(dest, path, StringComparison.OrdinalIgnoreCase)) return true; // sin cambios

                    if (isFile)
                    {
                        if (File.Exists(dest)) throw new IOException("Ya existe un fichero con ese nombre de destino.");
                        File.Move(path, dest);
                    }
                    else
                    {
                        if (IO.Directory.Exists(dest)) throw new IOException("Ya existe un directorio con ese nombre de destino.");
                        IO.Directory.Move(path, dest);
                    }

                    // actualizar tag y nombre visible en listview
                    if (listViewArchives.SelectedItems.Count > 0)
                    {
                        var sel = listViewArchives.SelectedItems[0];
                        sel.Tag = dest;
                        sel.Text = Path.GetFileName(dest);
                    }

                    return true;
                }

                // manejar fechas
                if (string.Equals(key, "Fecha creación", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "Fecha modif.", StringComparison.OrdinalIgnoreCase))
                {
                    if (!DateTime.TryParse(newValue, out DateTime dt)) throw new ArgumentException("Formato de fecha inválido.");

                    if (isFile)
                    {
                        if (string.Equals(key, "Fecha creación", StringComparison.OrdinalIgnoreCase))
                            File.SetCreationTime(path, dt);
                        else
                            File.SetLastWriteTime(path, dt);
                    }
                    else
                    {
                        if (string.Equals(key, "Fecha creación", StringComparison.OrdinalIgnoreCase))
                            IO.Directory.SetCreationTime(path, dt);
                        else
                            IO.Directory.SetLastWriteTime(path, dt);
                    }

                    return true;
                }

                // manejar atributos (intento básico)
                if (string.Equals(key, "Atributos", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse<FileAttributes>(newValue, true, out FileAttributes fa))
                    {
                        // usar File.SetAttributes para ficheros y directorios (funciona en ambos)
                        File.SetAttributes(path, fa);
                        return true;
                    }
                    else
                        throw new ArgumentException("Atributos inválidos.");
                }

                // para otros campos no intentamos escribir en el fichero; aceptamos el cambio en la vista.
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void dataGridViewMetadata_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            string clave = Convert.ToString(dataGridViewMetadata.Rows[e.RowIndex].Cells[0].Value);
            string valor = Convert.ToString(dataGridViewMetadata.Rows[e.RowIndex].Cells[1].Value);

            if (string.Equals(clave, "Coordinates", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(valor))
            {
                var parts = valor.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string lat = parts[0].Trim();
                    string lon = parts[1].Trim();
                    string url = $"https://www.google.com/maps/search/?api=1&query={lat},{lon}";
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("No se pudo abrir el navegador: " + ex.Message);
                    }
                }
            }
        }

        // Parsers auxiliares
        private double? ParseDmsToDecimal(string dms)
        {
            if (string.IsNullOrWhiteSpace(dms)) return null;

            if (dms.Contains("/"))
            {
                var parts = dms.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                               .Where(p => p.Contains("/"))
                               .ToArray();
                if (parts.Length >= 3)
                {
                    try
                    {
                        double[] vals = parts.Select(ParseRational).ToArray();
                        double dec = vals[0] + vals[1] / 60.0 + vals[2] / 3600.0;
                        if (dms.IndexOf("S", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            dms.IndexOf("W", StringComparison.OrdinalIgnoreCase) >= 0)
                            dec = -dec;
                        return dec;
                    }
                    catch { }
                }
            }

            try
            {
                var sign = 1;
                if (dms.IndexOf("S", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    dms.IndexOf("W", StringComparison.OrdinalIgnoreCase) >= 0) sign = -1;

                var numericParts = System.Text.RegularExpressions.Regex.Matches(dms, @"\d+(\.\d+)?")
                    .Cast<System.Text.RegularExpressions.Match>()
                    .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture))
                    .ToArray();

                if (numericParts.Length >= 1)
                {
                    double degrees = numericParts[0];
                    double minutes = numericParts.Length >= 2 ? numericParts[1] : 0;
                    double seconds = numericParts.Length >= 3 ? numericParts[2] : 0;
                    double dec = degrees + minutes / 60.0 + seconds / 3600.0;
                    return dec * sign;
                }
            }
            catch { }

            return null;
        }

        private double ParseRational(object rationalObj)
        {
            if (rationalObj == null) return 0.0;
            var type = rationalObj.GetType();

            if (rationalObj is double d) return d;
            if (rationalObj is float f) return f;
            if (rationalObj is int i) return i;

            var numProp = type.GetProperty("Numerator");
            var denProp = type.GetProperty("Denominator");
            if (numProp != null && denProp != null)
            {
                try
                {
                    var num = Convert.ToDouble(numProp.GetValue(rationalObj));
                    var den = Convert.ToDouble(denProp.GetValue(rationalObj));
                    if (den == 0) return 0.0;
                    return num / den;
                }
                catch { }
            }

            var s = rationalObj.ToString();
            if (s.Contains("/"))
            {
                var parts = s.Split('/');
                if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double a)
                    && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double b) && b != 0)
                    return a / b;
            }

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) return val;
            return 0.0;
        }

        private double? ConvertRationalDmsToDecimal(object[] rationals, object refObj)
        {
            if (rationals == null || rationals.Length < 3) return null;
            try
            {
                double d = ParseRational(rationals[0]);
                double m = ParseRational(rationals[1]);
                double s = ParseRational(rationals[2]);
                double dec = d + m / 60.0 + s / 3600.0;
                if (refObj != null)
                {
                    string r = refObj.ToString();
                    if (r.Equals("S", StringComparison.OrdinalIgnoreCase) || r.Equals("W", StringComparison.OrdinalIgnoreCase))
                        dec = -dec;
                }
                return dec;
            }
            catch { return null; }
        }

        // Handlers vacíos referenciados por el diseñador
        private void Form1_Load(object sender, EventArgs e) { /* implementar si hace falta */ }
        private void dataGridViewMetadata_CellContentClick(object sender, DataGridViewCellEventArgs e) { /* no usado */ }


        private void buttonCleanMetadata_Click(object sender, EventArgs e)
        {
            if (listViewArchives.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecciona un fichero en la lista para limpiar sus metadatos.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string path = listViewArchives.SelectedItems[0].Tag as string;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show("Fichero no válido o no existe.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // intentar encontrar exiftool en PATH
            string exiftoolPath = FindExifTool();
            if (!string.IsNullOrEmpty(exiftoolPath))
            {
                // usar exiftool para eliminación completa y fijar fechas
                string dateArg = StandardDateIso.Replace('T', ' ');
                bool ok = RunExifTool(exiftoolPath, path, dateArg);
                if (ok)
                {
                    // además fijar fechas del sistema de archivos
                    SetFileTimesTo2000(path);
                    mostrarMetadatosFichero(path);
                    MessageBox.Show("Eliminación completa de metadatos realizada (exiftool).", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("exiftool encontró un error al procesar el fichero. Se intentará el método alternativo.", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    try
                    {
                        FallbackRemoveMetadataForFile(path);
                        mostrarMetadatosFichero(path);
                        MessageBox.Show("Limpieza parcial completada.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error durante limpieza parcial: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            else
            {
                // informar al usuario que exiftool no está disponible y hacer fallback parcial
                var res = MessageBox.Show("ExifTool no se ha encontrado en el sistema. La eliminación completa no estará disponible. ¿Deseas continuar con la limpieza parcial (elimina EXIF básicos, intenta limpiar creador en .docx/.pptx/.xlsx y pone las fechas a 2000-01-01T00:00:00)?", "ExifTool no encontrado", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res != DialogResult.Yes) return;

                try
                {
                    FallbackRemoveMetadataForFile(path);
                    mostrarMetadatosFichero(path);
                    MessageBox.Show("Limpieza parcial completada. Para eliminación completa instala ExifTool y úsalo desde la aplicación.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error durante limpieza parcial: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string FindExifTool()
        {
            // intenta ejecutar "exiftool -ver" para comprobar si está en PATH
            string[] candidates = new[] { "exiftool", "exiftool.exe" };
            foreach (var cmd in candidates)
            {
                try
                {
                    var psi = new ProcessStartInfo(cmd, "-ver")
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (var p = Process.Start(psi))
                    {
                        if (p == null) continue;
                        p.WaitForExit(2000);
                        if (p.ExitCode == 0)
                            return cmd;
                    }
                }
                catch { /* no encontrado */ }
            }
            return null;
        }

        private bool RunExifTool(string exiftoolPath, string filePath, string dateArg)
        {
            try
            {
                // dateArg esperado "2000:01:01 00:00:00" (sin T)
                string args = $"-overwrite_original -all= -AllDates=\"{dateArg}\" -FileModifyDate=\"{dateArg}\" -FileCreateDate=\"{dateArg}\" \"{filePath}\"";
                var psi = new ProcessStartInfo(exiftoolPath, args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return false;
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(15000);
                    if (proc.ExitCode == 0)
                        return true;
                    Debug.WriteLine("exiftool stderr: " + stderr);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RunExifTool error: " + ex.Message);
                return false;
            }
        }

        private void SetFileTimesTo2000(string path)
        {
            try
            {
                var dt = DateTime.Parse("2000-01-01T00:00:00", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
                File.SetCreationTime(path, dt);
                File.SetLastWriteTime(path, dt);
                File.SetLastAccessTime(path, dt);
            }
            catch { /* ignorar fallos en ajuste de fechas del sistema de ficheros */ }
        }

        private void FallbackRemoveMetadataForFile(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();

            // imágenes: clonar y guardar para eliminar PropertyItems (EXIF básicos)
            var imageExts = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };
            if (imageExts.Contains(ext))
            {
                string tempFile = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), Path.GetFileName(path) + ".tmp");
                try
                {
                    using (var original = Image.FromFile(path))
                    {
                        using (var clean = new Bitmap(original.Width, original.Height, original.PixelFormat))
                        {
                            using (var g = Graphics.FromImage(clean))
                            {
                                g.DrawImage(original, 0, 0, original.Width, original.Height);
                            }

                            var imgFormat = GetImageFormatFromExtension(ext);
                            if (imgFormat != null)
                                clean.Save(tempFile, imgFormat);
                            else
                                clean.Save(tempFile); // fallback
                        }
                    }

                    File.Delete(path);
                    File.Move(tempFile, path);

                    // fijar fechas del sistema a la fecha estándar
                    SetFileTimesTo2000(path);
                }
                catch
                {
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                    throw;
                }
                return;
            }

            // OpenXML (.docx, .xlsx, .pptx): intentar limpiar creador y fechas
            var openXmlExts = new[] { ".docx", ".xlsx", ".pptx" };
            if (openXmlExts.Contains(ext))
            {
                RemoveCreatorFromOpenXml(path);
                SetFileTimesTo2000(path);
                return;
            }

            // Para PDF o binarios antiguos (.pdf, .doc), no hay garantía sin exiftool: intentar fijar solo fechas del sistema y avisar.
            var partialExts = new[] { ".pdf", ".doc" };
            if (partialExts.Contains(ext))
            {
                SetFileTimesTo2000(path);
                // no attempt to modify internal PDF metadata here (requires external tool or library)
                return;
            }

            // Para otros tipos, al menos fijar fechas de sistema
            SetFileTimesTo2000(path);
        }

        private void RemoveCreatorFromOpenXml(string path)
        {
            const string entryName = "docProps/core.xml";
            string tempPath = Path.Combine(Path.GetDirectoryName(path) ?? Path.GetTempPath(), Path.GetFileName(path) + ".tmpzip");
            using (var src = File.OpenRead(path))
            using (var srcZip = new ZipArchive(src, ZipArchiveMode.Read))
            using (var dst = File.Create(tempPath))
            using (var dstZip = new ZipArchive(dst, ZipArchiveMode.Create))
            {
                foreach (var entry in srcZip.Entries)
                {
                    if (!string.Equals(entry.FullName, entryName, StringComparison.OrdinalIgnoreCase))
                    {
                        var dstEntry = dstZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                        using (var inStream = entry.Open())
                        using (var outStream = dstEntry.Open())
                        {
                            inStream.CopyTo(outStream);
                        }
                    }
                    else
                    {
                        XDocument doc;
                        using (var inStream = entry.Open())
                        {
                            doc = XDocument.Load(inStream);
                        }

                        XNamespace dc = "http://purl.org/dc/elements/1.1/";
                        XNamespace dcterms = "http://purl.org/dc/terms/";

                        var creator = doc.Descendants(dc + "creator").FirstOrDefault();
                        if (creator != null) creator.Value = string.Empty;

                        var created = doc.Descendants(dcterms + "created").FirstOrDefault();
                        if (created != null) created.Value = StandardDateIso.Replace('T', ' ');
                        else doc.Root?.Add(new XElement(dcterms + "created", StandardDateIso.Replace('T', ' ')));

                        var modified = doc.Descendants(dcterms + "modified").FirstOrDefault();
                        if (modified != null) modified.Value = StandardDateIso.Replace('T', ' ');
                        else doc.Root?.Add(new XElement(dcterms + "modified", StandardDateIso.Replace('T', ' ')));

                        var dstEntry = dstZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                        using (var outStream = dstEntry.Open())
                        using (var writer = new StreamWriter(outStream, Encoding.UTF8))
                        {
                            doc.Save(writer);
                        }
                    }
                }
            }

            File.Delete(path);
            File.Move(tempPath, path);
        }

        private System.Drawing.Imaging.ImageFormat? GetImageFormatFromExtension(string ext)
        {
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    return System.Drawing.Imaging.ImageFormat.Jpeg;
                case ".png":
                    return System.Drawing.Imaging.ImageFormat.Png;
                case ".bmp":
                    return System.Drawing.Imaging.ImageFormat.Bmp;
                case ".gif":
                    return System.Drawing.Imaging.ImageFormat.Gif;
                case ".tiff":
                    return System.Drawing.Imaging.ImageFormat.Tiff;
                default:
                    return null;
            }
        }
    }
}