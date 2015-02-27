using System.Windows;

namespace Ninjacrab.PersistentWindows.WpfShell
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool IsClosed { get; set; }

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            IsClosed = true;
        }
    }
}
