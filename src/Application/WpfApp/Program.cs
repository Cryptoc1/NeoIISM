using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using NeoIISM.Application.UI;
using NeoIISM.Application.WpfApp.Hosting;
using NeoIISM.Infrastructure;

namespace NeoIISM.Application.WpfApp;

public static class Program
{
    [STAThread]
    public static void Main( string[] args )
    {
        using var host = CreateHostBuilder( args ).Build();
        Ioc.Default.ConfigureServices( host.Services );

        host.RunWpf();
    }

    private static void ConfigureServices( IServiceCollection services )
    {
        services.AddNeoIISMServices();
        services.AddNeoIISMUI();
    }

    private static IHostBuilder CreateHostBuilder( string[] args )
        => Host.CreateDefaultBuilder( args )
            .ConfigureServices( ( _, services ) => ConfigureServices( services ) )
            .ConfigureWpf<App>( ( _, wpf ) => wpf.UseXamlIsland<UwpApp.App>() );
}
