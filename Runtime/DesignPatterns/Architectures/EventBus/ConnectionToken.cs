using System;

namespace TnieYuPackage.DesignPatterns
{
    public readonly struct ConnectionToken<TEvent> : IDisposable
        where TEvent : IEventData
    {
        private readonly IEventSubscriber<TEvent> subscriber;

        public ConnectionToken(IEventSubscriber<TEvent> subscriber)
        {
            this.subscriber = subscriber;
        }

        public void Dispose()
        {
            GlobalEventBus.UnregisterHandler(subscriber);
        }
    }
}