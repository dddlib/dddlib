// <copyright file="Wheel.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.RuntimeMode.Plain
{
    public class Wheel : Entity
    {
        private readonly string serialNumber;

        public Wheel(string serialNumber)
        {
            Guard.Against.Null(() => serialNumber);

            this.serialNumber = serialNumber;
        }

        public string SerialNumber
        {
            get { return this.serialNumber; }
        }
    }
}
