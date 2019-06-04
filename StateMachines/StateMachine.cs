using Microsoft.Extensions.Logging;
using StateMachines.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace StateMachines
{

    public class Actor<TStatus> 
    {
        public Actor()
        {
        }

        public TStatus Status
        {
            get; set;
        }

        public override string ToString()
        {
            return string.Format("Actor: {0}, Status: {1}", GetType(), Status);
        }

    }

    public class StateTransition<TActor, TStatus>
        where TActor: Actor<TStatus>
    {
        public StateTransition(TActor source, Func<TActor, Task<TStatus>> operation)
        {
            Source = source;
            Operation = operation;
        }

        public Task<TStatus> ExecuteAsync()
        {
            return Operation(Source);
        }

        public TActor Source { get; }

        private Func<TActor, Task<TStatus>> Operation { get; }
    }

    public class StateMachineException : Exception
    {
        public StateMachineException(string message) :base(message)
        {
        }

        public StateMachineException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
    
    public class StateTransitionException : Exception
    {
        public StateTransitionException(string message, object errorStatus, Exception innerException) : base(message, innerException)
        {
            ErrorStatus = errorStatus;
        }

        public object ErrorStatus { get; }
    }

    public class StateMachineStatusChangingArgs<TActor, TStatus> : EventArgs
        where TActor : Actor<TStatus>
    {
        public StateMachineStatusChangingArgs(TActor actor, TStatus newStatus, string message)
        {
            this.Actor = actor;
            this.NewStatus = newStatus;
            this.Message = message;
        }

        public TActor Actor
        {
            get; 
        }

        public TStatus NewStatus
        {
            get;
            set;
        }

        public TStatus CurrentStatus
        {
            get { return Actor.Status; }
        }
        
        public string Message { get; set; }
    }

    public interface IStateMachineInputQueue<TActor>
    {
        Task<bool> ReadAsync(CancellationToken cancellationToken);
        IReadOnlyCollection<TActor> Data { get; }
    }

    public abstract class StateMachine<TActor, TStatus>
        where TActor : Actor<TStatus>
    {

        public StateMachine(ILoggerFactory loggerFactory, TStatus defaultErrorStatus)
        {
            Logger = loggerFactory.CreateLogger(GetType());
            DefaultErrorStatus = defaultErrorStatus;
        }

        public Task ExecuteAsync(IEnumerable<TActor> input, CancellationToken cancellationToken)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            return ExecuteAsyncCore(input, 0, cancellationToken);
        }

        public Task ExecuteAsync(IEnumerable<TActor> input, int batchSize, CancellationToken cancellationToken)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            if (batchSize < 0)
            {
                throw new ArgumentOutOfRangeException("blockSize");
            }

            return ExecuteAsyncCore(input, batchSize, cancellationToken);
        }

        public async Task ExecuteAsync(IStateMachineInputQueue<TActor> inputQueue, CancellationToken cancellationToken)
        {
            this.CancellationToken = cancellationToken;

            if (!await inputQueue.ReadAsync(cancellationToken))
            {
                return;
            }

            _dispatcher = new ActionBlock<TActor>((Action<TActor>)DispatchActor, new ExecutionDataflowBlockOptions { CancellationToken = cancellationToken });
            _processor = new ActionBlock<StateTransition<TActor, TStatus>>(ProcessTransition, new ExecutionDataflowBlockOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded });

            try
            {
                bool hasData = true;

                while (hasData)
                {
                    await ProcessQueueAsync(inputQueue.Data);
                    hasData = await inputQueue.ReadAsync(cancellationToken);
                }

            }
            finally
            {
                // Ensure that processing threads are no longer running
                _processor.Complete();
                _dispatcher.Complete();

                await Task.WhenAll(_dispatcher.Completion, _processor.Completion);
            }
        }

        private async Task ExecuteAsyncCore(IEnumerable<TActor> input, int batchSize, CancellationToken cancellationToken)
        {
            using (var inputQueue = new StateMachineInputQueueImpl<TActor>(input, batchSize))
            {
                await ExecuteAsync(inputQueue, cancellationToken).ConfigureAwait(false);
            }
        }


        private async Task ProcessQueueAsync(IReadOnlyCollection<TActor> items)
        {
            _countdown = new Countdown(items.Count);

            try
            {
                foreach (var item in items)
                {
                    _dispatcher.Post(item);
                }

                var countdownTask = _countdown.WaitAsync(CancellationToken);
                await Task.WhenAny(countdownTask, _processor.Completion, _dispatcher.Completion);
            }
            finally
            {
                _countdown.Dispose();
            }
        }

        private void DispatchActor(TActor actor)
        {
            StateTransition<TActor, TStatus> transition = null;
            try
            {
                transition = GetNextTransition(actor);
            }
            catch(Exception ex)
            {
                Logger.LogError(ex, "Error while getting next transition.");
            }


            if (transition != null && transition.Source != null)
            {
                _processor.Post(transition);
            }
            else
            {
                // No more tranistions found for this actor we should signal completion.
                _countdown.Release();
            }
        }

        private async Task ProcessTransition(StateTransition<TActor, TStatus> transition)
        {
            var dispatched = false;

            try
            {
                CancellationToken.ThrowIfCancellationRequested();

                // Capture status value before executng the transition
                TStatus prevStatus = transition.Source.Status;
                var newStatus = await transition.ExecuteAsync();
                await ChangeStatus(transition.Source, newStatus);

                if (transition.Source.Status.Equals(prevStatus))
                {
                    Logger.LogWarning("Status did not change during transition - possible logical error (status = {status}).", prevStatus);
                }

                // Place the actor to dispatcher queue
                _dispatcher.Post(transition.Source);
                dispatched = true;
            }
            catch (OperationCanceledException)
            {
                // Operation has been cancelled, simply drop the item
                // Since the state did not change the item will be picked up again from the queue
            }
            catch (StateMachineException ex)
            {
                // Process terminated due to internal issues, log message and drop the item
                // Since the state did not change the item will be picked up again from the queue
                Logger.LogError(ex, ex.Message);
            }
            catch (StateTransitionException ex)
            {
                await SetErrorStatus(transition.Source, (TStatus)ex.ErrorStatus, ex.Message, ex.InnerException);
            }
            catch (Exception ex)
            {
                await SetErrorStatus(transition.Source, DefaultErrorStatus, "Unexpected error occured.", ex);
            }
            finally
            {
                if (!dispatched)
                {
                    _countdown.Release();
                }
            }

        }

        protected abstract StateTransition<TActor, TStatus> GetNextTransition(TActor actor);

        protected StateTransition<TActor, TStatus> CreateTransition(TActor actor, Func<TActor, Task<TStatus>> operation)
        {
            return new StateTransition<TActor, TStatus>(actor, operation);
        }

        protected StateTransition<TActor, TStatus> Done()
        {
            return _doneTransition;
        }

        protected virtual Task OnStatusChanging(StateMachineStatusChangingArgs<TActor, TStatus> e)
        {
            return Task.CompletedTask;
        }

        protected StateTransitionException CreateTransitionError(string message, TStatus errorStatus, Exception innerException)
        {
            return new StateTransitionException(message, errorStatus, innerException);
        }

        protected async Task ChangeStatus(TActor actor, TStatus newStatus)
        {
            if (actor.Status.Equals(newStatus))
            {
                return;
            }

            try
            {
                var e = new StateMachineStatusChangingArgs<TActor, TStatus>(actor, newStatus, null);
                await OnStatusChanging(e);
                actor.Status = e.NewStatus;
            }

            catch(Exception ex)
            {
                throw new StateMachineException("Error changing status", ex);
            }
        }

        private async Task SetErrorStatus(TActor actor, TStatus newStatus, string message, Exception ex)
        {
            // This method should not throw, see ProcessTranstions for the usage pattern
            try
            {
                Logger.LogError(ex, "Process error: {message} (Actor : {actor}).", message, actor);
                var e = new StateMachineStatusChangingArgs<TActor, TStatus>(actor, newStatus, message);
                await OnStatusChanging(e);
                actor.Status = newStatus;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "Unexpected errror while setting error status.");
                actor.Status = newStatus; // Assume the error status 
            }

        }

        protected CancellationToken CancellationToken { get; private set; }

        protected ILogger Logger { get; }

        protected TStatus DefaultErrorStatus { get; }


        static StateTransition<TActor, TStatus> _doneTransition = new StateTransition<TActor, TStatus>(null, null);
        
        Countdown _countdown;
        ActionBlock<TActor> _dispatcher;
        ActionBlock<StateTransition<TActor, TStatus>> _processor;
    }
}
