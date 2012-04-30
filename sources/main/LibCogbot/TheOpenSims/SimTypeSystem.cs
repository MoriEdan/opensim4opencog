using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using cogbot.Listeners;
using cogbot.ScriptEngines;
using cogbot.Utilities;
using DotLisp;
using MushDLR223.ScriptEngines;
using MushDLR223.Utilities;
using OpenMetaverse;

namespace cogbot.TheOpenSims
{
    public class SimObjectType : BotMentalAspect
    {
        public UUID ID
        {
            get
            {
                return UUID.Zero;
                throw new NotImplementedException("BotMentalAspect.ID " + this);
            }
        }
        
        public FirstOrderTerm GetTerm()
        {
            throw new NotImplementedException();
        }

        public ICollection<NamedParam> GetInfoMap()
        {
            return WorldObjects.GetMemberValues("", this);
        }

        // Attachments
        public List<string> AcceptsChild = new List<string>(); // types that can attach to it
        public List<string> AcceptsParent = new List<string>(); // what bodypart to attach?  Book=LeftHand

        // Clasification
        public List<Regex> Match = new List<Regex>();  // regexpr match
        public List<Regex> NoMatch = new List<Regex>(); // wont be if one of these matches
        public List<Object> CodeMatch = new List<Object>();  // regexpr match

        // Defines Side-effects to change Prim in SL
        public string SitName = null;
        public string TouchName = null;

        // Configuration overrides
        public ListAsSet<string> SpecifiedProperties = new ListAsSet<string>();

        // Source for Load/Save 
        internal Object cons;

        // Uses for this type
        internal readonly Dictionary<string, SimTypeUsage> UsageAffect = new Dictionary<string, SimTypeUsage>();

        // Superclasses
        readonly public List<SimObjectType> SuperType = new List<SimObjectType>();

        internal bool IsUseType;
        internal bool IsInstanceType;

        internal bool IsObjectType
        {
            get
            {
                if (IsUseType || IsInstanceType) return false;
                return true;
            }
        }

        public string AspectName;
        public SimObjectType(string name)
            //: base(name)
        {
            AspectName = name;
        }

        public SimObjectType IsSubType(SimObjectType superType)
        {
            if (superType == this) return this;
            lock (SuperType) foreach (SimObjectType st in SuperType)
            {
                if (st == superType) return st;
                if (st.SuperType.Contains(superType)) return st;
                SimObjectType found = st.IsSubType(superType);
                if (found != null) return found;
            }
            return null;
        }


        public String ToDebugString()
        {
            if (cons != null) return cons.ToString();
            String str = ToString() + "[";
            lock (SuperType) SuperType.ForEach(delegate(SimObjectType item)
            {
                str += item.ToString() + " ";
            });
            return str.Trim() + "]";
        }


        public SimTypeUsage FindObjectUsage(string usename)
        {
            lock (UsageAffect)
            {
                List<SimTypeUsage> usages = new List<SimTypeUsage>();

                lock (SuperType)

                    foreach (SimObjectType type in SuperType)
                    {
                        SimTypeUsage find = type.FindObjectUsage(usename);
                        if (find != null)
                        {
                            usages.Add(find);
                        }
                    }

                if (UsageAffect.ContainsKey(usename))
                    usages.Add(UsageAffect[usename]);


                if (usages.Count == 0) return null;

                SimTypeUsage newUse = new SimTypeUsage(usename);

                foreach (SimTypeUsage use in usages)
                {
                    newUse.OverrideProperties(use);
                }

                // TODO maybe store for later?
                // usageAffect[usename] = newUse;

                return newUse;
            }
        }

        public SimTypeUsage CreateObjectUsage(string usename)
        {
            lock (UsageAffect)
            {
                if (UsageAffect.ContainsKey(usename))
                    return UsageAffect[usename];
                SimTypeUsage sou = new SimTypeUsage(usename);
                //  sou.TextName = usename;
                UsageAffect[usename] = sou;
                return sou;
            }
        }

