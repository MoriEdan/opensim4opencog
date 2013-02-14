#define MERGED_RDFSTORE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web;
using LogicalParticleFilter1;
using MushDLR223.Utilities;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Parsing;
using VDS.RDF.Parsing.Contexts;
using VDS.RDF.Parsing.Handlers;
using VDS.RDF.Parsing.Tokens;
using VDS.RDF.Query;
using VDS.RDF.Query.Expressions;
using VDS.RDF.Writing;
using ListOfBindings = System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, LogicalParticleFilter1.SIProlog.Part>>;
using StringWriter=System.IO.StringWriter;
using VDS.RDF.Writing.Formatting;
using PartList = LogicalParticleFilter1.SIProlog.PartListImpl;
namespace LogicalParticleFilter1
{
    static public partial class GraphWithDefExtensions
    {

        static public LogicalParticleFilter1.SIProlog.RdfRules ToTriples(this LogicalParticleFilter1.SIProlog.Rule rule,
            SIProlog.PNode pnode, IGraph kb)
        {
            return LogicalParticleFilter1.SIProlog.GraphWithDef.FromRule(rule, pnode, kb);
        }
    }

    public partial class SIProlog
    {
        /// <summary>
        /// i been serializing my apps semantic state to prolog.. 
        /// then was going to convert to RDF
        /// secretly i hoped i could just convert "prolog2RDF" 
        /// but it has been much harder than anticipated..   
        /// but seems converting to N3 is much easier!.
        ///  but i still have to have "semantic state".  
        /// i'll get a chance to see some crossover when i convert the N3 to RDF  (you guys say it is easy!)
        ///  then hrrm i suppose I'll get to see how closely prolog->n3->rdf   
        /// looks the original planned prolog->n3
        /// </summary>
        static public partial class GraphWithDef
        {
            [ThreadStatic]
            public static Rule CompilingRule = null;
            [ThreadStatic]
            public static INode CompilingMtNode = null;
            [ThreadStatic]
            public static PNode CompilingMt = null;
            public static string CompilingMtName
            {
                get
                {
                    var cr = CompilingRule;
                    if (cr != null)
                    {
                        string ret = cr.OptionalHomeMt;
                        if (!String.IsNullOrEmpty(ret)) return ret;
                    }
                    var cm = CompilingMt;
                    if (cm != null)
                    {
                        string ret = cm.Id;
                        if (!String.IsNullOrEmpty(ret)) return ret;
                    }
                    return threadLocal.curKB;
                }
            }

            public static T InCompiler<T>(PNode pnode, Rule rule, string kbName, Func<T> made)
            {
                var temp = threadLocal.curKB;
                var tCompilingRule = CompilingRule;
                var tCompilingMt = CompilingMt;
                var tCompilingMtNode = CompilingMtNode;
                try
                {
                    threadLocal.curKB = kbName;
                    CompilingRule = rule;
                    CompilingMt = pnode;
                    CompilingMtNode = C(rdfDefinations, CompilingMtName);
                    return made();
                }
                finally
                {
                    CompilingRule = tCompilingRule;
                    CompilingMt = tCompilingMt;
                    threadLocal.curKB = temp;
                    CompilingMtNode = tCompilingMtNode;
                }
            }

            public static RdfRules FromRule(Rule rule, PNode pnode, IGraph kb)
            {
                return InCompiler(pnode, rule, pnode.id,
                                  () =>
                                      {
                                          RdfRules made = FromRule0(rule, kb);
                                          if (made == null)
                                          {
                                              Warn("FromRule0 returned null");
                                              made = new RdfRules(kb);
                                              made.AddProducing(
                                                  MakeTriple(
                                                      CompilingMtNode,
                                                      kb.CreateUriNode("siprolog:sourceCode"),
                                                      kb.CreateLiteralNode(NoSymbols(rule.ToSource(SourceLanguage.Prolog)),
                                                                           "prolog")));
                                          }
                                          return made;
                                      });
            }

            public static RdfRules FromRule1(Rule rule, IGraph kb)
            {

                Part p = rule.AsTerm();
                RdfRules rules = PartToTriples(p, kb);
                rule.rdfRuleCache = rules;
                var trips = rules.ToTriples;
                kb.Assert(trips);
                return rules;
            }


            private static Term ToSexpr(Term term)
            {
                string fname = term.fname;
                var rest = MakePartList(term.ArgList.ToArray());
                string fnameP = PredicateToPropertyString(fname);
                return MakeList(Atom.FromName(fnameP), rest).AsTerm();
            }

