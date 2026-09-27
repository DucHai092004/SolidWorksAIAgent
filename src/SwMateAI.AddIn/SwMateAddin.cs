using System;
using System.Runtime.InteropServices;
using Xarial.XCad.Base.Attributes;
using Xarial.XCad.SolidWorks;
using Xarial.XCad.SolidWorks.UI;
using Xarial.XCad.UI;
using SwMateAI.Core.Agent;
using SwMateAI.UI;
using SwMateAI.UI.ViewModels;

namespace SwMateAI.AddIn
{
    /// <summary>
    /// SW-MATE AI SOLIDWORKS Add-In entry point.
    ///
    /// Inherits <see cref="SwAddInEx"/> from xCAD.NET which handles:
    ///   • ISwAddin implementation (ConnectToSW / DisconnectFromSW)
    ///   • COM registration via [ComRegisterFunction] (defined in base class)
    ///   • SOLIDWORKS registry keys (written by xCAD's RegistrationHelper on build)
    ///   • Task pane creation and lifecycle
    ///
    /// This class:
    ///   • Creates <see cref="AgentCore"/> with the live ISldWorks reference
    ///   • Creates the WPF Task Pane (<see cref="TaskPaneControl"/>)
    ///   • Wires the <see cref="TaskPaneViewModel"/> as the DataContext
    ///   • Triggers an initial GetModelInfo on connect
    /// </summary>
    [ComVisible(true)]
    [Guid("5C9A5E3B-4F8A-4B2C-9D1E-A7F2B3C4D5E6")]
    [Title("SW-MATE AI")]
    public class SwMateAddin : SwAddInEx
    {
        // ─── Fields ───────────────────────────────────────────────────────────

        private AgentCore _agentCore;
        private IXTaskPane<TaskPaneControl> _taskPane;
        private TaskPaneViewModel _viewModel;

        // ─── Add-In Lifecycle ─────────────────────────────────────────────────

        /// <summary>
        /// Called by SOLIDWORKS when the add-in is loaded.
        /// Sets up the AgentCore and WPF Task Pane.
        /// </summary>
        public override void OnConnect()
        {
            try
            {
                // Get the raw ISldWorks pointer from the xCAD application wrapper.
                // ISwApplication.Sw exposes the underlying COM object.
                var swApp = ((ISwApplication)Application).Sw;

                // Initialise AgentCore with the live SOLIDWORKS application reference.
                _agentCore = new AgentCore(swApp);

                // Create the Task Pane with the WPF UserControl.
                // xCAD.NET handles the WinForms/ElementHost bridge internally.
                _taskPane = CreateTaskPane<TaskPaneControl>();

                // Wire the ViewModel as DataContext.
                _viewModel = new TaskPaneViewModel(_agentCore);
                _taskPane.Control.DataContext = _viewModel;

                // Trigger initial model info load.
                _viewModel.RefreshInfo();
            }
            catch (Exception ex)
            {
                // Surface any startup errors to the user via SOLIDWORKS message box.
                Application.ShowMessageBox(
                    $"SW-MATE AI failed to load:\n\n{ex.Message}\n\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Called by SOLIDWORKS when the add-in is unloaded (SOLIDWORKS closing
        /// or user disabling add-in from Tools > Add-Ins).
        /// Must release the Task Pane to prevent crashes or memory leaks.
        /// </summary>
        public override void OnDisconnect()
        {
            try
            {
                _taskPane?.Close();
            }
            catch
            {
                // Suppress errors on disconnect to avoid crashing SOLIDWORKS on exit.
            }
            finally
            {
                _taskPane  = null;
                _viewModel = null;
                _agentCore = null;
            }
        }
    }
}
