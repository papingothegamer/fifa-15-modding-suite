using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms; // Requires System.Windows.Forms reference
using System.Windows.Media.Imaging;
using FifaLibrary;

namespace FIFA15.ScoreboardManager
{
    public class PackItem
    {
        public string OriginalFilePath { get; set; }
        public string FileName { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public string TargetId { get; set; }
        public Dictionary<string, string> TargetOptions { get; set; }
    }

    public partial class PackExplorerWindow : Window
    {
        private ObservableCollection<PackItem> _galleryItems = new ObservableCollection<PackItem>();
        private string _sourcePath;
        private string _outputPath;

        // Default FIFA 15 Dictionary for mapping
        private readonly Dictionary<string, string> _overlayTypes = new Dictionary<string, string>
        {
            { "none", "Do Not Port (Skip)" },
            { "9001", "Stamina Bar (9001)" },
            { "9002", "Scoreboard (9002)" },
            { "9003", "Match Intro (9003)" },
            { "9013", "Team Intro/Lineup (9013)" },
            { "9021", "Substitution (9021)" },
            { "9042", "Starting 11 List (9042)" },
            { "9044", "Player Stats Live (9044)" },
            { "9045", "Match Facts (9045)" },
            { "9072", "Goalscorer Live (9072)" },
            { "9073", "Sub Popup Mini (9073)" }
        };

        public PackExplorerWindow()
        {
            InitializeComponent();
            GalleryItems.ItemsSource = _galleryItems;
        }

        private void BtnBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the folder containing FIFA 14 .big overlays";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _sourcePath = dialog.SelectedPath;
                    TxtSourcePath.Text = _sourcePath;
                    ScanFolderAsync();
                }
            }
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the output folder for FIFA 15 ported files";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _outputPath = dialog.SelectedPath;
                    TxtOutputPath.Text = _outputPath;
                }
            }
        }

        private async void ScanFolderAsync()
        {
            _galleryItems.Clear();
            TxtStatus.Text = "Scanning folder for .big files...";

            if (string.IsNullOrEmpty(_sourcePath)) return;

            string[] files = Directory.GetFiles(_sourcePath, "*.big", SearchOption.TopDirectoryOnly);
            
            if (files.Length == 0)
            {
                TxtStatus.Text = "No .big files found.";
                return;
            }

            TxtStatus.Text = $"Processing {files.Length} files. Please wait...";

            // Use Task.Run to avoid blocking UI, then dispatch back to add items
            foreach (var file in files)
            {
                var packItem = await System.Threading.Tasks.Task.Run(() => ProcessBigFile(file));
                if (packItem != null)
                {
                    _galleryItems.Add(packItem);
                }
            }

            TxtStatus.Text = $"Scan complete. Found {_galleryItems.Count} valid overlays.";
        }

        private PackItem ProcessBigFile(string filePath)
        {
            try
            {
                var bigFile = new FifaBigFile(filePath);
                Bitmap largestBitmap = null;
                long largestSize = 0;

                // Find largest DDS to use as preview
                for (int i = 0; i < bigFile.Files.Length; i++)
                {
                    var archivedFile = bigFile.Files[i];
                    if (archivedFile != null && archivedFile.IsDds())
                    {
                        var ddsFile = bigFile.GetArchivedFile(i);
                        var dds = new DdsFile();
                        dds.Load(ddsFile);
                        var bmp = dds.GetBitmap();
                        if (bmp != null)
                        {
                            long size = bmp.Width * bmp.Height;
                            if (size > largestSize)
                            {
                                largestSize = size;
                                largestBitmap = bmp;
                            }
                        }
                    }
                }

                if (largestBitmap == null) return null;

                string fileName = Path.GetFileName(filePath);

                // Auto-map logic based on filename endsWith
                string autoMapId = "none";
                if (fileName.EndsWith("001.big")) autoMapId = "9001";
                else if (fileName.EndsWith("002.big")) autoMapId = "9002";
                else if (fileName.EndsWith("003.big")) autoMapId = "9003";
                else if (fileName.EndsWith("013.big")) autoMapId = "9013";
                else if (fileName.EndsWith("021.big")) autoMapId = "9021";
                else if (fileName.EndsWith("042.big")) autoMapId = "9042";
                else if (fileName.EndsWith("044.big")) autoMapId = "9044";
                else if (fileName.EndsWith("045.big")) autoMapId = "9045";
                else if (fileName.EndsWith("072.big")) autoMapId = "9072";
                else if (fileName.EndsWith("073.big")) autoMapId = "9073";

                var packItem = new PackItem
                {
                    OriginalFilePath = filePath,
                    FileName = fileName,
                    TargetId = autoMapId,
                    TargetOptions = _overlayTypes,
                    Thumbnail = null 
                };

                // Convert bitmap to WPF BitmapImage on UI Thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    packItem.Thumbnail = BitmapToImageSource(largestBitmap);
                });

                return packItem;
            }
            catch
            {
                return null;
            }
        }

        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Crucial for multi-threading
                return bitmapImage;
            }
        }

        private void BtnPort_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_outputPath))
            {
                System.Windows.MessageBox.Show("Please select an Output Folder first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int successCount = 0;

            foreach (var item in _galleryItems)
            {
                if (item.TargetId != "none")
                {
                    try
                    {
                        string newFileName = $"overlay_{item.TargetId}.big";
                        string destPath = Path.Combine(_outputPath, newFileName);

                        // 1. Copy the file
                        File.Copy(item.OriginalFilePath, destPath, true);

                        // 2. Re-pack using FifaLibrary to ensure valid header
                        var portedBig = new FifaBigFile(destPath);
                        portedBig.Save();

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Failed to port {item.FileName}:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            System.Windows.MessageBox.Show($"Successfully ported {successCount} files to FIFA 15!\n\nLocation: {_outputPath}", "Porting Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            TxtStatus.Text = $"Last port: {successCount} files exported.";
        }
    }
}
