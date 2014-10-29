using System.Windows;
using System.Windows.Controls;
using Ninjacrab.PersistentWindows.WpfShell.Diagnostics;

namespace Ninjacrab.PersistentWindows.WpfShell
{
    /// <summary>
    /// Interaction logic for DiagnosticsView.xaml
    /// </summary>
    public partial class DiagnosticsView : UserControl
    {
        private DiagnosticsViewModel viewModel;

        public DiagnosticsView()
        {
            InitializeComponent();
            viewModel = new DiagnosticsViewModel();
            this.DataContext = viewModel;
            Log.LogEvent += (level, message) =>
                {
                    Application.Current.Dispatcher.Invoke(() => 
                        {
                            viewModel.EventLog.Add(string.Format("{0}: {1}", level, message));
                            if (viewModel.EventLog.Count > 500)
                            {
                                viewModel.EventLog.RemoveAt(0);
                            }
                        });
                };
        }
    }
}
