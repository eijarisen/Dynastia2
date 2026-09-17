namespace Dynastia.Contracts;

public interface IYearExecutionBoundary
{
    IYearExecutionCheckpoint Capture();
}

public interface IYearExecutionCheckpoint
{
    void Restore();
}
