using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Toolkit.Win32.UI.XamlHost;

namespace NeoIISM.Application.WpfApp.Hosting;

public static class WpfHostingExtensions
{
    public static IHostBuilder ConfigureWpf<TWpfApp>( this IHostBuilder hostBuilder, Action<HostBuilderContext, IWpfBuilder>? configureWpf )
        where TWpfApp : System.Windows.Application
    {
        if( hostBuilder is null )
        {
            throw new ArgumentNullException( nameof( hostBuilder ) );
        }

        hostBuilder.ConfigureServices(
            ( context, services ) =>
            {
                var builder = new DefaultWpfBuilder( services );
                configureWpf?.Invoke( context, builder );

                services.AddSingleton<System.Windows.Application, TWpfApp>();
            }
        );

        hostBuilder.UseContentRoot( AppDomain.CurrentDomain.BaseDirectory );
        hostBuilder.UseWpfLifetime();

        return hostBuilder;
    }

    public static async Task RunWpfAsync( this IHost host, CancellationToken cancellation = default )
    {
        if( host is null )
        {
            throw new ArgumentNullException( nameof( host ) );
        }

        try
        {
            await host.StartAsync( cancellation )
                .ConfigureAwait( false );

            var _ = host.Services.GetRequiredService<XamlApplication>();
            var app = host.Services.GetRequiredService<System.Windows.Application>();
            app.Run();

            await host.WaitForShutdownAsync( cancellation )
                .ConfigureAwait( false );
        }
        finally
        {
            if( host is IAsyncDisposable asyncDisposable )
            {
                await asyncDisposable.DisposeAsync()
                    .ConfigureAwait( false );
            }
            else
            {
                host.Dispose();
            }
        }
    }

    public static void RunWpf( this IHost host )
        => RunWpfAsync( host, default )
            .ConfigureAwait( false )
            .GetAwaiter()
            .GetResult();

    public static Task RunWpfAsync( this IHostBuilder hostBuilder, CancellationToken cancellation )
    {
        if( hostBuilder is null )
        {
            throw new ArgumentNullException( nameof( hostBuilder ) );
        }

        return hostBuilder.Build()
            .RunWpfAsync( cancellation );
    }

    public static void RunWpf( this IHostBuilder hostBuilder )
        => RunWpfAsync( hostBuilder, default )
            .ConfigureAwait( false )
            .GetAwaiter()
            .GetResult();

    public static IHostBuilder UseWpfLifetime( this IHostBuilder hostBuilder )
    {
        if( hostBuilder is null )
        {
            throw new ArgumentNullException( nameof( hostBuilder ) );
        }

        return hostBuilder.ConfigureServices(
            ( _, services ) => services.AddSingleton<IHostLifetime>(
                serviceProvider => new WpfLifetime(
                    serviceProvider.GetRequiredService<System.Windows.Application>(),
                    serviceProvider.GetRequiredService<IHostApplicationLifetime>(),
                    ActivatorUtilities.CreateInstance<ConsoleLifetime>( serviceProvider )
                )
            )
        );
    }
}

public interface IWpfBuilder
{
    IWpfBuilder UseXamlIsland<TUwpApp>( )
        where TUwpApp : XamlApplication;
}

public class DefaultWpfBuilder : IWpfBuilder
{
    private readonly IServiceCollection services;

    public DefaultWpfBuilder( IServiceCollection services )
        => this.services = services ?? throw new ArgumentNullException( nameof( services ) );

    public IWpfBuilder UseXamlIsland<TUwpApp>( )
        where TUwpApp : XamlApplication
    {
        services.AddSingleton<XamlApplication, TUwpApp>();
        return this;
    }
}

public sealed class WpfLifetime : IHostLifetime, IDisposable
{
    #region Fields
    private readonly System.Windows.Application app;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly ConsoleLifetime defaultLifetime;
    #endregion

    public WpfLifetime( System.Windows.Application app, IHostApplicationLifetime applicationLifetime, ConsoleLifetime defaultLifetime )
    {
        this.app = app ?? throw new ArgumentNullException( nameof( app ) );
        this.applicationLifetime = applicationLifetime ?? throw new ArgumentNullException( nameof( applicationLifetime ) );
        this.defaultLifetime = defaultLifetime ?? throw new ArgumentNullException( nameof( defaultLifetime ) );
    }

    public void Dispose( )
    {
        app.Exit -= OnWindowsAppExit;
        if( defaultLifetime is IDisposable disposableLifetime )
        {
            disposableLifetime.Dispose();
        }
    }

    private void OnWindowsAppExit( object sender, ExitEventArgs e )
        => applicationLifetime.StopApplication();

    public Task StopAsync( CancellationToken cancellation )
        => defaultLifetime.StopAsync( cancellation );

    public Task WaitForStartAsync( CancellationToken cancellation )
    {
        app.Exit += OnWindowsAppExit;

        return defaultLifetime.WaitForStartAsync( cancellation );
    }
}
