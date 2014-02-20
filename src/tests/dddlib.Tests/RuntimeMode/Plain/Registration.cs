// <copyright file="Registration.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.RuntimeMode.Plain
{
    using System.Collections.Generic;

    public sealed class Registration : ValueObject<Registration>
    {
        public Registration(string number)
        {
            Guard.Against.Null(() => number);

            this.Number = number;
        }

        public string Number { get; private set; }

        // TODO (Cameron): Remove requirement for this.
        protected override IEnumerable<object> GetValue()
        {
            yield return this.Number;
        }
    }
}
