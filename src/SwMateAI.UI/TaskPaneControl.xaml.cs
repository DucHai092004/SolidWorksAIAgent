using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SwMateAI.UI.ViewModels;

namespace SwMateAI.UI
{
    /// <summary>
    /// WPF UserControl for the SW-MATE AI Task Pane.
    /// DataContext is set externally by the add-in.
    /// </summary>
    public partial class TaskPaneControl : UserControl
    {
        private bool _isInitialized;

        public TaskPaneControl()
        {
            InitializeComponent();
            ApplyLanguageSelectorContrast();
            _isInitialized = true;
        }

        private void ApplyLanguageSelectorContrast()
        {
            if (LanguageCombo == null) return;

            var surface = new SolidColorBrush(Color.FromRgb(22, 36, 58));
            var foreground = Brushes.White;
            LanguageCombo.Background = surface;
            LanguageCombo.Foreground = foreground;
            LanguageCombo.FontWeight = FontWeights.SemiBold;

            foreach (var rawItem in LanguageCombo.Items)
            {
                if (!(rawItem is ComboBoxItem item)) continue;
                item.Background = surface;
                item.Foreground = foreground;
                item.FontWeight = FontWeights.SemiBold;
                item.Padding = new Thickness(6, 4, 6, 4);
            }
        }

        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (!(LanguageCombo?.SelectedItem is ComboBoxItem item)) return;

            var languageCode = item.Tag as string ?? "vi-VN";
            ApplyLanguage(languageCode);
            ApplyLanguageSelectorContrast();

            if (DataContext is TaskPaneViewModel viewModel)
                viewModel.SetUiLanguage(languageCode);
        }

        private void ApplyLanguage(string languageCode)
        {
            var source = new Uri(
                $"/SwMateAI.UI;component/Localization/Strings.{languageCode}.xaml",
                UriKind.Relative);
            var dictionary = new ResourceDictionary { Source = source };

            if (Resources.MergedDictionaries.Count == 0)
                Resources.MergedDictionaries.Add(dictionary);
            else
                Resources.MergedDictionaries[0] = dictionary;
        }
    }
}
