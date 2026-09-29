using System.Globalization;

namespace dddlib.Tests.Support;

public sealed partial class Registration : ValueObject<Registration>
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
}