        public IList<SimTypeUsage> GetTypeUsages()
        {
            lock (UsageAffect)
            {
                HashSet<string> verbs = new HashSet<string>();
                foreach (string key in UsageAffect.Keys)
                {
                    verbs.Add(key);
                }
                foreach (SimObjectType st in SuperType)
                {
                    foreach (SimTypeUsage v in st.GetTypeUsages())
                    {
                        verbs.Add(v.UsageName);
                    }
                }
                List<SimTypeUsage> usages = new List<SimTypeUsage>();
                foreach (string st in verbs)
                {
                    SimTypeUsage use = FindObjectUsage(st);
                    use.ToString();
                    usages.Add(use);
                }

                return usages;
            }
        }

        public BotNeeds GetUsagePromise(string usename)
        {
            SimTypeUsage use = FindObjectUsage(usename);
            if (use == null) return BotNeeds.ZERO;
            return use.ChangePromise;

        }

        public float RateIt(BotNeeds from, SimTypeUsage use)
        {
            if (use == null) return -100f;
            BotNeeds sat = GetUsagePromise(use.UsageName).Copy();
            sat.AddFrom(from);
            sat.SetRange(0.0F, 100.0F);
            return sat.Total();
        }

        public BotNeeds GetUsageActual(string usename)
        {
            SimTypeUsage use = FindObjectUsage(usename);
            if (use == null) return BotNeeds.ZERO;
            return use.ChangeActual;
        }

        public override string ToString()
        {
            return GetType().Name + "::" + GetTypeName();
        }

        public string GetTypeName()
        {
            return AspectName;
        }

        public void ParseAffect(SimTypeUsage usage, object[] parseStr)
        {
            SimObjectType type = this;
            int i = 0;

            while (i < parseStr.Length)
            {
                if (parseStr[i] == null)
                {
                    i++;
                    continue;
                }
                string s = (string)parseStr[i++];//.ToString();

                if (s == "SuperType")
                {
                    String arg = parseStr[i++].ToString();
                    SimObjectType test = SimTypeSystem.FindObjectType(arg);
                    if (test == null)
                    {
                        throw new Exception("unknown super type " + arg + " for " + type);
                    }
                    AddSuperType(test);
                    //Not all types need to be by default usage types - was causing problems
                    // use types are fined by "Verb"
                    // usage = type.CreateObjectUsage(arg);
                    continue;
                }
                //if (s == "Match")
                //{
                //    String arg = parseStr[i++].ToString();
                //    arg = SimTypeSystem.MakeRegExpression(arg);
                //    type.Match.Add(new Regex(arg));
                //    continue;
                //}
                //if (s == "NoMatch")
                //{
                //    String arg = parseStr[i++].ToString();
                //    arg = SimTypeSystem.MakeRegExpression(arg);
                //    type.NoMatch.Add(new Regex(arg));
                //    continue;
                //}
                if (s == "Verb")
                {
                    String arg = parseStr[i++].ToString();
                    // TODO make sure creation order internalizes correctly
                    SimObjectType superType = SimTypeSystem.CreateObjectUse(arg,new object[0]);
                    AddSuperType(superType);
                    usage = type.CreateObjectUsage(arg);
                    continue;
                }
                // usage / distanceToExcite / etc
                FieldInfo fi = type.GetType().GetField(s);
                if (fi != null)
                {
                    type.SpecifiedProperties.AddTo(fi.Name);
                    SimTypeSystem.SetValue(fi, type, parseStr[i++]);
                    continue;
                }

                fi = type.GetType().GetField(s + "s");
                if (fi != null)
                {
                    type.SpecifiedProperties.AddTo(fi.Name);
                    SimTypeSystem.SetValue(fi, type, parseStr[i++]);
                    continue;
                }

                if (usage == null)
                {
                    if (type.IsUseType)
                        usage = type.CreateObjectUsage(type.GetTypeName());
                }
                fi = usage.GetType().GetField(s);
                if (fi != null)
                {
                    usage.SpecifiedProperties.Add(fi.Name);
                    SimTypeSystem.SetValue(fi, usage, parseStr[i++]);
                    continue;
                }

                fi = usage.GetType().GetField(s + "s");
                if (fi != null)
                {
                    usage.SpecifiedProperties.Add(fi.Name);
                    SimTypeSystem.SetValue(fi, usage, parseStr[i++]);
                    continue;
                }

                // Hygiene / Hunger
                fi = typeof(BotNeeds).GetField(s);
                if (fi != null)
                {
                    float ff = Single.Parse(parseStr[i++].ToString());
                    fi.SetValue(usage.ChangePromise, ff);
                    ff = Single.Parse(parseStr[i++].ToString());
                    fi.SetValue(usage.ChangeActual, ff);
                    continue;
                }
                DLRConsole.DebugWriteLine("ERROR: SimBots.ini-like dirrective unknown: '" + s + "'");
            }
        }