            private static Part MakePartList(params Part[] anslist)
            {
                Part answers = Atom.FromSource(FUNCTOR_NIL);
                for (int i = anslist.Length; i > 0; i--)
                {
                    answers = MakeList(anslist[i - 1], answers);
                }
                return answers;
            }
            private static Part MakePartIList(IList<Part> anslist)
            {
                Part answers = Atom.FromSource(FUNCTOR_NIL);
                for (int i = anslist.Count; i > 0; i--)
                {
                    answers = MakeList(anslist[i - 1], answers);
                }
                return answers;
            }
            private static RdfRules PartToTriples(Part p, IGraph kb)
            {
                RdfRules rules = new RdfRules(kb);
                if (!(p is Term))
                {
                    Warn("Called part to triples on " + p);
                    rules.RuleNode = PartToRdf(p, rules);
                    return rules;
                }
                var term = ToTranslated(p.AsTerm(), rules);
                if (!GlobalSharedSettings.TODO1Completed)
                {
                    return PartToTriplesVerySimple(p, kb);
                }
                var ruleNode = checkIsTerm(PartToRuleNode(term, p, rules, kb));
                if (ruleNode != null)
                {
                    rules.RuleNode = ruleNode;
                    rules.AddProducing(MakeTriple(ruleNode,
                                                  kb.CreateUriNode("siprolog:sourceCode"),
                                                  kb.CreateLiteralNode(NoSymbols(p.ToSource(SourceLanguage.Prolog))
                                                                       , "prolog")));
                }
                return rules;
            }

            private static RdfRules PartToTriplesVerySimple(Part p, IGraph kb)
            {
                throw new NotImplementedException();
            }

            private static INode PartToRuleNode(Part p, Part orig, RdfRules rules, IGraph kb)
            {
                string fname = p.fname;
                bool forceOneToOne = false;
                if (fname == "," || fname == ";")
                {
                    string fnameP = PredicateToPropertyString(fname);
                    IList<Part> makeList = null;
                    Term body = ConvertEachTerm(p, rules, kb, (list)=>
                                                                  {
                                                                      makeList = list;
                                                                      return p;
                                                                  });
                    if (makeList == null)
                    {
                        return checkIsTerm(PartToRuleNode(body, orig, rules, kb));
                    }
                    return checkIsTerm(PartToRuleNode(new Term(fnameP, false, new PartListImpl(makeList)), orig, rules, kb));
                }
                else if (fname == ":-" && p.Arity == 2)
                {
                    string fnameP = PredicateToPropertyString(fname);
                    var al = p.ArgList;
                    Term head = ConvertEachTerm(al[0], rules, kb, MakePartIList);
                    Term body = ConvertEachTerm(al[1], rules, kb, MakePartIList);
                    var p1 = new Term(fnameP, false, new PartListImpl(head, body));
                    var ret = PartToRuleNode(p1, orig, rules, kb);
                    return checkIsTerm(ret);
                }
                else if (p.Arity == 2 && (fname == "first" && fname == "rest"))
                {
                    forceOneToOne = true;
                }
                int ioa;
                if (p.Arity < 2)
                {
                    ioa = 0;
                }
                else
                {
                    ioa = GetInstanceOnArg(fname);
                }
                INode subjNode = null;
                if (ioa == -1)
                {
                    ioa = p.Arity == 2 ? 1 : 0;
                }
                bool mustMakeAsList;
                bool canb = false;
                if (ioa == 0)
                {
                    mustMakeAsList = true;
                }
                else
                {
                    subjNode = PartToRdf(p.ArgList[ioa - 1], rules);
                    canb = CanBeSubjectNode(subjNode);
                    if (canb)
                    {
                        mustMakeAsList = false;
                    }
                    else
                    {
                        mustMakeAsList = true;
                        ioa = 0;
                    }
                }
                int argNum = 0;
                int nodesCount = 0;

                List<INode> nodes = new List<INode>();
                foreach (Part part in p.ArgList)
                {
                    argNum++;
                    if (argNum == ioa) continue;
                    nodesCount++;
                    nodes.Add(PartToRdf(part, rules));
                }
                if (ioa == 0)
                {
                    if (nodesCount == 0)
                    {
                        Warn("No N3 possible " + p);
                    }
                }

                INode listRoot = MakeRdfList(kb, nodes, true);
                var toGraph = listRoot.Graph;
                bool listRootIsList = nodesCount > 1;
                if (!canb && !forceOneToOne)
                {
                    INode ruleNode = CreateBlankNode(kb, fname + CONSP);
                    Triple t = MakeTriple(ruleNode, PredicateToProperty(fname), listRoot);
                    rules.AddProducing(t);
                    rules.RuleNode = ruleNode;
                    return rules.RuleNode.CopyWNode(toGraph);
                }
                else
                {
                    if (listRootIsList)
                    {
                        Triple t = MakeTriple(subjNode, PredicateToProperty(fname), listRoot);
                        rules.AddProducing(t);
                        return rules.AsGraphLiteral();
                    }
                    else
                    {
                        Triple t = MakeTriple(subjNode, PredicateToProperty(fname), listRoot);
                        rules.AddProducing(t);
                        return rules.AsGraphLiteral();
                    }
                }

            }

