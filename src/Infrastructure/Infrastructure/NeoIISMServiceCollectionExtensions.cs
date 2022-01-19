using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.Administration;
using NeoIISM.Infrastructure.WebAdministration;
using NeoIISM.Infrastructure.WebAdministration.Abstractions;

namespace NeoIISM.Infrastructure;

public static class NeoIISMServiceCollectionExtensions
{
    public static IServiceCollection AddNeoIISMServices( this IServiceCollection services )
    {
        if( services is null )
        {
            throw new ArgumentNullException( nameof( services ) );
        }

        services.AddAutoMapper( typeof( NeoIISMServiceCollectionExtensions ).Assembly );

        // TODO: wrap with factory (w/ pooling??) to support remote admin?
        services.AddSingleton<IServerManagerClient>( _ => new ServerManagerClient( new ServerManager() ) );

        return services;
    }
}
