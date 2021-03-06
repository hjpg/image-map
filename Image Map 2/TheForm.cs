﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Linq;


namespace ImageMap
{
    public partial class TheForm : Form
    {
        ViewController Controller;
        static readonly string[] ImageExtensions = new[] { ".png", ".bmp", ".jpg", ".jpeg", ".gif" };
        string LastOpenPath = "";
        string JavaSavesFolder = "";
        string LastImgExportPath = "";
        string BedrockSavesFolder;
        WorldWindow JavaWorldDialog = new WorldWindow(Edition.Java);
        WorldWindow BedrockWorldDialog = new WorldWindow(Edition.Bedrock);
        public TheForm()
        {
            InitializeComponent();
            Controller = new ViewController(this);
        }

        private static string GenerateFilter(string description, string[] extensions)
        {
            string result = description + "|";
            foreach (string extension in extensions)
            {
                result += "*" + extension + ";";
            }
            result += "|All Files|*.*";
            return result;
        }

        private void TheForm_Load(object sender, EventArgs e)
        {
            // load up saved settings
            AddChestCheck.Checked = Properties.Settings.Default.GiveChest;
            LastOpenPath = Properties.Settings.Default.LastOpenPath;
            LastImgExportPath = Properties.Settings.Default.LastImgExportPath;
            AddChestCheck.Checked = Properties.Settings.Default.AddNewMaps;
            JavaSavesFolder = Properties.Settings.Default.JavaSavesFolder;
            BedrockSavesFolder = Properties.Settings.Default.BedrockSavesFolder;
        }

        private bool IsProcessingMaps = false;
        public void ProcessingMapsStart()
        {
            IsProcessingMaps = true;
            OpenButton.Enabled = false;
            SendButton.Enabled = false;
            SendButton.Text = "Processing...";
        }

        public void ProcessingMapsDone()
        {
            IsProcessingMaps = false;
            OpenButton.Enabled = true;
            SendButton.Enabled = true;
            SendButton.Text = "Send All to World";
        }

        private void SendMapsWithMessage(IEnumerable<MapIDControl> maps, string destination)
        {
            int conflicts = Controller.SendMapsToWorld(maps, MapReplaceOption.Info, destination);
            if (conflicts > 0)
            {
                var option = new ReplaceOptionDialog(conflicts);
                option.ShowDialog(this);
                Controller.SendMapsToWorld(maps, option.SelectedOption, destination);
            }
            else
                Controller.SendMapsToWorld(maps, MapReplaceOption.ReplaceExisting, destination);
        }

        private void OpenWorld(Edition edition, string folder)
        {
            var result = Controller.OpenWorld(edition, folder);
            if (result == ActionResult.MapsNotImported && MessageBox.Show("You have unsaved maps waiting to be imported! If you select a new world, these will be lost!\n\nDiscard unsaved maps?", "Wait a minute!", MessageBoxButtons.YesNo) == DialogResult.Yes)
                Controller.OpenWorld(edition, folder, bypass_mapwarning: true);
        }

        private void DraggedWorld(string folder)
        {
            if (!Directory.Exists(folder))
            {
                MessageBox.Show("Only world folders can be opened. Please don't drag ZIPs or MCWORLDs.\nIf you were trying to drag images, make sure to open a world first.", "Not a folder?");
                return;
            }
            if (File.Exists(Path.Combine(folder, "db", "CURRENT")))
                OpenWorld(Edition.Bedrock, folder);
            else if (Directory.Exists(Path.Combine(folder, "region")))
                OpenWorld(Edition.Java, folder);
            else
                MessageBox.Show("Couldn't tell what edition of Minecraft that world was.", "Not a world?");
        }

        private void SelectWorldWithMessage(Edition edition)
        {
            string folder = null;
            // edition-specific world picking
            if (edition == Edition.Java)
            {
                if (String.IsNullOrEmpty(JavaSavesFolder) || !Directory.Exists(JavaSavesFolder))
                    JavaSavesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @".minecraft\saves");
                JavaWorldDialog.SavesFolder = JavaSavesFolder;
                JavaWorldDialog.Show(this);
                if (!JavaWorldDialog.Confirmed)
                    return;
                folder = JavaWorldDialog.SelectedWorldFolder;
                JavaSavesFolder = JavaWorldDialog.SavesFolder;
            }
            else if (edition == Edition.Bedrock)
            {
                if (String.IsNullOrEmpty(BedrockSavesFolder) || !Directory.Exists(BedrockSavesFolder))
                    BedrockSavesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Packages\Microsoft.MinecraftUWP_8wekyb3d8bbwe\LocalState\games\com.mojang\minecraftWorlds");
                BedrockWorldDialog.SavesFolder = BedrockSavesFolder;
                BedrockWorldDialog.Show(this);
                if (!BedrockWorldDialog.Confirmed)
                    return;
                folder = BedrockWorldDialog.SelectedWorldFolder;
                BedrockSavesFolder = BedrockWorldDialog.SavesFolder;
            }
            // generic world opening and warning
            OpenWorld(edition, folder);
        }

