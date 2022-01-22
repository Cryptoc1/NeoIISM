using System.Linq;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using NeoIISM.Application.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NeoIISM.Application.UwpApp.Views;

public sealed partial class ApplicationPoolsPage : Page
{
    public ApplicationPoolsViewModel ViewModel => ( ApplicationPoolsViewModel )DataContext;

    public ApplicationPoolsPage( )
    {
        InitializeComponent();

        DataContext = Ioc.Default.GetService<ApplicationPoolsViewModel>();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded( object sender, RoutedEventArgs e )
    {
        ViewModel.IsActive = true;
        ViewModel.ReloadDataCommand.ExecuteAsync( default );
    }

    private void OnSelectedAppPoolChanged( object sender, SelectionChangedEventArgs args )
    {
        foreach( var item in args.RemovedItems.Cast<ApplicationPoolItem>() )
        {
            item.IsSelected = false;
            item.ReloadDataCommand.Cancel();

            item.IsActive = false;
        }

        foreach( var item in args.AddedItems.Cast<ApplicationPoolItem>() )
        {
            item.IsActive = true;
            item.IsSelected = true;
            if( !item.IsBusy && !item.ReloadDataCommand.IsRunning )
            {
                item.ReloadDataCommand.ExecuteAsync( default );
            }
        }
    }

    private void OnUnloaded( object sender, RoutedEventArgs args )
    {
        ViewModel.ReloadDataCommand.Cancel();
        ViewModel.IsActive = false;
    }
}
