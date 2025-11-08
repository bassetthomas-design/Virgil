using System;

namespace Virgil.App.Services;

public interface IFileLogger
{
    void Info(string message);
    void Error(string message);
}
