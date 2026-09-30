using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SwMateAI.UI
{
    public partial class TaskPaneControl
    {
        private readonly object _extractionTaskSync = new object();
        private readonly List<Button> _extractionTaskButtons = new List<Button>();
        private string _activeExtractionTask = string.Empty;
        private DateTime _activeExtractionStartedUtc;
        private ProgressBar _extractionProgressBar;
        private TextBlock _extractionProgressText;

        private bool TryBeginExtractionTask(string taskName, TextBlock status)
        {
            lock (_extractionTaskSync)
            {
                if (!string.IsNullOrWhiteSpace(_activeExtractionTask))
                {
                    TimeSpan elapsed = DateTime.UtcNow - _activeExtractionStartedUtc;
                    SetBomStatus(
                        status,
                        "[ĐANG BẬN] " + _activeExtractionTask + " đang chạy (" + FormatElapsed(elapsed) + "). Hãy chờ tác vụ này hoàn tất trước khi chạy tác vụ khác.",
                        251, 191, 36);
                    return false;
                }

                _activeExtractionTask = taskName ?? "Tác vụ";
                _activeExtractionStartedUtc = DateTime.UtcNow;
            }

            SetExtractionButtonsEnabled(false);
            UpdateExtractionProgress(0, 0, _activeExtractionTask + " — đang chuẩn bị...");
            return true;
        }

        private void EndExtractionTask(string detail = null)
        {
            string finishedTask;
            TimeSpan elapsed;
            lock (_extractionTaskSync)
            {
                finishedTask = _activeExtractionTask;
                elapsed = string.IsNullOrWhiteSpace(finishedTask)
                    ? TimeSpan.Zero
                    : DateTime.UtcNow - _activeExtractionStartedUtc;
                _activeExtractionTask = string.Empty;
                _activeExtractionStartedUtc = DateTime.MinValue;
            }

            SetExtractionButtonsEnabled(true);
            if (!string.IsNullOrWhiteSpace(detail))
                UpdateExtractionProgress(1, 1, detail);
            else if (!string.IsNullOrWhiteSpace(finishedTask))
                UpdateExtractionProgress(1, 1, finishedTask + " — hoàn tất trong " + FormatElapsed(elapsed) + ".");
            else
                UpdateExtractionProgress(0, 1, "Sẵn sàng.");
        }

        private void UpdateExtractionProgress(double current, double total, string detail)
        {
            Action update = delegate
            {
                if (_extractionProgressBar != null)
                {
                    bool indeterminate = total <= 0;
                    _extractionProgressBar.IsIndeterminate = indeterminate;
                    if (!indeterminate)
                    {
                        _extractionProgressBar.Minimum = 0;
                        _extractionProgressBar.Maximum = Math.Max(1, total);
                        _extractionProgressBar.Value = Math.Max(0, Math.Min(total, current));
                    }
                }

                if (_extractionProgressText != null)
                {
                    string prefix = total > 0
                        ? Math.Round(Math.Max(0, Math.Min(100, current / total * 100)), 0) + "% — "
                        : string.Empty;
                    _extractionProgressText.Text = prefix + (detail ?? string.Empty);
                }
            };

            if (Dispatcher.CheckAccess()) update();
            else Dispatcher.BeginInvoke(update);
        }

        private void SetExtractionButtonsEnabled(bool enabled)
        {
            Action update = delegate
            {
                foreach (Button button in _extractionTaskButtons)
                    if (button != null) button.IsEnabled = enabled;
            };

            if (Dispatcher.CheckAccess()) update();
            else Dispatcher.BeginInvoke(update);
        }

        private Button BuildTaskButton(string text, RoutedEventHandler click)
        {
            var button = BuildActionButton(text, click);
            _extractionTaskButtons.Add(button);
            return button;
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalHours >= 1)
                return ((int)elapsed.TotalHours) + "h " + elapsed.Minutes + "m";
            if (elapsed.TotalMinutes >= 1)
                return ((int)elapsed.TotalMinutes) + "m " + elapsed.Seconds + "s";
            return Math.Max(0, (int)elapsed.TotalSeconds) + "s";
        }
    }
}