            private static string NoSymbols( string prolog)
            {
                prolog = prolog.Replace("\"", " ");
                prolog = prolog.Replace("<", " ");
                prolog = prolog.Replace(">", " ");
                prolog = prolog.Replace(":", " ");
                prolog = prolog.Replace("  ", " ");
                return prolog;
            }

            public static INode checkIsTerm(INode rules)
            {
                checkNode(rules);
                /*if (rules.RuleNode == null)
                {
                    Warn("No rule node");
                }*/
                return rules; 
            }

            private static Term ConvertEachTerm(Part p0, RdfRules rules, IGraph kb, Func<IList<Part>, Part> makePartList)
            {
                List<Part> partList = new List<Part>();
                if (p0.fname == "," || p0.fname == ";")
                {
                    p0.ArgList.ToList().ForEach((p) => partList.Add(ConvertEachTerm(p, rules, kb, makePartList)));
                    if (partList.Count == 1)
                    {
                        return ConvertEachTerm(partList[0], rules, kb, makePartList);
                    }
                    if (partList.Count > 0)
                    {
                        return makePartList(partList).AsTerm();
                    }
                    if (partList.Count == 0)
                    {
                        Warn("No args to this");
                    }
                }
                return ToTranslated(p0.AsTerm(), rules);
            }

            private static INode MakeRdfList(IGraph kb, IList<INode> nodes, bool returnOneAsNode)
            {
                if (nodes.Count == 0) return Atom.PrologNIL.AsRDFNode().CopyWNode(kb);
                if (returnOneAsNode && nodes.Count == 1)
                {
                    return nodes[0].CopyWNode(kb);
                }
                return kb.AssertList(nodes, (node) => node.CopyWNode(kb));

            }

            public static RdfRules FromRule0(Rule rule, IGraph kb)
            {
                if (rule.rdfRuleCache != null) return rule.rdfRuleCache;

                if (GlobalSharedSettings.RdfSavedInPDB)
                {
                    RdfRules made = FromRule1(rule, kb);
                    rule.rdfRuleCache = made;
                    return made;
                }
                return FromRule2(rule, kb);
            }

            public static RdfRules FromRule2(Rule rule, IGraph kb)
            {

                if (IsRdfPrecoded(rule.head))
                {
                    return null;
                }

                var rdfRules = rule.rdfRuleCache = new RdfRules(kb);
                PartListImpl pl = null;
                if (rule.body != null)
                {
                    if (!IsBodyAlwaysTrue(rule.body.plist))
                    {
                        pl = (PartListImpl)rule.body.plist.CopyTerm;
                    }
                }
                Term rulehead = (Term)rule.head.CopyTerm;
                AddData(rulehead, pl, rdfRules);
                if (rdfRules.RuleNode != null)
                {
                    //  EnsureGraphPrefixes(kb, () => UriOfMt(rule.OptionalHomeMt));
                    rdfRules.AddProducing(MakeTriple(rdfRules.RuleNode,
                                                     kb.CreateUriNode("siprolog:sourceCode"),
                                                     kb.CreateLiteralNode(NoSymbols(rule.ToSource(SourceLanguage.Prolog)), "prolog")));
                }
                return rdfRules;
            }


            static bool IsRdfPrecoded(Term thisTerm)
            {
                var name = thisTerm.fname;
                int arity = thisTerm.Arity;
                if (QueryPredicateInfoOnce(name, "prologMappingType") == "PrologOnlyPredicate")
                {
                    return true;
                }
                if (IsRdfBuiltIn(name, arity, thisTerm, new RdfRules(rdfDefinations)))
                {
                    return false;
                }
                string key = name + "_" + arity;
                if (key == "not_1")
                {
                    return true;
                }
                if (key == "unify_2")
                {
                    return true;
                }
                if (key.Contains("sameAs"))
                {
                    return true;
                }
                if (key.Contains(":"))
                {
                    return true;
                }
                if (key.StartsWith("prolog")) return false;
                return false;
            }
            static bool IsRdfBuiltIn(string name, int arity, Part thisTerm, RdfRules rules)
            {

                if (arity < 2)
                {
                    if (arity == 1 && !IsLitteral(thisTerm.ArgList[0], rules))
                    {
                        return false;
                    }
                    return true;
                }
                if (arity > 2)
                {
                    if (name == TripleName) return true;
                    return false;
                }
                if (arity == 2 && name.StartsWith("prolog")) return true;
                if (arity == 2 && name.Contains(":")) return true;
                lock (PDB.builtin)
                {
                    if (PDB.builtin.ContainsKey(name + "/" + arity))
                    {
                        return true;
                    }
                    if (PDB.builtin.ContainsKey(name + "/N"))
                    {
                        return true;
                    }
                }

                if (QueryPredicateInfo(name, "prologMappingType") == "DirectOneToOneMapping")
                {
                    return true;
                }
                if (QueryPredicateInfo(name, "prologMappingTypeEquivalent") != null)
                {
                    return true;
                }
                string type = QueryPredicateInfo(name, "rdf:type");
                if (type != null)
                {
                    if (type.EndsWith("Property"))
                    {
                        return true;
                    }
                }
                return false;
            }
            public static string QueryPredicateInfo(string thisTerm, string pred)
            {
                return QueryPredicateInfo(thisTerm, pred, true);
            }
            public static string QueryPredicateInfo(string thisTerm, string pred, bool useFallbacks)
            {
                string s = QueryPredicateInfoOnce(thisTerm, pred);
                if (s != null || !useFallbacks) return s;
                string mtn = CompilingMtName;
                s = QueryPredicateInfoOnce(mtn, pred + "Mt");
                if (s != null)
                {
                    return s;
                }
                s = QueryPredicateInfoOnce("global", pred + "Mt");
                if (s != null)
                {
                    return s;
                }
                return null;
            }
            public static string QueryPredicateInfoOnce(string thisTerm, string pred)
            {
                SparqlParameterizedString query = new SparqlParameterizedString("SELECT ?object WHERE { @subj @pred ?object }");
                query.SetParameter("subj", C(rdfDefinations, thisTerm));
                query.SetParameter("pred", C(rdfDefinations, pred));
                object result = rdfDefinations.ExecuteQuery(query);
                if (result == null)
                {
                    return null;
                }
                SparqlResultSet rs = result as SparqlResultSet;
                string answer = null;
                if (rs.Count > 0)
                {
                    var res = rs[0];
                    var r = res["object"];
                    answer = GetTextString(r);
                }
                return answer;
            }

