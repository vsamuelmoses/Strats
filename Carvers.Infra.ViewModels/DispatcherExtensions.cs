using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Carvers.Infra.ViewModels
{
    public static class DispatcherExtensions
    {
        public static TaskScheduler UiTaskScheduler()
        {
            return SynchronizationContext.Current == null
                ? Application.Current.Dispatcher.ToTaskScheduler()
                : TaskScheduler.FromCurrentSynchronizationContext();
        }

        private static TaskScheduler ToTaskScheduler(
            this Dispatcher dispatcher,
            DispatcherPriority priority = DispatcherPriority.Normal)
        {
            var taskCompletionSource = new TaskCompletionSource<TaskScheduler>();
            var invocation = dispatcher.BeginInvoke(new Action(() =>
                taskCompletionSource.SetResult(TaskScheduler.FromCurrentSynchronizationContext())), priority);
            invocation.Aborted += (s, e) => taskCompletionSource.SetCanceled();
            return taskCompletionSource.Task.Result;
        }
    }
}
