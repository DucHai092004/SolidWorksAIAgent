using System.Windows.Controls;

namespace SwMateAI.UI
{
    /// <summary>
    /// WPF UserControl for the SW-MATE AI Task Pane.
    /// DataContext is set externally by the add-in (SwMateAddin.OnConnect) to a
    /// <see cref="ViewModels.TaskPaneViewModel"/> instance.
    /// This control has NO dependency on SOLIDWORKS API or xCAD.NET.
    /// </summary>
    public partial class TaskPaneControl : UserControl
    {
        public TaskPaneControl()
        {
            InitializeComponent();
        }
    }
}
