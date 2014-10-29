using System.ComponentModel;
using Microsoft.Practices.Prism.Mvvm;

namespace Ninjacrab.PersistentWindows.WpfShell
{
    public class DiagnosticsViewModel : BindableBase
    {
        public DiagnosticsViewModel()
        {
            EventLog = new BindingList<string>();
        }

        public const string AllProcessesPropertyName = "AllProcesses";
        private BindingList<string> allProcesses;
        public BindingList<string> EventLog
        {
            get { return allProcesses; }
            set { SetProperty(ref allProcesses, value); } 
        }

    }
}
