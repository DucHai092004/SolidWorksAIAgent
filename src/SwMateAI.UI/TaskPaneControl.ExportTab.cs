using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SwMateAI.Core.BOM;
using SwMateAI.Core.Exporting;
using SwMateAI.Core.Manufacturing;
using SwMateAI.UI.ViewModels;

namespace SwMateAI.UI
{
    public partial class TaskPaneControl
    {
        private bool _exportTabInstalled;
        private bool _exportBusy;
        private TextBox _exportOutputFolderBox;
        private TextBlock _exportOutputFolderStatus;
        private StackPanel _exportActionsPanel;
        private CheckBox _exportBomIncludeHiddenCheck;
        private TextBlock _exportBomStatus;
        private Button _exportBomButton;
        private TextBlock _exportStockStatus;
        private Button _exportStockButton;
        private Button _exportStockCancelButton;
        private StockWorkerJob _activeStockJob;

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
            _exportActionsPanel.Children.Add(BuildBomExcelCard());
            _exportActionsPanel.Children.Add(BuildStockExcelCard());
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

        private UIElement BuildBomExcelCard()
        {
            var border = ExportCard();
            var panel = new StackPanel();
            border.Child = panel;

            panel.Children.Add(new TextBlock
            {
                Text = "BOM EXCEL + ẢNH",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Đọc BOM ở phiên SolidWorks hiện tại, chụp thumbnail tuần tự bằng worker riêng và nhúng ảnh trực tiếp bằng OpenXML. Không cần Microsoft Excel.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 5)
            });

            _exportBomIncludeHiddenCheck = new CheckBox
            {
                Content = "Bao gồm chi tiết đang ẩn",
                IsChecked = true,
                Foreground = Brushes.White,
                FontSize = 9,
                Margin = new Thickness(0, 2, 0, 5)
            };
            panel.Children.Add(_exportBomIncludeHiddenCheck);

            _exportBomButton = new Button
            {
                Content = "XUẤT BOM EXCEL + ẢNH",
                Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 6, 8, 6),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _exportBomButton.Click += ExportBomExcel_Click;
            panel.Children.Add(_exportBomButton);

            _exportBomStatus = new TextBlock
            {
                Text = "Sẵn sàng.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            panel.Children.Add(_exportBomStatus);
            return border;
        }

        private UIElement BuildStockExcelCard()
        {
            var border = ExportCard();
            var panel = new StackPanel();
            border.Child = panel;

            panel.Children.Add(new TextBlock
            {
                Text = "BẢNG PHÔI EXCEL + ẢNH",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Bóc tách phôi từ Assembly bằng worker riêng, tính dạng/kích thước phôi, chụp ảnh và xuất OpenXML. Cột Công nghệ gia công và Nhà gia công được để trống để bổ sung sau.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 6)
            });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            _exportStockButton = new Button
            {
                Content = "XUẤT BẢNG PHÔI EXCEL",
                Background = new SolidColorBrush(Color.FromRgb(14, 165, 233)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 6, 8, 6)
            };
            _exportStockButton.Click += ExportStockExcel_Click;
            buttons.Children.Add(_exportStockButton);

            _exportStockCancelButton = new Button
            {
                Content = "HỦY XUẤT PHÔI",
                Background = new SolidColorBrush(Color.FromRgb(185, 28, 28)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(6, 0, 0, 0),
                IsEnabled = false
            };
            _exportStockCancelButton.Click += CancelStockExcel_Click;
            buttons.Children.Add(_exportStockCancelButton);
            panel.Children.Add(buttons);

            _exportStockStatus = new TextBlock
            {
                Text = "Sẵn sàng. Yêu cầu file Assembly (.SLDASM) đã lưu.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 9,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0)
            };
            panel.Children.Add(_exportStockStatus);
            return border;
        }

        private async void ExportBomExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_exportBusy) return;
            _exportBusy = true;
            if (_exportBomButton != null) _exportBomButton.IsEnabled = false;
            if (_exportStockButton != null) _exportStockButton.IsEnabled = false;

