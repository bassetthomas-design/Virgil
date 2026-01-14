using System.Diagnostics;

namespace Virgil.Services.Assistant;

public interface IRuntimeProcessRunner
{
    IRuntimeProcess? Start(ProcessStartInfo startInfo);
}
