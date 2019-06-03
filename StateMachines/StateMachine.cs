using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace StateMachines
{

    public class Actor<TStatus> 
        where TStatus : struct
    {
        public Actor(QueueItem item)
        {
            Item = item;
        }

        public QueueItem Item { get; }

        public TStatus Status
        {
            get
            {
                return ConvertStatusIdToStatus(Item.StatusId);
            }
            set
            {
                Item.StatusId = ConvertStatusToStatusId(value);
            }
        }

        public virtual object ConvertStatusToStatusId(TStatus status)
        {
            return Convert.ChangeType(status, Enum.GetUnderlyingType(typeof(TStatus)));
        }

        public virtual TStatus ConvertStatusIdToStatus(object statusId)
        {
            return (TStatus)Item.StatusId;

        }

    }

    public class StateTransition<TActor, TStatus>
        where TActor: Actor<TStatus>
        where TStatus: struct
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


    public abstract class StateMachine<TActor, TStatus> 
        where TActor : Actor<TStatus>
        where TStatus : struct
    {

        public StateMachine(ILoggerFactory loggerFactory, IScheduleService scheduleService, TStatus defaultErrorStatus)
        {
            Logger = loggerFactory.CreateLogger(GetType());
            ScheduleService = scheduleService;
            DefaultErrorStatus = defaultErrorStatus;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            this.CancellationToken = cancellationToken;

            IReadOnlyList<TActor> batch = await GetPendingActorsFromQueue();

            if (batch.Count == 0)
            {
                // Nothing to do
                return;
            }

            _dispatcher = new ActionBlock<TActor>((Action<TActor>)DispatchActor, new ExecutionDataflowBlockOptions { CancellationToken = cancellationToken });
            _processor = new ActionBlock<StateTransition<TActor, TStatus>>(ProcessTransition, new ExecutionDataflowBlockOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded });

            try
            {
                while (await ProcessQueueAsync(batch))
                {
                    batch = await GetPendingActorsFromQueue();
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

        private async Task<bool> ProcessQueueAsync(IReadOnlyList<TActor> items)
        {
            if (items.Count == 0)
            {
                return false;
            }

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

            if (!_processor.Completion.IsCompleted && !_dispatcher.Completion.IsCompleted)
            {
                return ((items is ListFragment<TActor> v)) ? v.MoreDataPending : false;
            }
            else
            {
                return false;
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

        protected abstract Task<IReadOnlyList<TActor>> GetPendingActorsFromQueue();

        protected abstract StateTransition<TActor, TStatus> GetNextTransition(TActor actor);

        protected StateTransition<TActor, TStatus> CreateTransition(TActor actor, Func<TActor, Task<TStatus>> operation)
        {
            return new StateTransition<TActor, TStatus>(actor, operation);
        }

        protected StateTransition<TActor, TStatus> Done()
        {
            return _doneTransition;
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
                await ScheduleService.ChangeStatus(actor.Item, actor.ConvertStatusToStatusId(newStatus), null);
                actor.Status = newStatus;
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
                Logger.LogError(ex, "Process error: {message} (ItemId : {itemId}, Status: {status}).", message, actor.Item.Id, actor.Status);
                await ScheduleService.ChangeStatus(actor.Item, actor.ConvertStatusToStatusId(newStatus), message);
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

        protected IScheduleService ScheduleService { get; }

        protected TStatus DefaultErrorStatus { get; }

        static StateTransition<TActor, TStatus> _doneTransition = new StateTransition<TActor, TStatus>(null, null);
        
        Countdown _countdown;
        ActionBlock<TActor> _dispatcher;
        ActionBlock<StateTransition<TActor, TStatus>> _processor;
    }
}