            private static string GetTextString(INode r)
            {
                var sr = "" + r;
                var vn = ToValueNode(r);
                if (vn != r) r = vn;
                if (r is IUriNode)
                {
                    IUriNode uriNode = r as IUriNode;
                    sr = uriNode.Uri.Fragment;
                }
                else if (r is ILiteralNode)
                {
                    sr = ((ILiteralNode)r).Value;
                }
                if (sr.StartsWith("#")) sr = sr.Substring(1);
                return sr;
            }

            private static bool IsLitteral(Part arg0, RdfRules triples)
            {
                if (arg0 == null) return true;
                var partToRdf = PartToRdf(arg0, triples);
                return partToRdf is ILiteralNode;
            }

            static private void AddData(Term head, PartListImpl rulebody, RdfRules triples)
            {
                var varNames = new List<string>();
                var newVarNames = new List<string>();
                int newVarCount;
                /// head = ToTranslated(head, triples);
                lock (head)
                {
                    rulebody = AnalyzeHead(head, true, varNames, newVarNames, out newVarCount, rulebody);
                    AddData(triples, head, rulebody, varNames, newVarCount, newVarNames);
                }
            }

            public static PartListImpl AnalyzeHead(Part head, bool replaceVars, ICollection<string> varNames, ICollection<string> newVarNames, out int newVarsNeeded, PartListImpl rulebody)
            {
                int newVarCount = 0;
                PartListImpl[] pl = { null };
                head.Visit((a, pr) =>
                {
                    if (!(a is Variable))
                    {
                        a.Visit(pr);
                        return a;
                    }
                    string an = ((Variable)a).vname;
                    if (newVarNames != null && newVarNames.Contains(an))
                    {
                        // Dont copy previously copied vars
                        return a;
                    }
                    if (varNames != null && !varNames.Contains(an))
                    {
                        // First time found
                        varNames.Add(an);
                        return a;
                    }
                    if (!replaceVars)
                    {
                        newVarCount++;
                        return a;
                    }
                    // copy the var to: newVarName
                    string newVarName = an + newVarCount;
                    if (newVarNames != null) newVarNames.Add(newVarName);
                    var r = new Variable(newVarName);
                    // add the unification to the partlist
                    var lpl = pl[0] = pl[0] ?? new PartListImpl();
                    newVarCount++;
                    lpl.AddPart(unifyvar(a, r));
                    return r;
                });
                newVarsNeeded = newVarCount;
                PartListImpl bpl = pl[0];
                if (bpl == null)
                {
                    return rulebody;
                }
                if (newVarCount > 0)
                {
                    if (rulebody != null)
                    {
                        foreach (Part p in rulebody)
                        {
                            bpl.AddPart(p);
                        }
                    }
                }
                return bpl;
            }

            static private void AddData(RdfRules rdfRules, Term head, PartListImpl rulebody, List<string> varNames, int newVarCount, List<string> newVarNamesMaybe)
            {
                if (IsRdfPrecoded(head))
                {
                    return;
                }
                lock (head)
                {
                    int newVarCount2;
                    var newVarNames = varNames;
                    varNames = new List<string>();
                    PartListImpl bpl = AnalyzeHead(head, true, varNames, newVarNames, out newVarCount2, rulebody);
                    if (newVarCount2 > 0)
                    {
                        if (rulebody != null)
                        {
                            foreach (Part p in rulebody)
                            {
                                bpl.AddPart(p);
                            }
                        }
                    }
                    if (bpl == null)
                    {
                        if (rulebody != null)
                        {
                            bpl = rulebody;
                        }
                    }
                    rulebody = bpl;
                }
                AddData2(head, rulebody, rdfRules);
            }

