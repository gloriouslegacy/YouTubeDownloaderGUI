using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace YouTubeDownloaderGUI
{
    public partial class MainWindow : Window
    {
        private string toolPath;
        private string ytDlpPath;
        private string ffmpegPath;
        private string downloadPath;

        public MainWindow()
        {
            InitializeComponent();

            // 경로 설정
            toolPath = AppDomain.CurrentDomain.BaseDirectory;
            ytDlpPath = Path.Combine(toolPath, "yt-dlp.exe");
            ffmpegPath = Path.Combine(toolPath, "ffmpeg.exe");
            downloadPath = Path.Combine(toolPath, "download");

            if (!Directory.Exists(downloadPath))
            {
                Directory.CreateDirectory(downloadPath);
            }

            // yt-dlp 업데이트 확인 로직 추가
            CheckForYtDlpUpdates();
        }

        private async void CheckForYtDlpUpdates()
        {
            if (File.Exists(ytDlpPath))
            {
                AppendLog("[yt-dlp Checking for updates...]");
                try
                {
                    // yt-dlp -U 명령 실행
                    var updateProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = ytDlpPath,
                            Arguments = "-U",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        },
                        EnableRaisingEvents = true
                    };

                    // 출력 리디렉션
                    updateProcess.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Dispatcher.Invoke(() => AppendLog(e.Data));
                        }
                    };
                    updateProcess.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Dispatcher.Invoke(() => AppendLog("[ERROR] " + e.Data));
                        }
                    };

                    updateProcess.Start();
                    updateProcess.BeginOutputReadLine();
                    updateProcess.BeginErrorReadLine();
                    await Task.Run(() => updateProcess.WaitForExit());
                    AppendLog("✅ yt-dlp update check complete.");
                }
                catch (Exception ex)
                {
                    AppendLog($"[ERROR] Failed to run yt-dlp update: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("yt-dlp.exe 파일이 없습니다. 스크립트 폴더에 넣어주세요.", "파일 없음", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            string urls = txtUrls.Text.Trim();
            if (string.IsNullOrEmpty(urls))
            {
                MessageBox.Show("다운로드할 링크를 입력하세요.");
                return;
            }

            if (!File.Exists(ytDlpPath))
            {
                MessageBox.Show("yt-dlp.exe가 실행 폴더에 없습니다.");
                return;
            }

            if (!File.Exists(ffmpegPath))
            {
                MessageBox.Show("ffmpeg.exe가 실행 폴더에 없습니다.");
                return;
            }

            string[] urlList = urls.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var url in urlList)
            {
                AppendLog($"> Downloading: {url}");

                // 출력 파일명 템플릿
                string outputTemplate = Path.Combine(downloadPath, "%(title)s.%(ext)s");

                string args = "";
                if (comboFormat.SelectedIndex == 0) // Video
                {
                    args =
                        $"-f \"bv*+ba/best\" " +
                        $"--merge-output-format mp4 " +
                        $"--ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\" " +
                        $"-o \"{outputTemplate}\" " +
                        $"\"{url}\"";
                }
                else if (comboFormat.SelectedIndex == 1) // Music
                {
                    args =
                        $"-f bestaudio " +
                        $"--extract-audio --audio-format mp3 --audio-quality 0 " +
                        $"--ffmpeg-location \"{Path.GetDirectoryName(ffmpegPath)}\" " +
                        $"-o \"{outputTemplate}\" " +
                        $"\"{url}\"";
                }

                await RunProcessAsync(ytDlpPath, args);
            }

            AppendLog("✅ 모든 다운로드가 완료되었습니다.");
            progressBar.Value = 0;
        }

        private async Task RunProcessAsync(string fileName, string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Dispatcher.Invoke(() =>
                {
                    AppendLog(e.Data);

                    // 진행률 % 추출
                    var match = Regex.Match(e.Data, @"(\d{1,3}\.\d)%");
                    if (match.Success && double.TryParse(match.Groups[1].Value, out double progress))
                    {
                        progressBar.Value = Math.Min(100, progress);
                    }
                });
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                Dispatcher.Invoke(() => AppendLog("[ERROR] " + e.Data));
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit());
        }

        private void AppendLog(string message)
        {
            txtOutput.AppendText(message + Environment.NewLine);
            txtOutput.ScrollToEnd();
        }
    }
}