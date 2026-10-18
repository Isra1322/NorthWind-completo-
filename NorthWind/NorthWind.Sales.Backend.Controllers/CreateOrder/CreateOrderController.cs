using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Builder;

public static class CreateOrderController
{
    public static WebApplication UseCreateOrderController(
        this WebApplication app)
    {
        app.MapPost(Endpoints.CreateOrder, CreateOrder).RequireAuthorization();
        app.MapGet("sales/orders/requests/{requestId:guid}", async (Guid requestId,
            NorthWind.Sales.Backend.BusinessObjects.Interfaces.Repositories.ICommandsRepository repository) =>
            await repository.FindOrderId(requestId) is int id ? Results.Ok(id) : Results.NotFound())
            .RequireAuthorization();

        return app;
    }

    public static async Task<int> CreateOrder(
        CreateOrderDto orderDto,
        ICreateOrderInputPort inputPort,
        ICreateOrderOutputPort presenter)
    {
        await inputPort.Handle(orderDto);
        return presenter.OrderId;
    }
}
