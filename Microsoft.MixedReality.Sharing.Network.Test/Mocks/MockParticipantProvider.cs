using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Test.Mocks
{
    public class MockParticipantProvider : IParticipantProvider
    {
        public IParticipant CurrentParticipant { get; } = new MockParticipant(Guid.NewGuid().ToString());

        public async Task<IParticipant> GetParticipantAsync(string id, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);

            return new MockParticipant(id);
        }
    }

    public class MockParticipant : IParticipant
    {
        public string Id { get; }

        public string DisplayName { get; }

        public MockParticipant(string id)
        {
            Id = id;
            DisplayName = $"DisplayName:{id}";
        }

        public bool Equals(IParticipant other)
        {
            return Equals(Id, other.Id);
        }

        public int CompareTo(IParticipant other)
        {
            return string.Compare(Id, other.Id);
        }

        public override bool Equals(object obj)
        {
            return obj is IParticipant other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id?.GetHashCode() ?? 0;
        }
    }
}
