using MagellanFileServices.Contracts;

namespace MagellanFileServices.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMagellanFileServices_RegistersIFileServices_AsResolvable()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMagellanFileServices();

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetService<IFileServices>();

        Assert.NotNull(resolved);
        Assert.IsType<FileServices>(resolved);
    }

    [Fact]
    public void AddMagellanFileServices_RegistersScoped_Lifetime()
    {
        var services = new ServiceCollection();
        services.AddMagellanFileServices();

        var descriptor = services.Single(d => d.ServiceType == typeof(IFileServices));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddMagellanFileServices_ScopedInstances_AreSharedWithinScope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMagellanFileServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var a = scope.ServiceProvider.GetRequiredService<IFileServices>();
        var b = scope.ServiceProvider.GetRequiredService<IFileServices>();

        Assert.Same(a, b);
    }

    [Fact]
    public void AddMagellanFileServices_DifferentScopes_ProduceDifferentInstances()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMagellanFileServices();

        using var provider = services.BuildServiceProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var a = scope1.ServiceProvider.GetRequiredService<IFileServices>();
        var b = scope2.ServiceProvider.GetRequiredService<IFileServices>();

        Assert.NotSame(a, b);
    }

    [Fact]
    public void AddMagellanFileServices_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddMagellanFileServices();

        Assert.Same(services, result);
    }
}
