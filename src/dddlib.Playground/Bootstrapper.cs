namespace dddlib.Playground
{
    using dddlib.Configuration;

    internal class Bootstrapper : IBootstrapper
    {
        public void Bootstrap(IConfiguration configure)
        {
            configure.AggregateRoot<DomainDDD.Car>()
                .ToReconstituteUsing(() => new DomainDDD.Car());

            configure.AggregateRoot<DomainES.Car>()
                .ToReconstituteUsing(() => new DomainES.Car());
        }
    }
}
