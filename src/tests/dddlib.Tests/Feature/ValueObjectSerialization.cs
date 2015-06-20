// <copyright file="ValueObjectSerialization.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Tests.Feature
{
    using System.Globalization;
    using dddlib.Configuration;
    using dddlib.Runtime;
    using dddlib.Tests.Sdk;
    using FluentAssertions;
    using Xbehave;

    // As someone who uses dddlib
    // In order to persist an aggregate root with a value object natural key that has a non-standard constructor
    // I want to be able to serialize and deserialize that value object (for the identity map)
    public abstract class ValueObjectSerialization : Feature
    {
        // NOTE (Cameron): Value object serialization, whilst defined in dddlib, is only used in dddlib.Persistence - so this test is a little 'raw'.
        public class CustomValueObjectSerializer : ValueObjectSerialization
        {
            [Scenario]
            public void Scenario(Subject instance, Subject otherInstance, string serializedSubject)
            {
                "Given a value object with a serializer defined in the bootstrapper"
                    .f(() => { });

                "And an instance of that value object"
                    .f(() =>
                    {
                        instance = new Subject { Value = "value" };
                    });

                "And that instance is serialized and deserialized"
                    .f(() =>
                    {
                        serializedSubject = Application.Current.GetValueObjectType(typeof(Subject)).Serializer.Serialize(instance);
                        otherInstance = (Subject)Application.Current.GetValueObjectType(typeof(Subject)).Serializer.Deserialize(serializedSubject);
                    });

                "Then the first instance is equal to the other instance"
                    .f(() => instance.Should().Be(otherInstance));

                "And "
                    .f(() => serializedSubject.Substring(0, 2).Should().Be("V:"));
            }

            public class Subject : ValueObject<Subject>
            {
                public string Value { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.ValueObject<Subject>().ToUseValueObjectSerializer(new SubjectSerializer());
                }
            }

            private class SubjectSerializer : IValueObjectSerializer
            {
                public string Serialize(object valueObject)
                {
                    var subject = (Subject)valueObject;
                    return string.Format(CultureInfo.InvariantCulture, "V:{0}", subject.Value);
                }

                public object Deserialize(string serializedValueObject)
                {
                    return new Subject { Value = serializedValueObject.Substring(2) };
                }
            }
        }

        public class CustomValueObjectSerializerViaDelegates : ValueObjectSerialization
        {
            [Scenario]
            public void Scenario(Subject instance, Subject otherInstance, string serializedSubject)
            {
                "Given a value object with a serializer defined in the bootstrapper"
                    .f(() => { });

                "And an instance of that value object"
                    .f(() =>
                    {
                        instance = new Subject { Value = "value" };
                    });

                "And that instance is serialized and deserialized"
                    .f(() =>
                    {
                        serializedSubject = Application.Current.GetValueObjectType(typeof(Subject)).Serializer.Serialize(instance);
                        otherInstance = (Subject)Application.Current.GetValueObjectType(typeof(Subject)).Serializer.Deserialize(serializedSubject);
                    });

                "Then the first instance is equal to the other instance"
                    .f(() => instance.Should().Be(otherInstance));

                "And "
                    .f(() => serializedSubject.Substring(0, 2).Should().Be("X:"));
            }

            public class Subject : ValueObject<Subject>
            {
                public string Value { get; set; }
            }

            private class BootStrapper : IBootstrap<Subject>
            {
                public void Bootstrap(IConfiguration configure)
                {
                    configure.ValueObject<Subject>()
                        .ToUseValueObjectSerializer(
                            subject => string.Format(CultureInfo.InvariantCulture, "X:{0}", subject.Value),
                            value => new Subject { Value = value.Substring(2) });
                }
            }
        }
    }
}
