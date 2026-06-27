using Moq;
using NUnit.Framework;
using SyncAgent.Models;
using SyncAgent.Tasks;

namespace SyncAgent.Tests.Tasks
{
    [TestFixture]
    public class TaskDispatcherTests
    {
        [Test]
        public void Unknown_task_type_returns_failed_result_without_throwing()
        {
            var dispatcher = new TaskDispatcher(new ITaskHandler[0]);
            var task = new SyncTask { TaskId = "t1", TaskType = "Nope" };

            var result = dispatcher.Dispatch(task);

            Assert.That(result.Status, Is.EqualTo("failed"));
            Assert.That(result.ErrorMessage, Does.Contain("Nope"));
            Assert.That(result.TaskId, Is.EqualTo("t1"));
        }

        [Test]
        public void Dispatches_to_matching_handler()
        {
            var handler = new Mock<ITaskHandler>();
            handler.Setup(h => h.CanHandle(TaskTypes.GetCustomers)).Returns(true);
            handler.Setup(h => h.Execute(It.IsAny<SyncTask>()))
                   .Returns(SyncResult.Completed("t1", TaskTypes.GetCustomers, new object[0]));
            var dispatcher = new TaskDispatcher(new[] { handler.Object });
            var task = new SyncTask { TaskId = "t1", TaskType = TaskTypes.GetCustomers };

            var result = dispatcher.Dispatch(task);

            Assert.That(result.Status, Is.EqualTo("completed"));
            handler.Verify(h => h.Execute(It.IsAny<SyncTask>()), Times.Once);
        }
    }
}
