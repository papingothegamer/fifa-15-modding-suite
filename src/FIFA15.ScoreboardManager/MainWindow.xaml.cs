using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FifaLibrary;
using Microsoft.Win32;

namespace FIFA15.ScoreboardManager
{
    /// <summary>
    /// Represents a single entry inside a .big archive for display in the file list.
    /// </summary>
    public class BigFileEntry
    {
        public int Index { get; set; }
        public string FileName { get; set; }
        public bool IsDds { get; set; }
        public int CompressedSize { get; set; }
        public int UncompressedSize { get; set; }

        public string TypeIcon => IsDds ? "🖼" : "📄";

        public string DisplayName => !string.IsNullOrEmpty(FileName)
            ? FileName
            : $"File #{Index}";

        public string SizeInfo
        {
            get
            {
                string sizeStr = FormatSize(UncompressedSize);
                string typeStr = IsDds ? "DDS Texture" : "Data";
                return $"{typeStr}  •  {sizeStr}";
            }
        }

        private static string FormatSize(int bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }
    }

    public partial class MainWindow : Window
    {
        private FifaBigFile _bigFile;
        private string _bigFilePath;
        private List<BigFileEntry> _entries = new List<BigFileEntry>();
        private bool _hasUnsavedChanges;

        public MainWindow()
        {
            InitializeComponent();
        }

        // ═══════════════════════════════════════════
        //  OPEN .BIG ARCHIVE
        // ═══════════════════════════════════════════

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Open a new archive anyway?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            var dlg = new OpenFileDialog
            {
                Title = "Open FIFA 15 .big Archive",
                Filter = "BIG Files (*.big)|*.big|All Files (*.*)|*.*",
                RestoreDirectory = true
            };

            if (dlg.ShowDialog() != true) return;

            LoadBigFile(dlg.FileName);
        }

        private void LoadBigFile(string path)
        {
            try
            {
                SetStatus("Loading archive...");
                _bigFile = new FifaBigFile(path);
                _bigFilePath = path;
                _hasUnsavedChanges = false;

                // Load the internal file list
                _bigFile.LoadArchivedFiles();

                _entries.Clear();
                var files = _bigFile.Files;
                if (files != null)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        var f = files[i];
                        if (f == null) continue;

                        _entries.Add(new BigFileEntry
                        {
                            Index = i,
                            FileName = f.Name ?? "",
                            IsDds = f.IsDds(),
                            CompressedSize = f.CompressedSize,
                            UncompressedSize = f.UncompressedSize
                        });
                    }
                }

                LstFiles.ItemsSource = null;
                LstFiles.ItemsSource = _entries;

                TxtArchiveName.Text = Path.GetFileName(path);
                TxtFileCount.Text = $"{_entries.Count} file(s)";
                BtnSave.IsEnabled = true;
                BtnExtractAll.IsEnabled = _entries.Count > 0;

                // Clear preview
                ShowEmptyPreview();