        public bool AddSuperType(SimObjectType test)
        {
            lock (SuperType)
            {
                if (SuperType.Contains(test)) return false;
                SuperType.Add(test);
                return true;
            }
        }


        internal void AddAllTypes(List<SimObjectType> list)
        {
            if (list.Contains(this)) return;
            list.Add(this);
            lock (SuperType) foreach (SimObjectType T in SuperType.ToArray())
            {
                T.AddAllTypes(list);
            }
        }

        public string GetTouchName()
        {
            if (!String.IsNullOrEmpty(TouchName)) return TouchName;
            List<SimObjectType> list = new List<SimObjectType>();
            AddAllTypes(list);
            list.Remove(this);
            SimObjectType pt = list.Find(delegate(SimObjectType sc)
            {
                    String tn = sc.TouchName;
                    return (!String.IsNullOrEmpty(tn));
            });
            return pt == null ? TouchName : pt.TouchName;
        }

        public string GetSitName()
        {
            if (!String.IsNullOrEmpty(SitName)) return SitName;
            List<SimObjectType> list = new List<SimObjectType>();
            AddAllTypes(list);
            list.Remove(this);
            SimObjectType pt = list.Find(delegate(SimObjectType sc)
            {
                    String tn = sc.SitName;
                    return (!String.IsNullOrEmpty(tn));
            });
            return pt == null ? SitName : pt.SitName;
        }

        internal bool IsComplete
        {
            get
            {
                lock (SuperType) return SuperType.Count > 0;
            }
        }
    }










    public class SimTypeSystem
    {

        public static SimObjectType UNKNOWN
        {
            get { return SimTypeSystem.GetObjectType("Unknown"); }
        }
        public static SimObjectType USEABLE
        {
            get { return SimTypeSystem.GetObjectType("Useable"); }
        }
        public static SimObjectType DOOR
        {
            get { return SimTypeSystem.GetObjectType("Door"); }
        }
        public static SimObjectType PASSABLE
        {
            get { return SimTypeSystem.GetObjectType("Passable"); }
        }
        public static SimObjectType BARRIER
        {
            get { return SimTypeSystem.GetObjectType("Barrier"); }
        }

        /**
         * 
         * STATIC METHODS
         * 
         * 
         **/

        static List<SimObjectType> objectTypes = new List<SimObjectType>();

        //the scripting language might supply a number as a parameter in a foriegn method call, so when i iterate thru the method signatures.. i have to recognise which ones are claiming to accept a numeric argument
        static public List<SimObjectType> GuessSimObjectTypes(Primitive.ObjectProperties props, SimObject obj)
        {
            List<SimObjectType> possibles = obj.Affordances.ObjectType.SuperType;

            if (props != null)

                LockInfo.WeaklyLock(possibles, TimeSpan.FromSeconds(10),
                                    () => DoGuessWork(props, obj, possibles),
                                    () => "Guess Sim Object Types for " + obj, DLRConsole.DebugWriteLine);
            return possibles;
        }

