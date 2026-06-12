using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using FifaLibrary;

namespace FIFA15.ScoreboardManager
{
    public partial class CreatorWindow : Window
    {
        private Dictionary<int, string> _droppedImages = new Dictionary<int, string>();

        public CreatorWindow()
        {
            InitializeComponent();
        }

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string file = files[0];
                    if (file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                    {
                        var border = sender as Border;
                        if (border != null && int.TryParse(border.Tag.ToString(), out int targetIndex))
                        {
                            _droppedImages[targetIndex] = file;

                            // Display it in the UI
                            var imgControl = FindImageControl(border);
                            if (imgControl != null)
                            {
                                imgControl.Source = new BitmapImage(new Uri(file));
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Please drop a PNG or DDS file.");
                    }
                }
            }
        }

        private System.Windows.Controls.Image FindImageControl(DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.Image img) return img;
                var res = FindImageControl(child);
                if (res != null) return res;
            }
            return null;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (_droppedImages.Count == 0)
            {
                MessageBox.Show("Please drop at least one texture to generate a custom scoreboard.", "Missing Textures", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "FIFA BIG Archive (*.big)|*.big",
                Title = "Save Custom Scoreboard",
                FileName = "overlay_9002.big"
            };

            if (dialog.ShowDialog() == true)
            {
                GenerateScoreboard(dialog.FileName);
            }
        }

        private void GenerateScoreboard(string outputPath)
        {
            try
            {
                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "Horizontal.big");
                if (!File.Exists(templatePath))
                {
                    MessageBox.Show($"Template not found at: {templatePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 1. Copy the base template to the output path
                File.Copy(templatePath, outputPath, true);

                // 2. Open the new big file using FifaLibrary14
                var bigFile = new FifaBigFile(outputPath);

                // 3. Inject dropped textures
                foreach (var kvp in _droppedImages)
                {
                    int index = kvp.Key;
                    string filePath = kvp.Value;

                    if (filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        // Convert PNG to DDS
                        var fifaFile = bigFile.GetArchivedFile(index);
                        var dds = new DdsFile();
                        dds.Load(fifaFile);
                        
                        using (var bmp = new Bitmap(filePath))
                        {
                            dds.ReplaceBitmap(bmp);
                            
                            string tempDdsPath = Path.GetTempFileName();
                            dds.Save(tempDdsPath);
                            
                            bigFile.ImportReplacingFile(tempDdsPath, index);
                            File.Delete(tempDdsPath);
                        }
                    }
                    else if (filePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                    {
                        // Direct DDS injection
                        bigFile.ImportReplacingFile(filePath, index);
                    }
                }

                MessageBox.Show($"Scoreboard successfully generated at:\n{outputPath}\n\nNote: Visual compositor currently manages hex layouts. This generated .big has the textures replaced correctly.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Generation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
