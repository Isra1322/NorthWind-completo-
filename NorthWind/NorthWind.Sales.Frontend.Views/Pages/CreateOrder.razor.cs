namespace NorthWind.Sales.Frontend.Views.Pages;

public partial class CreateOrder
{
    [Inject] public CreateOrderViewModel Order { get; set; } = null!;
    private ErrorBoundary? Boundary;
    private void Recover() => Boundary?.Recover();
}