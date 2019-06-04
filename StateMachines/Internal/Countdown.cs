using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StateMachines.Internal
{
    class Countdown : IDisposable
    {
        public Countdown(int initialCount)
        {
            _semaphore = new SemaphoreSlim(0, 1);
            _counter = initialCount;
        }

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return _semaphore.WaitAsync(cancellationToken);
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }

        public void Release()
        {
            var value = Interlocked.Decrement(ref _counter);
            if (value == 0)
            {
                _semaphore.Release();
            }
        }

        SemaphoreSlim _semaphore;
        int _counter;
    }
}
