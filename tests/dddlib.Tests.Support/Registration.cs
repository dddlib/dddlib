using System.Globalization;

namespace dddlib.Tests.Support;

// A value object. In v2 value objects are records: structural equality, == and != come for free. The legacy
// bootstrapper registered a case-insensitive equality comparer for Registration; in v2 that is an Equals override.
public sealed record Registration
{
    public Registration(string number, IRegistrationService registrationService)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentNullException.ThrowIfNull(registrationService);

        if (!registrationService.ConfirmValid(number))
        {
            throw new BusinessException(
                string.Format(CultureInfo.InvariantCulture, "The specified registration number '{0}' is invalid.", number));
        }

        this.Number = number;
    }

    internal Registration(string number)
    {
        this.Number = number;
    }

    public string Number { get; }

    public bool Equals(Registration? other) =>
        other is not null && string.Equals(this.Number, other.Number, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(this.Number);
}