            private static void AddData2(Term head, PartListImpl rulebodyIn, RdfRules rdfRules)
            {
                PartListImpl rulebody = null;
                if (!IsBodyAlwaysTrue(rulebodyIn))
                {
                    rulebody = new PartListImpl();
                    foreach (Term tin in rulebodyIn)
                    {
                        Term t = tin; //ToTranslated(tin, rdfRules);
                        if (t != null) rulebody.AddPart(t);
                    }
                }
                if (head.fname == "rdfinfo")
                {
                    foreach (var arg in head.ArgList)
                    {
                        rdfDefSync.AddRules(new RuleList() {new Rule((Term) arg)}, false);
                    }
                    return;
                }
                var ruleSubject = CreateConsequentNode(head, rdfRules, rulebody != null);
                if (ruleSubject != null)
                {
                    rdfRules.RuleNode = ruleSubject;
                }
                if (rulebody != null)
                {
                    foreach (Part p in rulebody.ArgList)
                    {
                        GatherTermAntecedants(p, rdfRules);
                    }
                }
                var definations = rdfRules.def;
                string bad = rdfRules.Check(definations);
                if (!String.IsNullOrEmpty(bad))
                {
                    bad += " in DB " + rdfRules.ToString();
                    Warn(bad);
                }
                rdfRules.RequirementsMet = String.IsNullOrEmpty(rdfRules.Check(definations));
            }

            static private void GatherTermAntecedants(Part part, RdfRules anteceeds)
            {
                if (part is Term)
                {
                    var rdf = CreateAntecedantNode((Term)part, anteceeds);
                    if (rdf != null)
                    {
                        anteceeds.AddSubject(rdf);
                    }
                    return;
                }
                throw ErrorBadOp("Part is not a Term " + part);
            }

            private static bool IsTriple(Term term)
            {
                if (term.fname == TripleName) return true;
                return false;
            }

            static private Term unifyvar(Part p1, Variable p2)
            {
                return MakeTerm("unify", p1, p2);
            }

            public static Triple CreateImplication(IGraph def, ICollection<Triple> bodytriples, ICollection<Triple> headtriples)
            {
                return MakeTriple(ToBracket(def, bodytriples), def.CreateUriNode("log:implies"),
                                  ToBracket(def, headtriples));
            }

            public static IGraphLiteralNode ToBracket(INodeFactory def, IEnumerable<Triple> bodytriples)
            {
                Graph subgraph = new Graph();
                foreach (Triple triple in bodytriples)
                {
                    subgraph.Assert(triple);
                }
                var group = def.CreateGraphLiteralNode(subgraph);
                return group;
            }

            static private INode CreateTriplesWithGeneratedSubject(Term term, RdfRules triples, bool isPrecond)
            {
                if (IsRdfBuiltIn(term.fname, term.Arity, term, triples))
                {
                    Warn("RDFBuiltin passed to Create Subject");
                    INode bio;
                    if (BuiltinToRDF(!isPrecond, term, triples, isPrecond ? (Action<Triple>)triples.AddRequirement : triples.AddConsequent, out bio))
                        return bio;
                }
                var headDef = GetPredicateProperty(term);
                INode subj = CreateInstance(headDef, term, triples, isPrecond ? NodeType.Variable : NodeType.Uri);
                triples.AddSubject(subj);
                var conds = AddTriplesSubject(term, triples, subj);
                foreach (Triple triple in conds)
                {
                    if (isPrecond)
                    {
                        triples.AddRequirement(triple);
                    }
                    else
                    {
                        triples.AddConsequent(triple);
                    }
                }
                //if (subj.NodeType != NodeType.Blank) return null;
                return subj;
            }

            static private List<Triple> AddTriplesSubject(Term term, RdfRules triples, INode subj)
            {
                int argNum = 1;
                var conds = new List<Triple>();
                var headDef = GetPredicateProperty(term);
                foreach (Part part in term.ArgList)
                {
                    RDFArgSpec argDef = GetAdef(headDef, argNum, false);
                    if (part is Variable)
                    {
                        if (argDef == null)
                        {
                            argDef = GetAdef(headDef, argNum, true);
                            argDef.AddRangeTypeName(((Variable)part).vname);
                        }
                    }
                    INode obj = PartToRdf(part, triples);
                    if (argDef == null)
                    {
                        argDef = GetAdef(headDef, argNum, true);
                    }
                    INode pred = argDef.GetRefNode(rdfDefinations);
                    conds.Add(MakeTriple(subj, pred, obj, false));
                    argNum++;
                }
                return conds;
            }

