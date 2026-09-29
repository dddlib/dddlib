namespace dddlib.Tests.Support;

public class Wheel : Entity
{
    public Wheel(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id cannot be an empty GUID.", nameof(id));
        }

        this.Id = id;
    }

    public Guid Id { get; }
}
