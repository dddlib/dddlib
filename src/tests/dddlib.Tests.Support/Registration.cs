// <copyright file="Registration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Support
{
    using System.Globalization;

    // NOTE (Cameron): Value objects should be sealed to take advantage of the ValueObject<T> base class.
    public sealed class Registration : ValueObject<Registration>
    {
        public Registration(string number, IRegistrationService registrationService)
        {
            Guard.Against.Null(() => number);
            Guard.Against.Null(() => registrationService);

            if (!registrationService.ConfirmValid(number))
            {
                throw new BusinessException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The specified registration number '{0}' is invalid.",
                        number));
            }

            this.Number = number;
        }

        internal Registration(string number)
        {
            this.Number = number;
        }

        public string Number { get; private set; }
    }
}