            static private INode CreateConsequentNode(Term termIn, RdfRules triples, bool hasBody)
            {
                var term = termIn;
                term = ToTranslated(term, triples);
                if (IsRdfBuiltIn(term.fname, term.Arity, term, triples))
                {
                    INode bio;
                    if (BuiltinToRDF(!hasBody, term, triples, triples.AddConsequent, out bio)) return null;
                }
                var rdf0 = CreateTriplesWithGeneratedSubject(term, triples, false);
                triples.AddSubject(rdf0);
                if (rdf0.NodeType != NodeType.Blank) return null;
                return rdf0;
            }

            static private INode CreateAntecedantNode(Term termIn, RdfRules triples)
            {
                var term = termIn;
                term = ToTranslated(term, triples);
                if (IsRdfBuiltIn(term.fname, term.Arity, term, triples))
                {
                    INode bio;
                    if (BuiltinToRDF(false, term, triples, triples.AddRequirement, out bio))
                    {
                        return bio;
                    }
                }
                var rdf0 = CreateTriplesWithGeneratedSubject(term, triples, true);
                triples.AddSubject(rdf0);
                return rdf0;
            }

            private static Term ToTranslated(Term term, RdfRules triples)
            {
                if (term.Arity == 0) return ToTranslated(MakeTerm("asserted", Atom.FromSource(term.fname)), triples);
                if (term.Arity == 1)
                {
                    Part arg0 = term.ArgList[0];
                    if (false && !IsLitteral(arg0, triples))
                        return ToTranslated(
                            MakeTerm("rdf:type", arg0, Atom.FromSource(PredicateToType(term.fname))), triples);
                    return ToTranslated(
                        MakeTerm("prologUnaryTrue", arg0, Atom.FromSource(PredicateToType(term.fname))), triples);
                }
                if (term.Arity == 2) return term;
                if (term.Arity > 2)
                {
                    // TO(DO maybe translate here
                    //var satementTerm = new Variable("TERM" + CONSP);
                    return term;
                }
                return term;
            }

            static private bool BuiltinToRDF(bool toplevel, Term term, RdfRules antecedants, Action<Triple> howToAdd, out INode bio)
            {
                var definations = antecedants.def;
                int arity = term.Arity;
                if (arity == 2)
                {
                    bio = PartToRdf(term.ArgList[0], antecedants);
                    howToAdd(MakeTriple(bio,
                                        PredicateToProperty(term.fname),
                                        PartToRdf(term.ArgList[1], antecedants), toplevel));
                    bio = null;
                    return true;
                }
                if (term.fname == TripleName && arity == 3)
                {
                    var al = term.ArgList;
                    bio = PartToRdf(al[0], antecedants);
                    howToAdd(MakeTriple(bio, PartToRdf(al[1], antecedants),
                               PartToRdf(al[2], antecedants)));
                    bio = null;
                    return true;
                }
                if (arity == 1)
                {
                    bio = PartToRdf(term.ArgList[0], antecedants);
                    if (!(bio is ILiteralNode))
                    {
                        var dataType = C(definations, PredicateToType(term.fname));
                        howToAdd(MakeTriple(bio, InstanceOf, dataType));
                        return true;
                    }
                }
                ErrorBadOp("Cannot create RDF (Builtin) from " + term);
                bio = null;
                return false;
            }

            static string PredicateToType(string unaryPred)
            {
                if (unaryPred == "call")
                {
                    return "rdf:Statement";
                }
                if (unaryPred == "not")
                {
                    return "rdf:FalseStatement";
                }
                return AsURIString(unaryPred);
            }

            public static string AsURIString(string unaryPred)
            {
                if (unaryPred.Contains(":")) return unaryPred;
                return ":" + unaryPred;
            }

            static public INode PredicateToProperty(string binaryPred)
            {
                string rdfName;
                if (TryPredicateToPropertyString(binaryPred, out rdfName))
                {
                    return C(rdfDefinations, rdfName);                    
                }
                return C(rdfDefinations, AsURIString(binaryPred));
            }

            private static readonly Dictionary<string, string> PredicateToPropertyStringTable =
                new Dictionary<string, string>()
                    {
                        {"unify", "owl:sameAs"},
                        {":-", "siprolog:impliedFrom"},
                        {",", "siprolog:andP"},
                        {";", "siprolog:orP"}
                    };
            private static bool TryPredicateToPropertyString(string binaryPred, out string rdfName)
            {
                lock (PredicateToPropertyStringTable) return PredicateToPropertyStringTable.TryGetValue(binaryPred, out rdfName);
            }
            private static string PredicateToPropertyString(string binaryPred)
            {
                string rdfName;
                if (TryPredicateToPropertyString(binaryPred, out rdfName))
                {
                    return rdfName;
                }
                return AsURIString(binaryPred);
            }

