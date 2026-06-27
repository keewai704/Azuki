using System.Diagnostics;
using Azookey.Core.Win32;
using Azookey.UI.Ipc;
using Azookey.UI.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Azookey.UI;

public partial class App : Application
{
    private WebApplication? webApplication;
    private Microsoft.UI.Xaml.Window? lifetimeWindow;

    public App()
    {
        InitializeComponent();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var candidateWindow = new CandidateWindow();
            var indicatorWindow = new IndicatorWindow();
            lifetimeWindow = CreateLifetimeWindow();
            DispatcherQueue dispatcherQueue = DispatcherQueue.GetForCurrentThread()
                ?? throw new InvalidOperationException("UI dispatcher is not available.");

            var coordinator = new UiWindowCoordinator(
                new WinUiWindowRenderer(dispatcherQueue, candidateWindow, indicatorWindow));

            webApplication = BuildWebApplication(coordinator);
            await webApplication.StartAsync();
        }
        catch (Exception exception)
        {
            UiHostStartupError? startupError = UiHostStartupErrors.Classify(exception);
            if (startupError is null)
            {
                throw;
            }

            Trace.TraceError(startupError.Value.Message);
            Trace.TraceError(exception.ToString());

            if (webApplication is not null)
            {
                await webApplication.DisposeAsync();
                webApplication = null;
            }

            Exit();
        }
    }

    private static WebApplication BuildWebApplication(IWindowActionSink sink)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseNamedPipes(options =>
        {
            options.CurrentUserOnly = false;
            options.CreateNamedPipeServerStream = UiNamedPipeServerStreamFactory.Create;
        });

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenNamedPipe("azookey_ui", listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        builder.Services.AddGrpc();
        builder.Services.AddSingleton(sink);

        WebApplication app = builder.Build();
        app.MapGrpcService<WindowServiceImpl>();
        return app;
    }

    private static Microsoft.UI.Xaml.Window CreateLifetimeWindow()
    {
        Microsoft.UI.Xaml.Window window = new();
        WindowInterop.MakeImeToolWindow(window);
        WindowInterop.MoveNoActivate(window, -32000, -32000, 1, 1);
        WindowInterop.ShowNoActivate(window);
        return window;
    }
}
