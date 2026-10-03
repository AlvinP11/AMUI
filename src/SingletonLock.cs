using System;
using System.Threading;

namespace AMUI
{
    // Roblox makes sure only one client runs by creating a mutex called
    // ROBLOX_singletonMutex and an event called ROBLOX_singletonEvent. A new
    // client that finds them fires the event, which tells the old client to
    // close. AMUI takes both names first. With a mutex sitting on the event's
    // name, Roblox can't create the event, so no client can close another.
    sealed class SingletonLock : IDisposable
    {
        const string MutexName = "ROBLOX_singletonMutex";
        const string EventName = "ROBLOX_singletonEvent";

        Mutex _mutex;
        Mutex _eventBlocker;
        bool _ownsMutex;
        bool _ownsEventBlocker;

        public bool IsActive { get { return _eventBlocker != null; } }
        public string LastError { get; private set; }

        // Only works while no Roblox client is open, since an open client
        // already owns the names.
        public bool TryAcquire()
        {
            if (IsActive)
                return true;
            try
            {
                _mutex = new Mutex(true, MutexName, out _ownsMutex);
                _eventBlocker = new Mutex(true, EventName, out _ownsEventBlocker);
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Release();
                return false;
            }
        }

        public void Release()
        {
            Close(ref _eventBlocker, _ownsEventBlocker);
            Close(ref _mutex, _ownsMutex);
        }

        public void Dispose()
        {
            Release();
        }

        static void Close(ref Mutex mutex, bool owned)
        {
            if (mutex == null)
                return;
            if (owned)
            {
                try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
            }
            mutex.Dispose();
            mutex = null;
        }
    }
}
