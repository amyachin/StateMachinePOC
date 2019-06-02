using Microsoft.Extensions.Logging;
using System;
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
        public Actor(RequestStatusRecord statusRecord)
        {
            StatusRecord = statusRecord;
        }

        public RequestStatusRecord StatusRecord { get; }

        public TStatus Status
        {
            get
            {
                return (TStatus)(object)StatusRecord.StatusId;
            }
            set
            {
                StatusRecord.StatusId = ConvertStatusToStatusId(value);
            }
        }

        public static int ConvertStatusToStatusId(TStatus value)
        {
            return(int)Convert.ChangeType(value, Enum.GetUnderlyingType(typeof(TStatus)));
        }

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
    
    public class StateTransitionError : Exception
    {
        public StateTransitionError(string message, object errorStatus, Exception innerException) : base(message, innerException)
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

        public class StateTransition
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


        public virtual async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            this.CancellationToken = cancellationToken;
            IList<TActor> batch = await GetPendingMessagesFromQueue();

            if (batch.Count == 0)
            {
                // Nothing to do
                return;
            }

            CancellationToken = cancellationToken;

            _dispatcher = new ActionBlock<TActor>(DispatchActor, new ExecutionDataflowBlockOptions { CancellationToken = cancellationToken });
            _processor = new ActionBlock<StateTransition>(ProcessTransition, new ExecutionDataflowBlockOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded });
            _countdown = new Countdown(batch.Count);

            try
            {
                foreach (var item in batch)
                {
                    _dispatcher.Post(item);
                }

                await _countdown.WaitAsync(cancellationToken);

                _processor.Complete();
                _dispatcher.Complete();

                await Task.WhenAll(_dispatcher.Completion, _processor.Completion);
            }
            finally
            {
                _countdown.Dispose();
            }
        }

        private void DispatchActor(TActor actor)
        {
            StateTransition transition = null;
            try
            {
                transition = GetNextTransition(actor);
            }
            catch(Exception ex)
            {
                Logger.LogError(ex, "Error while getting next transition.");
            }


            if (transition != null)
            {
                _processor.Post(transition);
            }
            else
            {
                // No more tranistions found for this actor we should signal completion.
                _countdown.Release();
            }
        }

        private async Task ProcessTransition(StateTransition transition)
        {
            // Capture status value before executng the transition
            TStatus prevStatus = transition.Source.Status;

            try
            {
                CancellationToken.ThrowIfCancellationRequested();
                var newStatus = await transition.ExecuteAsync();
                await ChangeStatus(transition.Source, newStatus);
            }
            catch (OperationCanceledException)
            {
                // Operation has been cancelled, simply drop the item
                // Since the state did not change the item will be picked up again from the queue
                _countdown.Release();
                return;
            }
            catch (StateMachineException ex)
            {
                // Process terminated due to internal issues, log message and drop the item
                // Since the state did not change the item will be picked up again from the queue
                Logger.LogError(ex, ex.Message);
                _countdown.Release();
                return;
            }
            catch (StateTransitionError ex)
            {
                await SetErrorStatus(transition.Source, (TStatus)ex.ErrorStatus, ex.Message, ex.InnerException);
            }
            catch (Exception ex)
            {
                await SetErrorStatus(transition.Source, DefaultErrorStatus, "Unexpected error occured.", ex);
            }


            if (transition.Source.Status.Equals(prevStatus))
            {
                Logger.LogWarning("Status did not change during transition - possible logical error (status = {status}).", prevStatus);
            }

            // Place the actor to dispatcher queue
            _dispatcher.Post(transition.Source);

        }

        protected abstract Task<IList<TActor>> GetPendingMessagesFromQueue();

        protected abstract StateTransition GetNextTransition(TActor actor);

        protected StateTransition CreateTransition(TActor actor, Func<TActor, Task<TStatus>> operation)
        {
            return new StateTransition(actor, operation);
        }

        protected StateTransitionError CreateTransitionError(string message, TStatus errorStatus, Exception innerException)
        {
            return new StateTransitionError(message, errorStatus, innerException);
        }

        protected async Task ChangeStatus(TActor actor, TStatus newStatus)
        {
            if (actor.Status.Equals(newStatus))
            {
                return;
            }

            try
            {
                await ScheduleService.ChangeStatus(actor.StatusRecord, Actor<TStatus>.ConvertStatusToStatusId(newStatus), null);
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
                Logger.LogError(ex, "Process error: {message} (requestId : {requestId}, status: {status}).", message, actor.StatusRecord.RequestId, actor.Status);
                await ScheduleService.ChangeStatus(actor.StatusRecord, Actor<TStatus>.ConvertStatusToStatusId(newStatus), message);
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

        Countdown _countdown;
        ActionBlock<TActor> _dispatcher;
        ActionBlock<StateTransition> _processor;
    }
}
