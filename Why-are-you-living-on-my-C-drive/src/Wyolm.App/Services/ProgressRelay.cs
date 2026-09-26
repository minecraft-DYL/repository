using Microsoft.UI.Dispatching;
using Wyolm.Core.Models;

namespace Wyolm.App.Services;

/// <summary>
/// 把引擎的进度回调安全地送到 UI 线程上。
/// <para>引擎在后台线程报告进度，而 WinUI 的控件只能在 UI 线程碰，
/// 所以这里统一做一次 DispatcherQueue 转发。</para>
/// </summary>
public sealed class ProgressRelay<T> : IProgress<T>
{
    private readonly DispatcherQueue _dispatcher;
    private readonly Action<T> _apply;

    public ProgressRelay(DispatcherQueue dispatcher, Action<T> apply)
    {
        _dispatcher = dispatcher;
        _apply = apply;
    }

    public void Report(T value)
    {
        if (!_dispatcher.TryEnqueue(() =>
            {
                try { _apply(value); }
                catch (Exception) { }
            }))
        {
            // 队列满了就丢一帧进度，不影响正确性。
        }
    }
}
