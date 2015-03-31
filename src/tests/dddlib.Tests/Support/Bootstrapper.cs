// <copyright file="Bootstrapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

////namespace dddlib.Tests.Acceptance.Support
////{
////    using dddlib.Configuration;
////    using dddlib.Tests.Acceptnace.Support;

////    public class Bootstrapper : IBootstrapper
////    {
////        public void Bootstrap(IConfiguration configure)
////        {
////            configure.AggregateRoot<Car>()
////                .ToUseNaturalKey(car => car.Registration).WithSerializer(null)
////                .ToUseNaturalKey(car => car.Registration).WithSerializer(Registration => Registration.Number, registrationNumber => new Registration(registrationNumber))
////                .ToReconstituteUsing(() => null);

////            configure.Entity<Wheel>()
////                .ToUseNaturalKey(wheel => wheel.SerialNumber);

////            configure.ValueObject<Registration>()
////                .ToMapToEvent<object>((registration, @event) => { })
////                .ToSerializeUsing(null)();
////        }
////    }
////}
