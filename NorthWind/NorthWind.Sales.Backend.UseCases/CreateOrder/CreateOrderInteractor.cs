namespace NorthWind.Sales.Backend.UseCases.CreateOrder;

internal class CreateOrderInteractor(
    ICreateOrderOutputPort outputPort,
    ICommandsRepository repository,
    IModelValidatorHub<CreateOrderDto> modelValidatorHub,
    IDomainEventHub<SpecialOrderCreatedEvent> domainEventHub,
    IDomainLogger domainLogger,
    IDomainTransaction domainTransaction,
    IUserService userService,
    ILogger<CreateOrderInteractor> logger) : ICreateOrderInputPort
{
    public async Task Handle(CreateOrderDto orderDto)
    {
        GuardUser.AgainstUnauthenticated(userService);

        OrderAggregate Order = new();
        bool recovered = false;

        try
        {
            domainTransaction.BeginTransaction();
            if (orderDto.RequestId is Guid requestId &&
                await repository.FindOrderId(requestId, true) is int existingId)
            {
                Order.Id = existingId;
                recovered = true;
            }
            else
            {
                await GuardModel.AgainstNotValid(modelValidatorHub, orderDto);
                Order = OrderAggregate.From(orderDto);
                await domainLogger.LogInformation(
                    new DomainLog(CreateOrderMessages.StartingPurchaseOrderCreation, userService.UserName));
                await repository.CreateOrder(Order);
                await repository.SaveChanges();

                await domainLogger.LogInformation(
                    new DomainLog(string.Format(
                        CreateOrderMessages.PurchaseOrderCreatedTemplate, Order.Id), userService.UserName));
            }

            domainTransaction.CommitTransaction();
        }
        catch
        {
            try
            {
                domainTransaction.RollbackTransaction();
            }
            catch (Exception rollbackException)
            {
                logger.LogError(rollbackException, "Error al revertir la transacción de creación de orden.");
            }

            try
            {
                await domainLogger.LogInformation(
                    new DomainLog(string.Format(
                        CreateOrderMessages.OrderCreationCancelledTemplate, Order.Id), userService.UserName));
            }
            catch (Exception auditException)
            {
                logger.LogError(auditException, "Error al registrar la cancelación de creación de orden.");
            }

            throw;
        }

        // The sale is committed. Notification failures must not cancel it or report a failed sale.
        if (!recovered && new SpecialOrderSpecification().IsSatisfiedBy(Order))
        {
            try
            {
                await domainEventHub.Raise(new SpecialOrderCreatedEvent(Order.Id, Order.OrderDetails.Count));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "La orden especial {OrderId} se guardó; falló su notificación por correo.", Order.Id);
            }
        }
        await outputPort.Handle(Order);
    }
}
