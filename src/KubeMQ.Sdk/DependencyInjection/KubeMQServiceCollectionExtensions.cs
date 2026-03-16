using System;
using KubeMQ.Sdk.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KubeMQ.Sdk.DependencyInjection;

/// <summary>
/// Extension methods for registering KubeMQ services with the ASP.NET Core DI container.
/// </summary>
public static class KubeMQServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="KubeMQClient"/> as a singleton with inline configuration.
    /// The client automatically connects on application startup via an
    /// <see cref="Microsoft.Extensions.Hosting.IHostedService"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure <see cref="KubeMQClientOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddKubeMQ(opts =&gt;
    /// {
    ///     opts.Address = "kubemq-server:50000";
    ///     opts.AuthToken = "my-token";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddKubeMQ(
        this IServiceCollection services,
        Action<KubeMQClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        RegisterClient(services);
        return services;
    }

    /// <summary>
    /// Registers <see cref="KubeMQClient"/> as a singleton with configuration binding.
    /// Binds from the "KubeMQ" section of the provided <see cref="IConfiguration"/>.
    /// The client automatically connects on application startup via an
    /// <see cref="Microsoft.Extensions.Hosting.IHostedService"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// // appsettings.json: { "KubeMQ": { "Address": "kubemq:50000" } }
    /// builder.Services.AddKubeMQ(builder.Configuration);
    /// </code>
    /// </example>
    public static IServiceCollection AddKubeMQ(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<KubeMQClientOptions>(configuration.GetSection("KubeMQ"));
        RegisterClient(services);
        return services;
    }

    private static void RegisterClient(IServiceCollection services)
    {
        services.TryAddSingleton<KubeMQClient>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KubeMQClientOptions>>();
            return new KubeMQClient(opts.Value);
        });
        services.TryAddSingleton<IKubeMQClient>(sp => sp.GetRequiredService<KubeMQClient>());
        services.AddHostedService<KubeMQConnectionHostedService>();
    }
}
