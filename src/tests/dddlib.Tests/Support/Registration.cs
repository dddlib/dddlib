// <copyright file="Registration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Acceptnace.Support
{
    public sealed class Registration : ValueObject<Registration>
    {
        public Registration(string number)
        {
            Guard.Against.Null(() => number);

            this.Number = number;
        }

        public string Number { get; private set; }
    }
}
