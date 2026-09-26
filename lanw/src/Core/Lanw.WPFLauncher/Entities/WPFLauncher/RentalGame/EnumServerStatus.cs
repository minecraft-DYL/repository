namespace Lanw.WPFLauncher.Entities.WPFLauncher.RentalGame;

/// <summary>
/// 租赁服服务器状态（由 Nirvana.WPFLauncher.Entities.WPFLauncher.RentalGame.EnumServerStatus 移植，自研）。
/// </summary>
public enum EnumServerStatus
{
    None = -1, // 0xFFFFFFFF
    ServerOff = 0,
    ServerOn = 1,
    Uninitialized = 2,
    Opening = 3,
    Closing = 4,
    OutOfDate = 5,
    SaveCleaning = 6,
    Resetting = 7,
    Upgrading = 8,
    DiscOverflow = 9
}
