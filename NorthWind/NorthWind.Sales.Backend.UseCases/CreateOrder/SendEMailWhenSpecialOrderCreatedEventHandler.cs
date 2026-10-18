using NorthWind.Sales.Backend.UseCases.Resources;

namespace NorthWind.Sales.Backend.UseCases.CreateOrder;

internal class SendEMailWhenSpecialOrderCreatedEventHandler(IMailService mailService)
    : IDomainEventHandler<SpecialOrderCreatedEvent>
{
    public Task Handle(SpecialOrderCreatedEvent createdOrder) =>
        mailService.SendMailToAdministrator(
            CreateOrderMessages.SendEmailSubject,
            string.Format(CreateOrderMessages.SendEmailBodyTemplate,
                createdOrder.OrderId, createdOrder.ProductsCount));
}
