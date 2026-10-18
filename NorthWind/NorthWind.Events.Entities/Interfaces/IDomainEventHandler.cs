namespace NorthWind.Events.Entities.Interfaces;

public interface IDomainEventHandler<EventType> where EventType : IDomainEvent
{
    Task Handle(EventType eventTypeInstance);
}
