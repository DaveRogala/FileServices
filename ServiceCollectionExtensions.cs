namespace MagellanFileServices;

/// <summary>
/// Extension methods for registering MagellanFileServices with an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="Services.FileServices"/> as the <see cref="Contracts.IFileServices"/>
    /// implementation with a <see cref="ServiceLifetime.Scoped"/> lifetime.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddMagellanFileServices(this IServiceCollection services)
    {
        services.Add(ServiceDescriptor.Scoped<IFileServices, Services.FileServices>());
        return services;
    }
}