            public static int GetInstanceOnArg(string name)
            {
                int instanceOnArg = GetInstanceOnArg(name, false);
                if (instanceOnArg > -1) return instanceOnArg;
                instanceOnArg = GetInstanceOnArg(name, true);
                if (instanceOnArg > -1) return instanceOnArg;
                if (name.Contains(":")) return 1;
                return instanceOnArg;
            }
            readonly static char[] lcaseLetters = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
            readonly static char[] ucaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
            private static int GetInstanceOnArg(string name, bool useFallbacks)
            {
                if (name == ":-") return 0;
                if (name == ",") return 0;
                if (name.IndexOfAny(lcaseLetters) == -1) return 0;
                string iarg = QueryPredicateInfo(name, "prologInstanceArg", useFallbacks);
                if (iarg != null)
                {
                    int arg;
                    if (Int32.TryParse(iarg, out arg))
                    {
                        return arg;
                    }
                }
                if (QueryPredicateInfo(name, "prologMappingType", useFallbacks) == "MapEachArgToProperty")
                {
                    return 0;
                }
                if (QueryPredicateInfo(name, "prologMappingType", useFallbacks) == "DirectOneToOneMapping")
                {
                    return 1;
                }
                return -1;
            }

            static private INode CreateInstance(PredicateProperty headDef, Term term, RdfRules graph, NodeType nodeType)
            {
                var name = term.fname;
                int instanceOnArg = GetInstanceOnArg(name);
                if (instanceOnArg > 0)
                {
                    INode inst = PartToRdf(term.ArgList[instanceOnArg - 1], graph);
                    if (!CanBeSubjectNode(inst))
                    {
                        Warn("Incompatible Subject node " + inst + " in " + term);
                    }
                    return inst;
                }
                return CreateInstance(headDef, graph, nodeType);
            }

            public static bool CanBeSubjectNode(INode node)
            {
                if (node == null)
                    return false;
                if (node.NodeType == NodeType.Blank) return true;
                if (node.NodeType == NodeType.Uri) return true;
                return false;
            }

            static private INode CreateInstance(PredicateProperty headDef, RdfRules graph, NodeType nodeType)
            {
                var definations = graph.def;
                int nxt = headDef.instanceNumber++;
                string iname = "PINST" + nxt + "_" + headDef.keyname;
                INode iln = null;
                switch (nodeType)
                {
                    case NodeType.Blank:
                        iln = CreateBlankNode(definations, iname);
                        break;
                    case NodeType.Variable:
                        iname = iname.Replace("_", "").Replace("_", "").ToUpper();
                        iln = definations.CreateVariableNode(iname);
                        break;
                    case NodeType.Uri:
                        iln = C(definations, iname);
                        break;
                    case NodeType.Literal:
                        iln = definations.CreateLiteralNode(iname);
                        break;
                    case NodeType.GraphLiteral:
                        throw new ArgumentOutOfRangeException("nodeType");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("nodeType");
                }
                var a = InstanceOf;
                var cn = headDef.classNode = headDef.classNode ?? C(rdfDefinations, RoboKindURI + headDef.classname);
                graph.AddProducing(MakeTriple(iln, a, cn, false));
                return iln;
            }

            public static string GetPredFunctor(INode node)
            {
                return Atom.MakeNodeAtom(node).fname;
            }
        }
        public void UpdateSharedGlobalPredDefs()
        {
            lock (SharedGlobalPredDefs)
            {
                if (!SharedGlobalPredDefsDirty)
                    return;
                SharedGlobalPredDefsDirty = false;
                foreach (var defs in SharedGlobalPredDefs.Values)
                {
                    foreach (var t in defs.DefinitionalRDFEnsurerd) rdfGraphAssert(rdfDefinations, t, true, true);
                }
            }
        }
    

        public partial class Rule
        {
            public RdfRules rdfRuleCache;


            public Term AsTerm()
            {
                if (body == null) return head;
                return RuleToTerm(head, body.plist);
            }
        }

        public class PredicateProperty
        {
            public string name;
            public int arity;
            public string classname;
            public string keyname;
            public int instanceNumber = 1;
            static public int varNumber = 1;
            public readonly Dictionary<int, RDFArgSpec> argDefs;
            public INode classNode;
            public string assertionMt;
            public readonly List<Triple> DefinitionalRDF = new List<Triple>();
            public List<Triple> DefinitionalRDFEnsurerd
            {
                get
                {
                    if (DefinitionalRDF.Count == 0)
                    {
                        RdfDocumentPred(rdfDefinations);
                    }
                    return DefinitionalRDF;
                }
            }


            /// <summary>
            /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
            /// </summary>
            /// <returns>
            /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
            /// </returns>
            /// <filterpriority>2</filterpriority>
            public override string ToString()
            {
                return GlobalSharedSettings.StructToString(this);
            }
            public string AToString
            {
                get { return ToString(); }
            }

