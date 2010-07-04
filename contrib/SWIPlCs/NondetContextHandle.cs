using System;
using System.Collections.Generic;
using SbsSW.SwiPlCs.Callback;
using SbsSW.SwiPlCs.Exceptions;

namespace SbsSW.SwiPlCs
{
    public interface SCCH
    {
        bool Setup(PlTermV a0);
        bool Call(PlTermV a0);
        bool Close(PlTermV a0);
        bool HasMore();
    }


    public interface NDCCH : SCCH
    {
        bool HasMore();
    }

    public interface NDCCHBOX : SCCH
    {
        bool Setup(PlTermV a0);
        bool Call(PlTermV a0);
        bool Close(PlTermV a0);
        bool HasMore();

        NDCCH New(ContextHandle handle, PlTermV a0);
        int Call0(ContextHandle handle, PlTermV termV);
        bool Close(ContextHandle handle, PlTermV a0);
    }

    abstract public class AbstractNondetMethod : IDisposable, NDCCHBOX
    {
        readonly private DelegateParameterBacktrackVarArgs del;
        protected string Module = null;
        protected string Name;
        protected int Arity = -1;

        protected AbstractNondetMethod()
        {
            del = BackrackImpl;
        }

        public virtual void Register()
        {
            if (Name == null) Name = GetType().Name.ToLower();
            if (Arity < 0)
            {
                Arity = 1;
            }
            libpl.PL_register_foreign_in_module(Module, Name, Arity, del,
                                                (int)(PlForeignSwitches.Nondeterministic | PlForeignSwitches.VarArgs));
        }

        public virtual void Dispose()
        {
            //   libpl.PL_register_foreign_in_module(Module, Name, Arity, del,
            //                                     (int)(PlForeignSwitches.Nondeterministic | PlForeignSwitches.VarArgs));
        }

        public int BackrackImpl(PlTerm a0, int arity, IntPtr control)
        {
            FRG fc = (FRG) (libpl.PL_foreign_control(control));
            ContextHandle handle;
            switch (fc)
            {
                case FRG.PL_FIRST_CALL:
                    {
                        handle = NondetContextHandle.ObtainHandle(control, Clone());
                        var av = new PlTermV(a0, arity);
                        handle.ManagedObject = New(handle, av);
                        return Call0(handle, av);
                    }
                    break;
                case FRG.PL_REDO:
                    {
                        handle = NondetContextHandle.FindHandle(control);
                        return Call0(handle, new PlTermV(a0, arity));
                    }
                    break;
                case FRG.PL_CUTTED:
                    {
                        handle = NondetContextHandle.FindHandle(control);
                        var av = new PlTermV(a0, arity);
                        bool res = Close(handle, av);
                        NondetContextHandle.ReleaseHandle(handle);
                        return res ? 1 : 0;
                    }
                    break;
                default:
                    {
                        throw new PlException("no frg");
                        return libpl.PL_fail;
                    }
                    break;
            }
        }

        public virtual NDCCH New(ContextHandle handle, PlTermV a0)
        {
            handle.Setup(a0);
            return (NDCCH)handle.ManagedObject;
        }

        public virtual int Call0(ContextHandle handle, PlTermV termV)
        {
            bool res = handle.Call(termV);
            bool more = handle.HasMore();
            if (more)
            {
                libpl.PL_retry(handle.Handle);
                return res ? 3 : 0;
            }
            return res ? 1 : 0;
        }

        public virtual bool Close(ContextHandle handle, PlTermV a0)
        {
            bool res = handle.Close(a0);
            return res;
        }

        public abstract AbstractNondetMethod Clone();

        public abstract bool Setup(PlTermV a0);
        public abstract bool Call(PlTermV a0);
        public abstract bool Close(PlTermV a0);
        public abstract bool HasMore();
    }

    public class NondetContextHandle : ContextHandle
    {
        static readonly LinkedList<ContextHandle> NonDetHandles = new LinkedList<ContextHandle>();

        static public void ReleaseHandle(ContextHandle hnd)
        {
            lock (NonDetHandles)
            {
                NondetContextHandle.ContextToObject.Remove(hnd.Context);
                hnd.Context = (IntPtr)0;
                NonDetHandles.AddLast(hnd);
            }
        }

        public delegate NondetContextHandle HandleMaker();
        static public ContextHandle ObtainHandle(IntPtr context, SCCH value)
        {
            lock (NonDetHandles)
            {
                ContextHandle hnd;
                if (NonDetHandles.Count == 0)
                {
                    hnd = new NondetContextHandle();
                }
                else
                {
                    hnd = NonDetHandles.First.Value;
                    NonDetHandles.RemoveFirst();
                }
                hnd.Context = context;
                hnd.ManagedObject = value;
                lock (NondetContextHandle.HandleToObject)
                {
                    NondetContextHandle.ContextToObject[context] = hnd;
                }
                return hnd;
            }
        }

        static public ContextHandle ObtainHandle(IntPtr context)
        {
            return ObtainHandle(context, null);
        }
        //static NondetContextHandle lastHandle;
        public static ContextHandle FindHandle(IntPtr context)
        {
            //if (context == (IntPtr)0) return lastHandle;
            lock (NondetContextHandle.HandleToObject) return NondetContextHandle.ContextToObject[context];
        }

        public NondetContextHandle()
        {
            ManagedObject = new ForNext(1, 20);
        }

        #region Overrides of AbstractNondetMethod

        public override bool Setup(PlTermV a0)
        {
            if (MissingImpl()) return false;
            return ManagedObject.Setup(a0);
        }

        private bool MissingImpl()
        {
            if (ManagedObject == null) throw new PlException("not impl");
            return ManagedObject == null;
        }

        public override bool Call(PlTermV a0)
        {
            if (MissingImpl()) return false;
            return ManagedObject.Call(a0);
        }

        public override bool Close(PlTermV a0)
        {
            if (MissingImpl()) return false;
            return ManagedObject.Close(a0);
        }

        public override bool  HasMore()
        {
            if (ManagedObject != null) return ManagedObject.HasMore();
            return false;
        }

        #endregion
    }

    abstract public class ContextHandle
    {
        public static Dictionary<int, ContextHandle> HandleToObject = new Dictionary<int, ContextHandle>();
        public static Dictionary<IntPtr, ContextHandle> ContextToObject = new Dictionary<IntPtr, ContextHandle>();
        public static int TotalHandles = 0;
        public ContextHandle()
        {
            lock (HandleToObject)
            {
                Handle = ++TotalHandles;
                HandleToObject[Handle] = this;
            }

        }
        public readonly int Handle;
        public IntPtr Context;
        public SCCH ManagedObject;

        public abstract bool Call(PlTermV termV);
        public abstract bool Close(PlTermV termV);
        public abstract bool Setup(PlTermV a0);
        public abstract bool HasMore();
    }
}