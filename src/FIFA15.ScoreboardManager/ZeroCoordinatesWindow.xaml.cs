using System;
using System.Windows;
using FifaLibrary;

namespace FIFA15.ScoreboardManager
{
    public partial class ZeroCoordinatesWindow : Window
    {
        private FifaBigFile _bigFile;
        private MainWindow _parent;

        public ZeroCoordinatesWindow(FifaBigFile bigFile, MainWindow parent)
        {
            InitializeComponent();
            _bigFile = bigFile;
            _parent = parent;
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            string xHex = TxtOffsetX.Text.Trim();
            string yHex = TxtOffsetY.Text.Trim();

            if (string.IsNullOrEmpty(xHex) && string.IsNullOrEmpty(yHex))
            {
                this.Close();
                return;
            }

            try
            {
                // The .dat file is typically the first file in the archive, or the only non-image file.
                // We'll just modify file index 0, which is always the .dat/.hex file in overlays.
                var datFile = _bigFile.GetArchivedFile(0);
                var reader = datFile.GetReader();
                byte[] data = reader.ReadBytes(datFile.UncompressedSize);
                datFile.ReleaseReader(reader);

                float offscreenValue = -10000f;
                byte[] valBytes = BitConverter.GetBytes(offscreenValue);

                bool changed = false;

                if (!string.IsNullOrEmpty(xHex))
                {
                    int xOffset = Convert.ToInt32(xHex, 16);
                    if (xOffset + 3 < data.Length)
                    {
                        Array.Copy(valBytes, 0, data, xOffset, 4);
                        changed = true;
                    }
                }

                if (!string.IsNullOrEmpty(yHex))
                {
                    int yOffset = Convert.ToInt32(yHex, 16);
                    if (yOffset + 3 < data.Length)
                    {
                        Array.Copy(valBytes, 0, data, yOffset, 4);
                        changed = true;
                    }
                }

                if (changed)
                {
                    // Create a temp file to import back into the archive
                    string tempFile = System.IO.Path.GetTempFileName();
                    System.IO.File.WriteAllBytes(tempFile, data);
                    _bigFile.ImportReplacingFile(tempFile, 0);
                    System.IO.File.Delete(tempFile);

                    _parent.MarkUnsavedChanges();
                    MessageBox.Show("Coordinates successfully pushed off-screen!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying offsets: {ex.Message}\nMake sure you entered valid hexadecimal offsets (e.g. 143C).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
