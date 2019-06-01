using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace StateMachines
{
    public interface IScheduleService
    {
        // Get requests for a particular type
        Task<IList<RequestStatusRecord>> GetPendingRequests(RequestType type, int maxCount);
        Task<ScheduleVisitRequest> GetScheduleVistRequest(long requestId);
        Task ChangeStatus(RequestStatusRecord record, RequestStatus newStatus, string message);
    }
}
