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


            Assert.IsTrue(service.PendingRequests.All(it => !it.StatusId.Equals((int)ScheduleVisitStatus.ScheduleVisitCompleted)));

            ScheduleVisitStateMachine stateMachine = new ScheduleVisitStateMachine(factory, service);
            await stateMachine.ExecuteAsync(service.PendingRequests.Select(it=> Helper.CreateScheduleVisitActor(it)), CancellationToken.None);
            Assert.IsTrue(service.PendingRequests.All(it => it.StatusId.Equals((int)ScheduleVisitStatus.ScheduleVisitCompleted)));
        }


        [TestMethod]
        public async Task TestCompletonOfEmpty()
        {
            var service = new ScheduleServiceMockup();
            LoggerFactory factory = new LoggerFactory();
            ScheduleVisitStateMachine stateMachine = new ScheduleVisitStateMachine(factory, service);
            await stateMachine.ExecuteAsync(new ScheduleVisitActor[0], CancellationToken.None);
        }

        [TestMethod]
        public async Task TestCompletonOfSingleItem()
        {
            var service = ScheduleServiceMockup.CreateBasic();

            LoggerFactory factory = new LoggerFactory();
            ScheduleVisitStateMachine stateMachine = new ScheduleVisitStateMachine(factory, service);
            var item = service.PendingRequests.First(it => it.Id == 1);
            await stateMachine.ExecuteAsync(new[] { Helper.CreateScheduleVisitActor(item) }, CancellationToken.None);
            Assert.IsTrue(item.StatusId.Equals((int)ScheduleVisitStatus.ScheduleVisitCompleted));
        }
    }
}
