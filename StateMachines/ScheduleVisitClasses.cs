using System;
using System.Collections.Generic;
using System.Text;

namespace StateMachines
{


    // Merges all statuses for all request types
    public enum RequestStatus
    {
        NewVisit = 0,
        ConsumerEnrollmentPending = 1,
        ConsumerEnrollmentRunning = 2,
        ScheduleVisitPending = 3,
        ScheduleVisitRunning = 4,
        ScheduleVisitCompleted = 5,

        // Failures
        ConsumerEnrollnmentFailed = 1001,
        ScheduleVisitFailed = 1002
    }

    public enum RequestType
    {
        ScheduleVisit,
        WrapUp
    }

    // Full information about ScheduleVisit request
    public class ScheduleVisitRequest
    {
        public long RequestId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
    }

    // Concise information about request (type and the current status)
    public class RequestStatusRecord
    {
        public long RequestId;

        public RequestType RequestType;
        public RequestStatus Status { get; set; }
    }
}
