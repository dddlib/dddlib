using System.Globalization;
using dddlib.Configuration;
using dddlib.Runtime;
using dddlib.Tests.Support;

namespace dddlib.Tests.Features;

// As someone who uses dddlib
// In order to store value objects as natural keys
// I need to be able to control how a value object is serialized
public abstract class ValueObjectSerialization : Feature
{
    public sealed class CustomValueObjectSerializer : ValueObjectSerialization
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object with a serializer defined in the bootstrapper
            // And an instance of that value object
            var instance = new Subject { Value = "value" };

            // And that instance is serialized and deserialized
            var serializer = Application.Current.GetValueObjectType(typeof(Subject)).Serializer;
            var serializedSubject = serializer.Serialize(instance);
            var otherInstance = (Subject)serializer.Deserialize(serializedSubject);

            // Then the first instance is equal to the other instance
            await Assert.That(otherInstance).IsEqualTo(instance);

            // And the serialized form came from the custom serializer
            await Assert.That(serializedSubject).StartsWith("V:");
        }

        public class Subject : ValueObject<Subject>
        {
            public string? Value { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.ValueObject<Subject>().ToUseValueObjectSerializer(new SubjectSerializer());
            }
        }

        private sealed class SubjectSerializer : IValueObjectSerializer
        {
            public string Serialize(object valueObject) =>
                string.Format(CultureInfo.InvariantCulture, "V:{0}", ((Subject)valueObject).Value);

            public object Deserialize(string serializedValueObject) => new Subject { Value = serializedValueObject[2..] };
        }
    }

    public sealed class CustomValueObjectSerializerViaDelegates : ValueObjectSerialization
    {
        [Test]
        public async Task Scenario()
        {
            // Given a value object with a serializer defined in the bootstrapper via delegates
            // And an instance of that value object
            var instance = new Subject { Value = "value" };

            // And that instance is serialized and deserialized
            var serializer = Application.Current.GetValueObjectType(typeof(Subject)).Serializer;
            var serializedSubject = serializer.Serialize(instance);
            var otherInstance = (Subject)serializer.Deserialize(serializedSubject);

            // Then the first instance is equal to the other instance
            await Assert.That(otherInstance).IsEqualTo(instance);

            // And the serialized form came from the custom serializer
            await Assert.That(serializedSubject).StartsWith("X:");
        }

        public class Subject : ValueObject<Subject>
        {
            public string? Value { get; set; }
        }

        private sealed class BootStrapper : IBootstrap<Subject>
        {
            public void Bootstrap(IConfiguration configure)
            {
                configure.ValueObject<Subject>()
                    .ToUseValueObjectSerializer(
                        subject => string.Format(CultureInfo.InvariantCulture, "X:{0}", subject.Value),
                        value => new Subject { Value = value[2..] });
            }
        }
    }
}
