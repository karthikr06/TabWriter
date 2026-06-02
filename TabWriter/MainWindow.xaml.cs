using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WinUIEx;

namespace TabWriter
{

    //Partial class for the MainWindow
    public sealed partial class MainWindow : Window
    {
        //toggle for if the file is safe to close or not
        private bool isSafeToClose = false;

        //variable for current Settings based on the AppSettings class
        private AppSettings currentSettings;

        //List for the font Sizes available. Common sizes are added
        //Only "get" takes place for now. Change it into combobox in future using WinUI 3 Gallery
        public List<double> FontSizes { get; } = new List<double>
        {
            8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 36, 48, 72
        };


        //constructor
        public MainWindow()
        {
            InitializeComponent();

            //Doing this crashes the app. Fix it in future releases after figuring out how to do this
            //App crashed when setting a png in the About section also. Look into that!

            //string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            //this.SetIcon("ms-appx:///Assets/icon.ico");

            currentSettings = SettingsManager.Load(); //loading the current settings

            ExtendsContentIntoTitleBar = true; //setting the properties of the app. Titlebar to be used for Tabs
            SetTitleBar(DragRegion); //the drag region will be the Title bar for dragging, double clicking and similar actions

            this.AppWindow.Closing += AppWindow_Closing; //On closing, call the AppWindow_Closing function

            this.SetWindowSize(380, 550); //setting the window default size (I think it's using WinUIEx which makes it easier

            //setting the minwidth and the max width so that the app can function correctly
            var windowManager = WinUIEx.WindowManager.Get(this);
            windowManager.MinWidth = 350;
            windowManager.MinHeight = 300;

            this.Activated += MainWindow_Activated; //Activating the main window
        }
        private void MainWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            this.Activated -= MainWindow_Activated;
            CreateNewTab("New Note");
            if (FontSizeBox != null)
            {
                FontSizeBox.SelectedItem = currentSettings.FontSize;
            }
        }

        //Creating a new Tab
        //Try to look for ways to improove this and make the entire logic stronger
        public void CreateNewTab(string tabTitle, string fileContent = "")
        {
            EditorControl newEditor = new EditorControl
            {
                VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch
            };
            newEditor.UpdateFontSize(currentSettings.FontSize);
            newEditor.SetText(fileContent);

            TabViewItem newTab = new TabViewItem
            {
                Header = tabTitle,
                IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource { Symbol = Symbol.Document },
                Content = newEditor
            };

            newEditor.TabTitleChanged += (sender, newTitle) =>
            {
                newTab.Header = newTitle;
            };

            MainTabView.TabItems.Add(newTab);
            MainTabView.SelectedItem = newTab;
        }

        private void MainTabView_AddTabButtonClick(TabView sender, object args)
        {
            CreateNewTab("New Note");
        }

        private async void MainTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            //for individual tabs
            bool isSafe = await CanCloseTabAsync(args.Tab);
            if (isSafe)
            {
                sender.TabItems.Remove(args.Tab);
                if (sender.TabItems.Count == 0) //if tabs are 0
                {
                    this.Close(); // Close the entire app!
                }
            }

        }
        public async void closeActiveTab()
        {
            if (MainTabView.SelectedItem is TabViewItem activeTab)
            {
                // Check if it's safe!
                bool isSafe = await CanCloseTabAsync(activeTab);

                if (isSafe)
                {
                    MainTabView.TabItems.Remove(activeTab);

                    // If that was the very last tab, close the whole app
                    if (MainTabView.TabItems.Count == 0)
                    {
                        this.Close();
                    }
                }
            }
        }

        private async void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (isSafeToClose) return;
            // Prevent the window from closing immediately
            args.Cancel = true;
            bool canClose = await RunExitRollCallAsync(); //Run a roll call for each tab and see if they can be closed
            if (canClose) //if they can close
            {
                isSafeToClose = true;
                // Now close the window for real
                this.Close();
            }
        }

        //Check if a particular tab can be closed
        private async Task<bool> CanCloseTabAsync(TabViewItem Tab)
        {
            if (Tab.Content is EditorControl editor)
            {
                MainTabView.SelectedItem = Tab; // Switch to the tab being closed
                return await editor.CheckUnsavedChangesAsync();
            }
            return true;
        }

        private async Task<bool> RunExitRollCallAsync()
        {
            foreach (TabViewItem tab in MainTabView.TabItems)
            {
                bool isSafe = await CanCloseTabAsync(tab);
                if (!isSafe)
                {
                    return false;
                }
            }
            return true;
        }


        public void CycleTabs(int direction)
        {
            int tabCount = MainTabView.TabItems.Count;
            if (tabCount <= 1) return;
            int currentIndex = MainTabView.SelectedIndex;
            int newIndex = currentIndex + direction;
            if (newIndex >= tabCount)
            {
                newIndex = 0;
            }
            else if (newIndex < 0)
            {
                newIndex = tabCount - 1;
            }

            MainTabView.SelectedIndex = newIndex;
        }

        private void MainTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabView.SelectedItem is TabViewItem selectedTab)
            {
                if (selectedTab.Content is EditorControl editor)
                {
                    editor.FocusTextEditor();
                }
            }
        }

        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWidget.Visibility = Visibility.Collapsed;
            if (MainTabView.SelectedItem is TabViewItem selectedTab &&
                selectedTab.Content is EditorControl editor)
            {
                editor.FocusTextEditor();
            }
        }
        public void ShowSettingsWidget()
        {
            SettingsWidget.Visibility = Visibility.Visible;
            EditorSettingsLoaded();

        }

        private void ApplyFontSize(string sizeText)
        {
            if (currentSettings == null) return;
            //Convert string to double
            if (double.TryParse(sizeText, out double newSize))
            {
                if (newSize < 8) newSize = 8;
                if (newSize > 150) newSize = 150;

                currentSettings.FontSize = newSize;
                SettingsManager.Save(currentSettings);

                FontSizeBox.SelectedItem = currentSettings.FontSize;

                foreach (TabViewItem tab in MainTabView.TabItems)
                {
                    if (tab.Content is EditorControl editor)
                    {
                        editor.UpdateFontSize(currentSettings.FontSize);
                    }
                }
            }
            else
            {
                FontSizeBox.SelectedItem = currentSettings.FontSize;
            }
        }

        private void FontSizeBox_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            ApplyFontSize(args.Text);
            args.Handled = true;
        }

        private void FontSizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontSizeBox.SelectedItem != null)
            {
                ApplyFontSize(FontSizeBox.SelectedItem.ToString()!);
            }
        }
        private void EditorSettingsLoaded()
        {
            if (currentSettings != null)
            {
                FontSizeBox.SelectedItem = currentSettings.FontSize;
            }
        }

        public void ShowAboutWidget()
        {
            AboutWidget.Visibility = Visibility.Visible;

        }

        private void CloseAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutWidget.Visibility = Visibility.Collapsed;
            if (MainTabView.SelectedItem is TabViewItem selectedTab &&
                selectedTab.Content is EditorControl editor)
            {
                editor.FocusTextEditor();
            }
        }

    }

}
