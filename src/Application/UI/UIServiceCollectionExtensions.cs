using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;

namespace NeoIISM.Application.UI;

public static class UIServiceCollectionExtensions
{
    public static IServiceCollection AddNeoIISMUI( this IServiceCollection services )
    {
        if( services is null )
        {
            throw new ArgumentNullException( nameof( services ) );
        }

        services.AddSingleton<IMessenger>( _ => WeakReferenceMessenger.Default );

        services.AddTransient<ApplicationPoolsViewModel>();
        services.AddTransient<SitesViewModel>();

        return services;
    }
}