            BomWorkerJob job = null;
            Process workerProcess = null;
            try
            {
                SetBomStatus("Đang đọc cấu trúc BOM...", Color.FromRgb(125, 211, 252));
                await Dispatcher.Yield(DispatcherPriority.Background);

                OutputPathResult output = ResolveExportOutputPath("SW-MATE-AI-BOM", ".xlsx");
                if (!output.Success)
                {
                    SetBomStatus("[FAIL] " + output.ErrorMessage, Color.FromRgb(248, 113, 113));
                    return;
                }

                var agent = TryGetAgentCore();
                if (agent == null)
                {
                    SetBomStatus("[FAIL] Không truy cập được AgentCore.", Color.FromRgb(248, 113, 113));
                    return;
                }

                var parameters = new Dictionary<string, object>
                {
                    ["Mode"] = "Indented",
                    ["RespectChildDisplay"] = true,
                    ["IncludeHidden"] = _exportBomIncludeHiddenCheck == null || _exportBomIncludeHiddenCheck.IsChecked == true,
                    ["ExportExcel"] = false,
                    ["ExportCsv"] = false
                };

                var toolResult = agent.ExecuteTool("CreateBOM", parameters);
                if (!toolResult.IsSuccess)
                {
                    SetBomStatus("[FAIL] " + toolResult.ErrorMessage, Color.FromRgb(248, 113, 113));
                    return;
                }

                var bom = toolResult.Data as BomResult;
                if (bom == null)
                {
                    SetBomStatus("[FAIL] Skill CreateBOM không trả về BomResult.", Color.FromRgb(248, 113, 113));
                    return;
                }

                string assemblyDirectory = Path.GetDirectoryName(typeof(TaskPaneControl).Assembly.Location) ?? string.Empty;
                string workerExe = Path.Combine(assemblyDirectory, "Workers", "Bom", "SwMateAI.BomWorker.exe");
                if (!File.Exists(workerExe))
                {
                    SetBomStatus(
                        "[FAIL] Không tìm thấy Workers\\Bom\\SwMateAI.BomWorker.exe. Hãy build lại project SwMateAI.AddIn.",
                        Color.FromRgb(248, 113, 113));
                    return;
                }

                SetBomStatus(
                    "Đã đọc " + bom.Items.Count + " dòng BOM. Đang tạo thumbnail bằng worker riêng...",
                    Color.FromRgb(125, 211, 252));
                await Dispatcher.Yield(DispatcherPriority.Background);

                var jobBuilder = new BomWorkerJobBuilder();
                job = jobBuilder.Create(bom, workerExe);

                var startInfo = new ProcessStartInfo
                {
                    FileName = workerExe,
                    Arguments = Quote(job.ManifestPath),
                    WorkingDirectory = Path.GetDirectoryName(workerExe) ?? assemblyDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                workerProcess = Process.Start(startInfo);
                if (workerProcess == null)
                {
                    SetBomStatus("[FAIL] Không khởi động được BOM worker.", Color.FromRgb(248, 113, 113));
                    return;
                }

                await Task.Run(() => workerProcess.WaitForExit());

                BomWorkerManifest manifest = BomWorkerManifestSerializer.Read(job.ManifestPath);
                jobBuilder.Apply(bom, manifest);
                if (workerProcess.ExitCode != 0 || !manifest.Finished || !string.IsNullOrWhiteSpace(manifest.FatalError))
                {
                    string workerError = !string.IsNullOrWhiteSpace(manifest.FatalError)
                        ? manifest.FatalError
                        : "Worker exit code=" + workerProcess.ExitCode;
                    SetBomStatus("[FAIL] BOM worker không hoàn tất an toàn: " + workerError,
                        Color.FromRgb(248, 113, 113));
                    return;
                }

                SetBomStatus(
                    "Worker hoàn tất " + bom.CapturedImageCount + "/" + bom.Items.Count +
                    " ảnh. Đang đóng gói Excel...",
                    Color.FromRgb(125, 211, 252));
                await Dispatcher.Yield(DispatcherPriority.Background);

                string exportedPath = new BomExcelExporter().Export(bom, output.FileSystemPath);
                bom.ExcelPath = output.FilePath;
                if (!File.Exists(exportedPath) && !File.Exists(output.FileSystemPath))
                {
                    SetBomStatus("[FAIL] File Excel không tồn tại sau khi xuất.", Color.FromRgb(248, 113, 113));
                    return;
                }

                string summary = "[PASS] " + output.FilePath +
                    "\nDòng BOM: " + bom.Items.Count +
                    " | Ảnh: " + bom.CapturedImageCount +
                    " | Suppressed bỏ qua: " + bom.SuppressedSkipped +
                    " | Hidden bỏ qua: " + bom.HiddenSkipped +
                    " | Missing bỏ qua: " + bom.MissingSkipped;
                if (bom.Warnings.Count > 0)
                    summary += "\nCảnh báo: " + bom.Warnings.Count + " (một số thumbnail/chi tiết cần kiểm tra).";

                SetBomStatus(summary, Color.FromRgb(52, 211, 153));
            }
            catch (Exception ex)
            {
                SetBomStatus("[EXCEPTION] " + ex.Message, Color.FromRgb(248, 113, 113));
            }
            finally
            {
                if (workerProcess != null)
                {
                    try { workerProcess.Dispose(); } catch { }
                }

                if (job != null && !string.IsNullOrWhiteSpace(job.JobDirectory))
                {
                    try { if (Directory.Exists(job.JobDirectory)) Directory.Delete(job.JobDirectory, true); }
                    catch { }
                }

                _exportBusy = false;
                if (_exportBomButton != null) _exportBomButton.IsEnabled = true;
                if (_exportStockButton != null) _exportStockButton.IsEnabled = true;
            }
        }

