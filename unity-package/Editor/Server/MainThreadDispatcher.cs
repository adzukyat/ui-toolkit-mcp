using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEditor;

namespace UIToolkitMcpPreviewServer.Server
{
    internal static class MainThreadDispatcher
    {
        private sealed class WorkItem
        {
            internal Func<object> execute;
            internal TaskCompletionSource<object> completion;
        }

        private static readonly ConcurrentQueue<WorkItem> Queue = new ConcurrentQueue<WorkItem>();
        private static bool _attached;

        internal static void Attach()
        {
            if (_attached)
                return;
            _attached = true;
            EditorApplication.update += Drain;
        }

        internal static void Detach()
        {
            if (!_attached)
                return;
            _attached = false;
            EditorApplication.update -= Drain;
            while (Queue.TryDequeue(out var item))
                item.completion.TrySetException(new InvalidOperationException("Unity domain is reloading."));
        }

        internal static Task<object> Enqueue(Func<object> execute)
        {
            var completion = new TaskCompletionSource<object>();
            Queue.Enqueue(new WorkItem { execute = execute, completion = completion });
            return completion.Task;
        }

        private static void Drain()
        {
            var budget = 8;
            while (budget-- > 0 && Queue.TryDequeue(out var item))
            {
                try
                {
                    item.completion.TrySetResult(item.execute());
                }
                catch (Exception exception)
                {
                    item.completion.TrySetException(exception);
                }
            }
        }
    }
}

