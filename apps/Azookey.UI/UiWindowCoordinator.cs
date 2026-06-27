using Azookey.Core.Win32;
using Azookey.UI.Candidate;
using Azookey.UI.Ipc;
using Azookey.UI.Windows;
using Microsoft.UI.Dispatching;

namespace Azookey.UI;

public interface IUiWindowRenderer
{
    void Render(CandidateState state);
}

public sealed class UiWindowCoordinator : IWindowActionSink
{
    private static readonly TimeSpan DefaultIndicatorDisplayDuration = TimeSpan.FromMilliseconds(500);

    private readonly object gate = new();
    private readonly IUiWindowRenderer renderer;
    private readonly TimeSpan indicatorDisplayDuration;
    private CandidateState state = CandidateState.Initial;
    private CancellationTokenSource? indicatorHideCancellation;
    private long indicatorHideGeneration;

    public UiWindowCoordinator(IUiWindowRenderer renderer)
        : this(renderer, DefaultIndicatorDisplayDuration)
    {
    }

    internal UiWindowCoordinator(IUiWindowRenderer renderer, TimeSpan indicatorDisplayDuration)
    {
        this.renderer = renderer;
        this.indicatorDisplayDuration = indicatorDisplayDuration;
    }

    public ValueTask SendAsync(WindowAction action, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            state = state.Apply(action);

            if (ShouldCancelIndicatorHide(action))
            {
                CancelIndicatorHideLocked();
            }

            if (ShouldScheduleIndicatorHide(action))
            {
                ScheduleIndicatorHideLocked();
            }

            renderer.Render(state);
        }

        return ValueTask.CompletedTask;
    }

    private static bool ShouldScheduleIndicatorHide(WindowAction action) =>
        action is WindowAction.SetInputMode or
            WindowAction.UpdateCandidateWindow { InputMode: not null, Visible: not true };

    private static bool ShouldCancelIndicatorHide(WindowAction action) =>
        action is WindowAction.Show or WindowAction.Hide or WindowAction.HideInputModeIndicator or
            WindowAction.UpdateCandidateWindow { Visible: true };

    private void ScheduleIndicatorHideLocked()
    {
        indicatorHideCancellation?.Cancel();
        CancellationTokenSource hideCancellation = new();
        CancellationToken cancellationToken = hideCancellation.Token;
        indicatorHideCancellation = hideCancellation;
        long generation = ++indicatorHideGeneration;

        _ = HideIndicatorAfterDelayAsync(hideCancellation, generation, cancellationToken);
    }

    private void CancelIndicatorHideLocked()
    {
        indicatorHideCancellation?.Cancel();
        indicatorHideCancellation = null;
        indicatorHideGeneration++;
    }

    private async Task HideIndicatorAfterDelayAsync(
        CancellationTokenSource hideCancellation,
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(indicatorDisplayDuration, cancellationToken);

            lock (gate)
            {
                if (!ReferenceEquals(indicatorHideCancellation, hideCancellation) ||
                    indicatorHideGeneration != generation ||
                    cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                indicatorHideCancellation = null;
                indicatorHideGeneration++;
                state = state.Apply(new WindowAction.HideInputModeIndicator());
                renderer.Render(state);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            lock (gate)
            {
                if (ReferenceEquals(indicatorHideCancellation, hideCancellation))
                {
                    indicatorHideCancellation = null;
                }
            }

            hideCancellation.Dispose();
        }
    }
}

internal sealed class WinUiWindowRenderer : IUiWindowRenderer
{
    private readonly DispatcherQueue dispatcherQueue;
    private readonly CandidateWindow candidateWindow;
    private readonly IndicatorWindow indicatorWindow;

    public WinUiWindowRenderer(
        DispatcherQueue dispatcherQueue,
        CandidateWindow candidateWindow,
        IndicatorWindow indicatorWindow)
    {
        this.dispatcherQueue = dispatcherQueue;
        this.candidateWindow = candidateWindow;
        this.indicatorWindow = indicatorWindow;
    }

    public void Render(CandidateState state)
    {
        if (dispatcherQueue.HasThreadAccess)
        {
            RenderCore(state);
            return;
        }

        _ = dispatcherQueue.TryEnqueue(() => RenderCore(state));
    }

    private void RenderCore(CandidateState state)
    {
        WindowVisibility visibility = WindowVisibility.FromState(state);

        if (visibility.HasVisibleWindow)
        {
            WindowRect position = state.Position ?? ToWindowRect(WindowInterop.GetFallbackInputRect());
            WindowInterop.MonitorWorkArea monitorWorkArea = WindowInterop.GetWorkArea(
                position.Left,
                position.Top,
                position.Right,
                position.Bottom);
            var workArea = new WorkArea(
                monitorWorkArea.Left,
                monitorWorkArea.Top,
                monitorWorkArea.Right,
                monitorWorkArea.Bottom);
            WindowSize candidateSize = visibility.ShowCandidate ? candidateWindow.MeasureWindowSize(state) : default;
            WindowRenderPlan renderPlan = WindowRenderPlan.Create(
                position,
                visibility,
                workArea,
                candidateSize,
                indicatorWindow.WindowSize);

            if (renderPlan.Candidate is { } candidatePlacement)
            {
                candidateWindow.SetPlacement(candidatePlacement.X, candidatePlacement.Y, candidatePlacement.Size);
            }

            if (renderPlan.Indicator is { } indicatorPlacement)
            {
                indicatorWindow.SetPlacement(indicatorPlacement.X, indicatorPlacement.Y);
            }
        }

        candidateWindow.Render(state);
        indicatorWindow.Render(state);
    }

    private static WindowRect ToWindowRect(WindowInterop.ScreenRect rect) =>
        new(rect.Top, rect.Left, rect.Bottom, rect.Right);
}
