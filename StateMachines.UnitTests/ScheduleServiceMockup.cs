using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace StateMachines.UnitTests
{
    class ScheduleServiceMockup : IScheduleService
    {

        public static ScheduleServiceMockup CreateBasic()
        {
            var mockup = new ScheduleServiceMockup();
            mockup.PendingRequests.Add(new RequestStatusRecord { RequestId = 1, RequestType = RequestType.ScheduleVisit, StatusId = (int)ScheduleVisitStatus.ConsumerEnrollmentPending });
            mockup.PendingRequests.Add(new RequestStatusRecord { RequestId = 2, RequestType = RequestType.ScheduleVisit, StatusId = (int)ScheduleVisitStatus.ConsumerEnrollmentRunning });
            mockup.PendingRequests.Add(new RequestStatusRecord { RequestId = 3, RequestType = RequestType.ScheduleVisit, StatusId = (int)ScheduleVisitStatus.ScheduleVisitPending });
            mockup.PendingRequests.Add(new RequestStatusRecord { RequestId = 4, RequestType = RequestType.ScheduleVisit, StatusId = (int)ScheduleVisitStatus.ScheduleVisitRunning});



            mockup.ScheduleVisitRequests.Add(new ScheduleVisitRequest { RequestId = 1, Email = "myemail1@noreply", FirstName = "John", LastName = "Smith" });
            mockup.ScheduleVisitRequests.Add(new ScheduleVisitRequest { RequestId = 2, Email = "myemail2@noreply", FirstName = "Steve", LastName = "Jobs" });
            mockup.ScheduleVisitRequests.Add(new ScheduleVisitRequest { RequestId = 3, Email = "myemail3@noreply", FirstName = "Adam", LastName = "Smith" });
            mockup.ScheduleVisitRequests.Add(new ScheduleVisitRequest { RequestId = 4, Email = "myemail4@noreply", FirstName = "Harry", LastName = "Potter" });

            return mockup;
        }


        public ScheduleServiceMockup()
        {
            PendingRequests = new List<RequestStatusRecord>();
            ScheduleVisitRequests = new List<ScheduleVisitRequest>();
        }

        public List<RequestStatusRecord> PendingRequests { get; }

        public List<ScheduleVisitRequest> ScheduleVisitRequests { get; }

        public Task ChangeStatus(RequestStatusRecord record, int newStatusId, string message)
        {
            return Task.CompletedTask;
        }

        public Task<IList<RequestStatusRecord>> GetPendingRequests(RequestType type, int maxCount)
        {
            var result = PendingRequests
                .Where(it => it.RequestType == type)
                .Take(maxCount)
                .ToList();

            return Task.FromResult<IList<RequestStatusRecord>>(result);
        }

        public Task<ScheduleVisitRequest> GetScheduleVisitRequest(long requestId)
        {
            var result = ScheduleVisitRequests.FirstOrDefault(it => it.RequestId == requestId);
            return Task.FromResult(result);
        }
    }
}
