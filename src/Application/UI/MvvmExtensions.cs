using System.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Nito.AsyncEx;

namespace NeoIISM.Application.UI;

public static class IAsyncRelayCommandExtensions
{
    public static async Task ExecuteAsync( this IAsyncRelayCommand command, object? parameter, CancellationToken cancellation )
    {
        if( command is null )
        {
            throw new ArgumentNullException( nameof( command ) );
        }

        using( cancellation.Register( ( ) => command.Cancel() ) )
        {
            await command.ExecuteAsync( parameter );
        }
    }
}

public interface IAsyncLockRelayCommand : IAsyncRelayCommand
{
    AsyncLock AsyncLock { get; }
}

public class AsyncLockRelayCommand : IAsyncLockRelayCommand
{
    #region Fields
    private readonly AsyncLock asyncLock;
    private readonly IAsyncRelayCommand command;
    #endregion

    #region Properties
    public AsyncLock AsyncLock => asyncLock;
    public Task? ExecutionTask => command.ExecutionTask;
    public bool CanBeCanceled => command.CanBeCanceled;
    public bool IsCancellationRequested => command.IsCancellationRequested;
    public bool IsRunning => command.IsRunning;
    #endregion

    #region Events
    public event EventHandler CanExecuteChanged { add => command.CanExecuteChanged += value; remove => command.CanExecuteChanged -= value; }
    public event PropertyChangedEventHandler PropertyChanged { add => command.PropertyChanged += value; remove => command.PropertyChanged -= value; }
    #endregion

    public AsyncLockRelayCommand( Func<CancellationToken, Task> execute )
    {
        asyncLock = new();
        command = new AsyncRelayCommand(
            async cancellation =>
            {
                using( await asyncLock.LockAsync( cancellation ) )
                {
                    await execute( cancellation );
                }
            }
        );
    }

    public void Cancel( ) => command.Cancel();
    public bool CanExecute( object parameter ) => command.CanExecute( parameter );
    public void Execute( object parameter ) => command.Execute( parameter );
    public Task ExecuteAsync( object? parameter ) => command.ExecuteAsync( parameter );
    public void NotifyCanExecuteChanged( ) => command.NotifyCanExecuteChanged();
}
