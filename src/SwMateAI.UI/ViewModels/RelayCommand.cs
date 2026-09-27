using System;
using System.Windows.Input;

namespace SwMateAI.UI.ViewModels
{
    /// <summary>
    /// Standard ICommand implementation for WPF data binding.
    /// Wraps a delegate action so ViewModels can expose commands without code-behind.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) =>
            _canExecute == null || _canExecute();

        public void Execute(object parameter) =>
            _execute();

        /// <summary>Forces WPF to re-evaluate CanExecute for all RelayCommands.</summary>
        public static void RaiseCanExecuteChanged() =>
            CommandManager.InvalidateRequerySuggested();
    }
}
