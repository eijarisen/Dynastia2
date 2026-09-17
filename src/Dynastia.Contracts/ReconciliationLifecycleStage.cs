namespace Dynastia.Contracts;

public enum ReconciliationLifecycleStage
{
    AfterNewGame = 0,
    AfterLoad = 1,
    BeforeYear = 2,
    AfterYear = 3,
    AfterImmediateAction = 4
}
