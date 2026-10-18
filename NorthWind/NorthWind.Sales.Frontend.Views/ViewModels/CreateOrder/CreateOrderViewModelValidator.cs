using NorthWind.Validation.Entities.Abstractions;
using NorthWind.Validation.Entities.Enums;

namespace NorthWind.Sales.Frontend.Views.ViewModels.CreateOrder;

internal class CreateOrderViewModelValidator(IModelValidatorHub<CreateOrderDto> validator)
    : AbstractViewModelValidator<CreateOrderDto, CreateOrderViewModel>(
        validator, ValidationConstraint.AlwaysValidate)
{
    protected override CreateOrderDto ToModel(CreateOrderViewModel viewModel) =>
        (CreateOrderDto)viewModel;
}
