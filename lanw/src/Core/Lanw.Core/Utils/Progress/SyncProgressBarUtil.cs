using System.Text;

namespace Lanw.Core.Utils.Progress;

public static class SyncProgressBarUtil {
    private static readonly Lock SyncLock = new();

    private static bool? _consoleAvailable;

    /// <summary>
    /// 当前进程是否已附加控制台。
    /// WinUI 桌面应用（WinExe）没有控制台，读取 Console.BufferWidth 会抛
    /// IOException("句柄无效")，因此渲染前必须先探测一次并缓存结果；
    /// 否则启动流程中的进度上报会把异常抛进业务链路。
    /// </summary>
    public static bool IsConsoleAvailable
    {
        get
        {
            if (_consoleAvailable is bool cached) {
                return cached;
            }

            bool available;
            try {
                _ = Console.BufferWidth;
                available = true;
            } catch (Exception) {
                available = false;
            }

            _consoleAvailable = available;
            return available;
        }
    }

    private static string Fg(ConsoleColor c)
    {
        return c switch {
            ConsoleColor.Black => "\e[30m",
            ConsoleColor.DarkRed => "\e[31m",
            ConsoleColor.DarkGreen => "\e[32m",
            ConsoleColor.DarkYellow => "\e[33m",
            ConsoleColor.DarkBlue => "\e[34m",
            ConsoleColor.DarkMagenta => "\e[35m",
            ConsoleColor.DarkCyan => "\e[36m",
            ConsoleColor.Gray => "\e[37m",
            ConsoleColor.DarkGray => "\e[90m",
            ConsoleColor.Red => "\e[91m",
            ConsoleColor.Green => "\e[92m",
            ConsoleColor.Yellow => "\e[93m",
            ConsoleColor.Blue => "\e[94m",
            ConsoleColor.Magenta => "\e[95m",
            ConsoleColor.Cyan => "\e[96m",
            ConsoleColor.White => "\e[97m",
            _ => "\e[37m"
        };
    }

    public class BarConfig {
        public int Width { get; set; } = 40;

        public char FillChar { get; set; } = '■';

        public char EmptyChar { get; set; } = '·';

        public bool ShowPercentage { get; set; } = true;

        public bool ShowElapsed { get; set; } = true;

        public bool ShowEta { get; set; } = true;

        public bool ShowSpinner { get; set; } = true;

        public ConsoleColor BarColor { get; set; } = ConsoleColor.Green;

        public ConsoleColor DoneColor { get; set; } = ConsoleColor.Cyan;

        public ConsoleColor EmptyColor { get; set; } = ConsoleColor.DarkGray;

        public ConsoleColor SpinnerColor { get; set; } = ConsoleColor.Yellow;

        public string Prefix { get; set; } = string.Empty;

        public string Suffix { get; set; } = string.Empty;

        public bool NewlineOnComplete { get; set; } = true;
    }

    public class ProgressBar(int total = 100, BarConfig? config = null) : IDisposable {
        private static readonly char[] BrailleFrames = [
            '|', '/', '─', '\\'
        ];

        private readonly BarConfig _cfg = config ?? new BarConfig();

        private readonly DateTime _startTime = DateTime.Now;

        private double _current;

        private bool _disposed;

        private int _tick;

        public void Dispose()
        {
            if (!_disposed) {
                if (_current < total) {
                    Report(total, "Done");
                }

                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        public void Update(ProgressReport update)
        {
            Report(update.Percent, update.Message);
        }

        private void Report(double current, string message)
        {
            if (_disposed) {
                return;
            }

            _current = current;
            _tick++;
            Render(message);
        }

        private void Render(string action)
        {
            // 无控制台（WinUI 桌面应用）：跳过渲染，避免 Console.BufferWidth/Write 抛 IOException
            if (!IsConsoleAvailable) {
                return;
            }

            using (SyncLock.EnterScope()) {
                ClearCurrent();

                var ratio = Math.Clamp(_current / total, 0.0, 1.0);
                var filled = (int)(ratio * _cfg.Width);
                var empty = _cfg.Width - filled;
                var complete = ratio >= 1.0;

                var sb = new StringBuilder(256);
                sb.Append(_cfg.Prefix);

                // ── Braille spinner / done icon ──
                if (_cfg.ShowSpinner) {
                    if (complete) {
                        sb.Append(Fg(ConsoleColor.Green));
                        sb.Append('√');
                    } else {
                        sb.Append(Fg(_cfg.SpinnerColor));
                        sb.Append(BrailleFrames[_tick % BrailleFrames.Length]);
                    }

                    sb.Append("\e[0m ");
                }

                // ── Bar with block-element caps ──
                sb.Append(Fg(ConsoleColor.DarkGray));
                sb.Append('▕');

                sb.Append(Fg(complete ? _cfg.DoneColor : _cfg.BarColor));
                sb.Append(new string(_cfg.FillChar, filled));

                sb.Append(Fg(_cfg.EmptyColor));
                sb.Append(new string(_cfg.EmptyChar, empty));

                sb.Append(Fg(ConsoleColor.DarkGray));
                sb.Append('▏');
                sb.Append("\e[0m");

                // ── Action text ──
                if (!string.IsNullOrEmpty(action)) {
                    sb.Append($" {action}");
                }

                // ── Percentage ──
                if (_cfg.ShowPercentage) {
                    sb.Append($" {ratio:P1}");
                }

                // ── Elapsed [mm:ss] ──
                var elapsed = DateTime.Now - _startTime;
                if (_cfg.ShowElapsed) {
                    sb.Append(Fg(ConsoleColor.DarkGray));
                    sb.Append($" [{elapsed:mm\\:ss}]");
                    sb.Append("\e[0m");
                }

                // ── ETA <mm:ss> ──
                if (_cfg.ShowEta && _current > 0 && !complete) {
                    var remaining = TimeSpan.FromMilliseconds(elapsed.TotalMilliseconds / _current * (total - _current));
                    sb.Append(Fg(_cfg.SpinnerColor));
                    sb.Append($" <{remaining:mm\\:ss}>");
                    sb.Append("\e[0m");
                }

                sb.Append(_cfg.Suffix);
                sb.Append("\e[0m");

                try {
                    Console.Write($"\r{sb}");

                    if (complete && _cfg.NewlineOnComplete) {
                        Console.WriteLine();
                    }
                } catch (IOException) {
                    // 控制台句柄失效：放弃后续渲染，不影响启动流程
                    _consoleAvailable = false;
                }
            }
        }

        public static void ClearCurrent()
        {
            if (!IsConsoleAvailable) {
                return; // 无控制台（WinUI），无事可做
            }

            try {
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            } catch (IOException) {
                // 控制台在运行中消失（句柄失效）：忽略，不能影响启动流程
            }
        }
    }

    public class ProgressReport {
        public required double Percent { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}