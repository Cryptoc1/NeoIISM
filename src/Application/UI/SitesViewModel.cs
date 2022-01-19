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

public class SitesViewModel : ObservableRecipient
{
    #region Fields
    private readonly IServerManagerClient serverManagerClient;
    private readonly IServiceProvider serviceProvider;
    #endregion

    #region Properties
    public IAsyncRelayCommand ReloadDataCommand { get; }
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

    private bool isBusy;
    private bool isSelected;
    private bool isSiteRunning;
    #endregion

    #region Properties
    public bool IsBusy { get => isBusy; set => SetProperty( ref isBusy, value ); }
    public bool IsSelected { get => isSelected; set => SetProperty( ref isSelected, value ); }
    public bool IsSiteRunning { get => isSiteRunning; set => SetProperty( ref isSiteRunning, value ); }
    public string SiteName => name;

    public IAsyncRelayCommand ReloadDataCommand { get; }
    public IAsyncRelayCommand ToggleCommand { get; }
    #endregion

    public SiteItem( string name, IServerManagerClient serverManagerClient )
    {
        ReloadDataCommand = new SiteCommand( this, ReloadDataAsync );
        ToggleCommand = new SiteCommand( this, ToggleAsync );

        busyLock = new();
        this.name = name;
        this.serverManagerClient = serverManagerClient;
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

    private Task ToggleAsync( CancellationToken cancellation ) => throw new NotImplementedException();

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