        static List<SimObjectType> DoGuessWork(Primitive.ObjectProperties props, SimObject obj, List<SimObjectType> possibles)
        {
            {
                {

                    List<SimObjectType> objectTypesLocally =
                        new List<SimObjectType>();
                    lock (SimTypeSystem.objectTypes)
                        objectTypesLocally.AddRange(SimTypeSystem.objectTypes);
                    foreach (SimObjectType otype in objectTypesLocally)
                    {
                        foreach (Regex smatch in otype.NoMatch)
                        {
                            // NoMatch
                            if (smatch.IsMatch(props.Name.ToLower()) && smatch.IsMatch(props.Description.ToLower()))
                            {
                                goto nextOType;
                            }
                        }
                        foreach (Object smatch in otype.CodeMatch)
                        {
                            // CodeMatch
                            if (IsCodeMatch(obj, smatch))
                            {
                                if (!possibles.Contains(otype))
                                {
                                    possibles.Add(otype);
                                    goto nextOType;
                                }
                            }
                            else
                            {
                                possibles.Remove(otype);
                            }
                        }
                        foreach (Regex smatch in otype.Match)
                        {
                            // Match
                            if (smatch.IsMatch(props.Name.ToLower()) && smatch.IsMatch(props.Description.ToLower()))
                            {
                                if (!possibles.Contains(otype))
                                {
                                    possibles.Add(otype);
                                    SetNames(props, otype);
                                }
                                break;
                            }
                        }
                        nextOType:
                        {
                        }
                    }
                    if (!String.IsNullOrEmpty(props.TouchName))
                    {
                        string verb = props.TouchName;
                        possibles.Add(SimTypeSystem.CreateObjectUse(verb,
                                                                    new object[]
                                                                        {
                                                                            "UseGrab", true
                                                                            ,
                                                                            "TextName",
                                                                            verb
                                                                        }));
                    }
                    if (!String.IsNullOrEmpty(props.SitName))
                    {
                        string verb = props.SitName;
                        possibles.Add(SimTypeSystem.CreateObjectUse(verb,
                                                                    new object[]
                                                                        {
                                                                            "UseSit", true,
                                                                            "TextName",
                                                                            verb
                                                                        }));
                    }
                    return possibles;
                }
            }
        }

        private static bool IsCodeMatch(SimObject obj, object smatch)
        {
            ScriptInterpreter interp = GetScriptInterpreter();
            interp.Intern("this", obj);
            string res = interp.Str(interp.Eval(smatch));
            if (res.ToLower().StartsWith("t")) return true;
            return false;
        }

        private static ScriptInterpreter ScriptInterpreter0 = null;
        private static ScriptInterpreter GetScriptInterpreter()
        {
            if (ScriptInterpreter0 == null) ScriptInterpreter0 = ScriptManager.LoadScriptInterpreter("lisp", null);
            return ScriptInterpreter0;
        }

        public static bool MatchString(string objName, string smatch)
        {
            objName = objName.ToLower();
            smatch = smatch.ToLower();
            String otypeAspectName = smatch;
            if (objName.Contains(otypeAspectName)) return true;
            if (smatch.Contains("*"))
            {
                otypeAspectName = smatch.Replace("*", " ");
                if (objName.Contains(otypeAspectName)) return true;
                otypeAspectName = smatch.Replace("*", "");
                if (objName.Contains(otypeAspectName)) return true;
                if (!smatch.Contains(".*"))
                {
                    smatch = smatch.Replace("*", ".*");
                }
                // Build Regex
                Regex regexPrimName = new Regex(smatch);
                if (regexPrimName.IsMatch(objName))
                {
                    return true;
                }
            }
            else
            {
                // Build Regex
                Regex regexPrimName = new Regex(".*" + smatch + ".*");
                if (regexPrimName.IsMatch(objName))
                {
                    return true;
                }
            }

          
            return false;
        }



        //static public string GetPrimTypeName(Primitive target)
        //{
        //    if (target.PrimData.PCode == PCode.Prim)
        //        return target.PrimData.Type.ToString();
        //    return target.PrimData.PCode.ToString();
        //}

        static private void SetNames(Primitive.ObjectProperties props, SimObjectType otype)
        {
            //= prim.Properties;
            if (props != null)
            {
                //      Primitive prim = null;
                if (String.IsNullOrEmpty(props.SitName))
                {
                    props.SitName = otype.GetSitName();
                    if (!String.IsNullOrEmpty(props.SitName))
                    {
                        // DLRConsole.WriteLine("[TODO] SetSitName(" + prim + "," + otype.GetSitName());
                    }
                }
                if (String.IsNullOrEmpty(props.TouchName))
                {
                    props.TouchName = otype.GetTouchName();
                    if (!String.IsNullOrEmpty(props.TouchName))
                    {
                        //  DLRConsole.WriteLine("[TODO] SetTextName(" + prim + "," + otype.GetTouchName());
                    }
                }
            }
        }

