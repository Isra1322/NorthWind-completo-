namespace NorthWind.Events.Entities.Interfaces;

public interface IDomainEventHub<EventType> where EventType : IDomainEvent
{
    Task Raise(EventType eventTypeInstance);
}
