using Virgil.Core;
namespace Virgil.Services;
public sealed class MonitoringService : IMonitoringService {
  private System.Timers.Timer? _t;
  public event EventHandler<SystemStats>? Updated;
  public bool IsRunning => _t is not null;
  public void Start(){ if(_t!=null) return; _t = new System.Timers.Timer(1500); _t.Elapsed += (s,e)=> Updated?.Invoke(this, Fake()); _t.Start(); }
  public void Stop(){ _t?.Stop(); _t = null; }
  private static SystemStats Fake(){ var r = new Random(); double f()=> r.Next(2,75); return new SystemStats(f(),f(),f(),f(), r.Next(30,75), r.Next(30,75), r.Next(28,55)); }
}
