// NUnit-compatible subset used by the Core/EditMode tests (only what they call). Keeps CI package-free.
using System; using System.Collections; using System.Linq; using System.Reflection;
namespace NUnit.Framework {
  [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute {}
  [AttributeUsage(AttributeTargets.Method)] public class TearDownAttribute : Attribute {}
  [AttributeUsage(AttributeTargets.Method)] public class SetUpAttribute : Attribute {}
  public static class StringAssert { public static void Contains(string e, string a, string m=null){ if(a==null||!a.Contains(e)) throw new Exception((m??"")+" expected to contain "+e); } public static void StartsWith(string e, string a, string m=null){ if(a==null||!a.StartsWith(e)) throw new Exception((m??"")+" expected to start with "+e); } }
  public class Constraint {
    public Func<object,bool> Pred; public string Desc; public double Tol = 0;
    public Constraint(Func<object,bool> p, string d){Pred=p;Desc=d;}
    public Constraint Within(double t){Tol=t;return this;}
  }
  public class EqualC : Constraint {
    public EqualC(object e) : base(null, "equal "+e) { Pred = a => Eq(a,e,Tol); }
    static bool Num(object o)=> o is int||o is float||o is double||o is long;
    public static bool Eq(object a, object e, double tol){
      if (Num(a)&&Num(e)) return Math.Abs(Convert.ToDouble(a)-Convert.ToDouble(e))<=tol+1e-12;
      if (a is IEnumerable ea && e is IEnumerable ee && !(a is string)) return ea.Cast<object>().SequenceEqual(ee.Cast<object>());
      return Equals(a,e);
    }
  }
  public static class Is {
    public static EqualC EqualTo(object e)=>new EqualC(e);
    public static Constraint Null=>new Constraint(a=>a==null,"null");
    public static Constraint True=>new Constraint(a=>a is bool b&&b,"true");
    public static Constraint False=>new Constraint(a=>a is bool b&&!b,"false");
    public static Constraint Zero=>new Constraint(a=>Convert.ToDouble(a)==0,"zero");
    public static Constraint Empty=>new Constraint(a=>!((IEnumerable)a).Cast<object>().Any(),"empty");
    public static Constraint Ordered=>new Constraint(a=>{var l=((IEnumerable)a).Cast<IComparable>().ToList(); for(int i=1;i<l.Count;i++) if(l[i-1].CompareTo(l[i])>0) return false; return true;},"ordered");
    public static Constraint GreaterThan(object x)=>new Constraint(a=>Convert.ToDouble(a)>Convert.ToDouble(x),">"+x);
    public static Constraint LessThan(object x)=>new Constraint(a=>Convert.ToDouble(a)<Convert.ToDouble(x),"<"+x);
    public static Constraint GreaterThanOrEqualTo(object x)=>new Constraint(a=>Convert.ToDouble(a)>=Convert.ToDouble(x),">="+x);
    public static Constraint LessThanOrEqualTo(object x)=>new Constraint(a=>Convert.ToDouble(a)<=Convert.ToDouble(x),"<="+x);
    public static class Not { public static Constraint Empty=>new Constraint(a=>((IEnumerable)a).Cast<object>().Any(),"not empty"); public static Constraint Null=>new Constraint(a=>a!=null,"not null"); }
  }
  public static class Does {
    public static Constraint Contain(object x)=>new Constraint(a=> a is string s ? s.Contains((string)x) : ((IEnumerable)a).Cast<object>().Contains(x),"contain "+x);
    public static Constraint StartWith(string x)=>new Constraint(a=> a is string s && s.StartsWith(x),"start "+x);
    public static class Not { public static Constraint Contain(object x){ var c=Does.Contain(x); return new Constraint(a=>!c.Pred(a),"not contain "+x);} }
  }
  public static class Has { public static class No { public static Constraint Member(object x){ return new Constraint(a=>!((System.Collections.IEnumerable)a).Cast<object>().Contains(x),"no member "+x);} } public static Constraint Member(object x)=>new Constraint(a=>((System.Collections.IEnumerable)a).Cast<object>().Contains(x),"member "+x); }
  public class AssertionException : Exception { public AssertionException(string m):base(m){} }
  public static class Assert {
    public static void That(object actual, Constraint c, string msg=null){ if(!c.Pred(actual)) throw new AssertionException($"expected {c.Desc} but was {actual} :: {msg}"); }
    public static void That(bool cond, string msg=null){ if(!cond) throw new AssertionException("false :: "+msg); }
    public static T Throws<T>(Action a) where T:Exception { try{a();}catch(T e){return e;} throw new AssertionException("no "+typeof(T).Name); }
  }
}
// Minimal test runner: every [Test] method, with [SetUp]/[TearDown]; prints failures and returns their count.
//   --coverage              print the scenario x competency coverage matrix (docs/CoverageMatrix.csv)
//   --check-coverage <csv>  fail when the committed matrix is stale
public static class Runner
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--coverage") { foreach (var l in Jobsite.Core.EvidenceModel.CoverageCsv()) Console.WriteLine(l); return 0; }
        if (args.Length > 1 && args[0] == "--check-coverage")
        {
            var want = Jobsite.Core.EvidenceModel.CoverageCsv().ToList();
            var have = System.IO.File.ReadAllText(args[1]).Replace("\r", "").TrimEnd('\n').Split('\n').ToList();
            if (want.SequenceEqual(have)) { Console.WriteLine($"coverage matrix up to date ({want.Count - 1} rows)"); return 0; }
            Console.WriteLine($"coverage matrix is stale: {args[1]} has {have.Count - 1} rows, the model gives {want.Count - 1}. Regenerate with --coverage.");
            return 1;
        }
        int pass = 0, fail = 0;
        foreach (var t in typeof(Runner).Assembly.GetTypes().OrderBy(t => t.Name))
            foreach (var m in t.GetMethods())
            {
                if (m.GetCustomAttribute<NUnit.Framework.TestAttribute>() == null) continue;
                var o = Activator.CreateInstance(t);
                foreach (var su in t.GetMethods().Where(x => x.GetCustomAttribute<NUnit.Framework.SetUpAttribute>() != null)) su.Invoke(o, null);
                try { m.Invoke(o, null); pass++; }
                catch (TargetInvocationException e) { fail++; Console.WriteLine("FAIL " + t.Name + "." + m.Name + ": " + e.InnerException.Message); }
                finally { foreach (var td in t.GetMethods().Where(x => x.GetCustomAttribute<NUnit.Framework.TearDownAttribute>() != null)) td.Invoke(o, null); }
            }
        Console.WriteLine($"passed {pass}, failed {fail}");
        return fail;
    }
}
