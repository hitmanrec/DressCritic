namespace DressmakerCritic.Diagnostics
{
    // An in-flight result belongs to one uninterrupted viewing of one state.
    // A -> B -> A and leaving/reopening the book both invalidate the old ticket.
    public sealed class EvaluationSelection
    {
        private object owner, state;
        private int index = -1;
        public long Revision { get; private set; }
        public bool Observe(object nextOwner, object nextState, int nextIndex)
        {
            if (ReferenceEquals(owner, nextOwner) && ReferenceEquals(state, nextState) && index == nextIndex) return false;
            owner = nextOwner; state = nextState; index = nextIndex;
            Revision++;
            return true;
        }
        public bool IsCurrent(long ticket) => owner != null && state != null && ticket == Revision;
        public bool Accepts(long ticket, object evaluatedState) => IsCurrent(ticket) && ReferenceEquals(state, evaluatedState);
    }
}
