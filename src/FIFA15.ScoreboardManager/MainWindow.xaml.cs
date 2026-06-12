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

                // Read full file for hex preview to allow saving
                byte[] data = reader.ReadBytes(fifaFile.UncompressedSize);
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
                        sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                    }

                    sb.AppendLine();
                }

                _isUpdatingHex = true;
                TxtHexView.Text = sb.ToString();
                TxtPreviewTitle.Text = entry.DisplayName;
                _isUpdatingHex = false;
                BtnSaveHex.IsEnabled = false;

                // If this is a scoreboard hex file (e.g. index 0 or 1), show compositor button
                BtnViewCompositor.Visibility = (entry.Index == 0 || entry.Index == 1) ? Visibility.Visible : Visibility.Collapsed;

                HexPreviewPanel.Visibility = Visibility.Visible;
                ImagePreviewPanel.Visibility = Visibility.Collapsed;
                EmptyPreviewPanel.Visibility = Visibility.Collapsed;
                CompositorPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                _isUpdatingHex = true;
                TxtHexView.Text = $"Error reading file: {ex.Message}";
                _isUpdatingHex = false;
                HexPreviewPanel.Visibility = Visibility.Visible;
                ImagePreviewPanel.Visibility = Visibility.Collapsed;
                EmptyPreviewPanel.Visibility = Visibility.Collapsed;
                CompositorPanel.Visibility = Visibility.Collapsed;
            }
        }

        private bool _isUpdatingHex;

        private void TxtHexView_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUpdatingHex)
            {
                BtnSaveHex.IsEnabled = true;
            }
        }

        private void BtnSaveHex_Click(object sender, RoutedEventArgs e)
        {
            var entry = LstFiles.SelectedItem as BigFileEntry;
            if (entry == null || _bigFile == null) return;

            try
            {
                // Parse the hex text
                var lines = TxtHexView.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var byteList = new List<byte>();

                foreach (var line in lines)
                {
                    if (line.Length < 10) continue;
                    // Format: 00000000  00 01 02 ... (up to 16 bytes)
                    string hexPart = line.Substring(10, Math.Min(line.Length - 10, 50)).Trim(); // 50 chars covers 16 bytes + spaces
                    string[] tokens = hexPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    foreach (var token in tokens)
                    {
                        if (token.Length == 2 && byte.TryParse(token, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                        {
                            byteList.Add(b);
                        }
                    }
                }

                byte[] newData = byteList.ToArray();

                // Save back to big file
                string tempFile = Path.GetTempFileName();
                File.WriteAllBytes(tempFile, newData);
                _bigFile.ImportReplacingFile(tempFile, entry.Index);
                File.Delete(tempFile);

                _hasUnsavedChanges = true;
                BtnSaveHex.IsEnabled = false;

                // Refresh metadata
                var updatedFile = _bigFile.GetArchivedFile(entry.Index);
                entry.CompressedSize = updatedFile.CompressedSize;
                entry.UncompressedSize = updatedFile.UncompressedSize;

                SetStatus($"Saved changes to {entry.DisplayName}  (unsaved archive)");
                MessageBox.Show("Hex data updated successfully. Don't forget to 'Save .big'.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to parse and save hex:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        //  COMPOSITOR
        // ═══════════════════════════════════════════

        private class CompositorElement
        {
            public int TextureIndex { get; set; }
            public int OffsetX { get; set; }
            public int OffsetY { get; set; }
            public System.Windows.Controls.Image ImageControl { get; set; }
        }

        private List<CompositorElement> _compositorElements = new List<CompositorElement>();
        private UIElement _draggedElement;
        private System.Windows.Point _dragStartPoint;
        private double _originalLeft;
        private double _originalTop;

        private void BtnViewCompositor_Click(object sender, RoutedEventArgs e)
        {
            var entry = LstFiles.SelectedItem as BigFileEntry;
            if (entry == null || _bigFile == null) return;

            HexPreviewPanel.Visibility = Visibility.Collapsed;
            CompositorPanel.Visibility = Visibility.Visible;
            
            LoadCompositor(entry);
        }

        private void BtnBackToHex_Click(object sender, RoutedEventArgs e)
        {
            CompositorPanel.Visibility = Visibility.Collapsed;
            HexPreviewPanel.Visibility = Visibility.Visible;
        }

        private void LoadCompositor(BigFileEntry hexFileEntry)
        {
            CompositorCanvas.Children.Clear();
            _compositorElements.Clear();

            try
            {
                var fifaFile = _bigFile.GetArchivedFile(hexFileEntry.Index);
                var reader = fifaFile.GetReader();
                byte[] data = reader.ReadBytes(fifaFile.UncompressedSize);
                fifaFile.ReleaseReader(reader);

                // Define 9002 Scoreboard Template based on Excel mappings
                var template = new List<(int TextureIdx, int OffsetX, int OffsetY)>
                {
                    (21, 0x143C, 0x1440), // Main Texture (Overall Position)
                    (50, 0x231C, 0x2320), // Home Team Colour
                    (53, 0x22CC, 0x22D0)  // Away Team Colour
                };

                foreach (var item in template)
                {
                    // Check if texture exists
                    if (item.TextureIdx < _bigFile.Files.Length && _bigFile.Files[item.TextureIdx] != null && _bigFile.Files[item.TextureIdx].IsDds())
                    {
                        var ddsFile = _bigFile.GetArchivedFile(item.TextureIdx);
                        var dds = new DdsFile();
                        dds.Load(ddsFile);
                        var bmp = dds.GetBitmap();

                        if (bmp != null)
                        {
                            var img = new System.Windows.Controls.Image
                            {
                                Source = ConvertBitmapToImageSource(bmp),
                                Width = bmp.Width,
                                Height = bmp.Height,
                                Cursor = System.Windows.Input.Cursors.SizeAll
                            };

                            // Read coordinates from hex (Float32 Little Endian)
                            float x = 0;
                            float y = 0;
                            if (item.OffsetX + 3 < data.Length)
                            {
                                x = BitConverter.ToSingle(data, item.OffsetX);
                            }
                            if (item.OffsetY + 3 < data.Length)
                            {
                                y = BitConverter.ToSingle(data, item.OffsetY);
                            }

                            // Translate game coordinates to Canvas coordinates
                            // For simplicity, we assume origin (0,0) is center of screen for game, and translate to top-left of 1920x1080 canvas
                            // Actually, many EA overlays use (0,0) as center.
                            double canvasX = (1920 / 2.0) + x;
                            double canvasY = (1080 / 2.0) - y; // Usually Y is inverted in 3D space

                            Canvas.SetLeft(img, canvasX);
                            Canvas.SetTop(img, canvasY);

                            img.MouseDown += (s, e) =>
                            {
                                _draggedElement = img;
                                _dragStartPoint = e.GetPosition(CompositorCanvas);
                                _originalLeft = Canvas.GetLeft(img);
                                _originalTop = Canvas.GetTop(img);
                                img.CaptureMouse();
                            };

                            CompositorCanvas.Children.Add(img);

                            _compositorElements.Add(new CompositorElement
                            {
                                TextureIndex = item.TextureIdx,
                                OffsetX = item.OffsetX,
                                OffsetY = item.OffsetY,
                                ImageControl = img
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load compositor:\n{ex.Message}", "Error");
            }
        }

        private void CompositorCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_draggedElement != null)
            {
                var currentPosition = e.GetPosition(CompositorCanvas);
                double offsetX = currentPosition.X - _dragStartPoint.X;
                double offsetY = currentPosition.Y - _dragStartPoint.Y;

                Canvas.SetLeft(_draggedElement, _originalLeft + offsetX);
                Canvas.SetTop(_draggedElement, _originalTop + offsetY);
            }
        }

        private void CompositorCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_draggedElement != null)
            {
                _draggedElement.ReleaseMouseCapture();
                _draggedElement = null;
            }
        }

        private void BtnSaveCompositor_Click(object sender, RoutedEventArgs e)
        {
            var entry = LstFiles.SelectedItem as BigFileEntry;
            if (entry == null || _bigFile == null) return;

            try
            {
                var fifaFile = _bigFile.GetArchivedFile(entry.Index);
                var reader = fifaFile.GetReader();
                byte[] data = reader.ReadBytes(fifaFile.UncompressedSize);
                fifaFile.ReleaseReader(reader);

                foreach (var el in _compositorElements)
                {
                    double canvasX = Canvas.GetLeft(el.ImageControl);
                    double canvasY = Canvas.GetTop(el.ImageControl);

                    // Translate Canvas coordinates back to game coordinates
                    float x = (float)(canvasX - (1920 / 2.0));
                    float y = (float)((1080 / 2.0) - canvasY);

                    byte[] xBytes = BitConverter.GetBytes(x);
                    byte[] yBytes = BitConverter.GetBytes(y);

                    if (el.OffsetX + 3 < data.Length)
                        Array.Copy(xBytes, 0, data, el.OffsetX, 4);
                    
                    if (el.OffsetY + 3 < data.Length)
                        Array.Copy(yBytes, 0, data, el.OffsetY, 4);
                }

                string tempFile = Path.GetTempFileName();
                File.WriteAllBytes(tempFile, data);
                _bigFile.ImportReplacingFile(tempFile, entry.Index);
                File.Delete(tempFile);

                _hasUnsavedChanges = true;
                
                // Refresh Hex View if we go back to it
                ShowHexPreview(entry);
                CompositorPanel.Visibility = Visibility.Visible;
                HexPreviewPanel.Visibility = Visibility.Collapsed;

                SetStatus("Saved compositor layout to memory (unsaved archive)");
                MessageBox.Show("Layout offsets saved to Hex. Don't forget to 'Save .big'.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save layout:\n{ex.Message}", "Error");
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