            public int GetArgNumForName(string argName)
            {
                foreach (KeyValuePair<int, RDFArgSpec> def in argDefs)
                {
                    if (def.Value.NameMatches(argName)) return def.Key;
                }
                throw ErrorBadOp(argName + " for " + this);

            }

            public PredicateProperty(int arity1)
            {
                arity = arity1;
                argDefs = new Dictionary<int, RDFArgSpec>(arity1 + 1);
            }


            public void WriteHtmlInfo(TextWriter writer)
            {
                writer.WriteLine("<pre>");
                writer.WriteLine("<strong><font color='green'>{0}</font></strong>", name + "/" + arity);
                foreach (KeyValuePair<int, RDFArgSpec> def in argDefs)
                {
                    writer.WriteLine(" Arg " + def.Key + " = " + def.Value.ArgNameInfo);
                }
                DumpTriplesPlain(DefinitionalRDFEnsurerd, writer, "{0}", rdfDefinations);
                writer.WriteLine("</pre>");
            }

            public void RdfDocumentPred(Graph rdef)
            {
                {
                    classNode = classNode ?? GraphWithDef.C(rdef, RoboKindURI + classname);
                    DefinitionalRDF.Add(MakeTriple(classNode, GraphWithDef.InstanceOf, GraphWithDef.PrologPredicateClass));
                    for (int i = 0; i < arity; i++)
                    {
                        RDFArgSpec adef = GraphWithDef.GetAdef(this, i + 1, false);
                        if (adef == null)
                        {
                            adef = GraphWithDef.GetAdef(this, i + 1, true);
                        }
                        var subj = adef.GetRefNode(rdfDefinations);
                        DefinitionalRDF.Add(MakeTriple(subj, GraphWithDef.InstanceOf, GraphWithDef.PrologPredicate));
                        DefinitionalRDF.Add(MakeTriple(subj, GraphWithDef.C(rdef, "rdfs:domain"), classNode));
                        DefinitionalRDF.Add(MakeTriple(subj, GraphWithDef.C(rdef, "rdfs:range"),
                                                       GraphWithDef.C(rdef, "rdfs:Literal")));
                        var localArgTypes = SIProlog.localArgTypes;
                        lock (localArgTypes) if (!localArgTypes.Contains(adef)) localArgTypes.Add(adef);
                    }
                }

            }
        }

        public class RDFArgSpec
        {
            public RDFArgSpec(int hintArgNum1Based)
            {
                SharedGlobalPredDefsDirty = true;
                argNumHint = hintArgNum1Based;
            }
            private string predicateArgName = "_";
            public INode predicateNode;
            public int argNumHint = -1;
            public HashSet<string> argNames = new HashSet<string>();
            public List<PredicateProperty> PredicateProperties = new List<PredicateProperty>();
            public string ArgNameInfo
            {
                get
                {
                    return predicateArgName;
                }
            }
            //public string assertionMt;
            public INode GetRefNode(IGraph def)
            {
                if (predicateArgName.EndsWith("_"))
                {
                    predicateArgName += argNumHint;
                    //Warn("Poorly named prolog argument spec will make a pooly named RDF predicate! " + this);

                }
                return predicateNode ??
                       (predicateNode = GraphWithDef.C(def, GraphWithDef.AsURIString(predicateArgName)));
            }
            /// <summary>
            /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
            /// </summary>
            /// <returns>
            /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
            /// </returns>
            /// <filterpriority>2</filterpriority>
            public override string ToString()
            {
                return GlobalSharedSettings.StructToString(this);
            }
            public string AToString
            {
                get { return ToString(); }
            }
            public void AddDomainType(PredicateProperty property, int argNumber1Based)
            {
                lock (PredicateProperties)
                {
                    PredicateProperties.Add(property);
                }
                string functor = property.name;
                if (!predicateArgName.ToLower().StartsWith(functor.ToLower()))
                {
                    predicateArgName = functor + "_" + predicateArgName.TrimStart("_".ToCharArray());
                }
                //AddRangeTypeName("_Arg" + argNumber1Based);
            }

            public static bool IsOkName(string func)
            {
                if (String.IsNullOrEmpty(func)) return false;
                foreach (var c in func)
                {
                    if (c == '_' || Char.IsLetter(c)) continue;
                    return false;
                }
                return true;
            }
            public void AddRangeTypeName(string functor)
            {
                functor = ProperCase(functor);
                if (!IsOkName(functor)) return;
                if (argNames.Add(functor) || !predicateArgName.ToLower().Contains(functor.ToLower()))
                {
                    SharedGlobalPredDefsDirty = true;
                    predicateArgName = predicateArgName + functor;
                }
            }

            private static string ProperCase(string functor)
            {
                functor = functor.Substring(0, 1).ToUpper() + functor.Substring(1);
                return functor;
            }

            public bool NameMatches(string name)
            {
                return predicateArgName.Contains(ProperCase(name));
            }
        }
    }
}