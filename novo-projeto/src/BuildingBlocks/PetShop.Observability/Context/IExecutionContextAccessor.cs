using PetShop.Observability.Propagation;

namespace PetShop.Observability.Context;

public interface IExecutionContextAccessor
{
    PropagationContextSnapshot? Current { get; }

    IDisposable Push(PropagationContextSnapshot context);
}
