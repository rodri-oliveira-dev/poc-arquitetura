using System.Threading;

using PetShop.Observability.Propagation;

namespace PetShop.Observability.Context;

public sealed class ExecutionContextAccessor : IExecutionContextAccessor
{
    private readonly AsyncLocal<ContextHolder?> _current = new();

    public PropagationContextSnapshot? Current => _current.Value?.Context;

    public IDisposable Push(PropagationContextSnapshot context)
    {
        ContextHolder? previous = _current.Value;
        var pushed = new ContextHolder(context);
        _current.Value = pushed;

        return new RestoreScope(this, pushed, previous);
    }

    private sealed class ContextHolder
    {
        public ContextHolder(PropagationContextSnapshot context)
        {
            Context = context;
        }

        public PropagationContextSnapshot Context { get; }
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly ExecutionContextAccessor _owner;
        private readonly ContextHolder _pushed;
        private readonly ContextHolder? _previous;
        private int _disposed;

        public RestoreScope(
            ExecutionContextAccessor owner,
            ContextHolder pushed,
            ContextHolder? previous)
        {
            _owner = owner;
            _pushed = pushed;
            _previous = previous;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (ReferenceEquals(_owner._current.Value, _pushed))
            {
                _owner._current.Value = _previous;
            }
        }
    }
}
