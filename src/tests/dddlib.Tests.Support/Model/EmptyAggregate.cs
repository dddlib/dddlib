namespace dddlib.Tests.Support.Model
{
    public class EmptyAggregate : AggregateRoot
    {
        [NaturalKey]
        public string NaturalKey
        {
            get { return string.Empty; }
        }
    }
}
