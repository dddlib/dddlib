// <copyright file="Car.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

////namespace dddlib.Tests.Acceptnace.Support
////{
////    using System.Collections.Generic;
////    using System.Globalization;

////    public class Car : AggregateRoot
////    {
////        private readonly List<Wheel> wheels = new List<Wheel>();
////        private readonly Registration registration;

////        public Car(Registration registration)
////        {
////            Guard.Against.Null(() => registration);

////            this.registration = registration;
////        }

////        public Registration Registration
////        {
////            get { return this.registration; }
////        }

////        public void Attach(Wheel wheel)
////        {
////            Guard.Against.Null(() => wheel);

////            if (this.wheels.Contains(wheel))
////            {
////                throw new BusinessException(
////                    string.Concat(
////                        CultureInfo.InvariantCulture,
////                        "The wheel with the serial number '{0}' is already attached to the car with the registration '{1}'.",
////                        wheel.SerialNumber, 
////                        this.Registration.Number));
////            }

////            if (this.wheels.Count > 4)
////            {
////                throw new BusinessException(
////                    string.Concat(
////                        CultureInfo.InvariantCulture,
////                        "Cannot attach the wheel with the serial number '{0}' to the car with the registration '{1}' as it already has 4 wheels.",
////                        wheel.SerialNumber,
////                        this.Registration.Number));
////            }

////            this.wheels.Add(wheel);
////        }

////        public void Drive()
////        {
////            if (this.wheels.Count == 4)
////            {
////                return;
////            }

////            throw new BusinessException(
////                string.Concat(
////                    CultureInfo.InvariantCulture,
////                    "Cannot drive to the car with the registration '{0}' as it only has {1} wheels.",
////                    this.Registration.Number,
////                    this.wheels.Count));
////        }
////    }
////}
