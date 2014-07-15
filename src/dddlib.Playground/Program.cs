namespace dddlib.Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            new Anemic.Application().Demonstrate();
            new Domain.Application().Demonstrate();
            new DomainDDD.Application().Demonstrate();
            new DomainES.Application().Demonstrate();
        }
    }
}
