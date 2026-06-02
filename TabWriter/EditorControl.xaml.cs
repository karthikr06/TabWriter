using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;

namespace TabWriter

{
    public sealed partial class EditorControl : UserControl
    {
        
        public Windows.Storage.StorageFile currentFile = null!; //no file selected initially, so null.
        public bool hasUnsavedChanges = false; //no file loaded, no changes
        public bool isLoadingFile = false; //toggle to prevent triggering unsaved changes logic when loading a file
        //event to tell the MainWindow to update the title of the tab when something changes that should change the title (like loading a file, or making unsaved changes)
        public event EventHandler<string> TabTitleChanged = null!;

        public EditorControl()
        {
            this.InitializeComponent();
        }

        // Textboxes care about their own text only. So when we load a file, we don't want to trigger the "unsaved changes" logic. Hence the isLoadingFile toggle.
        private void MainTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isLoadingFile) return;

            if (!hasUnsavedChanges)
            {
                hasUnsavedChanges = true;
                TabTitleChanged?.Invoke(this, (currentFile?.DisplayName ?? "New Note") + "*");
            }
        }

        // A helper method so the MainWindow can grab the text
        public string GetText()
        {
            return MainTextBox.Text;
        }

        // A helper method so the MainWindow can set the text
        public void SetText(string newText)
        {
            isLoadingFile = true;
            MainTextBox.Text = newText;
            isLoadingFile = false;
        }
        
        private void NewButtonClick(object sender, RoutedEventArgs e)
        {
            // We ask the MainWindow to open a new tab!
            var window = App._window as MainWindow;
            window?.CreateNewTab("New Note");
        }
        private void ExitButtonClick(object sender, RoutedEventArgs e)
        {
            // We ask the MainWindow to open a file picker and load a file!
            var window = App._window as MainWindow;
            window?.Close();
        }

        private async void SaveButtonClick(object sender, RoutedEventArgs e)
        {
            if (currentFile != null)
            {
                await Windows.Storage.FileIO.WriteTextAsync(currentFile, MainTextBox.Text);
                hasUnsavedChanges = false; //The file has no unsaved changes now

                //change the display name of the tab to match the file name, and remove the asterisk on Saving
                TabTitleChanged?.Invoke(this, currentFile.DisplayName);
            }
            else
            {
                SaveAsButtonClick(sender, e);
            }
        }
        private async void SaveAsButtonClick(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App._window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("Plain Text", new List<String>() { ".txt" });

            if (currentFile != null)
            {
                savePicker.SuggestedFileName = currentFile.DisplayName;
            }
            else
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                savePicker.SuggestedFileName = $"New Note - {timestamp}";
            }

            StorageFile file = await savePicker.PickSaveFileAsync();

            if (file != null)
            {
                currentFile = file;
                await Windows.Storage.FileIO.WriteTextAsync(file, MainTextBox.Text);
                hasUnsavedChanges = false;

                TabTitleChanged?.Invoke(this, currentFile.DisplayName);
            }
        }

        private async void OpenButtonClick(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            //Tell Windows, this app OWNS the file picker
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App._window);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
            //setup rules for the file picker
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add(".txt");
            StorageFile file = await openPicker.PickSingleFileAsync();

            if (file != null) //if we did not press cancel
            {
                currentFile = file;
                isLoadingFile = true;
                string text = await FileIO.ReadTextAsync(file);
                MainTextBox.Text = text;
                hasUnsavedChanges = false;
                isLoadingFile = false;
                TabTitleChanged?.Invoke(this, currentFile.DisplayName);
            }
        }
        public async Task<bool> CheckUnsavedChangesAsync() 
        {
            if (!hasUnsavedChanges) return true; // Safe to proceed!

            ContentDialog dialog = new ContentDialog //new content dialog with options to Save, Don't Save, or Cancel
            {
                Title = "Unsaved Changes",
                Content = "You have unsaved changes. Do you want to save them?",
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Don't Save",
                CloseButtonText = "Cancel",
                PrimaryButtonStyle = (Application.Current.Resources["AccentButtonStyle"] as Style),
                SecondaryButtonStyle = (Application.Current.Resources["DefaultButtonStyle"] as Style),
                CloseButtonStyle = (Application.Current.Resources["DefaultButtonStyle"] as Style),
                XamlRoot = this.XamlRoot // Required in WinUI 3 to know which window to pop up in
            };

            var result = await dialog.ShowAsync(); //wait for user's response

            if (result == ContentDialogResult.Primary)  //if Primary button
            {
                SaveButtonClick(null!, null!);
                return true;
            }
            else if (result == ContentDialogResult.Secondary)
            {
                return true; // User doesn't care, safe to proceed and wipe the text
            }

            return false; // User hit Cancel, stop the action!
        }

        private void CloseTabButtonClick(object sender, RoutedEventArgs e)
        {
            var window = App._window as MainWindow;
            window?.closeActiveTab();
        }


        //Quick Save has 2 cases
        //But it should be possible to reuse the save button's code here
        //Work on that for future updates to optimise the code better
        private async void QuickSaveButtonClick(object sender, RoutedEventArgs e)
        {
            //Current file is not saved anywhere
            if (currentFile == null)
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App._window);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".txt" });
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                savePicker.SuggestedFileName = $"New Note - {timestamp}";

                // Ask the user where to save it
                Windows.Storage.StorageFile pickedFile = await savePicker.PickSaveFileAsync();

                if (pickedFile == null)
                {
                    return; // The user hit Cancel, so abort the quick save!
                }

                // Save the file officially so Case 1 can take over next time
                currentFile = pickedFile;
            }

            try
            {
                await Windows.Storage.FileIO.WriteTextAsync(currentFile, MainTextBox.Text);

                string folderPath = System.IO.Path.GetDirectoryName(currentFile.Path)!;
                if (folderPath == null)
                {
                    return; //Safety check
                }
                string originalName = System.IO.Path.GetFileNameWithoutExtension(currentFile.Path);
                string extension = System.IO.Path.GetExtension(currentFile.Path);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

                string baseBackupName = $"{originalName} - {timestamp}";
                string backupFilePath = System.IO.Path.Combine(folderPath, baseBackupName + extension);

                int counter = 1;
                while (System.IO.File.Exists(backupFilePath))
                {
                    backupFilePath = System.IO.Path.Combine(folderPath, $"{baseBackupName} ({counter}){extension}");
                    counter++;
                }
                System.IO.File.WriteAllText(backupFilePath, MainTextBox.Text);

                hasUnsavedChanges = false;
                TabTitleChanged?.Invoke(this, currentFile.DisplayName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backup failed: {ex.Message}");
            }
        }
        // When the editor control is loaded, we want to focus the text box so the user can start typing immediately.
        // We also want to update the state of the edit menu (cut, copy, paste) based on whether there is text selected or text in the clipboard.
        private void EditorControl_Loaded(object sender, RoutedEventArgs e)
        {
            MainTextBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            updateEditMenuState();

        }
        public void FocusTextEditor()
        {
            MainTextBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            updateEditMenuState();
        }

        //For Ctrl + Tab and Ctrl + Shift + Tab shortcuts
        //Also used for Ctrl + Shift + F for opening the Font Settings
        private void PreviewKeyDownFunction(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            bool isCtrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            bool isShiftPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (isCtrlPressed)
            {
                if (e.Key == Windows.System.VirtualKey.Tab)
                {
                    e.Handled = true;
                    if (App._window is MainWindow window)
                    {
                        window.CycleTabs(isShiftPressed ? -1 : 1);
                    }
                }
                else if (isShiftPressed && e.Key == Windows.System.VirtualKey.F)
                    EditorSettings(sender, e);
            }
        }

        private void CutFunction(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MainTextBox.SelectedText)) return;
            CopyFunction(sender, e);
            MainTextBox.SelectedText = "";
        }
        private void CopyFunction(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MainTextBox.SelectedText)) return;
            var dataPackage = new DataPackage();
            dataPackage.SetText(MainTextBox.SelectedText);
            Clipboard.SetContent(dataPackage);
        }
        private async void PasteFunction(object sender, RoutedEventArgs e)
        {

            var clipboardContent = Clipboard.GetContent();
            if (clipboardContent.Contains(StandardDataFormats.Text))
            {
                string text = await clipboardContent.GetTextAsync();
                MainTextBox.SelectedText = text;
                MainTextBox.SelectionStart += text.Length;
                MainTextBox.SelectionLength = 0;
            }
            MainTextBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
        private void FindFunction(object sender, RoutedEventArgs e)
        {
            SearchWidget.Visibility = Visibility.Visible;
            ReplaceBox.Visibility = Visibility.Collapsed;
            ReplaceButton.Visibility = Visibility.Collapsed;
            FindButtons.Visibility = Visibility.Visible;
            FindBox.Focus(FocusState.Programmatic);
            ReplaceOption.IsChecked = false;
        }
        private void FindReplaceFunction(object sender, RoutedEventArgs e)
        {
            SearchWidget.Visibility = Visibility.Visible;
            ReplaceBox.Visibility = Visibility.Visible;
            FindButtons.Visibility = Visibility.Visible;
            ReplaceButton.Visibility = Visibility.Visible;
            FindBox.Focus(FocusState.Programmatic);
            ReplaceOption.IsChecked = true;

        }

        private void MainTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            updateEditMenuState();
        }
        private void updateEditMenuState()
        {
            var clipboardContent = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            MenuPaste.IsEnabled = clipboardContent.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text);
            bool hasHighlightedText = !string.IsNullOrEmpty(MainTextBox.SelectedText);
            MenuCut.IsEnabled = hasHighlightedText;
            MenuCopy.IsEnabled = hasHighlightedText;

        }

        private void FindNext_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch(true);
        }

        private void FindPrev_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch(false);
        }

        private void CloseSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchWidget.Visibility = Visibility.Collapsed;
            MainTextBox.Focus(FocusState.Programmatic);
        }
        private void ReplaceButtonClicked(object sender, RoutedEventArgs e)
        {
            if (ReplaceBox.Visibility == Visibility.Visible)
            {
                FindFunction(sender, e);
            }
            else
            {
                FindReplaceFunction(sender, e);
            }

        }
        private async void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            string query = FindBox.Text;
            string replacement = ReplaceBox.Text;

            if (string.IsNullOrEmpty(query)) return;

            int startIndex = 0;
            int replacementCount = 0;

            while (true)
            {
                int index = MainTextBox.Text.IndexOf(query, startIndex, StringComparison.OrdinalIgnoreCase);
                if (index == -1) break;

                MainTextBox.Select(index, query.Length);
                MainTextBox.SelectedText = replacement;
                startIndex = index + replacement.Length;
                replacementCount++;
                //checking only till the end
                if (startIndex >= MainTextBox.Text.Length) break;
            }

            if (replacementCount > 0)
            {
                ContentDialog replacedDialog = new ContentDialog
                {
                    Title = "Replaced!",
                    Content = $"Replaced {replacementCount} instance(s) of \"{FindBox.Text}\" with \"{ReplaceBox.Text}\"",
                    CloseButtonText = "Close",
                    XamlRoot = this.XamlRoot
                };
                await replacedDialog.ShowAsync();
            }
            else
            {
                showNotFound();
            }
        }
        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            string query = FindBox.Text;
            if (!string.IsNullOrEmpty(MainTextBox.SelectedText) &&
                MainTextBox.SelectedText.Equals(query, StringComparison.OrdinalIgnoreCase))
            {
                MainTextBox.SelectedText = ReplaceBox.Text;

            }

            PerformSearch(true, 1);
        }

        private async void PerformSearch(bool searchDown, int i = 0)
        {
            string query = FindBox.Text;
            if (string.IsNullOrEmpty(query)) return;

            string text = MainTextBox.Text;
            int index = -1;

            if (searchDown) //search from the end of the current selection
            {
                // Search forwards
                int start = MainTextBox.SelectionStart + MainTextBox.SelectionLength;
                index = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);

                // Wrap to top if not found
                if (index == -1) index = text.IndexOf(query, 0, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // Search backwards: Start from beginning of current selection
                int start = MainTextBox.SelectionStart;
                if (start > 0)
                {
                    index = text.LastIndexOf(query, start - 1, StringComparison.OrdinalIgnoreCase);
                }

                // Wrap to bottom if not found
                if (index == -1) index = text.LastIndexOf(query, text.Length - 1, StringComparison.OrdinalIgnoreCase);
            }

            if (index != -1)
            {
                MainTextBox.Select(index, query.Length);
                MainTextBox.Focus(FocusState.Programmatic);
            }
            if (index == -1 && i == 0)
            {
                showNotFound();
            }
        }
        private async void showNotFound()
        {
            if (!NotFoundPopUp.IsOpen)
            {
                NotFoundPopUp.IsOpen = true;
            }
        }

        private void EditorSettings(object sender, RoutedEventArgs e)
        {
            if (App._window is MainWindow window)
            {
                window.ShowSettingsWidget();
            }

        }
        private void AboutWidgetEnable(object sender, RoutedEventArgs e)
        {
            if(App._window is MainWindow window)
            {
                window.ShowAboutWidget();
            }
        }

        public void UpdateFontSize(double newSize)
        {
            MainTextBox.FontSize = newSize;
        }

    }
}