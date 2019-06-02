using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace StateMachines
{
    public interface IScheduleService
    {
        // Get requests for a particular type with non-termnal status
        Task<IList<RequestStatusRecord>> GetPendingRequests(RequestType type, int maxCount);
        Task ChangeStatus(RequestStatusRecord record, int newStatusId, string message);
        Task<ScheduleVisitRequest> GetScheduleVisitRequest(long requestId);

    }
}
