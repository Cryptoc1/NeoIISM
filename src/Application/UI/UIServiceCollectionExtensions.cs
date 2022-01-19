using Microsoft.Extensions.DependencyInjection;

namespace NeoIISM.Application.UI;

public static class UIServiceCollectionExtensions
{
    public static IServiceCollection AddNeoIISMUI( this IServiceCollection services )
    {
        if( services is null )
        {
            throw new ArgumentNullException( nameof( services ) );
        }

        services.AddTransient<ApplicationPoolsViewModel>();
        services.AddTransient<SitesViewModel>();

        return services;
    }
}
