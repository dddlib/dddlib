// <copyright file="Bug0002.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

////namespace dddlib.Tests.Bugs
////{
////    using System;
////    using Xunit;

////    public class Bug0001
////    {
////        [Fact]
////        public void ShoulWorkCorrectly()
////        {
////            var identityMap = new SqlServerIdentoityMap
////        }

////        public class Car : AggregateRoot
////        {
////            public Car(Registraion registration)
////            {
////                Guard.Against.Null(() => registration);

////                this.Registration = registration;
////            }

////            [NaturalKey]
////            public Registraion Registration { get; private set; }
////        }

////        public class Registraion : ValueObject<Registraion>
////        {
////            public Registraion(string number)
////            {
////                Guard.Against.Null(() => number);

////                this.Number = number.ToUpperInvariant();
////            }

////            public string Number { get; private set; }
////        }
////    }
////}
