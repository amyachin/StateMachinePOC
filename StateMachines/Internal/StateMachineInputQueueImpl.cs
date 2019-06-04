using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StateMachines.Internal
{
    class StateMachineInputQueueImpl<TActor> : IStateMachineInputQueue<TActor>, IDisposable
    {
        internal StateMachineInputQueueImpl(IEnumerable<TActor> input, int batchSize)
        {
            _batchSize = batchSize;
            _buffer = new List<TActor>(batchSize);

            Data = _buffer.AsReadOnly();
            _input = input;
        }

        public Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            _buffer.Clear();

            if (_enumerator == null)
            {
                _enumerator = _input.GetEnumerator();
            }

            int count = _batchSize > 0 ? _batchSize : int.MaxValue;

            while (count > 0 && _enumerator.MoveNext())
            {
                _buffer.Add(_enumerator.Current);
                count--;
            }

            return Task.FromResult(_buffer.Count > 0);
        }

        public IReadOnlyCollection<TActor> Data { get; }

        public void Dispose()
        {
            if (_enumerator != null)
            {
                _enumerator.Dispose();
            }
        }

        IEnumerator<TActor> _enumerator;
        IEnumerable<TActor> _input;

        List<TActor> _buffer;
        int _batchSize;

    }

}