        private void JavaWorldButton_Click(object sender, EventArgs e)
        {
            SelectWorldWithMessage(Edition.Java);
        }

        private void BedrockWorldButton_Click(object sender, EventArgs e)
        {
            SelectWorldWithMessage(Edition.Bedrock);
        }

        private void OpenButton_Click(object sender, EventArgs e)
        {
            var open_dialog = new OpenFileDialog()
            {
                Title = "Import image files to turn into maps",
                Multiselect = true,
                Filter = GenerateFilter("Image Files", ImageExtensions)
            };
            open_dialog.InitialDirectory = LastOpenPath;
            if (ShowCompatibleOpenDialog(open_dialog) == DialogResult.OK)
            {
                LastOpenPath = Path.GetDirectoryName(open_dialog.FileName);
                Controller.ImportImages(open_dialog.FileNames);
            }
        }

        private static DialogResult ShowCompatibleOpenDialog(OpenFileDialog d)
        {
            try
            {
                return d.ShowDialog();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                d.AutoUpgradeEnabled = false;
                return d.ShowDialog();
            }
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            SendMapsWithMessage(Controller.GetAllMaps(MapStatus.Importing), AddChestCheck.Checked ? ViewController.LOCAL_IDENTIFIER : ViewController.NOBODY_IDENTIFIER);
        }

