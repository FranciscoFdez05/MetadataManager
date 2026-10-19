using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using MetadataManager.Models;
using MetadataManager.Resources;
using MetadataManager.Services;
using MetadataManager.UI;

namespace MetadataManager
{
    /// <summary>
    /// Ventana principal: coordina la lista de archivos, la rejilla de metadatos
    /// y las operaciones de lectura, edición, limpieza y exportación.
    /// </summary>
    public partial class MainForm : Form
    {
        private const string HashProperty = "SHA-256";
        private const int PreviewMaxSize = 480;

        /// <summary>Espera tras la última pulsación antes de aplicar el filtro de propiedades.</summary>
        private const int FilterDelayMilliseconds = 200;

        /// <summary>Archivos que se limpian a la vez: suficiente para solapar disco y ExifTool sin saturar el equipo.</summary>
        private static readonly int CleanParallelism = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

        /// <summary>LVM_SCROLL: desplaza el contenido de un ListView.</summary>
        private const int LvmScroll = 0x1000 + 20;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

        private static readonly Color EditableCellColor = Color.FromArgb(255, 251, 230);
        private static readonly Color CategoryBackColor = Color.FromArgb(238, 242, 248);
        private static readonly Color CategoryForeColor = Color.FromArgb(30, 58, 110);

        private readonly List<MetadataEntry> _entries = new();
        private readonly HashSet<string> _collapsedCategories = new(StringComparer.OrdinalIgnoreCase);
        private readonly AppSettings _settings;
        private readonly string[] _initialPaths;

        private CancellationTokenSource? _loadCancellation;
        /// <summary>Cancela la limpieza o exportación en curso.</summary>
        private CancellationTokenSource? _operationCancellation;

        /// <summary>Enlace de la barra de estado para cancelar: la barra sigue activa mientras se trabaja.</summary>
        private readonly ToolStripStatusLabel _statusCancel;
        private Image? _preview;
        private string? _previewText;

        /// <summary>Archivo cuya vista previa espera la autorización del usuario.</summary>
        private string? _blockedPreviewPath;

        /// <summary>Ejecutables que el usuario ya ha autorizado durante esta sesión.</summary>
        private readonly HashSet<string> _allowedPreviews = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Evita que los eventos de la rejilla reaccionen mientras la repintamos.</summary>
        private bool _suspendGridEvents;

        private bool _isBusy;
        private string? _editingOriginalValue;
        /// <summary>Evita que el reparto de columnas se re-dispare a sí mismo.</summary>
        private bool _fittingFileColumns;

        private int _sortColumn = -1;
        private bool _sortAscending = true;

        /// <summary>Estilos compartidos por todas las filas: crear uno (y una fuente) por fila agota los recursos GDI.</summary>
        private readonly Font _categoryFont;
        private readonly DataGridViewCellStyle _categoryStyle;
        private readonly DataGridViewCellStyle _editableStyle;

        private readonly System.Windows.Forms.Timer _filterTimer;

        public MainForm() : this(new AppSettings(), Array.Empty<string>())
        {
        }

        /// <param name="settings">Preferencias cargadas del disco.</param>
        /// <param name="initialPaths">Rutas recibidas por línea de comandos.</param>
        public MainForm(AppSettings settings, string[] initialPaths)
        {
            InitializeComponent();

            _settings = settings ?? new AppSettings();
            _initialPaths = initialPaths ?? Array.Empty<string>();

            _categoryFont = new Font(dataGridViewMetadata.Font, FontStyle.Bold);
            _categoryStyle = new DataGridViewCellStyle
            {
                BackColor = CategoryBackColor,
                ForeColor = CategoryForeColor,
                SelectionBackColor = CategoryBackColor,
                SelectionForeColor = CategoryForeColor,
                Font = _categoryFont
            };
            _editableStyle = new DataGridViewCellStyle { BackColor = EditableCellColor };

            _filterTimer = new System.Windows.Forms.Timer { Interval = FilterDelayMilliseconds };
            _filterTimer.Tick += OnFilterTimerTick;

            _statusCancel = new ToolStripStatusLabel
            {
                IsLink = true,
                Visible = false,
                Text = Strings.ToolCancel,
                ToolTipText = Strings.TipCancel
            };
            _statusCancel.Click += (_, _) => CancelOperation();
            statusStrip.Items.Insert(statusStrip.Items.IndexOf(statusProgress) + 1, _statusCancel);
            KeyPreview = true;

            Disposed += (_, _) =>
            {
                _filterTimer.Dispose();
                _categoryFont.Dispose();
            };

            ApplyLocalization();
            ApplyGlyphs();
            LoadWindowIcon();

            Text = $"MetadataManager {ApplicationVersion}";
            listViewFiles.MultiSelect = true;
            pictureThumbnail.Visible = _settings.ShowThumbnail;

            UpdateExifToolStatus();
            UpdateCommandStates();
            SetStatus(Strings.StatusWelcome);
        }

        private static string ApplicationVersion =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        private FileEntry? SelectedFile =>
            listViewFiles.SelectedItems.Count > 0 ? listViewFiles.SelectedItems[0].Tag as FileEntry : null;

