using System.Diagnostics.CodeAnalysis;
using dddlib.Tests.Support;

namespace dddlib.Tests.Features.Generated;

// As someone who uses dddlib
// In order to signal a violation of a business rule to the caller
// I need to be able to throw a business exception from the domain model
//
// The legacy feature file held only a placeholder comment; this scenario makes the intent executable.
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Feature name kept for parity with the legacy suite.")]
public abstract partial class BusinessException : Feature
{
    public sealed partial class BusinessRuleViolation : BusinessException
    {
        [Test]
        public async Task Scenario()
        {
            // Given a subject with a business rule
            var subject = new Subject();

            // When the rule is violated
            var action = () => subject.Withdraw(subject.Balance + 1);

            // Then a business exception is thrown with the rule as its message
            var exception = await Assert.That(action).Throws<dddlib.BusinessException>();
            await Assert.That(exception!.Message).IsEqualTo("Insufficient funds.");
            await Assert.That(subject.Balance).IsEqualTo(0);
        }

        public partial class Subject : AggregateRoot
        {
            public int Balance { get; private set; }

            public void Withdraw(int amount)
            {
                if (amount > this.Balance)
                {
                    throw new dddlib.BusinessException("Insufficient funds.");
                }

                this.Balance -= amount;
            }
        }
    }
}
