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
            button1 = new Button();
            ((System.ComponentModel.ISupportInitialize)dataGridViewMetadata).BeginInit();
            SuspendLayout();
            // 
            // listViewArchives
            // 
            listViewArchives.AllowDrop = true;
            listViewArchives.FullRowSelect = true;
            listViewArchives.GridLines = true;
            listViewArchives.Location = new Point(14, 16);
            listViewArchives.Margin = new Padding(3, 4, 3, 4);
            listViewArchives.Name = "listViewArchives";
            listViewArchives.Size = new Size(169, 560);
            listViewArchives.TabIndex = 0;
            listViewArchives.UseCompatibleStateImageBehavior = false;
            listViewArchives.View = View.Details;
            listViewArchives.SelectedIndexChanged += listViewArchives_SelectedIndexChanged;
            // 
            // dataGridViewMetadata
            // 
            dataGridViewMetadata.AllowUserToAddRows = false;
            dataGridViewMetadata.AllowUserToDeleteRows = false;
            dataGridViewMetadata.BackgroundColor = SystemColors.Window;
            dataGridViewMetadata.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewMetadata.Columns.AddRange(new DataGridViewColumn[] { colPropiedad, colValor });
            dataGridViewMetadata.GridColor = SystemColors.WindowText;
            dataGridViewMetadata.Location = new Point(190, 80);
            dataGridViewMetadata.Margin = new Padding(3, 4, 3, 4);
            dataGridViewMetadata.Name = "dataGridViewMetadata";
            dataGridViewMetadata.RowHeadersVisible = false;
            dataGridViewMetadata.RowHeadersWidth = 51;
            dataGridViewMetadata.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridViewMetadata.Size = new Size(858, 496);
            dataGridViewMetadata.TabIndex = 1;
            dataGridViewMetadata.CellContentClick += dataGridViewMetadata_CellContentClick;
            // 
            // colPropiedad
            // 
            colPropiedad.MinimumWidth = 6;
            colPropiedad.Name = "colPropiedad";
            colPropiedad.Width = 250;
            // 
            // colValor
            // 
            colValor.MinimumWidth = 6;
            colValor.Name = "colValor";
            colValor.Width = 600;
            // 
            // buttonAddFile
            // 
            buttonAddFile.Location = new Point(190, 16);
            buttonAddFile.Margin = new Padding(3, 4, 3, 4);
            buttonAddFile.Name = "buttonAddFile";
            buttonAddFile.Size = new Size(114, 56);
            buttonAddFile.TabIndex = 2;
            buttonAddFile.Text = "add file";
            buttonAddFile.UseVisualStyleBackColor = true;
            // 
            // buttonclean
            // 
            buttonclean.Location = new Point(311, 16);
            buttonclean.Margin = new Padding(3, 4, 3, 4);
            buttonclean.Name = "buttonclean";
            buttonclean.Size = new Size(114, 56);
            buttonclean.TabIndex = 3;
            buttonclean.Text = "drop file";
            buttonclean.UseVisualStyleBackColor = true;
            // 
            // buttonCleanMetadata
            // 
            buttonCleanMetadata.Location = new Point(432, 16);
            buttonCleanMetadata.Margin = new Padding(3, 4, 3, 4);
            buttonCleanMetadata.Name = "buttonCleanMetadata";
            buttonCleanMetadata.Size = new Size(114, 56);
            buttonCleanMetadata.TabIndex = 4;
            buttonCleanMetadata.Text = "Clean Metadata";
            buttonCleanMetadata.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            button1.Location = new Point(552, 17);
            button1.Name = "button1";
            button1.Size = new Size(115, 56);
            button1.TabIndex = 5;
            button1.Text = "Save Metadata";
            button1.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1060, 583);
            Controls.Add(button1);
            Controls.Add(buttonCleanMetadata);
            Controls.Add(buttonclean);
            Controls.Add(buttonAddFile);
            Controls.Add(dataGridViewMetadata);
            Controls.Add(listViewArchives);
            Margin = new Padding(3, 4, 3, 4);
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
        private Button button1;
    }
}