        static bool IsInited = false;
        static bool IsIniting = false;
        static object InitLock = new object();
        static public void LoadDefaultTypes()
        {
            lock (InitLock)
            {
                if (IsInited || IsIniting) return;
                IsIniting = true;
                LoadDefaultTypes0();
                IsIniting = false;
                IsInited = true;
            }

        }
        static public void LoadDefaultTypes0()
        {
            FileInfo fi = new FileInfo("SimBots.ini");
            if (fi.Exists)
            {
                LoadConfig(fi.Name);
                return;
            }
            /*            
             Format of loader           
             */
            CreateObjectUse("OnMinuteTimer", //  Just being alive
                    "maximumDistance", 1000, // mostly anywhere
                    "Energy", -0.1, -0.1, //  needs rest every 1000 minutes
                    "Hunger", -1, -1, // hungry every 100 minutes
                    "Bladder", -1, -1, // toilet every 100 minutes
                    "Hygiene", 0, 0, // need bath
                    "Room", -1, -1, // needs space every 100 minutes
                    "Social", -1, -1, // needs people every 100 minutes
                    "Fun", -1, -1, // needs excitement every 100 minutes
                    "GenerallySadToHappy", -1, -1, // needs to be kept happy every 100 minutes
                    "Comfort", -1, -1, // needs to be kept comfy every 100 minutes
                    null);

        }

        public static void LoadConfig(string filename)
        {
            System.IO.FileStream f = System.IO.File.OpenRead(filename);
            StreamReader r = new StreamReader(f);
            r.BaseStream.Seek(0, SeekOrigin.Begin);
            TextReader tr = r;
            Interpreter interp = new DotLisp.Interpreter((Interpreter)ClientManager.SingleInstance.TaskInterperter.Impl);
            while (tr.Peek() != -1)
            {
                Object read = interp.Read(filename, tr);
                if (interp.Eof(read)) return;
                Cons cons = (Cons)read;
                SimObjectType type = LoadConfigCons(cons);
                type.cons = cons;
            }

        }

        public static SimObjectType LoadConfigCons(Cons cons)
        {
            Object first = Cons.First(cons);
            cons = (Cons)Cons.Rest(cons);
            first = ((Symbol)first).ToString();
            if (first.ToString().ToLower() == "createobjectuse")
            {
                Object second = Cons.First(cons);
                return CreateObjectUse(second.ToString(), ConsParams((Cons)Cons.Rest(cons)));
            }
            else
                if (first.ToString().ToLower() == "createobjecttype")
                {
                    Object second = Cons.First(cons);
                    return CreateObjectType(second.ToString(), ConsParams((Cons)Cons.Rest(cons)));
                }
                else
                {
                    return CreateObjectType(first.ToString(), ConsParams(cons));
                }
        }

        private static object[] ConsParams(Cons cons)
        {
            object[] consV = Cons.ToVector(cons);
            object[] o = new object[consV.Length];
            for (int i = 0; i < consV.Length; i++)
            {
                object v = consV[i];
                if (v is Cons)
                {
                   // v = Cons.First((Cons)v).ToString().Substring(1);
                }

                String s = v.ToString();
                if (v is Symbol)
                {
                    v = s;
                }
                else if (v is IConvertible)
                {
                    //v = s;
                }
                else
                {
                    //   v = v;
                }
                if (s == "true") v = true;
                else
                    if (s == "false") v = false;
                o[i] = v;
            }
            return o;
        }

        internal static SimObjectType CreateInstanceType(string name)
        {
            SimObjectType type = new SimObjectType(name) {IsInstanceType = true};
            return type;
        }

        static public SimObjectType CreateObjectUse(string classname, Cons parseStr)
        {
            return CreateObjectUse(classname, ConsParams(parseStr));
        }

        static public SimObjectType CreateObjectUse(string classname, params object[] parseStr)
        {
            if (parseStr.Length == 1 && parseStr[0] is object[]) parseStr = (object[])parseStr[0];
            SimObjectType type = GetObjectType(classname);
            type.AddSuperType(USEABLE);
            type.IsUseType = true;
            SimTypeUsage usage = type.CreateObjectUsage(classname);
            type.ParseAffect(usage, parseStr);
            return type;
        }

        static public SimTypeUsage CreateTypeUsage(string classname, params object[] parseStr)
        {
            if (parseStr.Length == 1 && parseStr[0] is object[]) parseStr = (object[])parseStr[0];
            SimObjectType type = GetObjectType(classname);
            type.AddSuperType(USEABLE);
            type.IsUseType = true;
            SimTypeUsage usage = type.CreateObjectUsage(classname);
            type.ParseAffect(usage, parseStr);
            return usage;
        }

        static public SimObjectType CreateObjectType(string aspectName, Cons parseStr)
        {
            return CreateObjectType(aspectName, ConsParams(parseStr));
        }

