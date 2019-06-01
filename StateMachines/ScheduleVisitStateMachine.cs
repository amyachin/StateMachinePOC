using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace StateMachines
{

    class ScheduleVisitActor
    {
        public ScheduleVisitActor(RequestStatusRecord statusRecord)
        {
            StatusRecord = statusRecord;
        }

        public RequestStatusRecord StatusRecord { get; }

        public RequestStatus Status { get { return StatusRecord.Status; } }

        public ScheduleVisitRequest Data { get; set; }


        // TODO: Encrollment-specific data listed here
    }

    class ScheduleVisitTransition
    {
        public ScheduleVisitTransition(ScheduleVisitActor source, Func<ScheduleVisitActor, Task<RequestStatus>> transition)
        {
            Source = source;
            Transition = transition;
        }

        public Task<RequestStatus> ExecuteAsync()
        {
            return Transition(Source);
        }

        public ScheduleVisitActor Source { get; }

        private Func<ScheduleVisitActor, Task<RequestStatus>> Transition { get; }
    }

    class ScheduleVisitException: Exception
    {
        public ScheduleVisitException(string message, RequestStatus errorStatus, Exception innerException):base(message, innerException)
        {
            ErrorStatus = errorStatus;
        }

        public RequestStatus ErrorStatus { get; }
    }

    public class ScheduleVisitStateMachine 
    {
        public ScheduleVisitStateMachine(IScheduleService service)
        {
            _service = service;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var batch = await _service.GetPendingRequests(RequestType.ScheduleVisit, 1000);
            if (batch.Count == 0)
            {
                // Nothing to do
                return;
            }

            _cancellationToken = cancellationToken;

            _dispatcher = new ActionBlock<ScheduleVisitActor>(DispatchActor, new ExecutionDataflowBlockOptions { CancellationToken = cancellationToken });
            _processor = new ActionBlock<ScheduleVisitTransition>(ProcessTransition, new ExecutionDataflowBlockOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = DataflowBlockOptions.Unbounded });
            _countdown = new Countdown(batch.Count);

            try
            {
                foreach (var item in batch)
                {
                    ScheduleVisitActor actor = new ScheduleVisitActor(item);
                    _dispatcher.Post(actor);
                }

                await _countdown.WaitAsync(cancellationToken);
            }
            finally
            {
                _countdown.Dispose();
            }
        }

        void DispatchActor(ScheduleVisitActor actor)
        {
            switch (actor.Status)
            {
                case RequestStatus.ConsumerEnrollmentPending:
                case RequestStatus.ConsumerEnrollmentRunning:
                    _processor.Post(new ScheduleVisitTransition(actor, EnrollConsumer));
                    break;

                case RequestStatus.ScheduleVisitPending:
                case RequestStatus.ScheduleVisitRunning:
                    _processor.Post(new ScheduleVisitTransition(actor, ScheduleVisitForEnrolledConsumer));
                    break;

                default:
                    // a terminal status reached
                    _countdown.Release();
                    break;
            }
        }


        async Task<RequestStatus> EnrollConsumer(ScheduleVisitActor source)
        {
            try
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (source.Data == null)
                {
                    source.Data = await _service.GetScheduleVistRequest(source.StatusRecord.RequestId);
                }

                if (source.Status == RequestStatus.ConsumerEnrollmentRunning)
                {
                    // Retrieve previously started enrollment 
                }

                // if enrollment has not started, do another one
                await ChangeStatus(source.StatusRecord, RequestStatus.ConsumerEnrollmentRunning);

                // TODO: Enroll consumer here

                return RequestStatus.ScheduleVisitPending;
            }
            catch(OperationCanceledException)
            {
                throw; // operation has been cancelled by host (graceful shutdown)
            }

            catch (Exception ex)
            {
                throw new ScheduleVisitException("Consumer enrollment failed.", RequestStatus.ConsumerEnrollnmentFailed, ex);
            }
        }

        async Task<RequestStatus> ScheduleVisitForEnrolledConsumer(ScheduleVisitActor source)
        {
            try
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (source.Data == null)
                {
                    source.Data = await _service.GetScheduleVistRequest(source.StatusRecord.RequestId);
                }

                await ChangeStatus(source.StatusRecord, RequestStatus.ScheduleVisitRunning);

                // TODO: schedule visit here

                return RequestStatus.ScheduleVisitCompleted;
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ScheduleVisitException("Schedule visit failed.", RequestStatus.ScheduleVisitFailed, ex);
            }
        }

        async Task ProcessTransition(ScheduleVisitTransition transition)
        {
            try
            {
                await transition.ExecuteAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch(ScheduleVisitException ex)
            {
                await ChangeStatusWithError(transition.Source.StatusRecord, ex.ErrorStatus, ex.Message, ex.InnerException);
            }
            catch(Exception ex)
            {
                await ChangeStatusWithError(transition.Source.StatusRecord, RequestStatus.ScheduleVisitFailed, "Unexpected error occured.", ex);
            }

        }

        async Task ChangeStatus(RequestStatusRecord record,  RequestStatus newStatus)
        {
            if (record.Status == newStatus)
            {
                return;
            }

            await _service.ChangeStatus(record, newStatus, null);
            record.Status = newStatus;
        }

        async Task ChangeStatusWithError(RequestStatusRecord record, RequestStatus newStatus, string message, Exception ex)
        {
            // TODO: Log exception
            await _service.ChangeStatus(record, newStatus, message);
            record.Status = newStatus;

        }

        Countdown _countdown;
        CancellationToken _cancellationToken;
        IScheduleService _service;
        ActionBlock<ScheduleVisitActor> _dispatcher;
        ActionBlock<ScheduleVisitTransition> _processor;
    }

}
