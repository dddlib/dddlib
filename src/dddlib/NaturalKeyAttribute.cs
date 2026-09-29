namespace dddlib;

/// <summary>
/// Marks the property of an entity that acts as its natural key. Entities of the same type with equal natural
/// key values are considered equal.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class NaturalKeyAttribute : Attribute
{
}
