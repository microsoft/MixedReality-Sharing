using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network
{
    /// <summary>
    /// Consumes messages from a <see cref="IMessageQueue"/> and fires an event for every message.
    /// </summary>
    /// <remarks>
    /// Each instance does the consuming and dispatching on its own thread.
    ///
    /// Only one of these should be active for a specific queue at any time. If you want multiple
    /// handlers to subscribe to a queue, create only one `MessageQueueConsumer` and add the
    /// handlers to its <see cref="MessageReceived"/> event.
    /// </remarks>
    public class MessageQueueConsumer : IDisposable
    {
        private CancellationTokenSource cts_ = new CancellationTokenSource();
        private Task consumeTask_;

        /// <summary>
        /// Fires when a message in the queue is consumed.
        /// </summary>
        /// <remarks>
        /// Event handlers for this consumer are all called by the same thread, so execution of a handler will block
        /// all the following handlers. If a handler needs to run lengthy processing, consider offloading the
        /// processing to a <see cref="Task"/>, or consuming the queue manually and doing your own scheduling instead.
        /// </remarks>
        event Action<IMessage> MessageReceived;

        public MessageQueueConsumer(IMessageQueue queue)
        {
            var token = cts_.Token;
            consumeTask_ = Task.Run(() =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var messages = queue.DequeueAll(token);

                        foreach (var message in messages)
                        {
                            if (token.IsCancellationRequested)
                            {
                                return;
                            }
                            MessageReceived(message);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            });
        }

        /// <summary>
        /// Stop the consumer. <see cref="MessageReceived"/> might still be called by the consuming
        /// thread until the end of this method. After this, it is safe to create a new `MessageQueueConsumer` for
        /// the same category.
        /// </summary>
        public void Dispose()
        {
            cts_.Cancel();
            cts_.Dispose();
            consumeTask_.Wait();
        }
    }
}