        private IReadOnlyList<ListViewItem> SelectedItems =>
            listViewFiles.SelectedItems.Cast<ListViewItem>().ToList();

        #region Arranque y cierre

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (_settings.WindowMaximized)
            {
                WindowState = FormWindowState.Maximized;
            }
            else if (_settings.WindowWidth >= 800 && _settings.WindowHeight >= 460)
            {
                // Se guarda el área de cliente: así la ventana no encoge un poco en cada arranque.
                ClientSize = new Size(_settings.WindowWidth, _settings.WindowHeight);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            RestoreSplitterDistance();
            FitFileColumns();

            if (_initialPaths.Length > 0) _ = AddPathsAsync(_initialPaths);
        }

        private void RestoreSplitterDistance()
        {
            try
            {
                int maximum = splitContainer.Width - splitContainer.Panel2MinSize - splitContainer.SplitterWidth;
                int minimum = splitContainer.Panel1MinSize;

                splitContainer.SplitterDistance = Math.Clamp(_settings.SplitterDistance, minimum, Math.Max(minimum, maximum));
            }
            catch (ArgumentException)
            {
                // La ventana es demasiado estrecha para ese divisor: se deja el valor por defecto.
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isBusy &&
                MessageBox.Show(this, Strings.MsgBusyOnExit, Strings.TitleError,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _loadCancellation?.Cancel();
            _operationCancellation?.Cancel();
            SaveSettings();

            base.OnFormClosing(e);
        }

        private void SaveSettings()
        {
            _settings.WindowMaximized = WindowState == FormWindowState.Maximized;

            if (WindowState == FormWindowState.Normal)
            {
                _settings.WindowWidth = ClientSize.Width;
                _settings.WindowHeight = ClientSize.Height;
            }

            _settings.SplitterDistance = splitContainer.SplitterDistance;
            SettingsService.Save(_settings);
        }

        private void LoadWindowIcon()
        {
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException)
            {
                // Sin icono asociado se usa el predeterminado de Windows Forms.
            }
        }

        /// <summary>
        /// Dibuja los iconos de la barra al tamaño exacto en píxeles de la pantalla actual. Si se
        /// dejaran a 16 px, la barra los estiraría con el escalado de Windows y se verían borrosos.
        /// </summary>
        private void ApplyGlyphs()
        {
            int size = LogicalToDeviceUnits(Glyphs.DesignSize);

            toolStrip.SuspendLayout();
            toolStrip.ImageScalingSize = new Size(size, size);

            foreach (var (button, glyph) in new (ToolStripButton, Glyph)[]
            {
                (buttonAddFiles, Glyph.AddFile),
                (buttonAddFolder, Glyph.AddFolder),
                (buttonRemove, Glyph.Remove),
                (buttonClean, Glyph.Clean),
                (buttonExport, Glyph.Save),
                (buttonExifTool, Glyph.Connect),
                (buttonOptions, Glyph.Options)
            })
            {
                button.Image = Glyphs.Get(glyph, size);
                button.ImageScaling = ToolStripItemImageScaling.None;
                button.ImageAlign = ContentAlignment.MiddleCenter;
                button.TextImageRelation = TextImageRelation.ImageBeforeText;
            }

            toolStrip.ResumeLayout();
        }

        /// <summary>Al mover la ventana a una pantalla con otro escalado, los iconos se vuelven a dibujar.</summary>
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ApplyGlyphs();
        }

        /// <summary>Vuelca los textos del idioma activo sobre los controles del diseñador.</summary>
        private void ApplyLocalization()
        {
            menuFile.Text = Strings.MenuFile;
            menuAddFiles.Text = Strings.MenuAddFiles;
            menuAddFolder.Text = Strings.MenuAddFolder;
            menuAddFolderFiles.Text = Strings.MenuAddFolderFiles;
            menuExport.Text = Strings.MenuExport;
            menuExportBatch.Text = Strings.MenuExportBatch;
            menuExit.Text = Strings.MenuExit;
            menuTools.Text = Strings.MenuTools;
            menuClean.Text = Strings.MenuClean;
            menuRemove.Text = Strings.MenuRemove;
            menuClearList.Text = Strings.MenuClearList;
            menuDetectExifTool.Text = Strings.MenuDetectExifTool;
            menuConnectExifTool.Text = Strings.MenuConnectExifTool;
            menuOptions.Text = Strings.MenuOptions;
            menuHelp.Text = Strings.MenuHelp;
            menuAbout.Text = Strings.MenuAbout;

            buttonAddFiles.Text = Strings.ToolAddFiles;
            buttonAddFiles.ToolTipText = Strings.TipAddFiles;
            buttonAddFolder.Text = Strings.ToolAddFolder;
            buttonRemove.Text = Strings.ToolRemove;
            buttonRemove.ToolTipText = Strings.TipRemove;
            buttonClean.Text = Strings.ToolClean;
            buttonClean.ToolTipText = Strings.TipClean;
            buttonExport.Text = Strings.ToolExport;
            buttonExport.ToolTipText = Strings.TipExport;
            buttonExifTool.Text = Strings.ToolExifTool;
            buttonExifTool.ToolTipText = Strings.TipExifTool;
            buttonOptions.Text = Strings.ToolOptions;
            labelFilter.Text = Strings.LabelFilter;
            textBoxFilter.ToolTipText = Strings.TipFilter;

            columnName.Text = Strings.ColumnName;
            columnType.Text = Strings.ColumnType;
            columnSize.Text = Strings.ColumnSize;
            columnProperty.HeaderText = Strings.ColumnProperty;
            columnValue.HeaderText = Strings.ColumnValue;

            listMenuOpen.Text = Strings.ContextOpen;
            listMenuOpenFolder.Text = Strings.ContextOpenFolder;
            listMenuClean.Text = Strings.MenuClean;
            listMenuRemove.Text = Strings.MenuRemove;
            gridMenuCopy.Text = Strings.ContextCopyValue;
            gridMenuEdit.Text = Strings.ContextEditValue;
            gridMenuMaps.Text = Strings.ContextMaps;
            gridMenuExpandAll.Text = Strings.ContextExpandAll;
            gridMenuCollapseAll.Text = Strings.ContextCollapseAll;
        }

