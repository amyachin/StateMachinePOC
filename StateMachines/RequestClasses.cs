namespace StateMachines
{
    public enum RequestType
    {
        ScheduleVisit,
        ScheduleWrapUp
    }

    // Status-related information about request (id, type and the current status)
    public class RequestStatusRecord
    {
        public long RequestId;
        public int StatusId { get; set; }

        public RequestType RequestType;
    }

}