using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Tools;
using SwMateAI.UI.ViewModels;

namespace SwMateAI.UI
{
    public partial class TaskPaneControl
    {
        private bool _extractionTabInstalled;

        private void InstallExtractionExportTab()
        {
            if (_extractionTabInstalled) return;

            var root = Content as Grid;
            if (root == null) return;

            var tabs = root.Children
                .OfType<TabControl>()
                .FirstOrDefault(child => Grid.GetRow(child) == 1);
            if (tabs == null) return;

            var agent = TryGetAgentCore();
            if (agent == null) return;

            NormalizeTabHeaders(tabs);

            tabs.Items.Add(new TabItem
            {
                Header = BuildTabHeader("Bóc tách & Xuất"),
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(22, 36, 58)),
                Content = BuildExtractionExportPanel(agent)
            });

            _extractionTabInstalled = true;
        }

        private static void NormalizeTabHeaders(TabControl tabs)
        {
            if (tabs == null) return;

            foreach (object rawItem in tabs.Items)
            {
                var tab = rawItem as TabItem;
                if (tab == null || tab.Header is Border) continue;

                string text = Convert.ToString(tab.Header);
                if (string.IsNullOrWhiteSpace(text)) continue;
                tab.Header = BuildTabHeader(text);
            }
        }

        private static Border BuildTabHeader(string text)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 36, 58)),
                Padding = new Thickness(7, 3, 7, 3),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 9
                }
            };
        }

        private UIElement BuildExtractionExportPanel(AgentCore agent)
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
                Text = "BÓC TÁCH & XUẤT HỒ SƠ",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 14
            });
            panel.Children.Add(new TextBlock
            {
                Text = "BOM, bảng phôi, bản vẽ chi tiết và PDF — chạy trực tiếp từ model đang mở.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 10)
            });

            var outputBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 54, 79)),
                Padding = new Thickness(7, 5, 7, 5),
                FontSize = 9,
                ToolTip = "Để trống để SW-MATE AI tự tạo thư mục SW-MATE_AI_Output cạnh model."
            };

            panel.Children.Add(BuildSection(
                "THƯ MỤC ĐẦU RA",
                "Để trống = dùng thư mục đầu ra tự động cạnh file SolidWorks.",
                outputBox));

            var status = new TextBlock
            {
                Text = "Sẵn sàng.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap
            };

            var bomButtons = new WrapPanel();
            bomButtons.Children.Add(BuildActionButton("XUẤT BOM EXCEL", delegate
            {
                RunExtractionTool(
                    agent,
                    "CreateBOM",
                    new Dictionary<string, object>
                    {
                        ["Mode"] = "LegacyFlat",
                        ["RespectChildDisplay"] = true,
                        ["ExportExcel"] = true,
                        ["ExportCsv"] = false,
                        ["OutputFolder"] = PrepareOutputFolder(outputBox.Text)
                    },
                    status,
                    "BOM Excel");
            }));

            bomButtons.Children.Add(BuildActionButton("XUẤT BẢNG PHÔI", delegate
            {
                string folder = PrepareOutputFolder(outputBox.Text);
                var parameters = new Dictionary<string, object>();
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    parameters["OutputPath"] = Path.Combine(
                        folder,
                        "StockMaterial_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");
                }
                RunExtractionTool(agent, "ExportManufacturingBreakdown", parameters, status, "Bảng phôi");
            }));

            panel.Children.Add(BuildSection(
                "BOM & BẢNG PHÔI",
                "Xuất BOM Excel hoặc bảng phôi/vật liệu từ Assembly đang mở.",
                bomButtons));

            var drawingButtons = new WrapPanel();
            drawingButtons.Children.Add(BuildActionButton("BẢN VẼ 1 CHI TIẾT", delegate
            {
                RunDrawingPackage(agent, outputBox.Text, false, true, false, status, "Bản vẽ đơn");
            }));

            drawingButtons.Children.Add(BuildActionButton("BẢN VẼ HÀNG LOẠT", delegate
            {
                if (!ConfirmBatch()) return;
                RunDrawingPackage(agent, outputBox.Text, true, true, false, status, "Bản vẽ hàng loạt");
            }));

            panel.Children.Add(BuildSection(
                "XUẤT BẢN VẼ TỰ ĐỘNG",
                "Tạo standard views và lưu SLDDRW. Hàng loạt sẽ xử lý các Part duy nhất trong Assembly.",
                drawingButtons));

            var pdfButtons = new WrapPanel();
            pdfButtons.Children.Add(BuildActionButton("PDF DRAWING HIỆN TẠI", delegate
            {
                string folder = PrepareOutputFolder(outputBox.Text);
                var parameters = new Dictionary<string, object>();
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    parameters["OutputPath"] = Path.Combine(
                        folder,
                        "Drawing_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf");
                }
                RunExtractionTool(agent, "ExportPDF", parameters, status, "PDF Drawing hiện tại");
            }));

            pdfButtons.Children.Add(BuildActionButton("TẠO + PDF 1 CHI TIẾT", delegate
            {
                RunDrawingPackage(agent, outputBox.Text, false, true, true, status, "SLDDRW + PDF đơn");
            }));

            pdfButtons.Children.Add(BuildActionButton("TẠO + PDF HÀNG LOẠT", delegate
            {
                if (!ConfirmBatch()) return;
                RunDrawingPackage(agent, outputBox.Text, true, true, true, status, "SLDDRW + PDF hàng loạt");
            }));

            panel.Children.Add(BuildSection(
                "PDF",
                "Xuất Drawing đang mở sang PDF hoặc tự tạo Drawing + PDF từ Part/Assembly.",
                pdfButtons));

            var refreshButton = BuildActionButton("LÀM MỚI NGỮ CẢNH", delegate
            {
                var viewModel = DataContext as TaskPaneViewModel;
                if (viewModel != null && viewModel.RefreshInfoCommand.CanExecute(null))
                {
                    viewModel.RefreshInfoCommand.Execute(null);
                    status.Text = "[OK] Đã làm mới ngữ cảnh SolidWorks.";
                    status.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                }
            });

            panel.Children.Add(BuildSection(
                "TRẠNG THÁI",
                "Kết quả thao tác gần nhất.",
                status,
                refreshButton));

            return scroll;
        }

        private static Border BuildSection(string title, string description, params UIElement[] children)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(17, 28, 46)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 54, 79)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(9),
                Margin = new Thickness(0, 0, 0, 9)
            };

            var panel = new StackPanel();
            border.Child = panel;
            panel.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 10
            });
            panel.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 8,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 7)
            });

            foreach (UIElement child in children)
            {
                if (child == null) continue;
                panel.Children.Add(child);
                FrameworkElement element = child as FrameworkElement;
                if (element != null && element.Margin == new Thickness(0))
                    element.Margin = new Thickness(0, 0, 0, 5);
            }

            return border;
        }

        private static Button BuildActionButton(string text, RoutedEventHandler click)
        {
            var button = new Button
            {
                Content = text,
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                FontSize = 9,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 6, 6),
                MinWidth = 125
            };
            button.Click += click;
            return button;
        }

        private void RunDrawingPackage(
            AgentCore agent,
            string outputText,
            bool batch,
            bool saveDrawing,
            bool exportPdf,
            TextBlock status,
            string label)
        {
            RunExtractionTool(
                agent,
                "ExportDrawingPackage",
                new Dictionary<string, object>
                {
                    ["Batch"] = batch,
                    ["SaveDrawing"] = saveDrawing,
                    ["ExportPdf"] = exportPdf,
                    ["Projection"] = "Third",
                    ["OutputFolder"] = PrepareOutputFolder(outputText)
                },
                status,
                label);
        }

        private void RunExtractionTool(
            AgentCore agent,
            string toolName,
            Dictionary<string, object> parameters,
            TextBlock status,
            string label)
        {
            status.Text = "[RUN] " + label + "...";
            status.Foreground = new SolidColorBrush(Color.FromRgb(125, 211, 252));

            try
            {
                ToolResult result = agent.ExecuteTool(toolName, parameters);
                if (result.IsSuccess)
                {
                    status.Text = "[OK] " + label + " hoàn tất. " + FormatToolData(result.Data);
                    status.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));

                    var viewModel = DataContext as TaskPaneViewModel;
                    if (viewModel != null && viewModel.RefreshInfoCommand.CanExecute(null))
                        viewModel.RefreshInfoCommand.Execute(null);
                }
                else
                {
                    status.Text = "[LỖI] " + label + ": " + result.ErrorMessage;
                    status.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                }
            }
            catch (Exception ex)
            {
                status.Text = "[EXCEPTION] " + label + ": " + ex.Message;
                status.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            }
        }

        private static string FormatToolData(object data)
        {
            var map = data as IDictionary<string, object>;
            if (map != null)
            {
                object succeeded;
                object failed;
                object folder;
                map.TryGetValue("Succeeded", out succeeded);
                map.TryGetValue("Failed", out failed);
                map.TryGetValue("OutputFolder", out folder);
                return "Thành công=" + Convert.ToString(succeeded) +
                       ", lỗi=" + Convert.ToString(failed) +
                       (folder == null ? string.Empty : ", thư mục=" + Convert.ToString(folder));
            }

            string text = Convert.ToString(data);
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
        }

        private static string PrepareOutputFolder(string raw)
        {
            string folder = (raw ?? string.Empty).Trim().Trim('"');
            if (folder.Length == 0) return string.Empty;
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static bool ConfirmBatch()
        {
            return MessageBox.Show(
                "Thao tác này sẽ tạo file cho nhiều chi tiết trong Assembly.\n\nTiếp tục?",
                "SW-MATE AI - Xuất hàng loạt",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
    }
}
