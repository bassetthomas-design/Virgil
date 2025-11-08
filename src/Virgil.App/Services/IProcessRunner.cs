using System;
using System.Threading.Tasks;

namespace Virgil.App.Services;

public interface IProcessRunner
{
    Task<int> RunAsync(string fileName, string arguments, Action<string>? onOutput = null, Action<string>? onError = null, bool elevate = false);
}
