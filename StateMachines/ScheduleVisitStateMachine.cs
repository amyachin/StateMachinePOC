using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace StateMachines
{

    public class ScheduleVisitActor : Actor<ScheduleVisitStatus>
    {
        public ScheduleVisitActor(RequestStatusRecord statusRecord) : base(statusRecord)
        {
        }

        public ScheduleVisitRequest Data { get; set; }

        // TODO: Add enrollment-specific data 
    }

    public class ScheduleVisitStateMachine : StateMachine<ScheduleVisitActor, ScheduleVisitStatus>
    {
        public ScheduleVisitStateMachine(ILoggerFactory loggerFactory, IScheduleService service) : base(loggerFactory, service, ScheduleVisitStatus.ScheduleVisitFailed)
        {
        }


        protected override async Task<IList<ScheduleVisitActor>> GetPendingActorsFromQueue()
        {
            return (await ScheduleService.GetPendingRequests(RequestType.ScheduleVisit, 100))
                .Select(it => new ScheduleVisitActor(it))
                .ToList();
        }

        protected override StateTransition<ScheduleVisitActor, ScheduleVisitStatus> GetNextTransition(ScheduleVisitActor actor)
        {
            switch (actor.Status)
            {
                case ScheduleVisitStatus.ConsumerEnrollmentPending:
                case ScheduleVisitStatus.ConsumerEnrollmentRunning:
                    return CreateTransition(actor, EnrollConsumer);

                case ScheduleVisitStatus.ScheduleVisitPending:
                case ScheduleVisitStatus.ScheduleVisitRunning:
                    return CreateTransition(actor, ScheduleVisitForEnrolledConsumer);

                default:
                    return Done();
            }
        }

        async Task<ScheduleVisitStatus> EnrollConsumer(ScheduleVisitActor source)
        {
            try
            {
                CancellationToken.ThrowIfCancellationRequested();

                if (source.Data == null)
                {
                    source.Data = await ScheduleService.GetScheduleVisitRequest(source.StatusRecord.RequestId);
                }

                if (source.Status == ScheduleVisitStatus.ConsumerEnrollmentRunning)
                {
                    // Retrieve previously started enrollment 
                }

                // if enrollment has not started, do another one
                await ChangeStatus(source, ScheduleVisitStatus.ConsumerEnrollmentRunning);

                // TODO: Enroll consumer here

                return ScheduleVisitStatus.ScheduleVisitPending;
            }

            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) 
            {
                throw CreateTransitionError("Consumer enrollment failed.", ScheduleVisitStatus.ConsumerEnrollnmentFailed, ex);
            }
        }

        async Task<ScheduleVisitStatus> ScheduleVisitForEnrolledConsumer(ScheduleVisitActor source)
        {
            try
            {
                CancellationToken.ThrowIfCancellationRequested();

                if (source.Data == null)
                {
                    source.Data = await ScheduleService.GetScheduleVisitRequest(source.StatusRecord.RequestId);
                }

                await ChangeStatus(source, ScheduleVisitStatus.ScheduleVisitRunning);

                // TODO: schedule visit here

                return ScheduleVisitStatus.ScheduleVisitCompleted;
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CreateTransitionError("Schedule visit failed.", ScheduleVisitStatus.ScheduleVisitFailed, ex);
            }
        }

    }

}
