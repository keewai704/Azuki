using Azookey.Core.Win32;
using Xunit;

namespace Azookey.Core.Tests.Win32;

public sealed class WindowInteropTests
{
    [Fact]
    public void ComposeImeToolWindowStyleRemovesChromeAndAddsPopup()
    {
        nint composed = WindowInterop.ComposeImeToolWindowStyle(0x10CF0000);

        Assert.Equal(unchecked((nint)(int)0x90000000), composed);
    }

    [Fact]
    public void ComposeImeToolWindowExStylePreservesExistingBitsAndAddsRequiredFlags()
    {
        nint composed = WindowInterop.ComposeImeToolWindowExStyle(0x00000200);

        Assert.Equal((nint)0x08000288, composed);
    }

    [Fact]
    public void ShowNoActivateFlagsShowWithoutMovingOrResizing()
    {
        Assert.Equal(0x0053u, WindowInterop.ShowNoActivateFlags);
    }

    [Fact]
    public void MoveNoActivateFlagsDoNotShowWindow()
    {
        Assert.Equal(0x0010u, WindowInterop.MoveNoActivateFlags);
    }
}
