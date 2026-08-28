namespace MetadataManager
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                _loadCancellation?.Dispose();
                _cleanCancellation?.Dispose();
                _preview?.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            menuStrip = new MenuStrip();
            menuFile = new ToolStripMenuItem();
            menuAddFiles = new ToolStripMenuItem();
            menuAddFolder = new ToolStripMenuItem();
            menuAddFolderFiles = new ToolStripMenuItem();
            menuFileSeparator1 = new ToolStripSeparator();
            menuExport = new ToolStripMenuItem();
            menuExportBatch = new ToolStripMenuItem();
            menuFileSeparator2 = new ToolStripSeparator();
            menuExit = new ToolStripMenuItem();
            menuTools = new ToolStripMenuItem();
            menuClean = new ToolStripMenuItem();
            menuToolsSeparator1 = new ToolStripSeparator();
            menuRemove = new ToolStripMenuItem();
            menuClearList = new ToolStripMenuItem();
            menuToolsSeparator2 = new ToolStripSeparator();
            menuDetectExifTool = new ToolStripMenuItem();
            menuConnectExifTool = new ToolStripMenuItem();
            menuOptions = new ToolStripMenuItem();
            menuHelp = new ToolStripMenuItem();
            menuAbout = new ToolStripMenuItem();
            toolStrip = new ToolStrip();
            buttonAddFiles = new ToolStripButton();
            buttonAddFolder = new ToolStripButton();
            buttonRemove = new ToolStripButton();
            toolStripSeparator1 = new ToolStripSeparator();
            buttonClean = new ToolStripButton();
            buttonExport = new ToolStripButton();
            toolStripSeparator2 = new ToolStripSeparator();
            buttonExifTool = new ToolStripButton();
            buttonOptions = new ToolStripButton();
            toolStripSeparator3 = new ToolStripSeparator();
            labelFilter = new ToolStripLabel();
            textBoxFilter = new ToolStripTextBox();
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            statusExifTool = new ToolStripStatusLabel();
            statusProgress = new ToolStripProgressBar();
            splitContainer = new SplitContainer();
            listViewFiles = new ListView();
            columnName = new ColumnHeader();
            columnType = new ColumnHeader();
            columnSize = new ColumnHeader();
            pictureThumbnail = new PictureBox();
            listContextMenu = new ContextMenuStrip(components);
            listMenuOpen = new ToolStripMenuItem();
            listMenuOpenFolder = new ToolStripMenuItem();
            listMenuSeparator = new ToolStripSeparator();
            listMenuClean = new ToolStripMenuItem();
            listMenuRemove = new ToolStripMenuItem();
            dataGridViewMetadata = new DataGridView();
            columnProperty = new DataGridViewTextBoxColumn();
            columnValue = new DataGridViewTextBoxColumn();
            gridContextMenu = new ContextMenuStrip(components);
            gridMenuCopy = new ToolStripMenuItem();
            gridMenuEdit = new ToolStripMenuItem();
            gridMenuMaps = new ToolStripMenuItem();
            gridMenuSeparator = new ToolStripSeparator();
            gridMenuExpandAll = new ToolStripMenuItem();
            gridMenuCollapseAll = new ToolStripMenuItem();
            menuStrip.SuspendLayout();
            toolStrip.SuspendLayout();
            statusStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer).BeginInit();
            splitContainer.Panel1.SuspendLayout();
            splitContainer.Panel2.SuspendLayout();
            splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureThumbnail).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewMetadata).BeginInit();
            listContextMenu.SuspendLayout();
            gridContextMenu.SuspendLayout();
            SuspendLayout();
            //
            // menuStrip
            //
            menuStrip.Items.AddRange(new ToolStripItem[] { menuFile, menuTools, menuHelp });
            menuStrip.Location = new Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.Size = new Size(1100, 24);
            menuStrip.TabIndex = 0;
            //
            // menuFile
            //
            menuFile.DropDownItems.AddRange(new ToolStripItem[] { menuAddFiles, menuAddFolder, menuAddFolderFiles, menuFileSeparator1, menuExport, menuExportBatch, menuFileSeparator2, menuExit });
            menuFile.Name = "menuFile";
            menuFile.Size = new Size(60, 20);
            menuFile.Text = "&Archivo";
            //
            // menuAddFiles
            //
            menuAddFiles.Name = "menuAddFiles";
            menuAddFiles.ShortcutKeys = Keys.Control | Keys.O;
            menuAddFiles.Size = new Size(300, 22);
            menuAddFiles.Text = "&Añadir archivos...";
            menuAddFiles.Click += OnAddFilesRequested;
            //
            // menuAddFolder
            //
            menuAddFolder.Name = "menuAddFolder";
            menuAddFolder.Size = new Size(300, 22);
            menuAddFolder.Text = "Añadir &carpeta...";
            menuAddFolder.Click += OnAddFolderRequested;
            //
            // menuAddFolderFiles
            //
            menuAddFolderFiles.Name = "menuAddFolderFiles";
            menuAddFolderFiles.Size = new Size(300, 22);
            menuAddFolderFiles.Text = "Añadir archivos de una carpeta...";
            menuAddFolderFiles.Click += OnAddFolderFilesRequested;
            //
            // menuFileSeparator1
            //
            menuFileSeparator1.Name = "menuFileSeparator1";
            menuFileSeparator1.Size = new Size(297, 6);
            //
            // menuExport
            //
            menuExport.Name = "menuExport";
            menuExport.ShortcutKeys = Keys.Control | Keys.S;
            menuExport.Size = new Size(300, 22);
            menuExport.Text = "&Guardar metadatos...";
            menuExport.Click += OnExportRequested;
            //
            // menuExportBatch
            //
            menuExportBatch.Name = "menuExportBatch";
            menuExportBatch.Size = new Size(300, 22);
            menuExportBatch.Text = "Guardar metadatos de toda la lista...";
            menuExportBatch.Click += OnExportBatchRequested;
            //
            // menuFileSeparator2
            //
            menuFileSeparator2.Name = "menuFileSeparator2";
            menuFileSeparator2.Size = new Size(297, 6);
            //
            // menuExit
            //
            menuExit.Name = "menuExit";
            menuExit.Size = new Size(300, 22);
            menuExit.Text = "&Salir";
            menuExit.Click += OnExitRequested;
            //
            // menuTools
            //
            menuTools.DropDownItems.AddRange(new ToolStripItem[] { menuClean, menuToolsSeparator1, menuRemove, menuClearList, menuToolsSeparator2, menuDetectExifTool, menuConnectExifTool, menuOptions });
            menuTools.Name = "menuTools";
            menuTools.Size = new Size(85, 20);
            menuTools.Text = "&Herramientas";
            //
            // menuClean
            //
            menuClean.Name = "menuClean";
            menuClean.ShortcutKeys = Keys.Control | Keys.L;
            menuClean.Size = new Size(260, 22);
            menuClean.Text = "&Limpiar metadatos";
            menuClean.Click += OnCleanRequested;
            //
            // menuToolsSeparator1
            //
            menuToolsSeparator1.Name = "menuToolsSeparator1";
            menuToolsSeparator1.Size = new Size(257, 6);
            //
            // menuRemove
            //
            menuRemove.Name = "menuRemove";
            menuRemove.ShortcutKeyDisplayString = "Supr";
            menuRemove.Size = new Size(260, 22);
            menuRemove.Text = "&Quitar de la lista";
            menuRemove.Click += OnRemoveRequested;
            //
            // menuClearList
            //
            menuClearList.Name = "menuClearList";
            menuClearList.Size = new Size(260, 22);
            menuClearList.Text = "&Vaciar la lista";
            menuClearList.Click += OnClearListRequested;
            //
            // menuToolsSeparator2
            //
            menuToolsSeparator2.Name = "menuToolsSeparator2";
            menuToolsSeparator2.Size = new Size(257, 6);
            //
            // menuDetectExifTool
            //
            menuDetectExifTool.Name = "menuDetectExifTool";
            menuDetectExifTool.Size = new Size(260, 22);
            menuDetectExifTool.Text = "&Buscar ExifTool de nuevo";
            menuDetectExifTool.Click += OnDetectExifToolRequested;
            //
            // menuConnectExifTool
            //
            menuConnectExifTool.Name = "menuConnectExifTool";
            menuConnectExifTool.Size = new Size(260, 22);
            menuConnectExifTool.Text = "&Conectar ExifTool...";
            menuConnectExifTool.Click += OnConnectExifToolRequested;
            //
            // menuOptions
            //
            menuOptions.Name = "menuOptions";
            menuOptions.Size = new Size(260, 22);
            menuOptions.Text = "&Opciones...";
            menuOptions.Click += OnOptionsRequested;
            //
            // menuHelp
            //
            menuHelp.DropDownItems.AddRange(new ToolStripItem[] { menuAbout });
            menuHelp.Name = "menuHelp";
            menuHelp.Size = new Size(53, 20);
            menuHelp.Text = "A&yuda";
            //
            // menuAbout
            //
            menuAbout.Name = "menuAbout";
            menuAbout.Size = new Size(180, 22);
            menuAbout.Text = "&Acerca de...";
            menuAbout.Click += OnAboutRequested;
            //
            // toolStrip
            //
            toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip.ImageScalingSize = new Size(16, 16);
            toolStrip.Items.AddRange(new ToolStripItem[] { buttonAddFiles, buttonAddFolder, buttonRemove, toolStripSeparator1, buttonClean, buttonExport, toolStripSeparator2, buttonExifTool, buttonOptions, toolStripSeparator3, labelFilter, textBoxFilter });
            toolStrip.Location = new Point(0, 24);
            toolStrip.Name = "toolStrip";
            toolStrip.Padding = new Padding(6, 2, 6, 2);
            toolStrip.Size = new Size(1100, 27);
            toolStrip.TabIndex = 1;
            //
            // buttonAddFiles
            //
            buttonAddFiles.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            buttonAddFiles.Name = "buttonAddFiles";
            buttonAddFiles.Size = new Size(118, 22);
            buttonAddFiles.Text = "Añadir archivos";
            buttonAddFiles.Click += OnAddFilesRequested;
            //
            // buttonAddFolder
            //
            buttonAddFolder.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            buttonAddFolder.Name = "buttonAddFolder";
            buttonAddFolder.Size = new Size(112, 22);
            buttonAddFolder.Text = "Añadir carpeta";
            buttonAddFolder.Click += OnAddFolderRequested;
            //
            // buttonRemove
            //
            buttonRemove.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            buttonRemove.Name = "buttonRemove";
            buttonRemove.Size = new Size(66, 22);
            buttonRemove.Text = "Quitar";
            buttonRemove.Click += OnRemoveRequested;
            //
            // toolStripSeparator1
            //
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(6, 25);
            //
            // buttonClean
            //
            buttonClean.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            buttonClean.Name = "buttonClean";
            buttonClean.Size = new Size(128, 22);
            buttonClean.Text = "Limpiar metadatos";
            buttonClean.Click += OnCleanRequested;
            //
            // buttonExport
            //
            buttonExport.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            buttonExport.Name = "buttonExport";
            buttonExport.Size = new Size(136, 22);
            buttonExport.Text = "Guardar metadatos";
            buttonExport.Click += OnExportRequested;
            //
            // toolStripSeparator2
            //
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(6, 25);
            //
            // buttonExifTool
            //
            buttonExifTool.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            buttonExifTool.Name = "buttonExifTool";
            buttonExifTool.Size = new Size(84, 22);
            buttonExifTool.Text = "ExifTool";
            buttonExifTool.Click += OnConnectExifToolRequested;
            //
            // buttonOptions
            //
            buttonOptions.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            buttonOptions.Name = "buttonOptions";
            buttonOptions.Size = new Size(84, 22);
            buttonOptions.Text = "Opciones";
            buttonOptions.Click += OnOptionsRequested;
            //
            // toolStripSeparator3
            //
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new Size(6, 25);
            //
            // labelFilter
            //
            labelFilter.Name = "labelFilter";
            labelFilter.Size = new Size(43, 22);
            labelFilter.Text = "Filtrar:";
            //
            // textBoxFilter
            //
            textBoxFilter.BorderStyle = BorderStyle.FixedSingle;
            textBoxFilter.Name = "textBoxFilter";
            textBoxFilter.Size = new Size(200, 23);
            textBoxFilter.TextChanged += OnFilterChanged;
            //
            // statusStrip
            //
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, statusExifTool, statusProgress });
            statusStrip.Location = new Point(0, 628);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(1100, 22);
            statusStrip.TabIndex = 3;
            //
            // statusLabel
            //
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(800, 17);
            statusLabel.Spring = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // statusExifTool
            //
            statusExifTool.BorderSides = ToolStripStatusLabelBorderSides.Left;
            statusExifTool.IsLink = true;
            statusExifTool.LinkBehavior = LinkBehavior.HoverUnderline;
            statusExifTool.Name = "statusExifTool";
            statusExifTool.Size = new Size(120, 17);
            statusExifTool.Click += OnConnectExifToolRequested;
            //
            // statusProgress
            //
            statusProgress.Name = "statusProgress";
            statusProgress.Size = new Size(140, 16);
            statusProgress.Style = ProgressBarStyle.Continuous;
            statusProgress.Visible = false;
            //
            // splitContainer
            //
            splitContainer.Dock = DockStyle.Fill;
            splitContainer.FixedPanel = FixedPanel.Panel1;
            splitContainer.Location = new Point(0, 51);
            splitContainer.Name = "splitContainer";
            splitContainer.Panel1.Controls.Add(listViewFiles);
            splitContainer.Panel1.Controls.Add(pictureThumbnail);
            splitContainer.Panel1.Padding = new Padding(8, 8, 4, 8);
            splitContainer.Panel1MinSize = 220;
            splitContainer.Panel2.Controls.Add(dataGridViewMetadata);
            splitContainer.Panel2.Padding = new Padding(4, 8, 8, 8);
            splitContainer.Panel2MinSize = 320;
            splitContainer.Size = new Size(1100, 577);
            splitContainer.SplitterDistance = 340;
            splitContainer.SplitterWidth = 6;
            splitContainer.TabIndex = 2;
            //
            // listViewFiles
            //
            listViewFiles.AllowDrop = true;
            listViewFiles.Columns.AddRange(new ColumnHeader[] { columnName, columnType, columnSize });
            listViewFiles.ContextMenuStrip = listContextMenu;
            listViewFiles.Dock = DockStyle.Fill;
            listViewFiles.FullRowSelect = true;
            listViewFiles.GridLines = true;
            listViewFiles.HideSelection = false;
            listViewFiles.Location = new Point(8, 8);
            listViewFiles.Name = "listViewFiles";
            listViewFiles.Size = new Size(328, 391);
            listViewFiles.TabIndex = 0;
            listViewFiles.UseCompatibleStateImageBehavior = false;
            listViewFiles.View = View.Details;
            listViewFiles.ColumnClick += OnFilesColumnClick;
            listViewFiles.SelectedIndexChanged += OnFileSelectionChanged;
            listViewFiles.DoubleClick += OnFileDoubleClick;
            listViewFiles.DragEnter += OnFilesDragEnter;
            listViewFiles.DragDrop += OnFilesDragDrop;
            listViewFiles.KeyDown += OnFilesKeyDown;
            //
            // columnName
            //
            columnName.Text = "Nombre";
            columnName.Width = 170;
            //
            // columnType
            //
            columnType.Text = "Tipo";
            columnType.Width = 90;
            //
            // columnSize
            //
            columnSize.Text = "Tamaño";
            columnSize.TextAlign = HorizontalAlignment.Right;
            columnSize.Width = 64;
            //
            // pictureThumbnail
            //
            pictureThumbnail.BackColor = SystemColors.Window;
            pictureThumbnail.BorderStyle = BorderStyle.FixedSingle;
            pictureThumbnail.Dock = DockStyle.Bottom;
            pictureThumbnail.Location = new Point(8, 399);
            pictureThumbnail.Name = "pictureThumbnail";
            pictureThumbnail.Size = new Size(328, 170);
            pictureThumbnail.SizeMode = PictureBoxSizeMode.Zoom;
            pictureThumbnail.TabIndex = 1;
            pictureThumbnail.TabStop = false;
            pictureThumbnail.Paint += OnPreviewPaint;
            pictureThumbnail.Click += OnPreviewClick;
            //
            // listContextMenu
            //
            listContextMenu.Items.AddRange(new ToolStripItem[] { listMenuOpen, listMenuOpenFolder, listMenuSeparator, listMenuClean, listMenuRemove });
            listContextMenu.Name = "listContextMenu";
            listContextMenu.Size = new Size(230, 98);
            //
            // listMenuOpen
            //
            listMenuOpen.Name = "listMenuOpen";
            listMenuOpen.Size = new Size(230, 22);
            listMenuOpen.Text = "Abrir";
            listMenuOpen.Click += OnOpenFileRequested;
            //
            // listMenuOpenFolder
            //
            listMenuOpenFolder.Name = "listMenuOpenFolder";
            listMenuOpenFolder.Size = new Size(230, 22);
            listMenuOpenFolder.Text = "Abrir ubicación";
            listMenuOpenFolder.Click += OnOpenContainingFolderRequested;
            //
            // listMenuSeparator
            //
            listMenuSeparator.Name = "listMenuSeparator";
            listMenuSeparator.Size = new Size(227, 6);
            //
            // listMenuClean
            //
            listMenuClean.Name = "listMenuClean";
            listMenuClean.Size = new Size(230, 22);
            listMenuClean.Text = "Limpiar metadatos";
            listMenuClean.Click += OnCleanRequested;
            //
            // listMenuRemove
            //
            listMenuRemove.Name = "listMenuRemove";
            listMenuRemove.Size = new Size(230, 22);
            listMenuRemove.Text = "Quitar de la lista";
            listMenuRemove.Click += OnRemoveRequested;
            //
            // dataGridViewMetadata
            //
            dataGridViewMetadata.AllowUserToAddRows = false;
            dataGridViewMetadata.AllowUserToDeleteRows = false;
            dataGridViewMetadata.AllowUserToResizeRows = false;
            dataGridViewMetadata.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dataGridViewMetadata.BackgroundColor = SystemColors.Window;
            dataGridViewMetadata.BorderStyle = BorderStyle.FixedSingle;
            dataGridViewMetadata.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            dataGridViewMetadata.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewMetadata.Columns.AddRange(new DataGridViewColumn[] { columnProperty, columnValue });
            dataGridViewMetadata.ContextMenuStrip = gridContextMenu;
            dataGridViewMetadata.Dock = DockStyle.Fill;
            dataGridViewMetadata.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            dataGridViewMetadata.Location = new Point(4, 8);
            dataGridViewMetadata.MultiSelect = false;
            dataGridViewMetadata.Name = "dataGridViewMetadata";
            dataGridViewMetadata.RowHeadersVisible = false;
            dataGridViewMetadata.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewMetadata.Size = new Size(742, 561);
            dataGridViewMetadata.TabIndex = 0;
            dataGridViewMetadata.CellBeginEdit += OnMetadataCellBeginEdit;
            dataGridViewMetadata.CellClick += OnMetadataCellClick;
            dataGridViewMetadata.CellDoubleClick += OnMetadataCellDoubleClick;
            dataGridViewMetadata.CellMouseDown += OnMetadataCellMouseDown;
            dataGridViewMetadata.CellValidating += OnMetadataCellValidating;
            dataGridViewMetadata.CellEndEdit += OnMetadataCellEndEdit;
            dataGridViewMetadata.DataError += OnMetadataDataError;
            //
            // columnProperty
            //
            columnProperty.HeaderText = "Propiedad";
            columnProperty.MinimumWidth = 120;
            columnProperty.Name = "columnProperty";
            columnProperty.ReadOnly = true;
            columnProperty.Width = 280;
            //
            // columnValue
            //
            columnValue.HeaderText = "Valor";
            columnValue.MinimumWidth = 120;
            columnValue.Name = "columnValue";
            columnValue.Width = 440;
            //
            // gridContextMenu
            //
            gridContextMenu.Items.AddRange(new ToolStripItem[] { gridMenuCopy, gridMenuEdit, gridMenuMaps, gridMenuSeparator, gridMenuExpandAll, gridMenuCollapseAll });
            gridContextMenu.Name = "gridContextMenu";
            gridContextMenu.Size = new Size(230, 120);
            gridContextMenu.Opening += OnGridContextMenuOpening;
            //
            // gridMenuCopy
            //
            gridMenuCopy.Name = "gridMenuCopy";
            gridMenuCopy.Size = new Size(230, 22);
            gridMenuCopy.Text = "Copiar valor";
            gridMenuCopy.Click += OnCopyValueRequested;
            //
            // gridMenuEdit
            //
            gridMenuEdit.Name = "gridMenuEdit";
            gridMenuEdit.Size = new Size(230, 22);
            gridMenuEdit.Text = "Editar valor";
            gridMenuEdit.Click += OnEditValueRequested;
            //
            // gridMenuMaps
            //
            gridMenuMaps.Name = "gridMenuMaps";
            gridMenuMaps.Size = new Size(230, 22);
            gridMenuMaps.Text = "Ver en Google Maps";
            gridMenuMaps.Click += OnOpenMapsRequested;
            //
            // gridMenuSeparator
            //
            gridMenuSeparator.Name = "gridMenuSeparator";
            gridMenuSeparator.Size = new Size(227, 6);
            //
            // gridMenuExpandAll
            //
            gridMenuExpandAll.Name = "gridMenuExpandAll";
            gridMenuExpandAll.Size = new Size(230, 22);
            gridMenuExpandAll.Text = "Expandir todo";
            gridMenuExpandAll.Click += OnExpandAllRequested;
            //
            // gridMenuCollapseAll
            //
            gridMenuCollapseAll.Name = "gridMenuCollapseAll";
            gridMenuCollapseAll.Size = new Size(230, 22);
            gridMenuCollapseAll.Text = "Contraer todo";
            gridMenuCollapseAll.Click += OnCollapseAllRequested;
            //
            // MainForm
            //
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1100, 650);
            Controls.Add(splitContainer);
            Controls.Add(statusStrip);
            Controls.Add(toolStrip);
            Controls.Add(menuStrip);
            KeyPreview = true;
            MainMenuStrip = menuStrip;
            MinimumSize = new Size(820, 480);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "MetadataManager";
            DragEnter += OnFilesDragEnter;
            DragDrop += OnFilesDragDrop;
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            toolStrip.ResumeLayout(false);
            toolStrip.PerformLayout();
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            splitContainer.Panel1.ResumeLayout(false);
            splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer).EndInit();
            splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureThumbnail).EndInit();
            ((System.ComponentModel.ISupportInitialize)dataGridViewMetadata).EndInit();
            listContextMenu.ResumeLayout(false);
            gridContextMenu.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip;
        private ToolStripMenuItem menuFile;
        private ToolStripMenuItem menuAddFiles;
        private ToolStripMenuItem menuAddFolder;
        private ToolStripMenuItem menuAddFolderFiles;
        private ToolStripSeparator menuFileSeparator1;
        private ToolStripMenuItem menuExport;
        private ToolStripMenuItem menuExportBatch;
        private ToolStripSeparator menuFileSeparator2;
        private ToolStripMenuItem menuExit;
        private ToolStripMenuItem menuTools;
        private ToolStripMenuItem menuClean;
        private ToolStripSeparator menuToolsSeparator1;
        private ToolStripMenuItem menuRemove;
        private ToolStripMenuItem menuClearList;
        private ToolStripSeparator menuToolsSeparator2;
        private ToolStripMenuItem menuDetectExifTool;
        private ToolStripMenuItem menuConnectExifTool;
        private ToolStripMenuItem menuOptions;
        private ToolStripMenuItem menuHelp;
        private ToolStripMenuItem menuAbout;
        private ToolStrip toolStrip;
        private ToolStripButton buttonAddFiles;
        private ToolStripButton buttonAddFolder;
        private ToolStripButton buttonRemove;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripButton buttonClean;
        private ToolStripButton buttonExport;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripButton buttonExifTool;
        private ToolStripButton buttonOptions;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripLabel labelFilter;
        private ToolStripTextBox textBoxFilter;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel statusExifTool;
        private ToolStripProgressBar statusProgress;
        private SplitContainer splitContainer;
        private ListView listViewFiles;
        private ColumnHeader columnName;
        private ColumnHeader columnType;
        private ColumnHeader columnSize;
        private PictureBox pictureThumbnail;
        private ContextMenuStrip listContextMenu;
        private ToolStripMenuItem listMenuOpen;
        private ToolStripMenuItem listMenuOpenFolder;
        private ToolStripSeparator listMenuSeparator;
        private ToolStripMenuItem listMenuClean;
        private ToolStripMenuItem listMenuRemove;
        private DataGridView dataGridViewMetadata;
        private DataGridViewTextBoxColumn columnProperty;
        private DataGridViewTextBoxColumn columnValue;
        private ContextMenuStrip gridContextMenu;
        private ToolStripMenuItem gridMenuCopy;
        private ToolStripMenuItem gridMenuEdit;
        private ToolStripMenuItem gridMenuMaps;
        private ToolStripSeparator gridMenuSeparator;
        private ToolStripMenuItem gridMenuExpandAll;
        private ToolStripMenuItem gridMenuCollapseAll;
    }
}