        static public SimObjectType SetSimType(string aspectName, Cons cons)
        {
            object[] parseStr = ConsParams(cons);
            SimObjectType type = GetObjectType(aspectName);
            SimTypeUsage usage = null;
            if (type.IsUseType)
                usage = type.CreateObjectUsage(aspectName);
            type.ParseAffect(usage, parseStr);
            return type;
        }

        static public SimObjectType CreateObjectType(string aspectName, params object[] parseStr)
        {
            if (parseStr.Length == 1 && parseStr[0] is object[]) parseStr = (object[])parseStr[0];
            SimObjectType type1 = FindObjectType(aspectName);
            if (type1 == null)
            {
                type1 = new SimObjectType(aspectName);
                type1.ParseAffect(null, new object[] { "Match", "* " + aspectName + " *" });
                lock (objectTypes) objectTypes.Add(type1);
            }
            type1.ParseAffect(null, parseStr);
            return type1;
        }

        static public SimObjectType FindObjectType(string aspectName)
        {
            lock (objectTypes) foreach (SimObjectType type in objectTypes)
                {
                    if (type.AspectName == aspectName) return type;
                }
            return null;
        }

        internal static SimTypeUsage FindObjectUse(string aspectName)
        {
            lock (objectTypes) foreach (SimObjectType type in objectTypes)
                {
                    if (!type.IsUseType) continue;
                    if (type.AspectName == aspectName) return type.FindObjectUsage(aspectName);
                }
            return null;
        }

        static public SimObjectType GetObjectType(string name)
        {
            SimObjectType type = FindObjectType(name);
            if (type == null)
            {
                type = new SimObjectType(name);
                lock (objectTypes) objectTypes.Add(type);
            }
            return type;
        }

        internal static string ListTypes(bool includeIncomplete, bool includeUsageTypes, bool includeObjectTypes, bool includeInstanceTypes)
        {
            string str = "";
            lock (objectTypes) foreach (SimObjectType type in objectTypes)
                {
                    if (!includeIncomplete && !type.IsComplete) continue;
                    if (!includeInstanceTypes && type.IsInstanceType) continue;
                    if (!includeUsageTypes && type.IsUseType) continue;
                    if (!includeObjectTypes && type.IsObjectType) continue;
                    str += "\n";
                    str += "\t" + type.ToDebugString() + "\n";
                    lock (type.UsageAffect) foreach (String key in type.UsageAffect.Keys)
                    {
                        str += "\t\t;;" + type.FindObjectUsage(key).ToDebugString()+"\n";
                    }
                }
            return str;
        }
        static public void SetValue(FieldInfo fi, object o, object p)
        {
            Type ftype = fi.FieldType;
            if (ftype.IsInstanceOfType(p))
            {
                fi.SetValue(o, p);
                return;
            }
            if (ftype.IsAssignableFrom(typeof(string)))
            {
                if (p == null) fi.SetValue(o, null);
                else fi.SetValue(o, p.ToString());
                return;
            }
            if (typeof(IList).IsAssignableFrom(ftype))
            {
                object fv = fi.GetValue(o);
                if (fv == null)
                {
                    fv = ftype.GetConstructor(new Type[0]).Invoke(new object[0]);
                }
                ftype = fv.GetType().GetGenericArguments()[0];
                p = CastTo(ftype, p);
                ((IList)fv).Add(p);
                fi.SetValue(o, fv);
                return;
            }
            fi.SetValue(o, CastTo(ftype, p));
        }

        static public object CastTo(Type ftype, object p)
        {
            if (p is String)
            {
                if (p.ToString().ToLower().Equals("null"))
                {
                    return null;
                }
                if (ftype == typeof(Regex))
                {
                    return new Regex(MakeRegExpression(p.ToString()));
                }
                if (ftype == typeof(SimObjectType))
                {
                    return CreateObjectType(p.ToString());
                }
                if (ftype == typeof(bool))
                {
                    return p.ToString().ToLower().StartsWith("t");
                }
            }
            return p;
        }

        internal static string MakeRegExpression(string arg)
        {
            if (arg.Contains("*"))
            {
                if (!arg.Contains(".*"))
                    arg = arg.Replace("*", ".*");
            }
            else
            {
                arg = ".*" + arg + ".*";
            }
            return arg.ToLower();
        }
    }
}
