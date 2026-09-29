using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Skills;
using SwMateAI.Core.Tools;
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
        private bool _testTabInstalled;

        public TaskPaneControl()
        {
            InitializeComponent();
            ApplyLanguageSelectorContrast();
            Loaded += TaskPaneControl_Loaded;
            _isInitialized = true;
        }

        private void TaskPaneControl_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyLanguageSelectorContrast();
            InstallVersion1TestTab();
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
                var item = rawItem as ComboBoxItem;
                if (item == null) continue;
                item.Background = surface;
                item.Foreground = foreground;
                item.FontWeight = FontWeights.SemiBold;
                item.Padding = new Thickness(6, 4, 6, 4);
            }
        }

        private void InstallVersion1TestTab()
        {
            if (_testTabInstalled) return;
            var root = Content as Grid;
            if (root == null) return;

            var mainScroll = root.Children
                .OfType<ScrollViewer>()
                .FirstOrDefault(child => Grid.GetRow(child) == 1);
            if (mainScroll == null) return;

            var agent = TryGetAgentCore();
            if (agent == null) return;

            root.Children.Remove(mainScroll);

            var tabs = new TabControl
            {
                Background = new SolidColorBrush(Color.FromRgb(11, 18, 32)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 54, 79)),
                Foreground = Brushes.White
            };
            Grid.SetRow(tabs, 1);

            tabs.Items.Add(new TabItem
            {
                Header = "Agent",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(22, 36, 58)),
                Content = mainScroll
            });

            tabs.Items.Add(new TabItem
            {
                Header = "Kiểm thử",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(22, 36, 58)),
                Content = BuildSkillTestPanel(agent)
            });

            root.Children.Add(tabs);
            _testTabInstalled = true;
        }

        private AgentCore TryGetAgentCore()
        {
            var viewModel = DataContext as TaskPaneViewModel;
            if (viewModel == null) return null;

            var field = typeof(TaskPaneViewModel).GetField(
                "_agentCore",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(viewModel) as AgentCore;
        }

        private UIElement BuildSkillTestPanel(AgentCore agent)
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
                Text = "KIỂM THỬ VERSION 1",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Chạy trực tiếp từng skill. Chỉ dùng file test/disposable, không dùng file sản xuất.",
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 10)
            });

            var metadata = agent.RegisteredSkillMetadata
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Name)
                .ToList();

            foreach (var group in metadata.GroupBy(item => item.Category))
            {
                var groupPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 8) };
                var expander = new Expander
                {
                    Header = group.Key + " (" + group.Count() + ")",
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromRgb(17, 28, 46)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(38, 54, 79)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8),
                    IsExpanded = group.Key.StartsWith("System", StringComparison.OrdinalIgnoreCase)
                };

                foreach (var item in group)
                    groupPanel.Children.Add(BuildSkillCard(agent, item));

                expander.Content = groupPanel;
                panel.Children.Add(expander);
            }

            return scroll;
        }

        private UIElement BuildSkillCard(AgentCore agent, SkillMetadata metadata)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 36, 58)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 54, 79)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 7)
            };
            var panel = new StackPanel();
            border.Child = panel;

            panel.Children.Add(new TextBlock
            {
                Text = metadata.Name,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            });
            panel.Children.Add(new TextBlock
            {
                Text = metadata.Description,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 3)
            });
            panel.Children.Add(new TextBlock
            {
                Text = BuildRequirementText(metadata),
                Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252)),
                FontSize = 8,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 5)
            });

            var parameterBox = new TextBox
            {
                Text = DefaultParametersFor(metadata.ToolName),
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 54, 79)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 9,
                ToolTip = "Định dạng: Key=Value;Key2=Value2"
            };
            panel.Children.Add(parameterBox);

            var resultText = new TextBlock
            {
                Text = "Chưa chạy",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var runButton = new Button
            {
                Content = metadata.RequiresConfirmation || metadata.RiskLevel == SkillRiskLevel.High
                    ? "Chạy (cần xác nhận)"
                    : "Chạy",
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 105
            };
            runButton.Click += delegate
            {
                RunSkillFromTestPanel(agent, metadata, parameterBox.Text, resultText);
            };

            panel.Children.Add(runButton);
            panel.Children.Add(resultText);
            return border;
        }

        private void RunSkillFromTestPanel(
            AgentCore agent,
            SkillMetadata metadata,
            string parameterText,
            TextBlock resultText)
        {
            if (metadata.RequiresConfirmation || metadata.RiskLevel == SkillRiskLevel.High)
            {
                var answer = MessageBox.Show(
                    "Skill '" + metadata.Name + "' có thể thay đổi model. Chỉ tiếp tục trên file test.\n\nTiếp tục?",
                    "SW-MATE AI - Xác nhận kiểm thử",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes)
                {
                    resultText.Text = "[CANCEL] Người dùng hủy kiểm thử.";
                    resultText.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                    return;
                }
            }

            try
            {
                Dictionary<string, object> parameters = ParseParameters(parameterText);
                ToolResult result = agent.ExecuteTool(metadata.ToolName, parameters);
                if (result.IsSuccess)
                {
                    resultText.Text = "[PASS] " + (Convert.ToString(result.Data) ?? "OK");
                    resultText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                    var viewModel = DataContext as TaskPaneViewModel;
                    if (viewModel != null && viewModel.RefreshInfoCommand.CanExecute(null))
                        viewModel.RefreshInfoCommand.Execute(null);
                }
                else
                {
                    resultText.Text = "[FAIL] " + result.ErrorMessage;
                    resultText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                }
            }
            catch (Exception ex)
            {
                resultText.Text = "[EXCEPTION] " + ex.Message;
                resultText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            }
        }

        private static Dictionary<string, object> ParseParameters(string text)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (string rawPair in text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = rawPair.IndexOf('=');
                if (separator <= 0) continue;

                string key = rawPair.Substring(0, separator).Trim();
                string value = rawPair.Substring(separator + 1).Trim();
                if (key.Length == 0) continue;

                bool boolValue;
                double numberValue;
                if (bool.TryParse(value, out boolValue))
                    result[key] = boolValue;
                else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out numberValue))
                    result[key] = numberValue;
                else if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out numberValue))
                    result[key] = numberValue;
                else
                    result[key] = value;
            }
            return result;
        }

        private static string BuildRequirementText(SkillMetadata metadata)
        {
            var parts = new List<string>
            {
                "Risk: " + metadata.RiskLevel
            };
            if (metadata.RequiresPartDocument) parts.Add("Part");
            else if (metadata.RequiresAssemblyDocument) parts.Add("Assembly");
            else if (metadata.RequiresActiveDocument) parts.Add("Active document");
            if (metadata.RequiresSelection) parts.Add("Selection required");
            if (metadata.RequiresConfirmation) parts.Add("Confirmation");
            if (metadata.SupportsUndo) parts.Add("Undo");
            return string.Join(" | ", parts);
        }

        private static string DefaultParametersFor(string toolName)
        {
            switch (toolName)
            {
                case "CreateRectangle": return "Width=60;Height=40";
                case "Extrude": return "Depth=20";
                case "CreateCircle": return "Diameter=10;X=0;Y=0";
                case "CutExtrude": return "Depth=10";
                case "AddDimension": return "Value=20";
                case "ModifyDimension": return "Name=D1@Sketch1;Value=20";
                case "FilletPlateCorners": return "Radius=5";
                case "ChamferPlateCorners": return "Distance=3";
                case "CreatePlate": return "Width=100;Height=80;Thickness=10";
                case "CreatePlateWithHole": return "Width=100;Height=80;Thickness=10;HoleDiameter=10;HoleDepth=10";
                case "AnalyzeFeatureImpact": return "FeatureName=Boss-Extrude1";
                case "InsertComponent": return "Path=;X=0;Y=0;Z=0";
                case "MoveComponent": return "ComponentName=;X=0;Y=0;Z=0";
                case "AddMate": return "MateType=Coincident;Distance=0";
                case "DeleteMate": return "MateName=Coincident1";
                case "ReplaceComponent": return "ComponentName=;NewPath=";
                case "ExportManufacturingBreakdown": return "OutputPath=";
                case "CreateBOM": return "ExportExcel=true;ExportCsv=true;OutputFolder=";
                case "CreateSheet": return "Name=Sheet2;PaperSize=A3;ScaleNumerator=1;ScaleDenominator=1";
                case "InsertStandardViews": return "Projection=Third";
                case "CreateSection": return "Label=A;Direction=Vertical;SourceViewName=Drawing View1";
                case "CreateDetail": return "Label=B;ScaleNumerator=2;ScaleDenominator=1";
                case "InsertDimensions": return "ViewName=Drawing View1";
                case "InsertDrawingBOM": return "TemplatePath=";
                case "FillTitleBlock": return "Title=Test Drawing;DrawingNumber=TEST-001;Revision=A";
                case "ExportPDF": return "OutputPath=";
                case "ExportDXF": return "OutputPath=";
                case "AnalyzeDrawingSource": return "Path=";
                default: return string.Empty;
            }
        }

        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            var item = LanguageCombo == null ? null : LanguageCombo.SelectedItem as ComboBoxItem;
            if (item == null) return;

            var languageCode = item.Tag as string ?? "vi-VN";
            ApplyLanguage(languageCode);
            ApplyLanguageSelectorContrast();

            var viewModel = DataContext as TaskPaneViewModel;
            if (viewModel != null)
                viewModel.SetUiLanguage(languageCode);
        }

        private void ApplyLanguage(string languageCode)
        {
            var source = new Uri(
                "/SwMateAI.UI;component/Localization/Strings." + languageCode + ".xaml",
                UriKind.Relative);
            var dictionary = new ResourceDictionary { Source = source };

            if (Resources.MergedDictionaries.Count == 0)
                Resources.MergedDictionaries.Add(dictionary);
            else
                Resources.MergedDictionaries[0] = dictionary;
        }
    }
}
