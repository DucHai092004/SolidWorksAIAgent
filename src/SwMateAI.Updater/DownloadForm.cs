using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SwMateAI.Updater
{
    /// <summary>Displays real download progress and leaves SOLIDWORKS untouched.</summary>
    internal sealed class DownloadForm : Form
    {
        private readonly ReleaseInfo _release;
        private readonly Label _status;
        private readonly ProgressBar _progress;

        internal DownloadForm(ReleaseInfo release)
        {
            _release = release;
            Text = "SW-MATE AI - Tải cập nhật";
            Width = 440;
            Height = 155;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            _status = new Label
            {
                Text = "Đang tải bản " + release.Version.ToString(3) + "...",
                AutoSize = false,
                Left = 18, Top = 18, Width = 390, Height = 34
            };
            _progress = new ProgressBar
            {
                Left = 18, Top = 65, Width = 390, Height = 22,
                Minimum = 0, Maximum = 100
            };
            Controls.Add(_status);
            Controls.Add(_progress);
            Shown += async (sender, args) => await DownloadAndStageAsync();
        }

        private async Task DownloadAndStageAsync()
        {
            try
            {
                var progress = new Progress<int>(percent =>
                {
                    _progress.Value = Math.Max(0, Math.Min(100, percent));
                    _status.Text = "Đang tải bản cập nhật... " + percent + "%";
                });
                string file = await UpdateService.DownloadAsync(_release, progress);
                _status.Text = "Đã tải và xác minh SHA-256.";
                DialogResult answer = MessageBox.Show(
                    "Bản cập nhật đã tải và kiểm tra SHA-256 thành công.\n\n" +
                    "Hãy lưu các file CAD và đóng SOLIDWORKS.\n" +
                    "Trình cập nhật sẽ đợi SOLIDWORKS đóng rồi tự cài phiên bản mới.\n\n" +
                    "Tiếp tục?",
                    "SW-MATE AI", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                    Program.StartApplyProcess(file, _release.Sha256);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể tải bản cập nhật:\n" + ex.Message,
                    "SW-MATE AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Close(); }
        }
    }
}
