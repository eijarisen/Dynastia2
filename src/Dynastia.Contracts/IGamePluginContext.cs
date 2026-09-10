namespace Dynastia.Contracts;

public interface IGamePluginContext
{
    T? GetService<T>() where T : class;

    void Log(string message);
}