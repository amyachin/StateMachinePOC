using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StateMachines
{
    public interface IScheduleService
    {
        Task<IList<QueueItem>> GetPendingItems(int maxCount, CancellationToken cancellationToken);

        Task ChangeStatus(QueueItem item, object newStatusId, string message);
    }


}
