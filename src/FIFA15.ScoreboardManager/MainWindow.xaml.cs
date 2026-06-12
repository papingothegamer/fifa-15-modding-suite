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
        private string _loadedFileName;

        private readonly Dictionary<string, string> _overlayDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "overlay_9001.big", "Stamina Bar Left" },
            { "overlay_9002.big", "Scoreboard" },
            { "overlay_9003.big", "Intro/Match Graphics" },
            { "overlay_9009.big", "Stamina Bar Right" },
            { "overlay_9012.big", "Referee/Foul Stats" },
            { "overlay_9013.big", "Bookings/Cards" },
            { "overlay_9015.big", "Score Update" },
            { "overlay_9020.big", "Extra Time/Penalty" },
            { "overlay_9021.big", "Substitution" },
            { "overlay_9042.big", "Match Stats/Possession" },
            { "overlay_9044.big", "Added Time" },
            { "overlay_9105.big", "Formation/Lineup" }
        };

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
                _bigFilePath = path;
                _loadedFileName = Path.GetFileName(path);
                _bigFile = new FifaBigFile(path);
                _hasUnsavedChanges = false;

                // Clear directory mode if active
                _overlayDirEntries.Clear();
                _overlayDirPath = null;
                BtnBatchImport.IsEnabled = false;

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

                string displayName = _loadedFileName;
                if (_overlayDictionary.TryGetValue(_loadedFileName, out string knownName))
                {
                    displayName = $"{_loadedFileName} ({knownName})";
                }

                TxtArchiveName.Text = displayName;
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

        public void MarkUnsavedChanges()
        {
            _hasUnsavedChanges = true;
            SetStatus("Coordinates updated (unsaved).");
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_bigFile == null || !_hasUnsavedChanges) return;

            try
            {
                SetStatus("Saving archive...");
                _bigFile.Save();
                
                // FIFA 15 requires 'BIGF' magic bytes. FifaLibrary14 saves as 'BIG4'. 
                // We must patch it to prevent game crashes!
                PatchToBigF(_bigFilePath);

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

            // Directory mode: show overlay preview instead of single-file preview
            if (_overlayDirEntries.Count > 0 && entry.Index >= 0 && entry.Index < _overlayDirEntries.Count)
            {
                ShowDirectoryOverlayPreview(_overlayDirEntries[entry.Index]);
                return;
            }

            PreviewFile(entry);
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Future implementation
        }

        private void PreviewFile(BigFileEntry entry)
        {
            BtnExport.IsEnabled = true;
            BtnImport.IsEnabled = true;
            BtnRemove.IsEnabled = true;

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

                // Save back to big file — write next to the .big to avoid AV issues
                string bigDir = Path.GetDirectoryName(_bigFilePath);
                string tempFile = Path.Combine(bigDir, "_temp_hex_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".bin");
                File.WriteAllBytes(tempFile, newData);
                _bigFile.ImportReplacingFile(tempFile, entry.Index);
                try { File.Delete(tempFile); } catch { }

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
            bool isSelected = LstFiles.SelectedItem != null;
            BtnExport.IsEnabled = isSelected;
            BtnImport.IsEnabled = isSelected;
            BtnRemove.IsEnabled = isSelected;
            TxtPreviewTitle.Text = "Select a file to preview";
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
        //  PREVIEW PANEL
        // ═══════════════════════════════════════════

        public static void PatchToBigF(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        fs.Seek(0, SeekOrigin.Begin);
                        fs.Write(new byte[] { 0x42, 0x49, 0x47, 0x46 }, 0, 4); // B I G F
                    }
                }
            }
            catch { /* Best effort */ }
        }

        private void BtnCreator_Click(object sender, RoutedEventArgs e)
        {
            var creatorWindow = new CreatorWindow();
            creatorWindow.Owner = this;
            creatorWindow.ShowDialog();
        }

        private void BtnExplorer_Click(object sender, RoutedEventArgs e)
        {
            var explorerWindow = new PackExplorerWindow();
            explorerWindow.Owner = this;
            explorerWindow.ShowDialog();
        }

        // ═══════════════════════════════════════════
        //  FILE EXTRACTION
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
                    // Export to disk first because memory streams in FifaLibrary are not expandable
                    // Write temp files NEXT TO the .big file to avoid AV locking in %TEMP%
                    var fifaFile = _bigFile.GetArchivedFile(entry.Index);
                    string bigDir = Path.GetDirectoryName(_bigFilePath);
                    string origDdsPath = Path.Combine(bigDir, "_temp_orig_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".dds");
                    fifaFile.Export(origDdsPath);

                    var dds = new DdsFile();
                    dds.Load(origDdsPath);

                    var newBitmap = new Bitmap(importPath);
                    dds.ReplaceBitmap(newBitmap);

                    // Save the modified DDS to a local temp file, then import it
                    string tempDds = Path.Combine(bigDir, "_temp_mod_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".dds");
                    dds.Save(tempDds);
                    _bigFile.ImportReplacingFile(tempDds, entry.Index);

                    // Clean up local temp files
                    try { File.Delete(origDdsPath); } catch { }
                    try { File.Delete(tempDds); } catch { }
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

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var entry = LstFiles.SelectedItem as BigFileEntry;
            if (entry == null || _bigFile == null) return;

            var result = MessageBox.Show(
                "This will replace the image with a 1x1 transparent texture. Do you want to continue?",
                "Remove Image", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    string tempPng = Path.Combine(Path.GetTempPath(), "transparent_1x1.png");
                    using (var bmp = new System.Drawing.Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        bmp.SetPixel(0, 0, System.Drawing.Color.Transparent);
                        bmp.Save(tempPng, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    if (entry.IsDds)
                    {
                        var fifaFile = _bigFile.GetArchivedFile(entry.Index);
                        string bigDir2 = Path.GetDirectoryName(_bigFilePath);
                        string origDdsPath = Path.Combine(bigDir2, "_temp_orig_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".dds");
                        fifaFile.Export(origDdsPath);

                        var dds = new DdsFile();
                        dds.Load(origDdsPath);

                        var newBitmap = new System.Drawing.Bitmap(tempPng);
                        dds.ReplaceBitmap(newBitmap);

                        string tempDds = Path.Combine(bigDir2, "_temp_mod_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".dds");
                        dds.Save(tempDds);
                        _bigFile.ImportReplacingFile(tempDds, entry.Index);

                        try { File.Delete(origDdsPath); } catch { }
                        try { File.Delete(tempDds); } catch { }
                    }
                    else
                    {
                        _bigFile.ImportReplacingFile(tempPng, entry.Index);
                    }

                    _hasUnsavedChanges = true;

                    var updatedFile = _bigFile.GetArchivedFile(entry.Index);
                    entry.CompressedSize = updatedFile.CompressedSize;
                    entry.UncompressedSize = updatedFile.UncompressedSize;

                    LstFiles_SelectionChanged(null, null);

                    var zeroWin = new ZeroCoordinatesWindow(_bigFile, this);
                    zeroWin.Owner = this;
                    zeroWin.ShowDialog();

                    SetStatus("Image hidden and coordinates updated (unsaved).");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error removing image:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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

                // Write next to the .big file to avoid AV issues
                string bigDir3 = Path.GetDirectoryName(_bigFilePath);
                string tempFile = Path.Combine(bigDir3, "_temp_comp_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".bin");
                File.WriteAllBytes(tempFile, data);
                _bigFile.ImportReplacingFile(tempFile, entry.Index);
                try { File.Delete(tempFile); } catch { }

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
        //  OVERLAY DIRECTORY MODE
        // ═══════════════════════════════════════════

        // Mapping of overlay IDs to friendly names
        private readonly Dictionary<string, string> _overlayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "9001", "Stamina Bar Left" },
            { "9002", "Scoreboard" },
            { "9003", "Penalties" },
            { "9009", "Stamina Bar Right" },
            { "9012", "Goalscorer Live" },
            { "9013", "Cards/Injury Big" },
            { "9015", "Scoreboard Kick-Off" },
            { "9018", "Line-up / Formation" },
            { "9020", "Referee" },
            { "9021", "Substitutions Big" },
            { "9042", "Opening / Match Intro" },
            { "9044", "Commentary" },
            { "9045", "Table View" },
            { "9072", "Cards/Injury Small" },
            { "9073", "Substitutions Small" },
            { "9074", "Statistics" },
            { "9098", "Goal Decision" },
            { "9102", "Line up bottom screen" },
            { "9105", "TV Logos" }
        };

        private string _overlayDirPath;
        private List<OverlayDirEntry> _overlayDirEntries = new List<OverlayDirEntry>();

        private class OverlayDirEntry
        {
            public string FilePath { get; set; }
            public string FileName { get; set; }
            public string OverlayId { get; set; } // e.g. "9002"
            public string FriendlyName { get; set; }
            public int MainTextureIndex { get; set; } // index of the largest DDS
        }

        private void BtnOpenDir_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Continue anyway?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select the FIFA 15 overlay directory (containing overlay_9xxx.big files)"
            };

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            _overlayDirPath = dlg.SelectedPath;
            _overlayDirEntries.Clear();

            string[] bigFiles = Directory.GetFiles(_overlayDirPath, "overlay_*.big", SearchOption.TopDirectoryOnly);

            if (bigFiles.Length == 0)
            {
                MessageBox.Show("No overlay_*.big files found in this folder.", "No Files Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Clear the single-file view
            _bigFile = null;
            _bigFilePath = null;
            _hasUnsavedChanges = false;
            _entries.Clear();
            LstFiles.ItemsSource = null;
            ShowEmptyPreview();

            // Scan each .big and find the main texture
            foreach (var filePath in bigFiles.OrderBy(f => f))
            {
                try
                {
                    string fileName = Path.GetFileName(filePath);

                    // Extract the overlay ID (e.g. "9002" from "overlay_9002.big")
                    string overlayId = Path.GetFileNameWithoutExtension(fileName).Replace("overlay_", "");

                    string friendlyName = _overlayNames.ContainsKey(overlayId) ? _overlayNames[overlayId] : overlayId;

                    var big = new FifaBigFile(filePath);
                    big.LoadArchivedFiles();

                    // Find the largest DDS texture
                    int mainIdx = -1;
                    long largestSize = 0;

                    for (int i = 0; i < big.Files.Length; i++)
                    {
                        if (big.Files[i] != null && big.Files[i].IsDds())
                        {
                            try
                            {
                                var ddsFile = big.GetArchivedFile(i);
                                var dds = new DdsFile();
                                dds.Load(ddsFile);
                                var bmp = dds.GetBitmap();
                                if (bmp != null)
                                {
                                    long size = bmp.Width * bmp.Height;
                                    if (size > largestSize)
                                    {
                                        largestSize = size;
                                        mainIdx = i;
                                    }
                                }
                            }
                            catch { /* skip unreadable textures */ }
                        }
                    }

                    _overlayDirEntries.Add(new OverlayDirEntry
                    {
                        FilePath = filePath,
                        FileName = fileName,
                        OverlayId = overlayId,
                        FriendlyName = friendlyName,
                        MainTextureIndex = mainIdx
                    });
                }
                catch (Exception ex)
                {
                    // Skip files that can't be loaded
                    System.Diagnostics.Debug.WriteLine($"Skipping {filePath}: {ex.Message}");
                }
            }

            // Display the directory entries in the file list
            _entries.Clear();
            foreach (var oe in _overlayDirEntries)
            {
                _entries.Add(new BigFileEntry
                {
                    Index = _overlayDirEntries.IndexOf(oe), // Use list index as key
                    FileName = $"overlay_{oe.OverlayId} — {oe.FriendlyName}",
                    IsDds = oe.MainTextureIndex >= 0,
                    CompressedSize = 0,
                    UncompressedSize = 0
                });
            }

            LstFiles.ItemsSource = null;
            LstFiles.ItemsSource = _entries;

            TxtArchiveName.Text = $"Directory: {Path.GetFileName(_overlayDirPath)} ({_overlayDirEntries.Count} overlays)";
            TxtFileCount.Text = $"{_overlayDirEntries.Count} overlay(s)";
            BtnSave.IsEnabled = false;
            BtnExtractAll.IsEnabled = false;
            BtnBatchImport.IsEnabled = true;

            SetStatus($"Loaded {_overlayDirEntries.Count} overlays from directory.");

            // Show the main texture of the first overlay if available
            if (_overlayDirEntries.Count > 0 && _overlayDirEntries[0].MainTextureIndex >= 0)
            {
                LstFiles.SelectedIndex = 0;
                ShowDirectoryOverlayPreview(_overlayDirEntries[0]);
            }
        }

        private void ShowDirectoryOverlayPreview(OverlayDirEntry oe)
        {
            if (oe.MainTextureIndex < 0)
            {
                ShowEmptyPreview();
                return;
            }

            try
            {
                var big = new FifaBigFile(oe.FilePath);
                big.LoadArchivedFiles();
                var ddsFile = big.GetArchivedFile(oe.MainTextureIndex);
                var dds = new DdsFile();
                dds.Load(ddsFile);
                var bitmap = dds.GetBitmap();

                if (bitmap != null)
                {
                    ImgPreview.Source = ConvertBitmapToImageSource(bitmap);
                    TxtImageInfo.Text = $"{bitmap.Width} × {bitmap.Height}  •  overlay_{oe.OverlayId} — {oe.FriendlyName}";
                    TxtPreviewTitle.Text = $"overlay_{oe.OverlayId} — {oe.FriendlyName}";

                    ImagePreviewPanel.Visibility = Visibility.Visible;
                    HexPreviewPanel.Visibility = Visibility.Collapsed;
                    EmptyPreviewPanel.Visibility = Visibility.Collapsed;
                    CompositorPanel.Visibility = Visibility.Collapsed;
                }
            }
            catch
            {
                ShowEmptyPreview();
            }
        }

        private void BtnBatchImport_Click(object sender, RoutedEventArgs e)
        {
            if (_overlayDirEntries.Count == 0)
            {
                MessageBox.Show("Please open an overlay directory first using 'Open Overlay Dir'.", "No Directory Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select the folder containing exported PNG textures (overlay_9xxx - Name.png)"
            };

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            string importFolder = dlg.SelectedPath;
            string[] pngFiles = Directory.GetFiles(importFolder, "overlay_*.png", SearchOption.TopDirectoryOnly);

            if (pngFiles.Length == 0)
            {
                MessageBox.Show("No overlay_*.png files found in this folder.", "No Files Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int imported = 0;
            var errorLog = new List<string>();

            foreach (var pngPath in pngFiles)
            {
                try
                {
                    string pngFileName = Path.GetFileNameWithoutExtension(pngPath);

                    // Extract overlay ID: "overlay_9002 - Scoreboard" → "9002"
                    string overlayId = null;
                    if (pngFileName.StartsWith("overlay_", StringComparison.OrdinalIgnoreCase))
                    {
                        string rest = pngFileName.Substring(8); // after "overlay_"
                        int dashIdx = rest.IndexOf(" - ");
                        overlayId = dashIdx >= 0 ? rest.Substring(0, dashIdx).Trim() : rest.Trim();
                    }

                    if (string.IsNullOrEmpty(overlayId))
                    {
                        errorLog.Add($"{Path.GetFileName(pngPath)}: Could not parse overlay ID from filename.");
                        continue;
                    }

                    // Find the matching overlay in our loaded directory
                    var matchingOverlay = _overlayDirEntries.FirstOrDefault(oe => oe.OverlayId == overlayId);
                    if (matchingOverlay == null)
                    {
                        errorLog.Add($"{Path.GetFileName(pngPath)}: No matching overlay_{overlayId}.big found in directory.");
                        continue;
                    }

                    if (matchingOverlay.MainTextureIndex < 0)
                    {
                        errorLog.Add($"{Path.GetFileName(pngPath)}: overlay_{overlayId}.big has no DDS textures.");
                        continue;
                    }

                    // Open the .big, get the main DDS, replace bitmap, save
                    var big = new FifaBigFile(matchingOverlay.FilePath);
                    big.LoadArchivedFiles();

                    var fifaFile = big.GetArchivedFile(matchingOverlay.MainTextureIndex);

                    // Export the original DDS NEXT TO the .big file to avoid AV locking in %TEMP%
                    string bigDir = Path.GetDirectoryName(matchingOverlay.FilePath);
                    string origDdsPath = Path.Combine(bigDir, "_temp_orig_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".dds");
                    fifaFile.Export(origDdsPath);

                    var dds = new DdsFile();
                    dds.Load(origDdsPath);

                    // Load the PNG and replace
                    var newBitmap = new Bitmap(pngPath);

                    // Check dimensions match
                    var existingBmp = dds.GetBitmap();
                    if (existingBmp != null && (existingBmp.Width != newBitmap.Width || existingBmp.Height != newBitmap.Height))
                    {
                        errorLog.Add($"{Path.GetFileName(pngPath)}: Dimension mismatch! PNG is {newBitmap.Width}×{newBitmap.Height}, but overlay_{overlayId}.big expects {existingBmp.Width}×{existingBmp.Height}. You need to resize the PNG first.");
                        // Clean up
                        try { File.Delete(origDdsPath); } catch { }
                        newBitmap.Dispose();
                        continue;
                    }

                    dds.ReplaceBitmap(newBitmap);

                    // Save the modified DDS next to the .big file, import back
                    string tempDds = Path.Combine(bigDir, "_temp_mod_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".dds");
                    dds.Save(tempDds);
                    big.ImportReplacingFile(tempDds, matchingOverlay.MainTextureIndex);

                    // Save the .big and patch to BIGF
                    big.Save();
                    PatchToBigF(matchingOverlay.FilePath);

                    imported++;

                    // Clean up local temp files
                    try { File.Delete(origDdsPath); } catch { }
                    try { File.Delete(tempDds); } catch { }
                    newBitmap.Dispose();
                }
                catch (Exception ex)
                {
                    errorLog.Add($"{Path.GetFileName(pngPath)}: {ex.Message}");
                }
            }

            // Show results
            string msg = $"Successfully imported {imported} texture(s) into the overlay directory!\n\nLocation: {_overlayDirPath}";
            if (errorLog.Count > 0)
            {
                msg += $"\n\nISSUES ({errorLog.Count}):\n" + string.Join("\n", errorLog.Take(15));
                if (errorLog.Count > 15) msg += $"\n...and {errorLog.Count - 15} more.";
                MessageBox.Show(msg, "Batch Import Complete with Issues", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(msg, "Batch Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            SetStatus($"Batch import: {imported} file(s) imported.");

            // Refresh the directory view
            if (_overlayDirEntries.Count > 0)
            {
                int selectedIdx = LstFiles.SelectedIndex;
                // Re-trigger preview for the selected item
                if (selectedIdx >= 0 && selectedIdx < _overlayDirEntries.Count)
                {
                    ShowDirectoryOverlayPreview(_overlayDirEntries[selectedIdx]);
                }
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
