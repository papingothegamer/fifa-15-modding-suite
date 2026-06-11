using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace FIFA15.AccessoryManager
{
    public partial class MainWindow : Window
    {
        private List<Player> _players;
        private List<Player> _filteredPlayers;

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        private BitmapSource ConvertToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            if (bitmap == null) return null;
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnBrowseDb_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select the folder containing fifa_ng_db.db and eng_us.db";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtDbFolderPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void BtnBrowseSceneAssets_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select your sceneassets folder";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtSceneAssetsPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void LoadDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = TxtDbFolderPath.Text;
                if (string.IsNullOrWhiteSpace(baseDir) || !Directory.Exists(baseDir))
                {
                    MessageBox.Show("Please select a valid database folder first.", "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // First try flat folder structure (all 4 files in one folder)
                string dbPath = Path.Combine(baseDir, "fifa_ng_db.db");
                string xmlPath = Path.Combine(baseDir, "fifa_ng_db-meta.xml");
                string langDbPath = Path.Combine(baseDir, "eng_us.db");
                string langXmlPath = Path.Combine(baseDir, "eng_us-meta.xml");

                // If not found in flat structure, try standard FIFA 15 root structure
                if (!File.Exists(dbPath) || !File.Exists(langDbPath))
                {
                    dbPath = Path.Combine(baseDir, "data", "db", "fifa_ng_db.db");
                    xmlPath = Path.Combine(baseDir, "data", "db", "fifa_ng_db-meta.xml");
                    langDbPath = Path.Combine(baseDir, "data", "loc", "eng_us.db");
                    langXmlPath = Path.Combine(baseDir, "data", "loc", "eng_us-meta.xml");
                }

                if (!File.Exists(dbPath) || !File.Exists(langDbPath))
                {
                    MessageBox.Show("Could not find the necessary database files. If selecting the FIFA 15 root folder, ensure data\\db and data\\loc exist. Otherwise, put all 4 DB files in a single flat folder.", "Files Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _players = NativeDbReader.LoadPlayers(dbPath, xmlPath, langDbPath, langXmlPath);
                
                // Populate Teams Dropdown
                var teamNames = _players.Where(p => !string.IsNullOrEmpty(p.TeamName)).Select(p => p.TeamName).Distinct().OrderBy(t => t).ToList();
                teamNames.Insert(0, "All Teams");
                ComboTeams.ItemsSource = teamNames;
                ComboTeams.SelectedIndex = 0;

                GridPlayers.ItemsSource = _players;
                MessageBox.Show($"Successfully loaded {_players.Count} players from EA Database.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GridPlayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridPlayers.SelectedItem is Player selectedPlayer)
            {
                UpdatePreviews(selectedPlayer);
            }
        }

        private void ComboTeams_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_players == null) return;
            string selectedTeam = ComboTeams.SelectedItem as string;

            if (string.IsNullOrEmpty(selectedTeam) || selectedTeam == "All Teams")
            {
                _filteredPlayers = _players;
            }
            else
            {
                _filteredPlayers = _players.Where(p => p.TeamName == selectedTeam).ToList();
            }

            GridPlayers.ItemsSource = _filteredPlayers;
        }

        private void UpdatePreviews(Player player)
        {
            ImgShoePreview.Source = null;
            ImgGlovePreview.Source = null;

            string sceneAssets = TxtSceneAssetsPath.Text;
            if (string.IsNullOrWhiteSpace(sceneAssets) || !Directory.Exists(sceneAssets)) return;

            // Load Shoe Texture
            if (player.ShoeId > 0)
            {
                string shoeFile = Path.Combine(sceneAssets, "shoe", $"shoe_{player.ShoeId}_0_textures.rx3");
                if (File.Exists(shoeFile))
                {
                    try {
                        var rx3 = new FifaLibrary.Rx3File();
                        if (rx3.Load(shoeFile)) {
                            var bitmaps = rx3.GetBitmaps();
                            if (bitmaps != null && bitmaps.Length > 0 && bitmaps[0] != null) {
                                ImgShoePreview.Source = ConvertToBitmapSource(bitmaps[0]);
                            }
                        }
                    } catch { }
                }
            }

            // Load Glove Texture
            if (player.GkGloveId > 0)
            {
                string gloveFile = Path.Combine(sceneAssets, "gkglove", $"gkglove_{player.GkGloveId}_0_textures.rx3");
                if (File.Exists(gloveFile))
                {
                    try {
                        var rx3 = new FifaLibrary.Rx3File();
                        if (rx3.Load(gloveFile)) {
                            var bitmaps = rx3.GetBitmaps();
                            if (bitmaps != null && bitmaps.Length > 0 && bitmaps[0] != null) {
                                ImgGlovePreview.Source = ConvertToBitmapSource(bitmaps[0]);
                            }
                        }
                    } catch { }
                }
            }
        }

        private void BtnGenerateLua_Click(object sender, RoutedEventArgs e)
        {
            if (_players == null || _players.Count == 0)
            {
                MessageBox.Show("Please load players first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output_Lua");
                Directory.CreateDirectory(outDir);

                string shoeOut = Path.Combine(outDir, "shoes.lua");
                string gloveOut = Path.Combine(outDir, "gkgloves.lua");
                string tapeOut = Path.Combine(outDir, "ankletapes.lua");

                LuaAssignmentGenerator.GenerateShoesLua(_players, shoeOut);
                LuaAssignmentGenerator.GenerateGkGlovesLua(_players, gloveOut);
                LuaAssignmentGenerator.GenerateAnkleTapesLua(_players, tapeOut);

                MessageBox.Show($"Generated Lua scripts in:\n{outDir}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating Lua: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}