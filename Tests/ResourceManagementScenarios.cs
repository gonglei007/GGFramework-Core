using System;
using System.Collections.Generic;

namespace GGFramework.Core.Tests
{
    // Shared by NUnit and the SDK-only runner. Every case owns fresh cache/queue
    // state; no Unity objects, files, network access or global state are required.
    public static class ResourceManagementScenarios
    {
        static void Need(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        static void Throws<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }

        public static Dictionary<string, Action> Cases()
        {
            return new Dictionary<string, Action>
            {
                ["active resources may exceed budget; ending scope evicts oldest idle value"] = () =>
                {
                    var released = new List<int>();
                    var c = new BudgetedResourceCache<string,int>(10, released.Add);
                    c.Add("a", 1, 6); c.Add("b", 2, 6); c.TrimToBudget();
                    Need(c.Count == 2 && c.TotalBytes == 12 && c.PinnedBytes == 12, "Active scope evicted");
                    c.UnpinAll(); c.TrimToBudget();
                    Need(c.Count == 1 && c.TotalBytes == 6 && released.Count == 1 && released[0] == 1, "Idle LRU wrong");
                },
                ["lookup updates recency across scopes"] = () =>
                {
                    var released = new List<int>(); var c = new BudgetedResourceCache<string,int>(6, released.Add);
                    c.Add("a",1,6); c.Add("b",2,6); int value; c.TryGet("a",out value);
                    c.UnpinAll(); c.TrimToBudget();
                    Need(released.Count == 1 && released[0] == 2 && c.TryGet("a",out value) && value == 1, "Touch order lost");
                },
                ["repeated access pins only once"] = () =>
                {
                    var c = new BudgetedResourceCache<string,int>(0, _ => { }); c.Add("a",1,6); c.UnpinAll();
                    for(int i=0;i<128;i++){int value;Need(c.TryGet("a",out value),"Missing");}
                    c.Trim(0); Need(c.PinnedBytes == 6 && c.TotalBytes == 6 && c.Count == 1,"Double pin accounting");
                },
                ["trim unused releases idle values but protects current use"] = () =>
                {
                    var released = new List<int>();var c=new BudgetedResourceCache<string,int>(100,released.Add);
                    c.Add("a",1,5);c.Add("b",2,7);c.UnpinAll();int value;c.TryGet("b",out value);c.Trim(0);
                    Need(c.Count==1&&c.TotalBytes==7&&c.PinnedBytes==7&&released[0]==1,"Low-memory protection changed");
                },
                ["budget changes evict idle data immediately"] = () =>
                {
                    var c=new BudgetedResourceCache<string,int>(100,_=>{});c.Add("a",1,5);c.UnpinAll();c.BudgetBytes=-1;
                    Need(c.BudgetBytes==0&&c.Count==0&&c.TotalBytes==0,"Budget was not clamped and applied");c.BudgetBytes=100;
                    Need(c.Evictions==1,"Increasing budget evicted again");
                },
                ["duplicate keys leave existing ownership intact"] = () =>
                {
                    int freed=0;var c=new BudgetedResourceCache<string,int>(10,_=>freed++);c.Add("a",1,5);
                    Throws<ArgumentException>(()=>c.Add("a",2,7));int value;
                    Need(c.Count==1&&c.TotalBytes==5&&c.TryGet("a",out value)&&value==1&&freed==0,"Duplicate changed owner");
                },
                ["negative weights reject before transfer"] = () =>
                {
                    var c=new BudgetedResourceCache<string,int>(10,_=>{});Throws<ArgumentOutOfRangeException>(()=>c.Add("a",1,-1));
                    Need(c.Count==0&&c.TotalBytes==0&&c.PinnedBytes==0,"Invalid weight mutated cache");
                },
                ["weight overflow rejects before transfer"] = () =>
                {
                    var c=new BudgetedResourceCache<string,int>(0,_=>{});c.Add("a",1,long.MaxValue);
                    Throws<OverflowException>(()=>c.Add("b",2,1));Need(c.Count==1&&c.TotalBytes==long.MaxValue,"Overflow mutated cache");
                },
                ["zero-weight resources still release on explicit clear"] = () =>
                {
                    int freed=0;var c=new BudgetedResourceCache<string,int>(0,_=>freed++);c.Add("a",1,0);c.UnpinAll();c.Trim(0);
                    c.Clear();Need(c.Count==0&&freed==1&&c.PinnedBytes==0,"Zero-weight owner leaked");
                },
                ["all release failures are reported after accounting and other releases"] = () =>
                {
                    int attempts=0;var c=new BudgetedResourceCache<string,int>(0,_=>{attempts++;throw new Exception("release");});
                    c.Add("a",1,3);c.Add("b",2,4);c.UnpinAll();
                    try{c.Trim(0);throw new InvalidOperationException("Missing failure");}catch(AggregateException e){Need(e.InnerExceptions.Count==2,"Release failures lost");}
                    Need(attempts==2&&c.Count==0&&c.TotalBytes==0&&c.PinnedBytes==0,"Failed cleanup retained ownership");
                },
                ["release callback cannot reenter cache mutation"] = () =>
                {
                    BudgetedResourceCache<string,int> c=null;c=new BudgetedResourceCache<string,int>(0,_=>c.Add("b",2,1));
                    c.Add("a",1,1);c.UnpinAll();Throws<AggregateException>(()=>c.Trim(0));
                    Need(c.Count==0&&c.TotalBytes==0,"Release reentered cache");c.Add("ok",3,1);Need(c.Count==1,"Guard remained stuck");
                },
                ["clear releases protected entries exactly once"] = () =>
                {
                    int freed=0;var c=new BudgetedResourceCache<string,int>(100,_=>freed++);c.Add("a",1,2);c.Add("b",2,3);
                    c.Clear();c.Clear();Need(freed==2&&c.Count==0&&c.TotalBytes==0&&c.PinnedBytes==0,"Hard teardown wrong");
                },
                ["cache comparer preserves ordinal resource path identity"] = () =>
                {
                    var c=new BudgetedResourceCache<string,int>(100,_=>{},StringComparer.Ordinal);c.Add("A",1,1);c.Add("a",2,1);
                    int v;Need(c.Count==2&&c.TryGet("A",out v)&&v==1&&c.TryGet("a",out v)&&v==2,"Paths merged");
                    var folded=new BudgetedResourceCache<string,int>(100,_=>{},StringComparer.OrdinalIgnoreCase);folded.Add("A",1,1);
                    Throws<ArgumentException>(()=>folded.Add("a",2,1));
                },
                ["cache miss neither pins nor changes ownership"] = () =>
                {
                    var c=new BudgetedResourceCache<string,int>(100,_=>{});c.Add("a",1,1);c.UnpinAll();int v;
                    Need(!c.TryGet("missing",out v)&&v==0&&c.PinnedBytes==0,"Miss changed ownership");c.Trim(0);Need(c.Count==0,"Miss blocked eviction");
                },
                ["queued and active requests share one lease and factory"] = () =>
                {
                    int creates=0;var q=new ResourceRequestQueue<string,object>(1);Func<object> make=()=>{creates++;return new object();};
                    var a=q.GetOrCreate("a",make);Need(ReferenceEquals(a,q.GetOrCreate("a",make)),"Queued duplicate");q.Pump(_=>{});
                    Need(ReferenceEquals(a,q.GetOrCreate("a",make))&&creates==1&&q.ActiveCount==1&&q.PendingCount==1,"Active duplicate");
                },
                ["queue schedules FIFO within its concurrency limit"] = () =>
                {
                    var q=new ResourceRequestQueue<string,object>(2);var started=new List<string>();var leases=new List<ResourceRequestQueue<string,object>.Lease>();
                    foreach(var id in new[]{"a","b","c","d"})leases.Add(q.GetOrCreate(id,()=>new object()));
                    q.Pump(x=>started.Add(x.Key));Need(string.Join("",started)=="ab"&&q.ActiveCount==2&&q.PendingCount==4,"Initial admission");
                    q.Complete(leases[0]);q.Pump(x=>started.Add(x.Key));
                    Need(string.Join("",started)=="abc"&&q.ActiveCount==2&&q.PendingCount==3&&q.PeakActiveCount==2,"FIFO/admission changed");
                },
                ["scope cancellation invalidates queued and running work before replacement"] = () =>
                {
                    var q=new ResourceRequestQueue<string,object>(1);var old=q.GetOrCreate("a",()=>new object());q.GetOrCreate("b",()=>new object());q.Pump(_=>{});
                    var cancelled=q.CancelAll();Need(cancelled.Length==2&&q.CancelledCount==2&&q.PendingCount==0&&q.ActiveCount==0&&!q.IsCurrent(old),"Cancellation incomplete");
                    var next=q.GetOrCreate("a",()=>new object());q.Pump(_=>{});
                    Need(!q.Complete(old)&&q.IsCurrent(next)&&q.ActiveCount==1,"Old callback returned new request");Need(q.Complete(next)&&q.ActiveCount==0,"New completion failed");
                },
                ["queued and duplicate completions cannot decrement active work"] = () =>
                {
                    var q=new ResourceRequestQueue<string,object>(1);var x=q.GetOrCreate("a",()=>new object());
                    Need(!q.Complete(x)&&q.PendingCount==1&&q.ActiveCount==0,"Queued request completed");q.Pump(_=>{});
                    Need(q.Complete(x)&&!q.Complete(x)&&q.ActiveCount==0,"Duplicate completion");
                },
                ["lowering concurrency preserves active requests and stops new admission"] = () =>
                {
                    var q=new ResourceRequestQueue<string,object>(2);var a=q.GetOrCreate("a",()=>new object());var b=q.GetOrCreate("b",()=>new object());q.GetOrCreate("c",()=>new object());
                    q.Pump(_=>{});q.MaxConcurrency=1;q.Complete(a);int starts=0;q.Pump(_=>starts++);
                    Need(starts==0&&q.ActiveCount==1,"Concurrency reduction aborted or overscheduled");q.Complete(b);q.Pump(_=>starts++);
                    Need(starts==1&&q.ActiveCount==1,"Queue did not recover");
                },
                ["raising concurrency admits additional queued work"] = () =>
                {
                    var q=new ResourceRequestQueue<string,object>(1);for(int i=0;i<4;i++)q.GetOrCreate(i.ToString(),()=>new object());
                    q.Pump(_=>{});q.MaxConcurrency=4;q.Pump(_=>{});Need(q.ActiveCount==4&&q.PeakActiveCount==4,"Raised limit ignored");
                },
                ["synchronous completion and pump reentry do not grow recursion"] = () =>
                {
                    var q=new ResourceRequestQueue<int,object>(2);for(int i=0;i<5000;i++)q.GetOrCreate(i,()=>new object());int starts=0;
                    Action<ResourceRequestQueue<int,object>.Lease> start=null;start=x=>{starts++;q.Complete(x);q.Pump(start);};q.Pump(start);
                    Need(starts==5000&&q.ActiveCount==0&&q.PendingCount==0,"Reentrant pump lost work");
                },
                ["start failure releases its admission slot and permits later work"] = () =>
                {
                    var q=new ResourceRequestQueue<string,object>(1);q.GetOrCreate("bad",()=>new object());var good=q.GetOrCreate("good",()=>new object());
                    Throws<InvalidOperationException>(()=>q.Pump(_=>throw new InvalidOperationException("start")));
                    Need(q.ActiveCount==0&&q.PendingCount==1,"Failed start leaked admission");q.Pump(_=>{});Need(q.IsCurrent(good)&&q.ActiveCount==1,"Queue stuck after start failure");
                },
                ["factory failure leaves no queued request and can retry"] = () =>
                {
                    var q=new ResourceRequestQueue<string,object>(1);Throws<InvalidOperationException>(()=>q.GetOrCreate("a",()=>throw new InvalidOperationException("factory")));
                    Need(q.PendingCount==0,"Failed factory registered");q.GetOrCreate("a",()=>new object());q.Pump(_=>{});Need(q.ActiveCount==1,"Factory retry failed");
                },
                ["null factories and invalid concurrency fail without poisoning queue"] = () =>
                {
                    Throws<ArgumentOutOfRangeException>(()=>new ResourceRequestQueue<string,object>(0));var q=new ResourceRequestQueue<string,object>(1);
                    Throws<ArgumentNullException>(()=>q.GetOrCreate("a",null));Throws<InvalidOperationException>(()=>q.GetOrCreate("a",()=>null));Throws<ArgumentOutOfRangeException>(()=>q.MaxConcurrency=0);
                    Need(q.PendingCount==0&&q.MaxConcurrency==1,"Invalid input mutated queue");
                },
                ["request paths use the configured comparer"] = () =>
                {
                    var q=new ResourceRequestQueue<string,object>(2,StringComparer.Ordinal);q.GetOrCreate("A",()=>new object());q.GetOrCreate("a",()=>new object());q.Pump(_=>{});
                    Need(q.PendingCount==2&&q.ActiveCount==2,"Ordinal request paths merged");
                },
                ["cancellation during start cannot complete a same-key replacement"] = () =>
                {
                    var q=new ResourceRequestQueue<string,object>(1);var old=q.GetOrCreate("a",()=>new object());ResourceRequestQueue<string,object>.Lease next=null;
                    q.Pump(x=>{if(ReferenceEquals(x,old)){q.CancelAll();next=q.GetOrCreate("a",()=>new object());q.Pump(_=>{});}});
                    Need(next!=null&&q.IsCurrent(next)&&q.ActiveCount==1&&!q.Complete(old),"Replacement lost its slot");q.Complete(next);Need(q.ActiveCount==0,"Replacement completion wrong");
                },
                ["null pump callback leaves pending work intact"] = () =>
                {
                    var q=new ResourceRequestQueue<string,object>(1);q.GetOrCreate("a",()=>new object());Throws<ArgumentNullException>(()=>q.Pump(null));
                    Need(q.PendingCount==1&&q.ActiveCount==0,"Invalid pump changed admission");
                },
                ["factory cannot recursively register the same key"] = () =>
                {
                    var q = new ResourceRequestQueue<string, object>(1);
                    int nestedCreates = 0;
                    Throws<InvalidOperationException>(() => q.GetOrCreate("a", () =>
                    {
                        q.GetOrCreate("a", () => { nestedCreates++; return new object(); });
                        return new object();
                    }));
                    Need(nestedCreates == 0 && q.PendingCount == 0 && q.ActiveCount == 0,
                        "Reentrant factory registered work or leaked ownership");
                    var next = q.GetOrCreate("a", () => new object());
                    q.Pump(_ => { });
                    Need(q.Complete(next), "Factory guard did not recover after failure");
                },
                ["factory cannot cancel or admit existing work or change its limit"] = () =>
                {
                    for (int operation = 0; operation < 5; operation++)
                    {
                        var q = new ResourceRequestQueue<string, object>(1);
                        var active = q.GetOrCreate("active", () => new object());
                        var waiting = q.GetOrCreate("waiting", () => new object());
                        q.Pump(_ => { });
                        int starts = 0;
                        Throws<InvalidOperationException>(() => q.GetOrCreate("new", () =>
                        {
                            switch (operation)
                            {
                                case 0: q.CancelAll(); break;
                                case 1: q.Complete(active); break;
                                case 2: q.MaxConcurrency = 3; break;
                                case 3: q.Pump(_ => starts++); break;
                                case 4: q.GetOrCreate("other", () => new object()); break;
                            }
                            return new object();
                        }));
                        Need(q.PendingCount == 2 && q.ActiveCount == 1 && q.MaxConcurrency == 1
                            && q.CancelledCount == 0 && starts == 0 && q.IsCurrent(active)
                            && q.IsCurrent(waiting), "Factory mutated pre-existing work");
                        Need(q.Complete(active), "Factory failure poisoned existing lease");
                    }
                },
                ["foreign and null leases cannot consume an active request"] = () =>
                {
                    var left = new ResourceRequestQueue<string, object>(1);
                    var right = new ResourceRequestQueue<string, object>(1);
                    var a = left.GetOrCreate("a", () => new object());
                    var b = right.GetOrCreate("a", () => new object());
                    left.Pump(_ => { }); right.Pump(_ => { });
                    Need(!left.IsCurrent(b) && !left.IsCurrent(null) && !left.Complete(b)
                        && !left.Complete(null) && left.ActiveCount == 1 && right.ActiveCount == 1,
                        "Foreign lease changed admission");
                    Need(left.Complete(a) && right.Complete(b), "Own lease stopped completing");
                },
                ["existing requests do not need another factory"] = () =>
                {
                    var q = new ResourceRequestQueue<string, object>(1, StringComparer.OrdinalIgnoreCase);
                    var first = q.GetOrCreate("Asset", () => new object());
                    Need(ReferenceEquals(first, q.GetOrCreate("ASSET", null)), "Queued lookup invoked factory");
                    q.Pump(_ => { });
                    Need(ReferenceEquals(first, q.GetOrCreate("asset", () => throw new Exception("unused"))),
                        "Active lookup invoked factory");
                    Need(q.Complete(first) && q.CancelAll().Length == 0 && q.CancelledCount == 0,
                        "Idle cancellation changed history");
                },
                ["start failure after cancellation preserves replacement for the next pump"] = () =>
                {
                    var q = new ResourceRequestQueue<string, object>(1);
                    var old = q.GetOrCreate("a", () => new object());
                    ResourceRequestQueue<string, object>.Lease next = null;
                    Throws<InvalidOperationException>(() => q.Pump(_ =>
                    {
                        q.CancelAll();
                        next = q.GetOrCreate("a", () => new object());
                        throw new InvalidOperationException("start failed after replacement");
                    }));
                    Need(q.PendingCount == 1 && q.ActiveCount == 0 && q.IsCurrent(next)
                        && !q.Complete(old), "Failed old start consumed replacement");
                    q.Pump(_ => { });
                    Need(q.Complete(next) && q.ActiveCount == 0, "Pump guard remained stuck");
                },
                ["synchronous completion followed by start failure does not double decrement"] = () =>
                {
                    var q = new ResourceRequestQueue<string, object>(1);
                    var first = q.GetOrCreate("a", () => new object());
                    var second = q.GetOrCreate("b", () => new object());
                    Throws<InvalidOperationException>(() => q.Pump(lease =>
                    {
                        Need(q.Complete(lease), "Synchronous completion failed");
                        throw new InvalidOperationException("after completion");
                    }));
                    Need(!q.IsCurrent(first) && q.IsCurrent(second) && q.ActiveCount == 0
                        && q.PendingCount == 1, "Completion decremented twice");
                    q.Pump(_ => { });
                    Need(q.Complete(second), "Remaining request was lost");
                },
                ["clear attempts every release including pinned values after failures"] = () =>
                {
                    var released = new List<int>();
                    var c = new BudgetedResourceCache<string, int>(100, value =>
                    {
                        released.Add(value);
                        if (value != 2) throw new InvalidOperationException("release " + value);
                    });
                    c.Add("a", 1, 2); c.Add("b", 2, 3, false); c.Add("c", 3, 4);
                    try { c.Clear(); throw new Exception("Missing aggregate"); }
                    catch (AggregateException error) { Need(error.InnerExceptions.Count == 2, "Cleanup errors lost"); }
                    Need(string.Join(",", released) == "1,2,3" && c.Count == 0 && c.TotalBytes == 0
                        && c.PinnedBytes == 0 && c.Evictions == 3, "Hard clear left owners or skipped releases");
                    c.Clear(); Need(released.Count == 3, "Cleanup failure retried detached resources");
                    c.Add("new", 4, 1); Need(c.Count == 1, "Release guard remained stuck");
                },
                ["all release-time cache mutations are rejected before state changes"] = () =>
                {
                    for (int operation = 0; operation < 6; operation++)
                    {
                        BudgetedResourceCache<string, int> c = null;
                        c = new BudgetedResourceCache<string, int>(10, _ =>
                        {
                            switch (operation)
                            {
                                case 0: c.Clear(); break;
                                case 1: c.Trim(0); break;
                                case 2: c.UnpinAll(); break;
                                case 3: c.BudgetBytes = 0; break;
                                case 4: int value; c.TryGet("kept", out value); break;
                                case 5: c.Add("new", 3, 1); break;
                            }
                        });
                        c.Add("old", 1, 5, false); c.Add("kept", 2, 10);
                        Throws<AggregateException>(() => c.TrimToBudget());
                        Need(c.Count == 1 && c.TotalBytes == 10 && c.PinnedBytes == 10 && c.BudgetBytes == 10,
                            "Release reentry mutated protected owner");
                        c.UnpinAll(); Need(c.PinnedBytes == 0, "Guard failed to reset");
                    }
                },
                ["negative trim target and reduced budget still preserve current ownership"] = () =>
                {
                    var released = new List<int>();
                    var c = new BudgetedResourceCache<string, int>(100, released.Add);
                    c.Add("idle", 1, 5, false); c.Add("active", 2, 7);
                    c.BudgetBytes = 0; c.Trim(-1);
                    Need(c.Count == 1 && c.TotalBytes == 7 && c.PinnedBytes == 7 && released.Count == 1,
                        "Budget reduction evicted active owner");
                    c.UnpinAll(); c.UnpinAll(); c.TrimToBudget();
                    Need(c.Count == 0 && c.TotalBytes == 0 && c.PinnedBytes == 0 && released.Count == 2,
                        "Ending scope did not make resources evictable");
                },
                ["invalid cache keys and missing release callback fail without ownership transfer"] = () =>
                {
                    Throws<ArgumentNullException>(() => new BudgetedResourceCache<string, int>(10, null));
                    int released = 0;
                    var c = new BudgetedResourceCache<string, int>(-1, _ => released++);
                    Throws<ArgumentNullException>(() => c.Add(null, 1, 1));
                    int value;
                    Throws<ArgumentNullException>(() => c.TryGet(null, out value));
                    Need(c.Count == 0 && c.TotalBytes == 0 && c.PinnedBytes == 0 && c.BudgetBytes == 0
                        && released == 0, "Invalid input transferred ownership");
                },
                ["many obsolete leases cannot consume the current request"] = () =>
                {
                    var q=new ResourceRequestQueue<string,object>(1);var old=new List<ResourceRequestQueue<string,object>.Lease>();
                    for(int i=0;i<100;i++){old.Add(q.GetOrCreate("a",()=>new object()));q.Pump(_=>{});q.CancelAll();}
                    var current=q.GetOrCreate("a",()=>new object());q.Pump(_=>{});foreach(var item in old)Need(!q.Complete(item),"Stale completion accepted");
                    Need(q.IsCurrent(current)&&q.ActiveCount==1&&q.PendingCount==1&&q.CancelledCount==100,"Current ownership changed");
                }
            };
        }
    }
}
