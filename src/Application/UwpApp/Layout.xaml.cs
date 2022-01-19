using System;
using System.Linq;
using System.Reflection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Muxc = Microsoft.UI.Xaml.Controls;

namespace NeoIISM.Application.UwpApp;

public sealed partial class Layout : Page
{
    #region Fields
    private static readonly string PageTypeNameFormat = $"{Assembly.GetExecutingAssembly().GetName().Name}.Views.{{0}}";
    #endregion

    public Layout( ) => InitializeComponent();

    private void OnLoaded( object sender, RoutedEventArgs args )
        => navigationView.SelectedItem = navigationView.MenuItems.First();

    private void OnNavSelectionChanged( Muxc.NavigationView nav, Muxc.NavigationViewSelectionChangedEventArgs args )
    {
        var pageType = GetPageType( args );
        if( pageType is null || contentFrame.CurrentSourcePageType == pageType )
        {
            return;
        }

        navigationView.Header = args.SelectedItemContainer.Content;
        contentFrame.Navigate( pageType );
    }

    private static Type GetPageType( Muxc.NavigationViewSelectionChangedEventArgs args )
    {
        if( args.IsSettingsSelected )
        {
            // TODO: implement settings page
            return null;
        }

        return Type.GetType( string.Format( PageTypeNameFormat, args.SelectedItemContainer.Tag ) );
    }
}
