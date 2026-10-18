namespace NorthWind.Result.Entities;

public class Result<ErrorResultType>
{
    public ErrorResultType ErrorValue { get; private set; } = default!;
    public bool HasError { get; private set; }
    public Result() { HasError = false; }
    public Result(ErrorResultType errorValue) { HasError = true; ErrorValue = errorValue; }
    public void HandleResult(Action<ErrorResultType> whenHasErrorAction, Action? whenIsSuccessAction = null)
    {
        if (HasError) whenHasErrorAction(ErrorValue);
        else whenIsSuccessAction?.Invoke();
    }
}
