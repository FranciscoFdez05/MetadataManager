using System;
using System.Globalization;
using System.Windows.Forms;
using MetadataManager.Resources;
using MetadataManager.Services;

namespace MetadataManager
{
    /// <summary>
    /// Diálogo de preferencias. Trabaja sobre una copia de la configuración:
    /// solo se escriben los cambios si el usuario acepta.
    /// </summary>
    public partial class OptionsForm : Form
    {
        private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

        public OptionsForm(AppSettings settings)
        {
            InitializeComponent();
            ApplyLocalization();

            Settings = settings.Clone();

            comboLanguage.Items.AddRange(new object[]
            {
                Strings.OptionsLanguageAuto,
                Strings.OptionsLanguageEs,
                Strings.OptionsLanguageEn
            });

            comboLanguage.SelectedIndex = Settings.Language switch
            {
                Localization.Spanish => 1,
                Localization.English => 2,
                _ => 0
            };

            radioOverwrite.Checked = Settings.OutputMode == CleanOutputMode.Overwrite;
            radioBackup.Checked = Settings.OutputMode == CleanOutputMode.Backup;
            radioCopy.Checked = Settings.OutputMode == CleanOutputMode.Copy;

            textBoxDate.Text = Settings.NormalizationDate;
            checkPreserveOrientation.Checked = Settings.PreserveOrientation;
            checkResetDates.Checked = Settings.ResetFileDates;
            checkUseExifTool.Checked = Settings.UseExifTool;
            checkShowThumbnail.Checked = Settings.ShowThumbnail;

            checkUseExifTool.Enabled = ExifTool.IsAvailable;
        }

        /// <summary>Configuración resultante; solo es válida si el diálogo devolvió OK.</summary>
        public AppSettings Settings { get; }

        private void ApplyLocalization()
        {
            Text = Strings.TitleOptions;
            groupClean.Text = Strings.OptionsGroupClean;
            labelOutputMode.Text = Strings.OptionsOutputMode;
            radioOverwrite.Text = Strings.OptionsModeOverwrite;
            radioBackup.Text = Strings.OptionsModeBackup;
            radioCopy.Text = Strings.OptionsModeCopy;
            labelDate.Text = Strings.OptionsDate;
            checkPreserveOrientation.Text = Strings.OptionsPreserveOrientation;
            checkResetDates.Text = Strings.OptionsResetDates;
            checkUseExifTool.Text = Strings.OptionsUseExifTool;
            groupInterface.Text = Strings.OptionsGroupInterface;
            labelLanguage.Text = Strings.OptionsLanguage;
            checkShowThumbnail.Text = Strings.OptionsShowThumbnail;
            labelRestart.Text = Strings.OptionsRestart;
            buttonAccept.Text = Strings.ButtonAccept;
            buttonCancel.Text = Strings.ButtonCancel;
        }

        private void OnAcceptClicked(object? sender, EventArgs e)
        {
            string date = textBoxDate.Text.Trim();

            if (!DateTime.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                MessageBox.Show(this, Strings.OptionsInvalidDate, Strings.TitleOptions,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

                DialogResult = DialogResult.None;
                textBoxDate.Focus();
                textBoxDate.SelectAll();
                return;
            }

            Settings.NormalizationDate = date;
            Settings.OutputMode = radioCopy.Checked
                ? CleanOutputMode.Copy
                : radioBackup.Checked ? CleanOutputMode.Backup : CleanOutputMode.Overwrite;

            Settings.PreserveOrientation = checkPreserveOrientation.Checked;
            Settings.ResetFileDates = checkResetDates.Checked;
            Settings.UseExifTool = checkUseExifTool.Checked;
            Settings.ShowThumbnail = checkShowThumbnail.Checked;

            Settings.Language = comboLanguage.SelectedIndex switch
            {
                1 => Localization.Spanish,
                2 => Localization.English,
                _ => Localization.Automatic
            };
        }
    }
}
