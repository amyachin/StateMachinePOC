using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace StateMachines.UnitTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public async Task TestScheduleVisitCompletion()
        {
            var service = ScheduleServiceMockup.CreateBasic();
            LoggerFactory factory = new LoggerFactory();


            Assert.IsTrue(service.PendingRequests.All(it => it.StatusId != (int)ScheduleVisitStatus.ScheduleVisitCompleted));

            ScheduleVisitStateMachine stateMachine = new ScheduleVisitStateMachine(factory, service);
            await stateMachine.ExecuteAsync(CancellationToken.None);

            Assert.IsTrue(service.PendingRequests.All(it => it.StatusId == (int)ScheduleVisitStatus.ScheduleVisitCompleted));
        }


        [TestMethod]
        public async Task TestCompletonOfEmpty()
        {
            var service = new ScheduleServiceMockup();
            LoggerFactory factory = new LoggerFactory();
            ScheduleVisitStateMachine stateMachine = new ScheduleVisitStateMachine(factory, service);
            await stateMachine.ExecuteAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task TestCompletonOfSingleItem()
        {
            var service = ScheduleServiceMockup.CreateBasic();
            service.PendingRequests.RemoveAll(it => it.RequestId != 1);

            LoggerFactory factory = new LoggerFactory();
            ScheduleVisitStateMachine stateMachine = new ScheduleVisitStateMachine(factory, service);
            await stateMachine.ExecuteAsync(CancellationToken.None);

            Assert.IsTrue(service.PendingRequests.All(it => it.StatusId == (int)ScheduleVisitStatus.ScheduleVisitCompleted));
        }
    }
}
