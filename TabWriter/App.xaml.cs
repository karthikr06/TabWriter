using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;

namespace TabWriter
{
    
    public partial class App : Application
    {
        public static Window? _window;
        
        public App()
        {
            InitializeComponent();
        }
        
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
