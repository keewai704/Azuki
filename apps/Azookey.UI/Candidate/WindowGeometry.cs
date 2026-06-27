using Azookey.UI.Ipc;

namespace Azookey.UI.Candidate;

public readonly record struct WorkArea(int Left, int Top, int Right, int Bottom);
public readonly record struct WindowSize(int Width, int Height);

public static class WindowGeometry
{
    private const int CandidateXOffset = 15;
    private const int CandidateYGap = 6;

    public static (int X, int Y) CandidateWindowPosition(WindowRect target, WindowSize size, WorkArea workArea)
    {
        int x = ClampStart(target.Left - CandidateXOffset, size.Width, workArea.Left, workArea.Right);
        int below = target.Bottom + CandidateYGap;
        int above = target.Top - size.Height - CandidateYGap;
        int y;

        if (below + size.Height <= workArea.Bottom)
        {
            y = below;
        }
        else if (above >= workArea.Top)
        {
            y = above;
        }
        else
        {
            int belowSpace = Math.Max(0, workArea.Bottom - target.Bottom);
            int aboveSpace = Math.Max(0, target.Top - workArea.Top);
            int preferred = belowSpace >= aboveSpace ? below : above;
            y = ClampStart(preferred, size.Height, workArea.Top, workArea.Bottom);
        }

        return (x, y);
    }

    private static int ClampStart(int preferred, int length, int min, int max)
    {
        if (max <= min || length >= max - min)
        {
            return min;
        }

        return Math.Clamp(preferred, min, max - length);
    }
}