        #endregion

        #region Preferencias y estado de la interfaz

        private void OnOptionsRequested(object? sender, EventArgs e)
        {
            using var dialog = new OptionsForm(_settings);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var updated = dialog.Settings;

            _settings.Language = updated.Language;
            _settings.NormalizationDate = updated.NormalizationDate;
            _settings.OutputMode = updated.OutputMode;
            _settings.PreserveOrientation = updated.PreserveOrientation;
            _settings.ResetFileDates = updated.ResetFileDates;
            _settings.UseExifTool = updated.UseExifTool;
            _settings.ShowThumbnail = updated.ShowThumbnail;

            SettingsService.Save(_settings);

            pictureThumbnail.Visible = _settings.ShowThumbnail;
            if (!_settings.ShowThumbnail) ClearPreview();

            _ = LoadSelectedMetadataAsync();
        }

        private void UpdateCommandStates()
        {
            bool hasSelection = listViewFiles.SelectedItems.Count > 0;
            bool hasFileSelection = SelectedItems.Any(item => (item.Tag as FileEntry)?.IsFile == true);
            bool hasItems = listViewFiles.Items.Count > 0;

            buttonRemove.Enabled = hasSelection;
            menuRemove.Enabled = hasSelection;
            listMenuRemove.Enabled = hasSelection;

            buttonClean.Enabled = hasFileSelection && !_isBusy;
            menuClean.Enabled = buttonClean.Enabled;
            listMenuClean.Enabled = buttonClean.Enabled;

            buttonExport.Enabled = hasSelection;
            menuExport.Enabled = hasSelection;
            menuExportBatch.Enabled = hasItems && !_isBusy;
            menuClearList.Enabled = hasItems;
        }

        private void UpdateExifToolStatus()
        {
            string? path = ExifTool.Locate();
            bool available = path is not null;

            statusExifTool.Text = available && ExifTool.Version is string version
                ? string.Format(CultureInfo.CurrentCulture, Strings.StatusExifToolVersion, version)
                : available ? Strings.StatusExifToolFound : Strings.StatusExifToolMissing;

            statusExifTool.ForeColor = available ? Color.FromArgb(0, 110, 40) : Color.FromArgb(160, 90, 0);

            statusExifTool.ToolTipText = available
                ? $"{Strings.TipExifToolFound}\n{path}"
                : $"{Strings.TipExifToolMissing}\n{Strings.TipExifToolConnect}";
        }

        private void SetBusy(bool busy, int total)
        {
            _isBusy = busy;
            statusProgress.Visible = busy;
            _statusCancel.Visible = busy;
            _statusCancel.Enabled = busy;
            statusProgress.Value = 0;
            statusProgress.Maximum = Math.Max(total, 1);

            toolStrip.Enabled = !busy;
            menuStrip.Enabled = !busy;
            listViewFiles.Enabled = !busy;
            dataGridViewMetadata.Enabled = !busy;

            UseWaitCursor = busy;
            UpdateCommandStates();
        }

        /// <summary>Pide que se detenga la operación en curso; los archivos ya terminados se conservan.</summary>
        private void CancelOperation()
        {
            if (!_isBusy || _operationCancellation is null || _operationCancellation.IsCancellationRequested) return;

            _operationCancellation.Cancel();
            _statusCancel.Enabled = false;
            SetStatus(Strings.StatusCancelling);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _isBusy)
            {
                CancelOperation();
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private void SetStatus(string message) => statusLabel.Text = message;

        private void ShowError(string title, Exception exception)
        {
            MessageBox.Show(this, $"{title}:\n\n{exception.Message}", Strings.TitleError,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void OnAboutRequested(object? sender, EventArgs e)
        {
            MessageBox.Show(this,
                string.Format(CultureInfo.CurrentCulture, Strings.AboutText, ApplicationVersion),
                Strings.TitleAbout, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnExitRequested(object? sender, EventArgs e) => Close();

        #endregion
    }
}
