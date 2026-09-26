// 多个测试类会改写 AppPaths.DataRoot 来做数据隔离，
// 并行跑会互相踩，因此整个测试程序集串行执行。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
