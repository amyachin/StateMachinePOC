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
        public ScheduleVisitActor() 
        {
        }
        public long RequestId { get; set; }
        public ScheduleVisitRequest Data { get; set; }

        public override string ToString()
        {
            return string.Format("RequestId: {0}, Status: {1}", RequestId, Status);
        }

        // TODO: Enrollment-specific data 
    }

    
    public class ScheduleVisitStateMachine : StateMachine<ScheduleVisitActor, ScheduleVisitStatus>
    {
        public ScheduleVisitStateMachine(ILoggerFactory loggerFactory, IScheduleVisitService service) : base(loggerFactory, ScheduleVisitStatus.ScheduleVisitFailed)
        {
            ScheduleService = service;
        }


        private IScheduleVisitService ScheduleService { get; }

        protected override StateTransition<ScheduleVisitActor, ScheduleVisitStatus> GetNextTransition(ScheduleVisitActor actor)
        {
            switch (actor.Status)
            {
                case ScheduleVisitStatus.ConsumerEnrollmentPending:
                case ScheduleVisitStatus.ConsumerEnrollmentRunning:
                    return CreateTransition(actor, EnrollConsumer, ScheduleVisitStatus.ConsumerEnrollnmentFailed);

                case ScheduleVisitStatus.ScheduleVisitPending:
                case ScheduleVisitStatus.ScheduleVisitRunning:
                    return CreateTransition(actor, ScheduleVisitForEnrolledConsumer, ScheduleVisitStatus.ScheduleVisitFailed);

                default:
                    return Done();
            }
        }

        protected override async Task OnStatusChanging(StateMachineStatusChangingArgs<ScheduleVisitActor, ScheduleVisitStatus> e)
        {
            await ScheduleService.ChangeStatus(e.Actor.RequestId, e.CurrentStatus.GetId(), e.NewStatus.GetId(), e.Message);
        }

        async Task<ScheduleVisitStatus> EnrollConsumer(ScheduleVisitActor source)
        {
            if (source.Data == null)
            {
                source.Data = await ScheduleService.GetScheduleVisitRequest(source.RequestId);
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

        async Task<ScheduleVisitStatus> ScheduleVisitForEnrolledConsumer(ScheduleVisitActor source)
        {
            if (source.Data == null)
            {
                source.Data = await ScheduleService.GetScheduleVisitRequest(source.RequestId);
            }

            await ChangeStatus(source, ScheduleVisitStatus.ScheduleVisitRunning);
            
            // TODO: schedule visit here
            return ScheduleVisitStatus.ScheduleVisitCompleted;
        }

    }

}
