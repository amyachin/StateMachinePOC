using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace StateMachines.UnitTests
{
    class ScheduleServiceMockup : IScheduleVisitService
    {

        public static ScheduleServiceMockup CreateBasic()
        {
            var mockup = new ScheduleServiceMockup();
            mockup.PendingRequests.Add(new QueueItem { Id = 1, StatusId = (int)ScheduleVisitStatus.ConsumerEnrollmentPending });
            mockup.PendingRequests.Add(new QueueItem { Id = 2, StatusId = (int)ScheduleVisitStatus.ConsumerEnrollmentRunning });
            mockup.PendingRequests.Add(new QueueItem { Id = 3, StatusId = (int)ScheduleVisitStatus.ScheduleVisitPending });
            mockup.PendingRequests.Add(new QueueItem { Id = 4, StatusId = (int)ScheduleVisitStatus.ScheduleVisitRunning});



            mockup.ScheduleVisitRequests.Add(new ScheduleVisitRequest { RequestId = 1, Email = "myemail1@noreply", FirstName = "John", LastName = "Smith" });
            mockup.ScheduleVisitRequests.Add(new ScheduleVisitRequest { RequestId = 2, Email = "myemail2@noreply", FirstName = "Steve", LastName = "Jobs" });
            mockup.ScheduleVisitRequests.Add(new ScheduleVisitRequest { RequestId = 3, Email = "myemail3@noreply", FirstName = "Adam", LastName = "Smith" });
            mockup.ScheduleVisitRequests.Add(new ScheduleVisitRequest { RequestId = 4, Email = "myemail4@noreply", FirstName = "Harry", LastName = "Potter" });

            return mockup;
        }


        public ScheduleServiceMockup()
        {
            PendingRequests = new List<QueueItem>();
            ScheduleVisitRequests = new List<ScheduleVisitRequest>();
        }

        public List<QueueItem> PendingRequests { get; }

        public List<ScheduleVisitRequest> ScheduleVisitRequests { get; }

        public Task ChangeStatus(QueueItem item, object newStatusId, string message)
        {
            return Task.CompletedTask;
        }

        public Task<IList<QueueItem>> GetPendingItems(int maxCount, CancellationToken cancellationToken)
        {
            var result = PendingRequests
                .Take(maxCount)
                .ToList();

            return Task.FromResult<IList<QueueItem>>(result);
        }

        public Task<ScheduleVisitRequest> GetScheduleVisitRequest(long requestId)
        {
            var result = ScheduleVisitRequests.FirstOrDefault(it => it.RequestId == requestId);
            return Task.FromResult(result);
        }
    }
}
