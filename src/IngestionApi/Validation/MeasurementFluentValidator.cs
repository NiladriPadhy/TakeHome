using FluentValidation;

namespace IngestionApi.Validation;

public class MeasurementFluentValidator : AbstractValidator<Measurement>
{
    public MeasurementFluentValidator()
    {
        RuleFor(m => m.MeasurementId)
            .NotEqual(Guid.Empty)
            .WithMessage("'Measurement Id' must not be equal to '00000000-0000-0000-0000-000000000000'.");

        RuleFor(m => m.Timestamp)
            .NotEqual(default(DateTimeOffset))
            .WithMessage("'Timestamp' must not be empty.");

        RuleFor(m => m.DeviceId)
            .NotEmpty();

        RuleFor(m => m.Type)
            .NotEmpty();

        RuleFor(m => m.Value)
            .Must(v => v.ValueKind != System.Text.Json.JsonValueKind.Undefined)
            .WithMessage("'Value' must be provided.");
    }
}
