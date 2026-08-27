namespace MetadataManager
{
    partial class OptionsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            groupClean = new GroupBox();
            labelOutputMode = new Label();
            radioOverwrite = new RadioButton();
            radioBackup = new RadioButton();
            radioCopy = new RadioButton();
            labelDate = new Label();
            textBoxDate = new TextBox();
            checkPreserveOrientation = new CheckBox();
            checkResetDates = new CheckBox();
            checkUseExifTool = new CheckBox();
            groupInterface = new GroupBox();
            labelLanguage = new Label();
            comboLanguage = new ComboBox();
            checkShowThumbnail = new CheckBox();
            labelRestart = new Label();
            buttonAccept = new Button();
            buttonCancel = new Button();
            groupClean.SuspendLayout();
            groupInterface.SuspendLayout();
            SuspendLayout();
            //
            // groupClean
            //
            groupClean.Controls.Add(labelOutputMode);
            groupClean.Controls.Add(radioOverwrite);
            groupClean.Controls.Add(radioBackup);
            groupClean.Controls.Add(radioCopy);
            groupClean.Controls.Add(labelDate);
            groupClean.Controls.Add(textBoxDate);
            groupClean.Controls.Add(checkPreserveOrientation);
            groupClean.Controls.Add(checkResetDates);
            groupClean.Controls.Add(checkUseExifTool);
            groupClean.Location = new Point(12, 12);
            groupClean.Name = "groupClean";
            groupClean.Size = new Size(456, 250);
            groupClean.TabIndex = 0;
            groupClean.TabStop = false;
            groupClean.Text = "Limpieza";
            //
            // labelOutputMode
            //
            labelOutputMode.AutoSize = true;
            labelOutputMode.Location = new Point(16, 28);
            labelOutputMode.Name = "labelOutputMode";
            labelOutputMode.Size = new Size(120, 15);
            labelOutputMode.TabIndex = 0;
            labelOutputMode.Text = "Archivos originales:";
            //
            // radioOverwrite
            //
            radioOverwrite.AutoSize = true;
            radioOverwrite.Location = new Point(28, 50);
            radioOverwrite.Name = "radioOverwrite";
            radioOverwrite.Size = new Size(160, 19);
            radioOverwrite.TabIndex = 1;
            radioOverwrite.Text = "Sobrescribir el original";
            radioOverwrite.UseVisualStyleBackColor = true;
            //
            // radioBackup
            //
            radioBackup.AutoSize = true;
            radioBackup.Location = new Point(28, 74);
            radioBackup.Name = "radioBackup";
            radioBackup.Size = new Size(200, 19);
            radioBackup.TabIndex = 2;
            radioBackup.Text = "Crear copia de seguridad .bak";
            radioBackup.UseVisualStyleBackColor = true;
            //
            // radioCopy
            //
            radioCopy.AutoSize = true;
            radioCopy.Location = new Point(28, 98);
            radioCopy.Name = "radioCopy";
            radioCopy.Size = new Size(280, 19);
            radioCopy.TabIndex = 3;
            radioCopy.Text = "Dejar el original y crear una copia limpia";
            radioCopy.UseVisualStyleBackColor = true;
            //
            // labelDate
            //
            labelDate.AutoSize = true;
            labelDate.Location = new Point(16, 134);
            labelDate.Name = "labelDate";
            labelDate.Size = new Size(140, 15);
            labelDate.TabIndex = 4;
            labelDate.Text = "Fecha de normalización:";
            //
            // textBoxDate
            //
            textBoxDate.Location = new Point(180, 131);
            textBoxDate.Name = "textBoxDate";
            textBoxDate.Size = new Size(180, 23);
            textBoxDate.TabIndex = 5;
            //
            // checkPreserveOrientation
            //
            checkPreserveOrientation.AutoSize = true;
            checkPreserveOrientation.Location = new Point(19, 166);
            checkPreserveOrientation.Name = "checkPreserveOrientation";
            checkPreserveOrientation.Size = new Size(260, 19);
            checkPreserveOrientation.TabIndex = 6;
            checkPreserveOrientation.Text = "Conservar la orientación de las imágenes";
            checkPreserveOrientation.UseVisualStyleBackColor = true;
            //
            // checkResetDates
            //
            checkResetDates.AutoSize = true;
            checkResetDates.Location = new Point(19, 190);
            checkResetDates.Name = "checkResetDates";
            checkResetDates.Size = new Size(320, 19);
            checkResetDates.TabIndex = 7;
            checkResetDates.Text = "Normalizar también las fechas del sistema de archivos";
            checkResetDates.UseVisualStyleBackColor = true;
            //
            // checkUseExifTool
            //
            checkUseExifTool.AutoSize = true;
            checkUseExifTool.Location = new Point(19, 214);
            checkUseExifTool.Name = "checkUseExifTool";
            checkUseExifTool.Size = new Size(250, 19);
            checkUseExifTool.TabIndex = 8;
            checkUseExifTool.Text = "Usar ExifTool cuando esté disponible";
            checkUseExifTool.UseVisualStyleBackColor = true;
            //
            // groupInterface
            //
            groupInterface.Controls.Add(labelLanguage);
            groupInterface.Controls.Add(comboLanguage);
            groupInterface.Controls.Add(checkShowThumbnail);
            groupInterface.Controls.Add(labelRestart);
            groupInterface.Location = new Point(12, 272);
            groupInterface.Name = "groupInterface";
            groupInterface.Size = new Size(456, 122);
            groupInterface.TabIndex = 1;
            groupInterface.TabStop = false;
            groupInterface.Text = "Interfaz";
            //
            // labelLanguage
            //
            labelLanguage.AutoSize = true;
            labelLanguage.Location = new Point(16, 31);
            labelLanguage.Name = "labelLanguage";
            labelLanguage.Size = new Size(48, 15);
            labelLanguage.TabIndex = 0;
            labelLanguage.Text = "Idioma:";
            //
            // comboLanguage
            //
            comboLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            comboLanguage.Location = new Point(180, 28);
            comboLanguage.Name = "comboLanguage";
            comboLanguage.Size = new Size(250, 23);
            comboLanguage.TabIndex = 1;
            //
            // checkShowThumbnail
            //
            checkShowThumbnail.AutoSize = true;
            checkShowThumbnail.Location = new Point(19, 62);
            checkShowThumbnail.Name = "checkShowThumbnail";
            checkShowThumbnail.Size = new Size(240, 19);
            checkShowThumbnail.TabIndex = 2;
            checkShowThumbnail.Text = "Mostrar la vista previa de la imagen";
            checkShowThumbnail.UseVisualStyleBackColor = true;
            //
            // labelRestart
            //
            labelRestart.AutoSize = true;
            labelRestart.ForeColor = SystemColors.GrayText;
            labelRestart.Location = new Point(16, 92);
            labelRestart.Name = "labelRestart";
            labelRestart.Size = new Size(380, 15);
            labelRestart.TabIndex = 3;
            labelRestart.Text = "El cambio de idioma se aplicará al reiniciar la aplicación.";
            //
            // buttonAccept
            //
            buttonAccept.DialogResult = DialogResult.OK;
            buttonAccept.Location = new Point(292, 406);
            buttonAccept.Name = "buttonAccept";
            buttonAccept.Size = new Size(85, 28);
            buttonAccept.TabIndex = 2;
            buttonAccept.Text = "Aceptar";
            buttonAccept.UseVisualStyleBackColor = true;
            buttonAccept.Click += OnAcceptClicked;
            //
            // buttonCancel
            //
            buttonCancel.DialogResult = DialogResult.Cancel;
            buttonCancel.Location = new Point(383, 406);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(85, 28);
            buttonCancel.TabIndex = 3;
            buttonCancel.Text = "Cancelar";
            buttonCancel.UseVisualStyleBackColor = true;
            //
            // OptionsForm
            //
            AcceptButton = buttonAccept;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = buttonCancel;
            ClientSize = new Size(480, 446);
            Controls.Add(buttonCancel);
            Controls.Add(buttonAccept);
            Controls.Add(groupInterface);
            Controls.Add(groupClean);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "OptionsForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Opciones";
            groupClean.ResumeLayout(false);
            groupClean.PerformLayout();
            groupInterface.ResumeLayout(false);
            groupInterface.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private GroupBox groupClean;
        private Label labelOutputMode;
        private RadioButton radioOverwrite;
        private RadioButton radioBackup;
        private RadioButton radioCopy;
        private Label labelDate;
        private TextBox textBoxDate;
        private CheckBox checkPreserveOrientation;
        private CheckBox checkResetDates;
        private CheckBox checkUseExifTool;
        private GroupBox groupInterface;
        private Label labelLanguage;
        private ComboBox comboLanguage;
        private CheckBox checkShowThumbnail;
        private Label labelRestart;
        private Button buttonAccept;
        private Button buttonCancel;
    }
}