                SetStatus($"Loaded {Path.GetFileName(path)} — {_entries.Count} file(s)");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load archive:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SetStatus("Error loading archive.");
            }
        }

        // ═══════════════════════════════════════════
        //  SAVE .BIG ARCHIVE
        // ═══════════════════════════════════════════

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_bigFile == null) return;

            try
            {
                SetStatus("Saving archive...");
                _bigFile.Save();
                _hasUnsavedChanges = false;
                SetStatus($"Saved {Path.GetFileName(_bigFilePath)} successfully.");
                MessageBox.Show(
                    "Archive saved successfully!",
                    "Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save archive:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SetStatus("Error saving archive.");
            }
        }

        // ═══════════════════════════════════════════
        //  FILE SELECTION → PREVIEW
        // ═══════════════════════════════════════════

        private void LstFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var entry = LstFiles.SelectedItem as BigFileEntry;
            if (entry == null)
            {
                ShowEmptyPreview();
                return;
            }

            BtnImport.IsEnabled = true;
            BtnExport.IsEnabled = true;

            if (entry.IsDds)
            {
                ShowImagePreview(entry);
            }
            else
            {
                ShowHexPreview(entry);
            }
        }

        private void ShowImagePreview(BigFileEntry entry)
        {
            try
            {
                var fifaFile = _bigFile.GetArchivedFile(entry.Index);
                var dds = new DdsFile();
                dds.Load(fifaFile);
                var bitmap = dds.GetBitmap();

                if (bitmap != null)
                {
                    ImgPreview.Source = ConvertBitmapToImageSource(bitmap);
                    TxtImageInfo.Text = $"{bitmap.Width} × {bitmap.Height}  •  {entry.SizeInfo}";
                    TxtPreviewTitle.Text = entry.DisplayName;

                    ImagePreviewPanel.Visibility = Visibility.Visible;
                    HexPreviewPanel.Visibility = Visibility.Collapsed;
                    EmptyPreviewPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Fallback to hex if bitmap conversion fails
                    ShowHexPreview(entry);
                }
            }
            catch
            {
                // Fallback to hex view if DDS loading fails
                ShowHexPreview(entry);
            }
        }

        private void ShowHexPreview(BigFileEntry entry)
        {
            try
            {
                var fifaFile = _bigFile.GetArchivedFile(entry.Index);
                var reader = fifaFile.GetReader();

                // Read up to 4KB for hex preview
                int bytesToRead = Math.Min(4096, fifaFile.UncompressedSize);
                byte[] data = reader.ReadBytes(bytesToRead);
                fifaFile.ReleaseReader(reader);

                var sb = new StringBuilder();
                int lineWidth = 16;
                for (int i = 0; i < data.Length; i += lineWidth)
                {
                    // Address
                    sb.Append($"{i:X8}  ");

                    // Hex bytes
                    for (int j = 0; j < lineWidth; j++)
                    {
                        if (i + j < data.Length)
                            sb.Append($"{data[i + j]:X2} ");
                        else
                            sb.Append("   ");

                        if (j == 7) sb.Append(" ");
                    }

                    sb.Append(" ");

                    // ASCII
                    for (int j = 0; j < lineWidth && i + j < data.Length; j++)
                    {
                        byte b = data[i + j];
                        sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                    }

                    sb.AppendLine();
                }

                if (data.Length < fifaFile.UncompressedSize)
                {
                    sb.AppendLine();
                    sb.AppendLine($"... showing first {bytesToRead} of {fifaFile.UncompressedSize} bytes");
                }

                TxtHexView.Text = sb.ToString();
                TxtPreviewTitle.Text = entry.DisplayName;

                HexPreviewPanel.Visibility = Visibility.Visible;
                ImagePreviewPanel.Visibility = Visibility.Collapsed;
                EmptyPreviewPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                TxtHexView.Text = $"Error reading file: {ex.Message}";
                HexPreviewPanel.Visibility = Visibility.Visible;
                ImagePreviewPanel.Visibility = Visibility.Collapsed;
                EmptyPreviewPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ShowEmptyPreview()
        {
            EmptyPreviewPanel.Visibility = Visibility.Visible;
            ImagePreviewPanel.Visibility = Visibility.Collapsed;
            HexPreviewPanel.Visibility = Visibility.Collapsed;
            TxtPreviewTitle.Text = "Select a file to preview";
            BtnImport.IsEnabled = false;
            BtnExport.IsEnabled = false;
        }

        // ═══════════════════════════════════════════
        //  EXPORT (single file)
        // ═══════════════════════════════════════════

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var entry = LstFiles.SelectedItem as BigFileEntry;
            if (entry == null || _bigFile == null) return;

            string defaultExt;
            string filter;

            if (entry.IsDds)
            {
                defaultExt = ".png";
                filter = "PNG Image (*.png)|*.png|DDS File (*.dds)|*.dds|All Files (*.*)|*.*";
            }
            else
            {
                defaultExt = ".bin";
                filter = "Binary File (*.bin)|*.bin|All Files (*.*)|*.*";
            }

            var dlg = new SaveFileDialog
            {
                Title = "Export File",
                FileName = Path.GetFileNameWithoutExtension(entry.DisplayName) + defaultExt,
                Filter = filter,
                RestoreDirectory = true
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                if (entry.IsDds && dlg.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    // Export as PNG
                    var fifaFile = _bigFile.GetArchivedFile(entry.Index);
                    var dds = new DdsFile();
                    dds.Load(fifaFile);
                    var bitmap = dds.GetBitmap();
                    bitmap.Save(dlg.FileName, ImageFormat.Png);
                }
                else
                {
                    // Export raw file using FifaLibrary
                    var fifaFile = _bigFile.GetArchivedFile(entry.Index);
                    fifaFile.Export(Path.GetDirectoryName(dlg.FileName));

                    // Rename the exported file to the user's chosen name
                    string exportedPath = Path.Combine(Path.GetDirectoryName(dlg.FileName), fifaFile.Name);
                    if (File.Exists(exportedPath) && exportedPath != dlg.FileName)
                    {
                        if (File.Exists(dlg.FileName)) File.Delete(dlg.FileName);
                        File.Move(exportedPath, dlg.FileName);
                    }
                }

                SetStatus($"Exported: {Path.GetFileName(dlg.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export:\n\n{ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════
        //  EXTRACT ALL
        // ═══════════════════════════════════════════

        private void BtnExtractAll_Click(object sender, RoutedEventArgs e)
        {
            if (_bigFile == null || _entries.Count == 0) return;

            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder to extract all files into",
                ShowNewFolderButton = true
            };

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            string outputDir = dlg.SelectedPath;
            int exported = 0;
            int failed = 0;

            // Create a subfolder named after the archive
            string archiveName = Path.GetFileNameWithoutExtension(_bigFilePath);
            string extractDir = Path.Combine(outputDir, archiveName);
            Directory.CreateDirectory(extractDir);

            // Also create a "textures" subfolder for PNG exports
            string texturesDir = Path.Combine(extractDir, "textures");
            Directory.CreateDirectory(texturesDir);

            foreach (var entry in _entries)
            {
                try
                {
                    var fifaFile = _bigFile.GetArchivedFile(entry.Index);

                    // Export raw file
                    fifaFile.Export(extractDir);

                    // If DDS, also export a PNG preview
                    if (entry.IsDds)
                    {
                        try
                        {
                            var dds = new DdsFile();
                            dds.Load(fifaFile);
                            var bitmap = dds.GetBitmap();
                            string pngName = Path.GetFileNameWithoutExtension(entry.DisplayName) + ".png";
                            bitmap.Save(Path.Combine(texturesDir, pngName), ImageFormat.Png);
                        }
                        catch { /* PNG export is best-effort */ }
                    }

                    exported++;
                }
                catch
                {
                    failed++;
                }
            }

            string msg = $"Extracted {exported} file(s) to:\n{extractDir}";
            if (failed > 0) msg += $"\n\n{failed} file(s) failed to extract.";

            MessageBox.Show(msg, "Extract Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            SetStatus($"Extracted {exported}/{_entries.Count} files.");
        }

        // ═══════════════════════════════════════════
        //  IMPORT (replace file)
        // ═══════════════════════════════════════════

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var entry = LstFiles.SelectedItem as BigFileEntry;
            if (entry == null || _bigFile == null) return;

            string filter;
            if (entry.IsDds)
            {
                filter = "PNG Image (*.png)|*.png|DDS File (*.dds)|*.dds|All Files (*.*)|*.*";
            }
            else
            {
                filter = "All Files (*.*)|*.*";
            }

            var dlg = new OpenFileDialog
            {
                Title = $"Import replacement for: {entry.DisplayName}",
                Filter = filter,
                RestoreDirectory = true
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                string importPath = dlg.FileName;

                if (entry.IsDds && importPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    // PNG → DDS replacement
                    // Load the existing DDS to get the format, then replace its bitmap
                    var fifaFile = _bigFile.GetArchivedFile(entry.Index);
                    var dds = new DdsFile();
                    dds.Load(fifaFile);

                    var newBitmap = new Bitmap(importPath);
                    dds.ReplaceBitmap(newBitmap);

                    // Save the modified DDS to a temp file, then import it
                    string tempDds = Path.GetTempFileName();
                    try
                    {
                        dds.Save(tempDds);
                        _bigFile.ImportReplacingFile(tempDds, entry.Index);
                    }
                    finally
                    {
                        if (File.Exists(tempDds)) File.Delete(tempDds);
                    }
                }
                else
                {
                    // Raw file replacement
                    _bigFile.ImportReplacingFile(importPath, entry.Index);
                }

                _hasUnsavedChanges = true;

                // Refresh the entry metadata
                var updatedFile = _bigFile.GetArchivedFile(entry.Index);
                entry.CompressedSize = updatedFile.CompressedSize;
                entry.UncompressedSize = updatedFile.UncompressedSize;

                // Refresh preview
                LstFiles_SelectionChanged(null, null);

                SetStatus($"Imported: {Path.GetFileName(importPath)} → {entry.DisplayName}  (unsaved)");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to import:\n\n{ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════

        private BitmapSource ConvertBitmapToImageSource(Bitmap bitmap)
        {
            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = ms;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }

        private void SetStatus(string text)
        {
            TxtStatus.Text = text;
        }
    }
}
