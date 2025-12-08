using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agile.API.Clients.Infrastructure
{
    /// <summary>
    /// Extension methods for registering Agile.API.Clients services with dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the default <see cref="IRetryPolicyProvider"/> implementation to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// Registers <see cref="RetryPolicyProvider"/> as a singleton implementing <see cref="IRetryPolicyProvider"/>.
        /// Uses TryAddSingleton to allow custom implementations to be registered first.
        /// </remarks>
        public static IServiceCollection AddRetryPolicyProvider(this IServiceCollection services)
        {
            services.TryAddSingleton<IRetryPolicyProvider, RetryPolicyProvider>();
            return services;
        }

        /// <summary>
        /// Adds a custom <see cref="IRetryPolicyProvider"/> implementation to the service collection.
        /// </summary>
        /// <typeparam name="TProvider">The custom retry policy provider type.</typeparam>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddRetryPolicyProvider<TProvider>(this IServiceCollection services)
            where TProvider : class, IRetryPolicyProvider
        {
            services.AddSingleton<IRetryPolicyProvider, TProvider>();
            return services;
        }
    }
}
