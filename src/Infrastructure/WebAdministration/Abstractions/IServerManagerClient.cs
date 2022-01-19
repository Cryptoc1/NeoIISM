using Microsoft.Web.Administration;

namespace NeoIISM.Infrastructure.WebAdministration.Abstractions;

public interface IServerManagerClient
{
    Task<T> OpenAsync<T>( Func<ServerManager, CancellationToken, Task<T>> accessor, CancellationToken cancellation = default );
}

public static class IServerManagerClientExtensions
{
    public static Task<T> OpenAsync<T>( this IServerManagerClient client, Func<ServerManager, T> accessor, CancellationToken cancellation = default )
    {
        if( client is null )
        {
            throw new ArgumentNullException( nameof( client ) );
        }

        if( accessor is null )
        {
            throw new ArgumentNullException( nameof( accessor ) );
        }

        return client.OpenAsync<T>(
            ( serverManager, _ ) => Task.FromResult( accessor( serverManager ) ),
            cancellation
        );
    }
}
