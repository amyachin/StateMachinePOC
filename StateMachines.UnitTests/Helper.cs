using System;
using System.Collections.Generic;
using System.Text;

namespace StateMachines.UnitTests
{
    static class Helper
    {
        internal static ScheduleVisitActor CreateScheduleVisitActor(QueueItem item)
        {
            return new ScheduleVisitActor { RequestId = item.Id, Status = (ScheduleVisitStatus)item.StatusId, StatusId = item.StatusId };
        }
    }
}
