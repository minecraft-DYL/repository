namespace Lanw.WPFLauncher.Entities;

/// <summary>
/// X19 业务异常（由 Nirvana.WPFLauncher.Entities.EntityX19Exception 移植，自研）。
/// </summary>
public class EntityX19Exception(string message, object entityWpfLauncher) : Exception(message)
{
    public new object Data { get; } = entityWpfLauncher;
}