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

public class ApplicationPoolsViewModel : ObservableRecipient
{
    #region Fields
    private readonly IServerManagerClient serverManagerClient;
    private readonly IServiceProvider serviceProvider;
    #endregion

    #region Properties
    public ObservableCollection<ApplicationPoolItem> ApplicationPools { get; }
    public IAsyncRelayCommand ReloadDataCommand { get; }
    #endregion

    public ApplicationPoolsViewModel(
        IMessenger messenger,
        IServerManagerClient serverManagerClient,
        IServiceProvider serviceProvider
    ) : base( messenger )
    {
        ApplicationPools = new();
        ReloadDataCommand = new AsyncLockRelayCommand( ReloadDataAsync );

        this.serverManagerClient = serverManagerClient;
        this.serviceProvider = serviceProvider;
    }

    private async Task ReloadDataAsync( CancellationToken cancellation )
    {
        var reloadTasks = new List<Task>();
        var appPoolNames = await serverManagerClient.OpenAsync(
            serverManager => serverManager.ApplicationPools.Select( appPool => appPool.Name ).OrderBy( name => name ),
            cancellation
        );

        var appPools = appPoolNames.Select(
            name => ActivatorUtilities.CreateInstance<ApplicationPoolItem>( serviceProvider, name )
        );

        ApplicationPools.Clear();
        foreach( var appPool in appPools )
        {
            ApplicationPools.Add( appPool );
            reloadTasks.Add( appPool.ReloadDataCommand.ExecuteAsync( default, cancellation ) );
        }

        await Task.WhenAll( reloadTasks );
    }
}

public class ApplicationPoolItem : ObservableRecipient
{
    #region Fields
    private readonly AsyncLock busyLock;
    private readonly string name;
    private readonly IServerManagerClient serverManagerClient;
    private readonly AppPoolCommand startCommand;
    private readonly AppPoolCommand stopCommand;

    private bool isBusy;
    private bool isAppPoolRunning;
    private bool isSelected;
    #endregion

    #region Properties
    public bool IsBusy { get => isBusy; set => SetProperty( ref isBusy, value ); }
    public bool IsAppPoolRunning { get => isAppPoolRunning; set => SetProperty( ref isAppPoolRunning, value ); }
    public bool IsSelected { get => isSelected; set => SetProperty( ref isSelected, value ); }
    public string AppPoolName => name;

    public IAsyncRelayCommand RecycleCommand { get; }
    public IAsyncRelayCommand ReloadDataCommand { get; }
    public IAsyncRelayCommand StartCommand => startCommand;
    public IAsyncRelayCommand StopCommand => stopCommand;
    public IAsyncRelayCommand ToggleCommand { get; }
    #endregion

    public ApplicationPoolItem( string name, IMessenger messenger, IServerManagerClient serverManagerClient )
        : base( messenger )
    {
        RecycleCommand = new AppPoolCommand( this, RecycleAsync );
        ReloadDataCommand = new AppPoolCommand( this, ReloadDataAsync );
        ToggleCommand = new AppPoolCommand( this, ToggleAsync );

        busyLock = new();
        this.name = name;
        this.serverManagerClient = serverManagerClient;
        startCommand = new AppPoolCommand( this, StartAsync );
        stopCommand = new AppPoolCommand( this, StopAsync );
    }

    private async Task RecycleAsync( CancellationToken cancellation )
    {
        IsAppPoolRunning = await serverManagerClient.OpenAsync(
            serverManager => serverManager.ApplicationPools[ name ].Recycle() is ObjectState.Started,
            cancellation
        );
    }

    private async Task ReloadDataAsync( CancellationToken cancellation )
    {
        IsAppPoolRunning = await serverManagerClient.OpenAsync(
            serverManager =>
            {
                var appPool = serverManager.ApplicationPools[ name ];
                return appPool.State is ObjectState.Starting or ObjectState.Started;
            },
            cancellation
        );
    }

    private async Task StartAsync( CancellationToken cancellation )
    {
        IsAppPoolRunning = await serverManagerClient.OpenAsync(
            async ( serverManager, cancellation ) =>
            {
                var appPool = serverManager.ApplicationPools[ name ];
                if( appPool.State is ObjectState.Started )
                {
                    return true;
                }

                appPool.Start();
                while( appPool.State is ObjectState.Starting )
                {
                    await Task.Delay( 1, cancellation );
                }

                return appPool.State is ObjectState.Started;
            },
            cancellation
        );
    }

    private async Task StopAsync( CancellationToken cancellation )
    {
        bool isStopped = await serverManagerClient.OpenAsync(
            async ( serverManager, cancellation ) =>
            {
                var appPool = serverManager.ApplicationPools[ name ];
                if( appPool.State is ObjectState.Stopped )
                {
                    return true;
                }

                appPool.Stop();
                while( appPool.State is ObjectState.Stopping )
                {
                    await Task.Delay( 1, cancellation );
                }

                return appPool.State is ObjectState.Stopped;
            },
            cancellation
        );

        IsAppPoolRunning = !isStopped;
    }

    private async Task ToggleAsync( CancellationToken cancellation )
    {
        using( await startCommand.AsyncLock.LockAsync( cancellation ) )
        using( await stopCommand.AsyncLock.LockAsync( cancellation ) )
        {
            await ( isAppPoolRunning ? StopAsync( cancellation ) : StartAsync( cancellation ) );
        }
    }

    private class AppPoolCommand : IAsyncLockRelayCommand
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

        public AppPoolCommand( ApplicationPoolItem appPool, Func<CancellationToken, Task> execute )
        {
            command = new AsyncLockRelayCommand(
                async cancellation =>
                {
                    using( await appPool.busyLock.LockAsync( cancellation ) )
                    {
                        appPool.IsBusy = true;

                        await execute( cancellation );

                        appPool.IsBusy = false;
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
