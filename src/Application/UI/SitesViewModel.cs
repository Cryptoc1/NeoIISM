using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Web.Administration;
using NeoIISM.Infrastructure.WebAdministration.Abstractions;
using Nito.AsyncEx;

namespace NeoIISM.Application.UI;

public class SitesViewModel : ObservableRecipient, IRecipient<SiteDeletedMessage>
{
    #region Fields
    private readonly IServerManagerClient serverManagerClient;
    private readonly IServiceProvider serviceProvider;
    #endregion

    #region Properties
    public IAsyncLockRelayCommand ReloadDataCommand { get; }
    public ObservableCollection<SiteItem> Sites { get; }
    #endregion

    public SitesViewModel( IMessenger messenger, IServerManagerClient serverManagerClient, IServiceProvider serviceProvider )
        : base( messenger )
    {
        ReloadDataCommand = new AsyncLockRelayCommand( ReloadDataAsync );
        Sites = new();

        this.serverManagerClient = serverManagerClient;
        this.serviceProvider = serviceProvider;
    }

    public void Receive( SiteDeletedMessage message )
    {
        using( ReloadDataCommand.AsyncLock.Lock() )
        {
            var appPool = Sites.Single( site => site.SiteName == message.SiteName );
            Sites.Remove( appPool );
        }
    }

    private async Task ReloadDataAsync( CancellationToken cancellation )
    {
        var reloadTasks = new List<Task>();
        var siteNames = await serverManagerClient.OpenAsync(
            serverManager => serverManager.Sites.Select( site => site.Name ).OrderBy( name => name ),
            cancellation
        );

        var sites = siteNames.Select(
            name => ActivatorUtilities.CreateInstance<SiteItem>( serviceProvider, name )
        );

        Sites.Clear();
        foreach( var site in sites )
        {
            Sites.Add( site );
            reloadTasks.Add( site.ReloadDataCommand.ExecuteAsync( default, cancellation ) );
        }

        await Task.WhenAll( reloadTasks );
    }
}

public class SiteItem : ObservableRecipient
{
    #region Fields
    private readonly AsyncLock busyLock;
    private readonly string name;
    private readonly IServerManagerClient serverManagerClient;
    private readonly IAsyncLockRelayCommand startCommand;
    private readonly IAsyncLockRelayCommand stopCommand;

    private bool isBusy;
    private bool isSelected;
    private bool isSiteRunning;
    #endregion

    #region Properties
    public bool IsBusy { get => isBusy; set => SetProperty( ref isBusy, value ); }
    public bool IsSelected { get => isSelected; set => SetProperty( ref isSelected, value ); }
    public bool IsSiteRunning { get => isSiteRunning; set => SetProperty( ref isSiteRunning, value ); }
    public string SiteName => name;

    public IAsyncRelayCommand DeleteCommand { get; }
    public IAsyncRelayCommand ReloadDataCommand { get; }
    public IAsyncRelayCommand StartCommand => startCommand;
    public IAsyncRelayCommand StopCommand => stopCommand;
    public IAsyncRelayCommand ToggleCommand { get; }
    #endregion

    public SiteItem( string name, IServerManagerClient serverManagerClient )
    {
        DeleteCommand = new SiteCommand( this, DeleteAsync );
        ReloadDataCommand = new SiteCommand( this, ReloadDataAsync );
        ToggleCommand = new SiteCommand( this, ToggleAsync );

        busyLock = new();
        this.name = name;
        this.serverManagerClient = serverManagerClient;
        startCommand = new SiteCommand( this, StartAsync );
        stopCommand = new SiteCommand( this, StopAsync );
    }

    private async Task ReloadDataAsync( CancellationToken cancellation )
    {
        IsSiteRunning = await serverManagerClient.OpenAsync(
            serverManager =>
            {
                var appPool = serverManager.Sites[ name ];
                return appPool.State is ObjectState.Starting or ObjectState.Started;
            },
            cancellation
        );
    }

    private async Task DeleteAsync( CancellationToken cancellation )
    {
        bool deleted = await serverManagerClient.OpenAsync(
            serverManager =>
            {
                var site = serverManager.Sites[ name ];
                serverManager.Sites.Remove( site );

                serverManager.CommitChanges();
                return serverManager.Sites[ name ] is null;
            },
            cancellation
        );

        if( deleted )
        {
            Messenger.Send( new SiteDeletedMessage( name ) );
        }
    }

    private async Task StartAsync( CancellationToken cancellation )
    {
        IsSiteRunning = await serverManagerClient.OpenAsync(
            async ( serverManager, cancellation ) =>
            {
                var site = serverManager.Sites[ name ];
                if( site.State is ObjectState.Started )
                {
                    return true;
                }

                site.Start();
                while( site.State is ObjectState.Starting )
                {
                    await Task.Delay( 1, cancellation );
                }

                return site.State is ObjectState.Started;
            },
            cancellation
        );
    }

    private async Task StopAsync( CancellationToken cancellation )
    {
        bool stopped = await serverManagerClient.OpenAsync(
            async ( serverManager, cancellation ) =>
            {
                var site = serverManager.Sites[ name ];
                if( site.State is ObjectState.Stopped )
                {
                    return true;
                }

                site.Stop();
                while( site.State is ObjectState.Stopping )
                {
                    await Task.Delay( 1, cancellation );
                }

                return site.State is ObjectState.Stopped;
            },
            cancellation
        );

        IsSiteRunning = !stopped;
    }

    private async Task ToggleAsync( CancellationToken cancellation )
    {
        using( await startCommand.AsyncLock.LockAsync( cancellation ) )
        using( await stopCommand.AsyncLock.LockAsync( cancellation ) )
        {
            await ( isSiteRunning ? StopAsync( cancellation ) : StartAsync( cancellation ) );
        }
    }

    private class SiteCommand : IAsyncLockRelayCommand
    {
        #region Fields
        private readonly IAsyncLockRelayCommand command;
        #endregion

        #region Properties
        public AsyncLock AsyncLock => command.AsyncLock;
        public Task? ExecutionTask => command.ExecutionTask;
        public bool CanBeCanceled => command.CanBeCanceled;
        public bool IsCancellationRequested => command.IsCancellationRequested;
        public bool IsRunning => command.IsRunning;
        #endregion

        #region Events
        public event EventHandler CanExecuteChanged { add => command.CanExecuteChanged += value; remove => command.CanExecuteChanged -= value; }
        public event PropertyChangedEventHandler PropertyChanged { add => command.PropertyChanged += value; remove => command.PropertyChanged -= value; }
        #endregion

        public SiteCommand( SiteItem site, Func<CancellationToken, Task> execute )
        {
            command = new AsyncLockRelayCommand(
                async cancellation =>
                {
                    using( await site.busyLock.LockAsync( cancellation ) )
                    {
                        site.IsBusy = true;

                        await execute( cancellation );

                        site.IsBusy = false;
                    }
                }
            );
        }

        public Task ExecuteAsync( object? parameter ) => command.ExecuteAsync( parameter );
        public void Cancel( ) => command.Cancel();
        public void NotifyCanExecuteChanged( ) => command.NotifyCanExecuteChanged();
        public bool CanExecute( object parameter ) => command.CanExecute( parameter );
        public void Execute( object parameter ) => command.Execute( parameter );
    }
}

public class SiteDeletedMessage
{
    public string SiteName { get; }

    public SiteDeletedMessage( string name )
        => SiteName = name;
}
