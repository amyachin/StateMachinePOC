using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace StateMachines
{
  
    public enum ScheduleVisitStatus
    {
        Draft = 0,
        ConsumerEnrollmentPending = 1,
        ConsumerEnrollmentRunning = 2,
        ScheduleVisitPending = 3,
        ScheduleVisitRunning = 4,
        ScheduleVisitCompleted = 5,

        // Failures
        ConsumerEnrollnmentFailed = 1001,
        ScheduleVisitFailed = 1002
    }


    public static class ScheduleVisitStatusExtensions
    {
        public static int GetId(this ScheduleVisitStatus status)
        {
            return (int)status;
        }
    }

    // Full information about ScheduleVisit request
    public class ScheduleVisitRequest
    {
        public long RequestId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
    }

    public interface IScheduleVisitService 
    {
        Task ChangeStatus(long requestId, int currentStatusId, int newStatusId, string message);

        Task<ScheduleVisitRequest> GetScheduleVisitRequest(long requestId);
    }


}
