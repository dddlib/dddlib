namespace dddlib.Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            //new Anemic.Playground().Play();
            //new Domain.Playground().Play();
            //new DomainDDD.Playground().Play();
            //new DomainES.Playground().Play();

            new Anemic.Application().Demonstrate();
            new Domain.Application().Demonstrate();
            new DomainDDD.Application().Demonstrate();
            new DomainES.Application().Demonstrate();
        }
    }
}
