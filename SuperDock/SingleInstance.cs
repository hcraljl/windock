using System.Security.Principal;

namespace SuperDock;
internal sealed class SingleInstance : IDisposable
{
    readonly Mutex mutex;
    readonly EventWaitHandle wake;
    RegisteredWaitHandle? listener;
    bool acquired;
    public SingleInstance(string? scope=null)
    {
        var name=@"Local\windock."+(scope??WindowsIdentity.GetCurrent().User!.Value);
        mutex=new Mutex(false,name+".instance");wake=new EventWaitHandle(false,EventResetMode.AutoReset,name+".wake");
    }
    public bool Acquire()
    {
        try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
        if(!acquired)wake.Set();return acquired;
    }
    public void Listen(Action reveal)=>listener=ThreadPool.RegisterWaitForSingleObject(wake,(_,_)=>reveal(),null,Timeout.Infinite,false);
    public void Dispose(){listener?.Unregister(null);wake.Dispose();if(acquired){mutex.ReleaseMutex();acquired=false;}mutex.Dispose();}
}
