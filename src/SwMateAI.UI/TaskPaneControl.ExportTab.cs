using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SwMateAI.Core.Exporting;
using SwMateAI.UI.ViewModels;

namespace SwMateAI.UI
{
    public partial class TaskPaneControl
    {
        private bool _exportTabInstalled;
        private TextBox _exportOutputFolderBox;
        private TextBlock _exportOutputFolderStatus;
        private StackPanel _exportActionsPanel;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Loaded += ExportTab_Loaded;
        }

        private void ExportTab_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(InstallExportTab), DispatcherPriority.Loaded);
        }

        private void InstallExportTab()
        {
            if (_exportTabInstalled) return;
            var root = Content as Grid;
            if (root == null) return;

            var tabs = root.Children.OfType<TabControl>()
                .FirstOrDefault(child => Grid.GetRow(child) == 1);
            if (tabs == null) return;
            if (tabs.Items.OfType<TabItem>().Any(x => Convert.ToString(x.Header) == "Bóc tách & Xuất"))
            {
                _exportTabInstalled = true;
                return;
            }

            tabs.Items.Add(new TabItem
            {
                Header = "Bóc tách & Xuất",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(22, 36, 58)),
                Content = BuildExportPanel()
            });
            _exportTabInstalled = true;
        }

        private UIElement BuildExportPanel()
        {
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = new SolidColorBrush(Color.FromRgb(11, 18, 32))
            };
            var panel = new StackPanel { Margin = new Thickness(10) };
            scroll.Content = panel;

            panel.Children.Add(new TextBlock
            {
                Text = "BÓC TÁCH & XUẤT",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Version 1 · xử lý tuần tự từng chức năng theo bộ 100 test case.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                Margin = new Thickness(0, 3, 0, 10)
            });

            panel.Children.Add(BuildOutputFolderCard());
            _exportActionsPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            panel.Children.Add(_exportActionsPanel);
            return scroll;
        }

        private UIElement BuildOutputFolderCard()
        {
            var border = ExportCard();
            var panel = new StackPanel();
            border.Child = panel;

            panel.Children.Add(new TextBlock
            {
                Text = "THƯ MỤC ĐẦU RA",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Để trống: lưu cùng thư mục file CAD đang mở. Có thể nhập đường dẫn tuyệt đối hoặc UNC.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 6)
            });

            _exportOutputFolderBox = new TextBox
            {
                Text = string.Empty,
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 54, 79)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 5, 7, 5),
                FontSize = 10
            };
            panel.Children.Add(_exportOutputFolderBox);

            var validate = new Button
            {
                Content = "KIỂM TRA THƯ MỤC",
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 7, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            validate.Click += ValidateOutputFolder_Click;
            panel.Children.Add(validate);

            _exportOutputFolderStatus = new TextBlock
            {
                Text = "Chưa kiểm tra.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            panel.Children.Add(_exportOutputFolderStatus);
            return border;
        }

        private void ValidateOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            var result = ResolveExportOutputPath("SW-MATE-AI-Check", ".tmp");
            if (result.Success)
            {
                _exportOutputFolderStatus.Text = "[PASS] " + result.DirectoryPath +
                    (string.IsNullOrWhiteSpace(result.Warning) ? string.Empty : "\n" + result.Warning);
                _exportOutputFolderStatus.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            }
            else
            {
                _exportOutputFolderStatus.Text = "[FAIL] " + result.ErrorMessage;
                _exportOutputFolderStatus.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            }
        }

        private OutputPathResult ResolveExportOutputPath(string baseFileName, string extension)
        {
            var viewModel = DataContext as TaskPaneViewModel;
            return new OutputPathResolver().Resolve(new OutputPathRequest
            {
                RequestedDirectory = _exportOutputFolderBox == null ? string.Empty : _exportOutputFolderBox.Text,
                ActiveModelPath = viewModel == null ? string.Empty : viewModel.FilePath,
                BaseFileName = baseFileName ?? string.Empty,
                Extension = extension ?? string.Empty
            });
        }

        private static Border ExportCard()
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(17, 28, 46)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 54, 79)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 8)
            };
        }
    }
}