        private async void ExportStockExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_exportBusy) return;
            _exportBusy = true;
            if (_exportStockButton != null) _exportStockButton.IsEnabled = false;
            if (_exportBomButton != null) _exportBomButton.IsEnabled = false;

            StockWorkerJob job = null;
            Process workerProcess = null;
            try
            {
                var viewModel = DataContext as TaskPaneViewModel;
                string assemblyPath = viewModel == null ? string.Empty : viewModel.FilePath;
                if (string.IsNullOrWhiteSpace(assemblyPath) ||
                    !string.Equals(Path.GetExtension(assemblyPath), ".SLDASM", StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(assemblyPath))
                {
                    SetStockStatus(
                        "[FAIL] Hãy mở và lưu một file Assembly (.SLDASM) trước khi xuất bảng phôi.",
                        Color.FromRgb(248, 113, 113));
                    return;
                }

                OutputPathResult output = ResolveExportOutputPath("SW-MATE-AI-PHOI", ".xlsx");
                if (!output.Success)
                {
                    SetStockStatus("[FAIL] " + output.ErrorMessage, Color.FromRgb(248, 113, 113));
                    return;
                }

                string assemblyDirectory = Path.GetDirectoryName(typeof(TaskPaneControl).Assembly.Location) ?? string.Empty;
                string workerExe = Path.Combine(assemblyDirectory, "Workers", "Stock", "SwMateAI.StockWorker.exe");
                if (!File.Exists(workerExe))
                {
                    SetStockStatus(
                        "[FAIL] Không tìm thấy Workers\\Stock\\SwMateAI.StockWorker.exe. Hãy build lại project SwMateAI.AddIn.",
                        Color.FromRgb(248, 113, 113));
                    return;
                }

                job = new StockWorkerJobBuilder().Create(assemblyPath, output.FileSystemPath);
                _activeStockJob = job;

                var startInfo = new ProcessStartInfo
                {
                    FileName = workerExe,
                    Arguments = Quote(job.ManifestPath),
                    WorkingDirectory = Path.GetDirectoryName(workerExe) ?? assemblyDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                SetStockStatus("Đang bóc tách phôi bằng worker nền...", Color.FromRgb(125, 211, 252));
                await Dispatcher.Yield(DispatcherPriority.Background);

                workerProcess = Process.Start(startInfo);
                if (workerProcess == null)
                {
                    SetStockStatus("[FAIL] Không khởi động được Stock worker.", Color.FromRgb(248, 113, 113));
                    return;
                }
                if (_exportStockCancelButton != null) _exportStockCancelButton.IsEnabled = true;

                await Task.Run(() => workerProcess.WaitForExit());

                StockWorkerManifest manifest = StockWorkerManifestSerializer.Read(job.ManifestPath);
                if (manifest.Cancelled)
                {
                    SetStockStatus(
                        "[CANCELLED] Đã hủy an toàn tại " + manifest.ProcessedItems + "/" + manifest.TotalItems +
                        " chi tiết. Không giữ file Excel dở.",
                        Color.FromRgb(251, 191, 36));
                    return;
                }

                if (workerProcess.ExitCode != 0 || !manifest.Finished || !string.IsNullOrWhiteSpace(manifest.FatalError))
                {
                    string workerError = !string.IsNullOrWhiteSpace(manifest.FatalError)
                        ? manifest.FatalError
                        : "Worker exit code=" + workerProcess.ExitCode;
                    SetStockStatus("[FAIL] Stock worker không hoàn tất an toàn: " + workerError,
                        Color.FromRgb(248, 113, 113));
                    return;
                }

                if (!File.Exists(output.FileSystemPath) || new FileInfo(output.FileSystemPath).Length <= 0)
                {
                    SetStockStatus("[FAIL] File Excel phôi không tồn tại sau khi worker hoàn tất.",
                        Color.FromRgb(248, 113, 113));
                    return;
                }

                SetStockStatus(
                    "[PASS] " + output.FilePath +
                    "\nDòng phôi: " + manifest.OutputRows +
                    " | Ảnh: " + manifest.CapturedImageCount +
                    " | Đã xử lý: " + manifest.ProcessedItems + "/" + manifest.TotalItems,
                    Color.FromRgb(52, 211, 153));
            }
            catch (Exception ex)
            {
                SetStockStatus("[EXCEPTION] " + ex.Message, Color.FromRgb(248, 113, 113));
            }
            finally
            {
                if (_exportStockCancelButton != null) _exportStockCancelButton.IsEnabled = false;
                _activeStockJob = null;

                if (workerProcess != null)
                {
                    try { workerProcess.Dispose(); } catch { }
                }

                if (job != null && !string.IsNullOrWhiteSpace(job.JobDirectory))
                {
                    try { if (Directory.Exists(job.JobDirectory)) Directory.Delete(job.JobDirectory, true); }
                    catch { }
                }

                _exportBusy = false;
                if (_exportStockButton != null) _exportStockButton.IsEnabled = true;
                if (_exportBomButton != null) _exportBomButton.IsEnabled = true;
            }
        }

        private void CancelStockExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_activeStockJob == null)
            {
                SetStockStatus("Không có tác vụ bảng phôi đang chạy.", Color.FromRgb(148, 163, 184));
                return;
            }

            StockWorkerJobBuilder.RequestCancel(_activeStockJob);
            if (_exportStockCancelButton != null) _exportStockCancelButton.IsEnabled = false;
            SetStockStatus("Đã gửi yêu cầu hủy. Worker sẽ dừng ở checkpoint an toàn gần nhất...",
                Color.FromRgb(251, 191, 36));
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private void SetBomStatus(string text, Color color)
        {
            if (_exportBomStatus == null) return;
            _exportBomStatus.Text = text ?? string.Empty;
            _exportBomStatus.Foreground = new SolidColorBrush(color);
        }

        private void SetStockStatus(string text, Color color)
        {
            if (_exportStockStatus == null) return;
            _exportStockStatus.Text = text ?? string.Empty;
            _exportStockStatus.Foreground = new SolidColorBrush(color);
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
