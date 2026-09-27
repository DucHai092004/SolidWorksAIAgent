using System;
using System.Windows;
using System.Windows.Controls;
using SwMateAI.UI.ViewModels;

namespace SwMateAI.UI
{
    /// <summary>
    /// WPF UserControl for the SW-MATE AI Task Pane.
    /// DataContext is set externally by the add-in.
    /// </summary>
    public partial class TaskPaneControl : UserControl
    {
        public TaskPaneControl()
        {
            InitializeComponent();
        }

        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(LanguageCombo?.SelectedItem is ComboBoxItem item)) return;

            var languageCode = item.Tag as string ?? "vi-VN";
            ApplyLanguage(languageCode);

            if (DataContext is TaskPaneViewModel viewModel)
                viewModel.SetUiLanguage(languageCode);
        }

        private void ApplyLanguage(string languageCode)
        {
            var source = new Uri($"Localization/Strings.{languageCode}.xaml", UriKind.Relative);
            var dictionary = new ResourceDictionary { Source = source };

            if (Resources.MergedDictionaries.Count == 0)
                Resources.MergedDictionaries.Add(dictionary);
            else
                Resources.MergedDictionaries[0] = dictionary;
        }
    }
}
