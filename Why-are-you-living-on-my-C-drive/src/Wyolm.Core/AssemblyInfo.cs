using System.Runtime.CompilerServices;

// 测试需要直接驱动重解析点、安全删除这类内部实现，所以对测试程序集开放内部可见性。
[assembly: InternalsVisibleTo("Wyolm.Core.Tests")]
