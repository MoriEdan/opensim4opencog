using System;
using System.Collections.Generic;
using System.Text;
using DotLisp;
using MushDLR223.Utilities;

namespace MushDLR223.ScriptEngines
{
    sealed public class DotLispInterpreter : DotLispInterpreterBase, ScriptInterpreter
    {
        public DotLispInterpreter() : base()
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override DotLispInterpreterBase MakeInterp(object self)
        {
            var v = new DotLispInterpreter();
            v._dotLispInterpreter = new Interpreter(_dotLispInterpreter);
            v.Intern("*SELF*", self);
            return v;
        } // method: newInterpreter
    }
    abstract public class DotLispInterpreterBase : LispInterpreter
    {
        public object Impl
        {
            get { return _dotLispInterpreter;  }
        }
        internal Interpreter _dotLispInterpreter;
        static private int _dotLispInterpreterCount = 0;
        static private object _dotLispInterpreterInitLock = new object();

        public DotLisp.Interpreter dotLispInterpreter
        {
            get { return _dotLispInterpreter; }
        }

        public override void Dispose()
        {
            dotLispInterpreter.Dispose();
        }

        public override bool LoadsFileType(string filename)
        {
            return filename.EndsWith("lisp") || base.LoadsFileType0(filename);
        }

        public override bool IsSubscriberOf(string eventName)
        {
            if (!eventName.Contains(".")) eventName = eventName.ToLower();
            DotLisp.Symbol o = dotLispInterpreter.intern(eventName);//.Read("DefinedFunction", new System.IO.StringReader(eventName));           
            if (o!=null && o.isDefined()) return true;
            return false;
        }

        public override object GetSymbol(string eventName)
        {
            eventName = eventName.ToLower();
            DotLisp.Symbol o = dotLispInterpreter.intern(eventName);//.Read("DefinedFunction", new System.IO.StringReader(eventName));           
            return o;
        }

        public override void InternType(Type t)
        {
            EnsureInit();
            dotLispInterpreter.InternType(t);
            ScriptManager.AddType(t);
        }

        public DotLispInterpreterBase():base()
        {
        }

        public void WriteLine(string s, params object[] args)
        {
            DLRConsole.DebugWriteLine(s, args);
        }

        public void EnsureInit()
        {
            lock (_dotLispInterpreterInitLock)
            {
                if (_dotLispInterpreter == null)
                {
                    _dotLispInterpreterCount++;
                    _dotLispInterpreter = new DotLisp.Interpreter(null);
                    _dotLispInterpreter.LoadFile("boot.lisp");
                    _dotLispInterpreter.LoadFile("extra.lisp");
                }
            }
        }

        sealed public override void Init(object self)
        {
            EnsureInit();
            Intern("*SELF*", self);
        }

        /// <summary>
        /// 
        /// 
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public override bool LoadFile(string filename, OutputDelegate WriteLine)
        {
            if (!filename.EndsWith(".lisp"))
            {
                filename = filename + ".lisp";
            }
            System.IO.FileInfo fi = new System.IO.FileInfo(filename);
            if (fi.Exists)
            {
                dotLispInterpreter.LoadFile(filename);
                return true;
            }      
            return false;
        } // method: LoadFile


        /// <summary>
        /// 
        /// </summary>
        /// <param name="context_name"></param>
        /// <param name="stringCodeReader"></param>
        /// <returns></returns>
        public override object Read(string context_name, System.IO.TextReader stringCodeReader, OutputDelegate WriteLine)
        {
            return dotLispInterpreter.Read(context_name, stringCodeReader);
        } // method: Read


        /// <summary>
        /// 
        /// </summary>
        /// <param name="codeTree"></param>
        /// <returns></returns>
        public override bool Eof(object codeTree)
        {
           return dotLispInterpreter.Eof(codeTree);
        } // method: Eof


        /// <summary>
        /// 
        /// </summary>
        /// <param name="varname"></param>
        /// <param name="textForm"></param>
        public override void Intern(string varname, object value)
        {
            EnsureInit();
            dotLispInterpreter.Intern(varname, value);
        } // method: Intern


        /// <summary>
        /// 
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public override object Eval(object code)
        {
            EnsureInit();
            return dotLispInterpreter.Eval(code);
        } // method: Eval


        /// <summary>
        /// 
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public override string Str(object code)
        {
            return dotLispInterpreter.Str(code);
        } // method: Str


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// 
        public abstract DotLispInterpreterBase MakeInterp(object self);

        public override ScriptInterpreter newInterpreter(object self)
        {
            return (ScriptInterpreter)MakeInterp(self);
        } // method: newInterpreter
    }
}