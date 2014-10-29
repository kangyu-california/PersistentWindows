using System.Windows;

namespace Ninjacrab.PersistentWindows.WpfShell
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public PersistentWindowProcessor Processor { get; set; }

        public MainWindow()
        {
            InitializeComponent();
        }
    }
}
