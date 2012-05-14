/*********************************************************
* 
*  Author:        Douglas R. Miles
*  Copyright (C): 2008, Logicmoo - http://www.kqml.org
*
*  This library is free software; you can redistribute it and/or
*  modify it under the terms of the GNU Lesser General Public
*  License as published by the Free Software Foundation; either
*  version 2.1 of the License, or (at your option) any later version.
*
*  This library is distributed in the hope that it will be useful,
*  but WITHOUT ANY WARRANTY; without even the implied warranty of
*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
*  Lesser General Public License for more details.
*
*  You should have received a copy of the GNU Lesser General Public
*  License along with this library; if not, write to the Free Software
*  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
*
*********************************************************/
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Swicli.Library
{

    /// <summary>
    /// Same as Queue except Dequeue function blocks until there is an object to return.
    /// Note: This class does not need to be synchronized
    /// </summary>
    public class BlockingQueue<T> : Queue<T>
    {
        private object SyncRoot;
        private bool open = true;

        /// <summary>
        /// Create new BlockingQueue.
        /// </summary>
        /// <param name="col">The System.Collections.ICollection to copy elements from</param>
        public BlockingQueue(IEnumerable<T> col)
            : base(col)
        {
            SyncRoot = new object();
            open = true;
        }

        /// <summary>
        /// Create new BlockingQueue.
        /// </summary>
        /// <param name="capacity">The initial number of elements that the queue can contain</param>
        public BlockingQueue(int capacity)
            : base(capacity)
        {
            SyncRoot = new object();
            open = true;
        }

        /// <summary>
        /// Create new BlockingQueue.
        /// </summary>
        public BlockingQueue()
            : base()
        {
            SyncRoot = new object();
            open = true;
        }

        /// <summary>
        /// BlockingQueue Destructor (Close queue, resume any waiting thread).
        /// </summary>
        ~BlockingQueue()
        {
            Close();
        }

        /// <summary>
        /// Remove all objects from the Queue.
        /// </summary>
        public new void Clear()
        {
            lock (SyncRoot)
            {
                base.Clear();
            }
        }

        /// <summary>
        /// Remove all objects from the Queue, resume all dequeue threads.
        /// </summary>
        public void Close()
        {
            lock (SyncRoot)
            {
                open = false;
                base.Clear();
                Monitor.PulseAll(SyncRoot); // resume any waiting threads
            }
        }

        /// <summary>
        /// Removes and returns the object at the beginning of the Queue.
        /// </summary>
        /// <returns>Object in queue.</returns>
        public new T Dequeue()
        {
            return Dequeue(Timeout.Infinite);
        }

        /// <summary>
        /// Removes and returns the object at the beginning of the Queue.
        /// </summary>
        /// <param name="timeout">time to wait before returning</param>
        /// <returns>Object in queue.</returns>
        public T Dequeue(TimeSpan timeout)
        {
            return Dequeue(timeout.Milliseconds);
        }

        /// <summary>
        /// Removes and returns the object at the beginning of the Queue.
        /// </summary>
        /// <param name="timeout">time to wait before returning (in milliseconds)</param>
        /// <returns>Object in queue.</returns>
        public T Dequeue(int timeout)
        {
            lock (SyncRoot)
            {
                while (open && (base.Count == 0))
                {
                    if (!Monitor.Wait(SyncRoot, timeout))
                        throw new InvalidOperationException("Timeout");
                }
                if (open)
                    return base.Dequeue();
                else
                    throw new InvalidOperationException("Queue Closed");
            }
        }

        public bool Dequeue(int timeout, ref T obj)
        {
            lock (SyncRoot)
            {
                while (open && (base.Count == 0))
                {
                    if (!Monitor.Wait(SyncRoot, timeout))
                        return false;
                }
                if (open)
                {
                    obj = base.Dequeue();
                    return true;
                }
                else
                {
                    obj = default(T);
                    return false;
                }
            }
        }

        /// <summary>
        /// Adds an object to the end of the Queue
        /// </summary>
        /// <param name="obj">Object to put in queue</param>
        public new void Enqueue(string name, T obj)
        {
            lock (SyncRoot)
            {
                base.Enqueue(obj);
                Monitor.Pulse(SyncRoot);
            }
        }

        /// <summary>
        /// Open Queue.
        /// </summary>
        public void Open()
        {
            lock (SyncRoot)
            {
                open = true;
            }
        }

        /// <summary>
        /// Gets flag indicating if queue has been closed.
        /// </summary>
        public bool Closed
        {
            get { return !open; }
        }
    }
    public abstract class PrologGenericDelegate
    {
        public static BlockingQueue<Action> PrologEventQueue = new BlockingQueue<Action>();
        static private void PrologEventLoop()
        {
            Action outgoingPacket = null;

            // FIXME: This is kind of ridiculous. Port the HTB code from Simian over ASAP!
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            while (true)
            {
                if (PrologEventQueue.Dequeue(100, ref outgoingPacket))
                {
                    // Very primitive rate limiting, keeps a fixed buffer of time between each packet
                    stopwatch.Stop();
                    if (stopwatch.ElapsedMilliseconds < 10)
                    {
                        //Logger.DebugLog(String.Format("Rate limiting, last packet was {0}ms ago", ms));
                        Thread.Sleep(10 - (int)stopwatch.ElapsedMilliseconds);
                    }
                    try
                    {
                        outgoingPacket();
                    }
                    catch (Exception)
                    {
                    }
                    stopwatch.Start();
                }
            }
        }

        public static Thread PrologGenericDelegateThread;
        static public void EnsureStated()
        {
            if (PrologGenericDelegateThread == null)
            {
                PrologGenericDelegateThread = new Thread(PrologEventLoop);
                PrologGenericDelegateThread.Name = "PrologEventSerializer";
                PrologGenericDelegateThread.TrySetApartmentState(ApartmentState.STA);
                PrologGenericDelegateThread.IsBackground = true;
                PrologClient.RegisterThread(PrologGenericDelegateThread);
                PrologGenericDelegateThread.Start();
            }
        }

#if USE_MUSHDLR
        public static TaskQueueHandler PrologEventQueue = new TaskQueueHandler("PrologEventQueue");
#endif
        private Type[] ParamTypes;
        private bool IsVoid;
        private int ParamArity = -1;
        /// <summary>
        /// prolog predicate arity (invokeMethod.IsStatic ? 0 : 1) + ParamArity + (IsVoid ? 0 : 1);
        /// </summary>       
        public int PrologArity;
        public Delegate Delegate;
        public Type ReturnType;

        private MethodInfo _handlerMethod;
        public MethodInfo HandlerMethod
        {
            get
            {
                if (_handlerMethod != null) return _handlerMethod;
                if (ParamTypes == null) throw new InvalidOperationException("First set instance of DelegateType!");
                Type c = GetType();
                if (IsVoid)
                {
                    if (ParamArity == 0) return c.GetMethod("GenericFun0");
                    return c.GetMethod("GenericFun" + ParamArity).MakeGenericMethod(ParamTypes);
                }
                Type[] typesPlusReturn = new Type[ParamArity + 1];
                Array.Copy(ParamTypes, typesPlusReturn, ParamArity);
                typesPlusReturn[ParamArity] = ReturnType;
                return _handlerMethod = c.GetMethod("GenericFunR" + ParamArity).MakeGenericMethod(typesPlusReturn);
            }
        }

        public void SetInstanceOfDelegateType(Type delegateType)
        {
            var invokeMethod = delegateType.GetMethod("Invoke");
            ReturnType = invokeMethod.ReturnType;
            ParameterInfo[] parms = invokeMethod.GetParameters();
            IsVoid = ReturnType == typeof(void);
            ParamArity = parms.Length;
            // For non static we like to send the first argument in from the Origin's value
            PrologArity = (invokeMethod.IsStatic ? 0 : 1) + ParamArity + (IsVoid ? 0 : 1);
            ParamTypes = new Type[ParamArity];
            for (int i = 0; i < ParamArity; i++)
            {
                ParamTypes[i] = parms[i].ParameterType;
            }
            Delegate = Delegate.CreateDelegate(delegateType, this, HandlerMethod);
            //SyncLock = SyncLock ?? Delegate;
        }

        //public abstract object CallProlog(params object[] args);

        // non-void functions 0-6
        public R GenericFunR0<R>()
        {
            return (R)CallProlog();
        }
        public R GenericFunR1<A, R>(A a)
        {
            return (R)CallProlog(a);
        }
        public R GenericFunR2<A, B, R>(A a, B b)
        {
            return (R)CallProlog(a, b);
        }
        public R GenericFunR3<A, B, C, R>(A a, B b, C c)
        {
            return (R)CallProlog(a, b, c);
        }
        public R GenericFunR4<A, B, C, D, R>(A a, B b, C c, D d)
        {
            return (R)CallProlog(a, b, c, d);
        }
        public R GenericFunR5<A, B, C, D, E, R>(A a, B b, C c, D d, E e)
        {
            return (R)CallProlog(a, b, c, d, e);
        }
        public R GenericFunR6<A, B, C, D, E, F, R>(A a, B b, C c, D d, E e, F f)
        {
            return (R)CallProlog(a, b, c, d, e, f);
        }
        public R GenericFunR7<A, B, C, D, E, F, G, R>(A a, B b, C c, D d, E e, F f, G g)
        {
            return (R)CallProlog(a, b, c, d, e, f, g);
        }
        public R GenericFunR8<A, B, C, D, E, F, G, H, R>(A a, B b, C c, D d, E e, F f, G g, H h)
        {
            return (R)CallProlog(a, b, c, d, e, f, g, h);
        }

        // void functions 0-6
        public void GenericFun0()
        {
            CallPrologV();
        }

        public void GenericFun1<A>(A a)
        {
            CallPrologV(a);
        }
        public void GenericFun2<A, B>(A a, B b)
        {
            CallPrologV(a, b);
        }
        public void GenericFun3<A, B, C>(A a, B b, C c)
        {
            CallPrologV(a, b, c);
        }
        public void GenericFun4<A, B, C, D>(A a, B b, C c, D d)
        {
            CallPrologV(a, b, c, d);
        }
        public void GenericFun5<A, B, C, D, E>(A a, B b, C c, D d, E e)
        {
            CallPrologV(a, b, c, d, e);
        }
        public void GenericFun6<A, B, C, D, E, F>(A a, B b, C c, D d, E e, F f)
        {
            CallPrologV(a, b, c, d, e, f);
        }
        public void GenericFun7<A, B, C, D, E, F, G>(A a, B b, C c, D d, E e, F f, G g)
        {
            CallPrologV(a, b, c, d, e, f, g);
        }
        public void GenericFun8<A, B, C, D, E, F, G, H>(A a, B b, C c, D d, E e, F f, G g, H h)
        {
            CallPrologV(a, b, c, d, e, f, g, h);
        }

        public /*virtual*/ void CallPrologV(params object[] paramz)
        {
            if (!IsUsingGlobalQueue)
            {
                CallProlog0(paramz);
                return;
            }
            PrologEventQueue.Open();
            string threadName = null;// "CallProlog " + Thread.CurrentThread.Name;
            PrologEventQueue.Enqueue(threadName, () => CallProlog0(paramz));
            EnsureStated();
        }
        public /*virtual*/ object CallProlog(params object[] paramz)
        {
            if (!IsUsingGlobalQueue) return CallProlog0(paramz);
            string threadName = null;//"CallProlog " + Thread.CurrentThread.Name;
            AutoResetEvent are = new AutoResetEvent(false);
            object[] result = new object[1];
            PrologEventQueue.Enqueue(threadName, () =>
                                         {
                                            result[0] = CallProlog0(paramz);
                                            are.Set();
                                         });
            EnsureStated();
            are.WaitOne();
            return result[0];
        }

        private object CallProlog0(object[] paramz)
        {
            if (IsSyncronous)
            {
                var syncLock = SyncLock ?? Delegate;
                if (syncLock != null)
                {
                    lock (syncLock)
                    {
                        return CallPrologFast(paramz);
                    }
                }
            }
            return CallPrologFast(paramz);
        }

        public abstract object CallPrologFast(object[] paramz);

        static public bool IsUsingGlobalQueue = true;
        public bool IsSyncronous = true;
        public object SyncLock;

    }
}