        private void TheForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            // save settings
            Properties.Settings.Default.JavaSavesFolder = JavaSavesFolder;
            Properties.Settings.Default.BedrockSavesFolder = BedrockSavesFolder;
            Properties.Settings.Default.LastOpenPath = LastOpenPath;
            Properties.Settings.Default.LastImgExportPath = LastImgExportPath;
            Properties.Settings.Default.AddNewMaps = AddChestCheck.Checked;
            Properties.Settings.Default.GiveChest = AddChestCheck.Checked;
            Properties.Settings.Default.Save();
        }

        private void TheForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Controller.UnsavedChanges() && MessageBox.Show("You imported some maps, but you haven't sent them over to the world yet. You need to press \"Send All to World\" to do that. If you exit now, these maps will disappear.\n\nWould you like to exit anyway?", "Wait a Minute!", MessageBoxButtons.YesNo) == DialogResult.No)
                e.Cancel = true;
        }

        private void ImportZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Html))
                e.Effect = DragDropEffects.Copy;
        }

        private void ImportZone_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            Controller.ImportImages(files);
        }

        private void TheForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void TheForm_DragDrop(object sender, DragEventArgs e)
        {
            string file = ((string[])e.Data.GetData(DataFormats.FileDrop)).First();
            DraggedWorld(file);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (MapViewZone.Visible)
            {
                if (MapView.SelectedTab == ImportTab)
                {
                    if (keyData == (Keys.V | Keys.Control))
                    {
                        if (Clipboard.ContainsFileDropList())
                        {
                            var files = Clipboard.GetFileDropList();
                            string[] array = new string[files.Count];
                            files.CopyTo(array, 0);
                            Controller.ImportImages(array);
                        }
                        else if (Clipboard.ContainsImage())
                        {
                            var image = Clipboard.GetImage();
                            Controller.ImportImages(image);
                        }
                        return true;
                    }
                    else if (keyData == Keys.Delete)
                    {
                        ImportContextDiscard_Click(this, new EventArgs());
                        return true;
                    }
                }
                else if (MapView.SelectedTab == ExistingTab)
                {
                    if (keyData == Keys.Delete)
                    {
                        ExistingContextDelete_Click(this, new EventArgs());
                        return true;
                    }
                }
            }
            if (keyData == (Keys.A | Keys.Control))
            {
                if (MapView.SelectedTab == ImportTab)
                    Controller.SelectAll(MapStatus.Importing);
                else if (MapView.SelectedTab == ExistingTab)
                    Controller.SelectAll(MapStatus.Existing);
                return true;
            }
            else if (keyData == (Keys.D | Keys.Control))
            {
                if (MapView.SelectedTab == ImportTab)
                    Controller.DeselectAll(MapStatus.Importing);
                else if (MapView.SelectedTab == ExistingTab)
                    Controller.DeselectAll(MapStatus.Existing);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ImportContextSend_Click(object sender, EventArgs e)
        {
            SendMapsWithMessage(Controller.GetSelectedMaps(MapStatus.Importing), AddChestCheck.Checked ? ViewController.LOCAL_IDENTIFIER : ViewController.NOBODY_IDENTIFIER);
        }

        private void ChangeMapIDs(IEnumerable<MapIDControl> boxes, MapStatus area)
        {
            var input = new IDInputDialog(boxes.First().ID);
            input.ShowDialog(this);
            if (input.Confirmed)
            {
                long firstid;
                if (input.WantsAuto)
                    firstid = Controller.GetSafeID();
                else
                    firstid = input.SelectedID;
                int count = Controller.ChangeMapIDs(boxes, firstid, area, MapReplaceOption.Info);
                if (count > 0)
                {
                    var picker = new ReplaceOptionDialog(count);
                    picker.ShowDialog(this);
                    Controller.ChangeMapIDs(boxes, firstid, area, picker.SelectedOption);
                }
                else
                    Controller.ChangeMapIDs(boxes, firstid, area, MapReplaceOption.Skip);
            }
        }

        private void ImportContextChangeID_Click(object sender, EventArgs e)
        {
            ChangeMapIDs(Controller.GetSelectedMaps(MapStatus.Importing).ToArray(), MapStatus.Importing);
        }

        private void ImportContextDiscard_Click(object sender, EventArgs e)
        {
            var selected = Controller.GetSelectedMaps(MapStatus.Importing).ToArray();
            foreach (var box in selected)
            {
                Controller.RemoveFromZone(box, MapStatus.Importing);
                box.Map.Dispose();
            }
        }

        private void ImportContextSelectAll_Click(object sender, EventArgs e)
        {
            if (Controller.GetAllMaps(MapStatus.Importing).All(x => x.Selected))
                Controller.DeselectAll(MapStatus.Importing);
            else
                Controller.SelectAll(MapStatus.Importing);
        }

        private void ExistingContextAdd_Click(object sender, EventArgs e)
        { }

        private void ExistingContextChangeID_Click(object sender, EventArgs e)
        {
            ChangeMapIDs(Controller.GetSelectedMaps(MapStatus.Existing).ToArray(), MapStatus.Existing);
        }

        private void ExistingContextExport_Click(object sender, EventArgs e)
        {
            var selected = Controller.GetSelectedMaps(MapStatus.Existing);
            // super epic way to check if there is exactly one item
            bool onlyone = selected.Take(2).Count() == 1;
            var export_dialog = new SaveFileDialog()
            {
                Title = "Export this map as a PNG",
                Filter = "Image Files|*.png|All Files|*.*"
            };
            if (onlyone)
                export_dialog.FileName = selected.First().GetMapName() + ".png";
            else
                export_dialog.FileName = "";
            export_dialog.InitialDirectory = LastImgExportPath;
            if (export_dialog.ShowDialog() == DialogResult.OK)
            {
                LastImgExportPath = Path.GetDirectoryName(export_dialog.FileName);
                if (onlyone)
                    Controller.SaveMap(selected.First(), export_dialog.FileName);
                else
                    Controller.SaveMaps(selected, Path.ChangeExtension(export_dialog.FileName, ""));
            }
        }

        private void ExistingContextDelete_Click(object sender, EventArgs e)
        {
            var selected = Controller.GetSelectedMaps(MapStatus.Existing);
            if (selected.Any() && MessageBox.Show("Deleting these maps will remove all copies from the world permanently.\n\nWould you like to delete these maps?", $"Delete {selected.Count()} maps?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                Controller.DeleteMapsFromWorld(selected);
        }

        private void ExistingContextPlayerName_Click(object sender, EventArgs e)
        {
            var playername = ((ToolStripMenuItem)sender).Text;
            try
            {
                bool success = Controller.AddChests(Controller.GetSelectedMaps(MapStatus.Existing).Select(x => x.ID), playername);
                if (!success)
                    MessageBox.Show("There wasn't enough space to fit the chests in your inventory. One or more were not added.", "Chest alert!");
            }
            catch (FileNotFoundException ex)
            {
                MessageBox.Show($"Could not find any player in that world with name {playername}\n\nFull error: {ex.Message}", "Player not found");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unknown error happened: {ex.Message}", "Error!");
            }
        }

        private void ExistingContextSelectAll_Click(object sender, EventArgs e)
        {
            if (Controller.GetAllMaps(MapStatus.Existing).All(x => x.Selected))
                Controller.DeselectAll(MapStatus.Existing);
            else
                Controller.SelectAll(MapStatus.Existing);
        }

        private void ImportContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Controller.GetAllMaps(MapStatus.Importing).All(x => x.Selected))
                ImportContextSelectAll.Text = "Deselect all";
            else
                ImportContextSelectAll.Text = "Select all";
            ImportContextSend.Enabled = !IsProcessingMaps;
            ImportContextDiscard.Enabled = !IsProcessingMaps;
            ImportContextChangeID.Enabled = !IsProcessingMaps;
        }

        private void ExistingContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ExistingContextAdd.DropDownItems.Clear();
            ExistingContextAdd.DropDownItems.AddRange(Controller.GetPlayerDestinations().Select(x => new ToolStripMenuItem(x, null, ExistingContextPlayerName_Click)).ToArray());
            if (Controller.GetAllMaps(MapStatus.Existing).All(x => x.Selected))
                ExistingContextSelectAll.Text = "Deselect all";
            else
                ExistingContextSelectAll.Text = "Select all";
        }
    }
}
