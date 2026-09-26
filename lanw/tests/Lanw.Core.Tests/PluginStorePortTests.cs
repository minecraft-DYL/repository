using System.Text.Json;
using Lanw.Core.Utils;
using Lanw.Public.Entities.Nirvana;
using Lanw.Public.Entities.Plugin;
using Lanw.Public.Message;

namespace Lanw.Core.Tests;

/// <summary>插件商城协议（Lanw.Public.Entities.Plugin/Nirvana + 消息面）移植测试。</summary>
public class PluginStorePortTests {
    // ---------- 商城列表/详情实体 ----------

    [Fact]
    public void EntityComponents_ShouldRoundTripContractJson()
    {
        const string json = "{\"id\":\"p1\",\"name\":\"示例插件\",\"shortDescription\":\"desc\",\"publisher\":\"pub\",\"downloadCount\":42}";

        var entity = JsonSerializer.Deserialize<EntityComponents>(json);

        Assert.NotNull(entity);
        Assert.Equal("p1", entity.Id);
        Assert.Equal("示例插件", entity.Name);
        Assert.Equal("desc", entity.ShortDescription);
        Assert.Equal("pub", entity.Publisher);
        Assert.Equal(42, entity.DownloadCount);

        var serialized = JsonSerializer.Serialize(entity);
        Assert.Contains("\"id\":\"p1\"", serialized);
        Assert.Contains("\"downloadCount\":42", serialized);
    }

    [Fact]
    public void EntityPlugin_ShouldDeserializeDependencies()
    {
        const string json = "{\"detailDescription\":\"detail\",\"downloadCount\":7,\"id\":\"p1\",\"logoUrl\":\"logo\",\"name\":\"示例插件\"," +
                            "\"publishDate\":\"2026-01-01\",\"publisher\":\"pub\",\"shortDescription\":\"short\",\"version\":\"1.0.0\"," +
                            "\"dependencies\":[{\"id\":\"dep\",\"name\":\"依赖\"}]}";

        var entity = JsonSerializer.Deserialize<EntityPlugin>(json);

        Assert.NotNull(entity);
        Assert.Equal("detail", entity.DetailDescription);
        Assert.Equal(7, entity.DownloadCount);
        Assert.Equal("logo", entity.LogoUrl);
        Assert.Equal("2026-01-01", entity.PublishDate);
        Assert.Equal("1.0.0", entity.Version);
        var dependency = Assert.Single(entity.Dependencies!);
        Assert.Equal("dep", dependency.Id);
        Assert.Equal("依赖", dependency.Name);
    }

    [Fact]
    public void EntityPluginDependency_ShouldRoundTripIdAndName()
    {
        var dependency = new EntityPluginDependency {
            Id = "dep",
            Name = "依赖"
        };

        var serialized = JsonSerializer.Serialize(dependency);
        Assert.Contains("\"id\":\"dep\"", serialized);

        var parsed = JsonSerializer.Deserialize<EntityPluginDependency>(serialized);
        Assert.NotNull(parsed);
        Assert.Equal("dep", parsed.Id);
        Assert.Equal("依赖", parsed.Name);
    }

    [Fact]
    public void EntityPluginDownResponse_ShouldDeserializeNestedDependencies()
    {
        const string json = "{\"fileHash\":\"HASH\",\"fileSize\":1024,\"id\":\"p1\",\"dependencies\":[{\"fileHash\":\"H2\",\"fileSize\":64,\"id\":\"dep\"}]}";

        var entity = JsonSerializer.Deserialize<EntityPluginDownResponse>(json);

        Assert.NotNull(entity);
        Assert.Equal("HASH", entity.FileHash);
        Assert.Equal(1024, entity.FileSize);
        Assert.Equal("p1", entity.Id);
        var dependency = Assert.Single(entity.Dependencies!);
        Assert.Equal("dep", dependency.Id);
        Assert.Equal(64, dependency.FileSize);
        Assert.Null(dependency.Dependencies);
    }

    // ---------- 服务器依赖实体 ----------

    [Fact]
    public void EntityDependence_ShouldDeserializeDependencyIds()
    {
        const string json = "{\"data\":[{\"id\":\"a\"},{\"id\":\"b\"}]}";

        var entity = JsonSerializer.Deserialize<EntityDependence>(json);

        Assert.NotNull(entity);
        Assert.Equal(2, entity.Data.Length);
        Assert.Equal(new[] { "a", "b" }, entity.Data.Select(item => item.Id).ToArray());
    }

    // ---------- 插件程序集指纹 ----------

    [Fact]
    public void EntityPluginAssembly_ShouldCompareBySha256()
    {
        var path = Path.Combine(Path.GetTempPath(), "lanw-plugin-" + Guid.NewGuid().ToString("N") + ".dll");
        var other = Path.Combine(Path.GetTempPath(), "lanw-plugin-" + Guid.NewGuid().ToString("N") + ".dll");
        try {
            File.WriteAllText(path, "plugin-binary");
            File.WriteAllText(other, "plugin-binary");
            File.WriteAllText(other + ".other", "different-binary");

            var entity = new EntityPluginAssembly(path, typeof(EntityPluginAssembly).Assembly);

            Assert.Same(typeof(EntityPluginAssembly).Assembly, entity.Assembly);
            Assert.True(entity.Equals(path));
            Assert.True(entity.Equals(other)); // 内容相同 → SHA256 相同
            Assert.False(entity.Equals(other + ".other"));
            Assert.Equal(Tools.ComputeSha256(path), Tools.ComputeSha256(other));
        } finally {
            File.Delete(path);
            File.Delete(other);
            File.Delete(other + ".other");
        }
    }

    // ---------- 消息面存在性（离线契约，不触发网络） ----------

    [Fact]
    public void PluginMessages_ShouldExposeStoreContract()
    {
        Assert.NotNull(typeof(PluginMessage).GetMethod(nameof(PluginMessage.Initialize), Type.EmptyTypes));
        Assert.NotNull(typeof(PluginMessage).GetMethod(nameof(PluginMessage.InitializeAuto), Type.EmptyTypes));
        Assert.NotNull(typeof(PluginMessage).GetMethod(nameof(PluginMessage.GetDependenceList), [typeof(string), typeof(string)]));
        Assert.NotNull(typeof(PlugInstoreMessage).GetMethod(nameof(PlugInstoreMessage.GetPluginList)));
        Assert.NotNull(typeof(PlugInstoreMessage).GetMethod(nameof(PlugInstoreMessage.GetPluginDetail), [typeof(string)]));
        Assert.NotNull(typeof(PlugInstoreMessage).GetMethod(nameof(PlugInstoreMessage.Install), [typeof(string)]));
        Assert.NotNull(typeof(PlugInstoreMessage).GetMethod(nameof(PlugInstoreMessage.AutoUpdateCheck), Type.EmptyTypes));
    }
}
