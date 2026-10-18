using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NorthWind.Entities.Interfaces;
using NorthWind.Events.Entities.Interfaces;
using NorthWind.Sales.Entities.Dtos.CreateOrder;
using NorthWind.Sales.Backend.BusinessObjects.Aggregates;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces.CreateOrder;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces.Repositories;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.ValueObjects;
using NorthWind.Exceptions.Entities.Exceptions;

public static class EventChecks
{
    public static async Task Main()
    {
        var results = new List<object>();
        foreach (var scenario in new[] { "one", "two", "three", "duplicates", "special", "validation", "persistence" })
        {
            var repo = new RepositoryProbe { Fail = scenario == "persistence" };
            var mail = new MailProbe(repo);
            var output = new OutputProbe(repo);
            var services = new ServiceCollection();
            services.AddUseCasesServices().AddEventServices();
            services.AddSingleton<ICommandsRepository>(repo);
            services.AddSingleton<IMailService>(mail);
            services.AddSingleton<ICreateOrderOutputPort>(output);
            services.AddSingleton<IModelValidatorHub<CreateOrderDto>>(new ValidatorProbe(scenario != "validation"));
            using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using var scope = provider.CreateScope();
            var input = scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>();
            int[] ids = scenario switch { "one" => [1], "two" => [1,3], "three" => [1,3,4], "duplicates" => [1,3,4,1], _ => [1,3,4,1,5] };
            var dto = new CreateOrderDto("ALFKI", "31 Oriente 810", "Puebla", "México", "72500", ids.Select(id => new CreateOrderDetailDto(id, 35, 5)));
            Exception? caught = null;
            try { await input.Handle(dto); } catch (Exception ex) { caught = ex; }
            if (scenario == "validation")
            {
                Check(caught is ValidationException && !repo.Created && mail.Messages.Count == 0 && !output.Called, "Validation prevents persistence and event");
            }
            else if (scenario == "persistence")
            {
                Check(caught is UpdateException && repo.Created && !repo.Saved && mail.Messages.Count == 0 && !output.Called, "Persistence failure prevents event");
            }
            else
            {
                Check(caught is null && repo.Saved && output.Called && output.OrderId == 1234, "Successful output after persistence");
                Check(repo.Order!.OrderDetails.Count == ids.Distinct().Count(), "Count uses grouped products");
                if (scenario == "special")
                    Check(mail.Messages.SequenceEqual(new[] { ("Orden especial creada", "Se ha creado la orden 1234 con 4 productos.") }), "Real handler formats final id and unique count");
                else
                    Check(mail.Messages.Count == 0, "No event for at most three unique products");
                if (scenario == "duplicates") Check(repo.Order.OrderDetails.Single(d => d.ProductId == 1).Quantity == 10, "Repeated quantities grouped");
            }
            results.Add(new { name = scenario, passed = true, mailCalls = mail.Messages.Count });
        }
        var sequence = new List<int>();
        var hubServices = new ServiceCollection().AddEventServices();
        hubServices.AddSingleton<IDomainEventHandler<ProbeEvent>>(new ProbeHandler(sequence, 1));
        hubServices.AddSingleton<IDomainEventHandler<ProbeEvent>>(new ProbeHandler(sequence, 2));
        using (var provider = hubServices.BuildServiceProvider())
        using (var scope = provider.CreateScope())
        {
            var hub = scope.ServiceProvider.GetRequiredService<IDomainEventHub<ProbeEvent>>();
            await hub.Raise(new ProbeEvent());
            Check(sequence.SequenceEqual(new[] { 1, 2 }), "All handlers awaited sequentially");
        }
        results.Add(new { name = "sequential-handlers", passed = true });
        File.WriteAllText("artifacts/verification9/event-results.json", JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(results));
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    sealed class RepositoryProbe : ICommandsRepository
    {
        public bool Fail, Created, Saved;
        public OrderAggregate? Order;
        public Task CreateOrder(OrderAggregate order) { Created = true; Order = order; return Task.CompletedTask; }
        public Task SaveChanges() { if (Fail) throw new UpdateException("Controlled test failure"); Order!.Id = 1234; Saved = true; return Task.CompletedTask; }
    }
    sealed class MailProbe(RepositoryProbe repo) : IMailService
    {
        public readonly List<(string, string)> Messages = [];
        public Task SendMailToAdministrator(string subject, string body) { Check(repo.Saved, "Event must follow SaveChanges"); Messages.Add((subject, body)); return Task.CompletedTask; }
    }
    sealed class OutputProbe(RepositoryProbe repo) : ICreateOrderOutputPort
    {
        public bool Called;
        public int OrderId { get; private set; }
        public Task Handle(OrderAggregate order) { Check(repo.Saved, "Output must follow SaveChanges"); Called = true; OrderId = order.Id; return Task.CompletedTask; }
    }
    sealed class ValidatorProbe(bool valid) : IModelValidatorHub<CreateOrderDto>
    {
        public IEnumerable<ValidationError> Errors => valid ? [] : [new ValidationError("CustomerId", "Test error")];
        public Task<bool> Validate(CreateOrderDto model) => Task.FromResult(valid);
    }
    public sealed class ProbeEvent : IDomainEvent { }
    sealed class ProbeHandler(List<int> sequence, int number) : IDomainEventHandler<ProbeEvent>
    {
        public async Task Handle(ProbeEvent value) { await Task.Delay(10); sequence.Add(number); }
    }
}
