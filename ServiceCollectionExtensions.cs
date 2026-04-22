namespace MagellanFileServices;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FileServices"/> as the <see cref="IFileServices"/> implementation
    /// with a scoped lifetime.
    /// </summary>
    public static IServiceCollection AddMagellanFileServices(this IServiceCollection services)
    {
        services.Add(ServiceDescriptor.Scoped<IFileServices, FileServices>());
        return services;
    }
}
