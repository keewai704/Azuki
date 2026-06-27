using System.IO.Pipes;

namespace Azookey.UI.Ipc;

public static class UiNamedPipeSecurity
{
    internal const string SecurityDescriptorSddl =
        "D:(A;;GA;;;AC)(A;;GA;;;RC)(A;;GA;;;SY)(A;;GA;;;BA)(A;;GA;;;BU)S:(ML;;NW;;;LW)";

    public static PipeSecurity Create()
    {
        PipeSecurity security = new();
        security.SetSecurityDescriptorSddlForm(SecurityDescriptorSddl);
        return security;
    }
}
