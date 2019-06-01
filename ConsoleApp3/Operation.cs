using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ConsoleApp3
{

    public class ScheduledVisitActor 
    {
        public long RequestId { get; set; }
        public ScheduleVisitRequestStatus Status { get; set; }

        public bool IsPending { get { return Status < ScheduleVisitRequestStatus.ScheduleVisitCompleted; } }

        public object Data { get; set; }
    }


    public enum ScheduleVisitRequestStatus
    {
        Default = 0,
        Pending = 1,
        ConsumerEnrollmentPending = 2,
        ConsumerEnrollmentRunning = 3,
        ScheduleVisitPending = 4,
        ScheduleVisitRunning = 5,
        ScheduleVisitCompleted = 6,
        ConsumerEnrollnmentFailed = 1001,
        ScheduleVisitFailed = 1002
    }

    public class ScheduleVisitQueueProcessor
    {

        public ScheduleVisitQueueProcessor()
        {

        }

        ActionBlock<ScheduledVisitActor> _dispatcher;
        ActionBlock<Tuple<ScheduledVisitActor, Transtion>> _processor;
        SemaphoreSlim _countdown;

        void DispatchActor(ScheduledVisitActor actor)
        {
            switch(actor.Status)
            {
                case ScheduleVisitRequestStatus.Pending:
                case ScheduleVisitRequestStatus.ConsumerEnrollmentPending:
                case ScheduleVisitRequestStatus.ConsumerEnrollmentRunning:
                    _processor.Post(Tuple.Create(actor, new Transtion(EnrollConsumer)));
                    break;

                case ScheduleVisitRequestStatus.ScheduleVisitPending:
                case ScheduleVisitRequestStatus.ScheduleVisitRunning:
                    _processor.Post(Tuple.Create(actor, new Transtion(ScheduleVisit)));
                    break;

                default:
                    _countdown.Release();
                    break;
            }
        }

        async Task ProcessTranstion(ScheduledVisitActor actor, Transtion transition)
        {
            try
            {
                var newStatus = await transition(actor);
                if (newStatus != ScheduleVisitRequestStatus.Default)
                {
                    await UpdateStatusInDatabase(actor, newStatus);
                }

            }
            catch(Exception ex)
            {
                var newStatus = ScheduleVisitRequestStatus.ScheduleVisitFailed;
                await UpdateStatusInDatabase(actor, newStatus, ex);
            }


            _dispatcher.Post(actor);
        }

        Task UpdateStatusInDatabase(ScheduledVisitActor actor, ScheduleVisitRequestStatus newStatus, Exception ex = null)
        {
            return Task.CompletedTask;
        }

        async Task ProcessQueue(IList<Tuple<long, ScheduleVisitRequestStatus>> items, CancellationTokenSource cts)
        {
            using (_countdown = new SemaphoreSlim(items.Count))
            {

                foreach (var item in items)
                {
                    var actor = new ScheduledVisitActor { RequestId = item.Item1, Status = item.Item2 };
                    _dispatcher.Post(actor);
                }

                await _countdown.WaitAsync(cts.Token);
            }
        }

        delegate Task<ScheduleVisitRequestStatus> Transtion(ScheduledVisitActor source);

        Task LoadData(ScheduledVisitActor actor)
        {
            return Task.CompletedTask;
        }

        async Task<ScheduleVisitRequestStatus> EnrollConsumer(ScheduledVisitActor source)
        {
            try
            {
                await LoadData(source);

                return ScheduleVisitRequestStatus.ScheduleVisitPending;
            }
            catch (Exception ex)
            {
                return ScheduleVisitRequestStatus.ConsumerEnrollnmentFailed;
            }
        }

        async Task<ScheduleVisitRequestStatus> ScheduleVisit(ScheduledVisitActor source)
        {
            try
            {
                await LoadData(source);
                return ScheduleVisitRequestStatus.ScheduleVisitPending;
            }
            catch (Exception ex)
            {
                return ScheduleVisitRequestStatus.ScheduleVisitFailed;
            }
        }

    }





}
