using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            var tabs = root.Children.OfType<TabControl>().FirstOrDefault(child => Grid.GetRow(child) == 1);
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
                if (!string.IsNullOrWhiteSpace(text)) tab.Header = BuildTabHeader(text);
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
                Text = "Ưu tiên ổn định: tác vụ nặng chạy tuần tự; BOM và Drawing/PDF batch được tách khỏi phiên SolidWorks chính.",
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
                ToolTip = "Để trống để tự tạo SW-MATE_AI_Output cạnh model đang mở."
            };
            panel.Children.Add(BuildSection(
                "THƯ MỤC ĐẦU RA",
                "Để trống = tự lưu cạnh model. File trùng tên sẽ tự thêm hậu tố, không ghi đè.",
                outputBox));

            var status = new TextBlock
            {
                Text = "Sẵn sàng.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap
            };

            var bomButtons = new WrapPanel();
            bomButtons.Children.Add(BuildActionButton("BOM EXCEL + ẢNH", delegate
            {
                StartBomExcelWorker(agent, outputBox.Text, status);
            }));
            bomButtons.Children.Add(BuildActionButton("BẢNG PHÔI EXCEL", delegate
            {
                try
                {
                    string folder = PrepareOutputFolder(outputBox.Text);
                    var parameters = new Dictionary<string, object>();
                    if (!string.IsNullOrWhiteSpace(folder))
                        parameters["OutputPath"] = Path.Combine(folder, "StockMaterial_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");
                    RunExtractionTool(agent, "ExportManufacturingBreakdown", parameters, status, "Bảng phôi Excel");
                }
                catch (Exception ex)
                {
                    SetBomStatus(status, "[LỖI] Bảng phôi: " + ex.Message, 248, 113, 113);
                }
            }));
            panel.Children.Add(BuildSection(
                "BOM & BẢNG PHÔI",
                "BOM có ảnh chạy worker riêng. Bảng phôi dùng OpenXML, không mở Excel; ảnh chỉ lấy preview an toàn, không đổi Part đang làm việc.",
                bomButtons));

            var drawingButtons = new WrapPanel();
            drawingButtons.Children.Add(BuildActionButton("DRAWING 1 CHI TIẾT", delegate
            {
                RunDrawingPackage(agent, outputBox.Text, false, true, false, status, "Drawing đơn");
            }));
            drawingButtons.Children.Add(BuildActionButton("DRAWING HÀNG LOẠT", delegate
            {
                RunDrawingPackage(agent, outputBox.Text, true, true, false, status, "Drawing hàng loạt");
            }));
            panel.Children.Add(BuildSection(
                "XUẤT BẢN VẼ TỰ ĐỘNG",
                "Tạo standard views và SLDDRW. Batch chạy tuần tự ở SolidWorks nền riêng; lỗi một Part không dừng cả lô.",
                drawingButtons));

            var pdfButtons = new WrapPanel();
            pdfButtons.Children.Add(BuildActionButton("PDF DRAWING HIỆN TẠI", delegate
            {
                try
                {
                    string folder = PrepareOutputFolder(outputBox.Text);
                    var parameters = new Dictionary<string, object>();
                    if (!string.IsNullOrWhiteSpace(folder))
                        parameters["OutputPath"] = Path.Combine(folder, "Drawing_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf");
                    RunExtractionTool(agent, "ExportPDF", parameters, status, "PDF Drawing hiện tại");
                }
                catch (Exception ex)
                {
                    SetBomStatus(status, "[LỖI] PDF: " + ex.Message, 248, 113, 113);
                }
            }));
            pdfButtons.Children.Add(BuildActionButton("DRAWING + PDF 1 CHI TIẾT", delegate
            {
                RunDrawingPackage(agent, outputBox.Text, false, true, true, status, "Drawing + PDF đơn");
            }));
            pdfButtons.Children.Add(BuildActionButton("DRAWING + PDF HÀNG LOẠT", delegate
            {
                RunDrawingPackage(agent, outputBox.Text, true, true, true, status, "Drawing + PDF hàng loạt");
            }));
            panel.Children.Add(BuildSection(
                "PDF",
                "PDF Drawing hiện tại chạy trực tiếp. Tạo Drawing + PDF dùng worker riêng để bảo vệ phiên SolidWorks chính.",
                pdfButtons));

            var utilityButtons = new WrapPanel();
            utilityButtons.Children.Add(BuildActionButton("LÀM MỚI NGỮ CẢNH", delegate
            {
                var viewModel = DataContext as TaskPaneViewModel;
                if (viewModel != null && viewModel.RefreshInfoCommand.CanExecute(null))
                {
                    viewModel.RefreshInfoCommand.Execute(null);
                    SetBomStatus(status, "[OK] Đã làm mới ngữ cảnh SolidWorks.", 52, 211, 153);
                }
            }));
            utilityButtons.Children.Add(BuildSecondaryButton("MỞ THƯ MỤC ĐẦU RA", delegate
            {
                try
                {
                    string folder = ResolveOutputFolder(agent, outputBox.Text);
                    Directory.CreateDirectory(folder);
                    Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    SetBomStatus(status, "[LỖI] Không mở được thư mục: " + ex.Message, 248, 113, 113);
                }
            }));
            panel.Children.Add(BuildSection(
                "TRẠNG THÁI",
                "Kết quả và tiến trình gần nhất. Batch sẽ cập nhật từng file khi worker xử lý.",
                status,
                utilityButtons));

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
            panel.Children.Add(new TextBlock { Text = title, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 10 });
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
                var element = child as FrameworkElement;
                if (element != null && element.Margin == new Thickness(0)) element.Margin = new Thickness(0, 0, 0, 5);
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
                MinWidth = 135
            };
            button.Click += click;
            return button;
        }

        private static Button BuildSecondaryButton(string text, RoutedEventHandler click)
        {
            var button = BuildActionButton(text, click);
            button.Background = new SolidColorBrush(Color.FromRgb(51, 65, 85));
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
            StartDrawingExportWorker(agent, outputText, batch, saveDrawing, exportPdf, status, label);
        }

        private void RunExtractionTool(
            AgentCore agent,
            string toolName,
            Dictionary<string, object> parameters,
            TextBlock status,
            string label)
        {
            SetBomStatus(status, "[RUN] " + label + "...", 125, 211, 252);
            try
            {
                ToolResult result = agent.ExecuteTool(toolName, parameters);
                if (result.IsSuccess)
                {
                    SetBomStatus(status, "[OK] " + label + " hoàn tất. " + FormatToolData(result.Data), 52, 211, 153);
                    var viewModel = DataContext as TaskPaneViewModel;
                    if (viewModel != null && viewModel.RefreshInfoCommand.CanExecute(null)) viewModel.RefreshInfoCommand.Execute(null);
                }
                else
                    SetBomStatus(status, "[LỖI] " + label + ": " + result.ErrorMessage, 248, 113, 113);
            }
            catch (Exception ex)
            {
                SetBomStatus(status, "[EXCEPTION] " + label + ": " + ex.Message, 248, 113, 113);
            }
        }

        private static string FormatToolData(object data)
        {
            var map = data as IDictionary<string, object>;
            if (map != null)
            {
                map.TryGetValue("Succeeded", out object succeeded);
                map.TryGetValue("Failed", out object failed);
                map.TryGetValue("OutputFolder", out object folder);
                map.TryGetValue("LogPath", out object log);
                return "Thành công=" + Convert.ToString(succeeded) +
                       ", lỗi=" + Convert.ToString(failed) +
                       (folder == null ? string.Empty : ", thư mục=" + Convert.ToString(folder)) +
                       (log == null ? string.Empty : ", log=" + Convert.ToString(log));
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

        private static string ResolveOutputFolder(AgentCore agent, string raw)
        {
            string requested = (raw ?? string.Empty).Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(requested)) return requested;
            AgentContext context = agent.ObserveContext();
            string documentPath = context.DocumentPath ?? string.Empty;
            string root = !string.IsNullOrWhiteSpace(documentPath)
                ? Path.GetDirectoryName(documentPath)
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(root ?? string.Empty, "SW-MATE_AI_Output");
        }
    }
}
