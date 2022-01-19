using Microsoft.Web.Administration;
using NeoIISM.Infrastructure.WebAdministration.Abstractions;
using Nito.AsyncEx;

namespace NeoIISM.Infrastructure.WebAdministration;

public sealed class ServerManagerClient : IServerManagerClient, IDisposable
{
    #region Fields
    private readonly AsyncLock managerLock;
    private readonly ServerManager serverManager;
    #endregion

    public ServerManagerClient( ServerManager serverManager )
    {
        managerLock = new();
        this.serverManager = serverManager;
    }

    public void Dispose( )
        => serverManager?.Dispose();

    public async Task<T> OpenAsync<T>( Func<ServerManager, CancellationToken, Task<T>> accessor, CancellationToken cancellation = default )
    {
        if( accessor is null )
        {
            throw new ArgumentNullException( nameof( accessor ) );
        }

        using( await managerLock.LockAsync( cancellation ) )
        {
            return await accessor( serverManager, cancellation );
        }
    }
}
