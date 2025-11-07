namespace MetadataManager
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            listViewArchives = new ListView();
            dataGridViewMetadata = new DataGridView();
            colPropiedad = new DataGridViewTextBoxColumn();
            colValor = new DataGridViewTextBoxColumn();
            buttonAddFile = new Button();
            buttonclean = new Button();
            buttonCleanMetadata = new Button();
            ((System.ComponentModel.ISupportInitialize)dataGridViewMetadata).BeginInit();
            SuspendLayout();
            // 
            // listViewArchives
            // 
            listViewArchives.AllowDrop = true;
            listViewArchives.FullRowSelect = true;
            listViewArchives.GridLines = true;
            listViewArchives.Location = new Point(12, 12);
            listViewArchives.Name = "listViewArchives";
            listViewArchives.Size = new Size(148, 426);
            listViewArchives.TabIndex = 0;
            listViewArchives.UseCompatibleStateImageBehavior = false;
            listViewArchives.View = View.Details;
            listViewArchives.SelectedIndexChanged += listViewArchives_SelectedIndexChanged;
            // 
            // dataGridViewMetadata
            // 
            dataGridViewMetadata.AllowUserToAddRows = false;
            dataGridViewMetadata.AllowUserToDeleteRows = false;
            // Desactivar AutoSize Fill para permitir barra horizontal
            dataGridViewMetadata.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dataGridViewMetadata.BackgroundColor = SystemColors.Window;
            dataGridViewMetadata.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewMetadata.Columns.AddRange(new DataGridViewColumn[] { colPropiedad, colValor });
            // forzar barras de desplazamiento cuando haga falta
            dataGridViewMetadata.ScrollBars = ScrollBars.Both;
            dataGridViewMetadata.GridColor = SystemColors.WindowText;
            dataGridViewMetadata.Location = new Point(166, 60);
            dataGridViewMetadata.Name = "dataGridViewMetadata";
            dataGridViewMetadata.RowHeadersVisible = false;
            dataGridViewMetadata.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewMetadata.Size = new Size(630, 378);
            dataGridViewMetadata.TabIndex = 1;
            dataGridViewMetadata.CellContentClick += dataGridViewMetadata_CellContentClick;
            // establecer anchos explícitos para que pueda aparecer la barra horizontal
            colPropiedad.Width = 250;
            colValor.Width = 600;
            // 
            // buttonAddFile
            // 
            buttonAddFile.Location = new Point(166, 12);
            buttonAddFile.Name = "buttonAddFile";
            buttonAddFile.Size = new Size(100, 42);
            buttonAddFile.TabIndex = 2;
            buttonAddFile.Text = "add file";
            buttonAddFile.UseVisualStyleBackColor = true;
            // 
            // buttonclean
            // 
            buttonclean.Location = new Point(272, 12);
            buttonclean.Name = "buttonclean";
            buttonclean.Size = new Size(100, 42);
            buttonclean.TabIndex = 3;
            buttonclean.Text = "clean";
            buttonclean.UseVisualStyleBackColor = true;
            // 
            // buttonCleanMetadata
            // 
            buttonCleanMetadata.Location = new Point(378, 12);
            buttonCleanMetadata.Name = "buttonCleanMetadata";
            buttonCleanMetadata.Size = new Size(100, 42);
            buttonCleanMetadata.TabIndex = 4;
            buttonCleanMetadata.Text = "Clean Metadata";
            buttonCleanMetadata.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(802, 444);
            Controls.Add(buttonCleanMetadata);
            Controls.Add(buttonclean);
            Controls.Add(buttonAddFile);
            Controls.Add(dataGridViewMetadata);
            Controls.Add(listViewArchives);
            Name = "Form1";
            Text = "MetadataManager Open Source 1.0.0";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)dataGridViewMetadata).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private ListView listViewArchives;
        private DataGridView dataGridViewMetadata;
        private Button buttonAddFile;
        private Button buttonclean;
        private Button buttonCleanMetadata;
        private DataGridViewTextBoxColumn colPropiedad;
        private DataGridViewTextBoxColumn colValor;
    }
}
