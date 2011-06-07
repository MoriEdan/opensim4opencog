/*********************************************************
* 
*  Author:        Uwe Lesta
*  Copyright (C): 2008, Uwe Lesta SBS-Softwaresysteme GmbH
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


/*
http://www.codeproject.com/csharp/legacyplugins.asp
 * http://www.cnblogs.com/Dah/archive/2007/01/07/614040.html
 * ggf. 
 * http://www.pcreview.co.uk/forums/thread-2241486.php
 * http://www.msnewsgroups.net/group/microsoft.public.dotnet.languages.csharp/topic12656.aspx
 * 
 * tool to generate the pinvoke lines from Visual Studio 2005
 * http://www.pinvoke.net/
 * */



using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

using System.Security.Permissions;

using Microsoft.Win32.SafeHandles;

//using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;



namespace SbsSW.SwiPlCs
{

	#region Safe Handles and Native imports
	// See http://msdn.microsoft.com/msdnmag/issues/05/10/Reliability/ for more about safe handles.
	[SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
	sealed class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		private SafeLibraryHandle() : base(true) { }

		protected override bool ReleaseHandle()
		{
			return NativeMethods.FreeLibrary(handle);
		}

		public bool UnLoad()
		{
			return ReleaseHandle();
		}

	}

	static class NativeMethods
	{
		const string s_kernel = "kernel32";
		[DllImport(s_kernel, CharSet = CharSet.Auto, BestFitMapping = false, SetLastError = true)]
		public static extern SafeLibraryHandle LoadLibrary(string fileName);

		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		[DllImport(s_kernel, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool FreeLibrary(IntPtr hModule);

        // see: http://blogs.msdn.com/jmstall/archive/2007/01/06/Typesafe-GetProcAddress.aspx
        [DllImport(s_kernel, CharSet = CharSet.Ansi, BestFitMapping = false, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, String procname);
	}
	#endregion // Safe Handles and Native imports



	// for details see http://msdn2.microsoft.com/en-us/library/06686c8c-6ad3-42f7-a355-cbaefa347cfc(vs.80).aspx
	// and http://blogs.msdn.com/fxcop/archive/2007/01/14/faq-how-do-i-fix-a-violation-of-movepinvokestonativemethodsclass.aspx

	//NativeMethods - This class does not suppress stack walks for unmanaged code permission. 
	//    (System.Security.SuppressUnmanagedCodeSecurityAttribute must not be applied to this class.) 
	//    This class is for methods that can be used anywhere because a stack walk will be performed.

	//SafeNativeMethods - This class suppresses stack walks for unmanaged code permission. 
	//    (System.Security.SuppressUnmanagedCodeSecurityAttribute is applied to this class.) 
	//    This class is for methods that are safe for anyone to call. Callers of these methods are not 
	//    required to do a full security review to ensure that the usage is secure because the methods are harmless for any caller.

	//UnsafeNativeMethods - This class suppresses stack walks for unmanaged code permission. 
	//    (System.Security.SuppressUnmanagedCodeSecurityAttribute is applied to this class.) 
	//    This class is for methods that are potentially dangerous. Any caller of these methods must do a 
	//    full security review to ensure that the usage is secure because no stack walk will be performed.


	[ System.Security.SuppressUnmanagedCodeSecurityAttribute ]
	public static class SafeNativeMethods
	{
		//private const string DllFileName = @"D:\Lesta\swi-pl\pl\bin\LibPl.dll";
        //"libpl.dll" for 5.7.8; 
        //public const string DllFileName = @"swiprolog\bin\swipl.dll";
        public const string DllFileName = @"swipl.dll";
        //public const string DllFileName = @"C:\Program Files\pl\bin\swipl.dll";
        static SafeNativeMethods()
        {
            if (!File.Exists(DllFileName))
            {
                Console.WriteLine("No such file: " + DllFileName);
            }
            if (!File.Exists(DllFileName1))
            {
                Console.WriteLine("No such file: " + DllFileName);
            }
        }

        public static string PlLib
        {
            get { return @"swipl.dll"; }
        }

	    //public const string DllFileName = @"swipl.dll";
		public static string DllFileName1
		{
			get
			{
			    var fileName = DllFileName;
                if (File.Exists(fileName)) return fileName;
			    fileName = Path.Combine(Path.Combine(PrologClient.SwiHomeDir ?? PrologClient.AltSwiHomeDir, "bin"), PlLib);
                if (File.Exists(fileName)) return fileName;
                return DllFileName;
			}
		}
	    /////////////////////////////
		/// libpl
		///
        // das funktioniert NICHT wenn umlaute e.g. � im pfad sind.
        [DllImport(DllFileName,CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        internal static extern int PL_initialise(int argc, String[] argv);
		[DllImport(DllFileName)]
			// PL_EXPORT(int)		PL_is_initialised(int *argc, char ***argv);
		internal static extern int PL_is_initialised([In, Out] ref int argc, [In, Out] ref String[] argv);
		[DllImport(DllFileName)]
		internal static extern int PL_is_initialised(IntPtr argc, IntPtr argv);
		[DllImport(DllFileName)]
		internal static extern int PL_halt(int i);
		[DllImport(DllFileName)]
		internal static extern void PL_cleanup(int status);



            // PL_EXPORT(int)		PL_register_foreign_in_module(const char *module, const char *name, int arity, pl_function_t func, int flags);
            // typedef unsigned long	foreign_t
        // int PL_register_foreign_in_module(const char *module, const char *name, int arity, foreign_t (*function)(), int flags)
        [DllImport(DllFileName, CallingConvention=CallingConvention.Cdecl, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        internal static extern int PL_register_foreign_in_module(string module, string name, int arity, Delegate function, int flags);

		//	 ENGINES (MT-ONLY)
		// TYPES :  PL_engine_t			-> void *
		//			PL_thread_attr_t	-> struct
		[DllImport(DllFileName)]
			// PL_EXPORT(PL_engine_t)	PL_create_engine(PL_thread_attr_t *attributes);
		internal static extern IntPtr PL_create_engine(IntPtr attr);
		[DllImport(DllFileName)]	// PL_EXPORT(int)		PlSetEngine(PL_engine_t engine, PL_engine_t *old);
		internal static extern int PL_set_engine(IntPtr engine, [In, Out] ref IntPtr old);
		[DllImport(DllFileName)]	// PL_EXPORT(int)		PL_destroy_engine(PL_engine_t engine);
		internal static extern int PL_destroy_engine(IntPtr engine);


        [DllImport(DllFileName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		internal static extern uint PL_new_atom(string text);
        [DllImport(DllFileName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)] // return const char *
//TODO ausprobieren		[return: MarshalAs(UnmanagedType.LPStr)]
        //internal static extern String PL_atom_chars(uint t_atom);
		internal static extern IntPtr PL_atom_chars(uint t_atom);
        
        // Pl_Query
        [DllImport(DllFileName)]
        internal static extern uint PL_query(uint pl_query_switch);
        
        // PlFrame
        [DllImport(DllFileName)]
        internal static extern uint PL_open_foreign_frame();
        [DllImport(DllFileName)]
		internal static extern void PL_close_foreign_frame(uint fid_t);
		[DllImport(DllFileName)]
		internal static extern void PL_rewind_foreign_frame(uint fid_t);
        // record recorded erase
        [DllImport(DllFileName)]
        internal static extern uint PL_record(uint term_t);
        [DllImport(DllFileName)]
        internal static extern void PL_recorded(uint record_t, uint term_t);
        [DllImport(DllFileName)]
        internal static extern void PL_erase(uint record_t);
        // PlQuery
		[DllImport(DllFileName)]
		internal static extern int PL_next_solution(uint qid_t);
        [DllImport(DllFileName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		internal static extern IntPtr PL_predicate(string name, int arity, string module);
		[DllImport(DllFileName)]
			//qid_t PL_open_query(module_t m, int flags, predicate_t pred, term_t t0);
		internal static extern uint PL_open_query(IntPtr module, int flags, IntPtr pred, uint term);
        [DllImport(DllFileName)]
        internal static extern void PL_cut_query(uint qid);
        [DllImport(DllFileName)]
        internal static extern void PL_close_query(uint qid);
	
		// PlTerm
        [DllImport(DllFileName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)] // return term_t
		internal static extern void PL_put_atom_chars(uint term, string chars);
		//__pl_export term_t	PL_new_term_ref(void);
		[DllImport(DllFileName)] // return term_t
		internal static extern uint PL_new_term_ref();
		//__pl_export void	PL_put_integer(term_t term, long i);
		[DllImport(DllFileName)]
		internal static extern void PL_put_integer(uint term, long i);
		[DllImport(DllFileName)]
		internal static extern void PL_put_float(uint term, double i);
		// __pl_export void	PL_put_atom(term_t term, atom_t atom);
		[DllImport(DllFileName)]
		internal static extern void PL_put_atom(uint term, uint atom_handle);
		// __pl_export int		PL_get_chars(term_t term, char **s, unsigned int flags);
        //[DllImport(DllFileName)]
        //internal static extern int PL_get_chars(uint term, ref string s, uint flags);
        [DllImport(DllFileName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        internal static extern int PL_get_chars(uint term, [In, Out]ref IntPtr s, uint flags);

        // __pl_export int		PL_get_long(term_t term, long *i);
        [DllImport(DllFileName)]
        internal static extern int PL_get_long(uint term, [In, Out] ref int i);
        // __pl_export int		PL_get_long(term_t term, long *i);
        [DllImport(DllFileName)]
        internal static extern int PL_get_long(uint term, [In, Out] ref long i);
        // __pl_export int		PL_get_float(term_t term, double *f);
		[DllImport(DllFileName)]
		internal static extern int PL_get_float(uint term, [In, Out] ref double i);
		// __pl_export int		PL_get_atom(term_t term, atom_t *atom);
		[DllImport(DllFileName)]
		internal static extern int PL_get_atom(uint term, [In, Out] ref uint atom_t);
		//__pl_export int		PL_term_type(term_t term);
		[DllImport(DllFileName)]
		internal static extern int PL_term_type(uint t);

		// COMPARE
		//__pl_export int		PL_compare(term_t t1, term_t t2);
		[DllImport(DllFileName)]
		internal static extern int PL_compare(uint term1, uint term2);

 

		// PlTermV
		[DllImport(DllFileName)] // return term_t
		internal static extern uint PL_new_term_refs(int n);
		//__pl_export void	PL_put_term(term_t t1, term_t t2);
		[DllImport(DllFileName)] 
		internal static extern void PL_put_term(uint t1, uint t2);

		// PlCompound
		// __pl_export int PL_chars_to_term(const char *chars, term_t term);
		//__pl_export void	PL_cons_functor_v(term_t h, functor_t fd, term_t A0);
		//__pl_export functor_t	PL_new_functor(atom_t f, int atom);
        [DllImport(DllFileName, CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        //[DllImport(DllFileName)]
        internal static extern int PL_chars_to_term(string chars, uint term);
        //internal static extern int PL_chars_to_term([In, MarshalAs(UnmanagedType.LPStr)]String chars, uint term);
        [DllImport(DllFileName)]
		internal static extern void PL_cons_functor_v(uint term, uint functor_t, uint term_a0 );
		[DllImport(DllFileName)]
		internal static extern uint PL_new_functor(uint atom_a, int a);

		//__pl_export void	PL_put_string_chars(term_t term, const char *chars);
		//__pl_export void	PL_put_string_nchars(term_t term, unsigned int len, const char *chars);
		//__pl_export void	PL_put_list_codes(term_t term, const char *chars);
		//__pl_export void	PL_put_list_chars(term_t term, const char *chars);
        [DllImport(DllFileName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		internal static extern void PL_put_string_chars(uint term_t, string chars);
        [DllImport(DllFileName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		internal static extern void PL_put_string_nchars(uint term_t, int len, string chars);
        [DllImport(DllFileName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		internal static extern void PL_put_list_codes(uint term_t, string chars);
        [DllImport(DllFileName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		internal static extern void PL_put_list_chars(uint term_t, string chars);
		[DllImport(DllFileName)]
		internal static extern void PL_put_list(uint term_t);

		// Testing the type of a term
		//__pl_export int		PL_is_variable(term_t term);
		//__pl_export int		PL_is_list(term_t term);
		// ...
		[DllImport(DllFileName)]
		internal static extern int PL_is_variable(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_is_ground(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_is_atom(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_is_string(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_is_integer(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_is_float(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_is_compound(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_is_list(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_is_atomic(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_is_number(uint term_t);

		// LISTS (PlTail)
		//__pl_export term_t	PL_copy_term_ref(term_t from);
		//__pl_export int		PL_unify_list(term_t l, term_t h, term_t term);
		//__pl_export int		PL_unify_nil(term_t l);
		//__pl_export int		PL_get_list(term_t l, term_t h, term_t term);
		//__pl_export int		PL_get_nil(term_t l);
		// __pl_export int		PL_unify(term_t t1, term_t t2);
		[DllImport(DllFileName)]
		internal static extern uint PL_copy_term_ref(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_unify_list(uint term_t_l, uint term_t_h, uint term_t_t);
		[DllImport(DllFileName)]
		internal static extern int PL_unify_nil(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_get_list(uint term_t_l, uint term_t_h, uint term_t_t);
		[DllImport(DllFileName)]
		internal static extern int PL_get_nil(uint term_t);
		[DllImport(DllFileName)]
		internal static extern int PL_unify(uint t1,  uint t2);
        [DllImport(DllFileName)]
        internal static extern int PL_unify_integer(uint t1, Int32 n);
        [DllImport(DllFileName)]
        internal static extern int PL_unify_integer(uint t1, Int64 n);
        [DllImport(DllFileName)]
        internal static extern int PL_unify_float(uint t1, double n);
        [DllImport(DllFileName, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        internal static extern int PL_unify_atom_chars(uint t1, string atom);



		// Exceptions
		// Handling exceptions
		//__pl_export term_t	PL_exception(qid_t _qid);
		//__pl_export int		PL_raise_exception(term_t exception);
		//__pl_export int		PL_throw(term_t exception);
		[DllImport(DllFileName)]
		internal static extern uint PL_exception(uint qid);
		[DllImport(DllFileName)]
		internal static extern int PL_raise_exception(uint exception_term);
		//__pl_export int		PL_get_arg(int index, term_t term, term_t atom);
		[DllImport(DllFileName)]
		internal static extern int PL_get_arg(int index, uint t, uint a );
		//__pl_export int		PL_get_name_arity(term_t term, atom_t *Name, int *Arity);
		[DllImport(DllFileName)]
		internal static extern int PL_get_name_arity(uint t, ref uint name, ref int arity);

		// ******************************
		// *	  PROLOG THREADS		*
		// ******************************

		// from file pl-itf.h
		/*
		typedef struct
				{
					unsigned long	    local_size;		// Stack sizes
					unsigned long	    global_size;
					unsigned long	    trail_size;
					unsigned long	    argument_size;
					char *	    alias;					// alias Name
				} PL_thread_attr_t;
		*/
		//PL_EXPORT(int)	PL_thread_self(void);	/* Prolog thread id (-1 if none) */
		//PL_EXPORT(int)	PL_thread_attach_engine(PL_thread_attr_t *attr);
		//PL_EXPORT(int)	PL_thread_destroy_engine(void);
		//PL_EXPORT(int)	PL_thread_at_exit(void (*function)(void *), void *closure, int global);
		[DllImport(DllFileName)]
		internal static extern int PL_thread_self();
		[DllImport(DllFileName)]
		internal static extern int PL_thread_attach_engine(IntPtr attr);
		//internal static extern int PL_thread_attach_engine(ref PL_thread_attr_t attr);
		[DllImport(DllFileName)]
		internal static extern int PL_thread_destroy_engine();



        // ******************************
        // *	  PROLOG STREAM's		*
        // ******************************


        #region structurs

        // int Slinesize

        // IOFUNCTIONS  Sfilefunctions






        /*
         * long ssize_t
         * 
        typedef ssize_t (*Sread_function)(void *handle, char *buf, size_t bufsize);
        typedef ssize_t (*Swrite_function)(void *handle, char*buf, size_t bufsize);
        typedef long  (*Sseek_function)(void *handle, long pos, int whence);
        typedef int64_t (*Sseek64_function)(void *handle, int64_t pos, int whence);
        typedef int   (*Sclose_function)(void *handle);
        typedef int   (*Scontrol_function)(void *handle, int action, void *arg);


        typedef struct io_functions
        { Sread_function	read;		//* fill the buffer
          Swrite_function	write;		//* empty the buffer 
          Sseek_function	seek;		//* seek to position 
          Sclose_function	close;		//* close stream 
          Scontrol_function	control;	//* Info/control 
          Sseek64_function	seek64;		//* seek to position (intptr_t files) 
        } IOFUNCTIONS;
        */

        
        // IOSTREAM    S__iob[3]
        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public struct MIOSTREAM
        {
            /*
            char		    *bufp;		    // `here'
            char		    *limitp;		    // read/write limit 
            char		    *buffer;		    // the buffer 
            char		    *unbuffer;	    // Sungetc buffer 
            int			    lastc;		    // last character written 
            int			    magic;		    // magic number SIO_MAGIC 
            int  			bufsize;	    // size of the buffer 
            int			    flags;		    // Status flags 
            IOPOS			posbuf;		    // location in file 
            IOPOS *		    position;	    // pointer to above 
            IntPtr	        *handle;		    // function's handle 
            MIOFUNCTIONS	*functions;	    // open/close/read/write/seek 
            int		        locks;		    // lock/unlock count 
            */
            //IOLOCK *		    mutex;		    // stream mutex 
            IntPtr mutex;

            long[] place_holder_1;
					            // SWI-Prolog 4.0.7 
              //void			    (*close_hook)(void* closure);
              //void *		    closure;
              //                  // SWI-Prolog 5.1.3 
              //int			    timeout;	    // timeout (milliseconds) 
              //                  // SWI-Prolog 5.4.4 
              //char *		    message;	    // error/warning message 
              //IOENC			    encoding;	    // character encoding used 
              //struct io_stream *	tee;		// copy data to this stream 
              //mbstate_t *		mbstate;	    // ENC_ANSI decoding 
              //struct io_stream *	upstream;	// stream providing our input 
              //struct io_stream *	downstream;	// stream providing our output 
              //unsigned		    newline : 2;	// Newline mode 
              //void *		    exception;	    // pending exception (record_t) 
              //intptr_t		    reserved[2];	// reserved for extension 
        };

        /*

         * 
typedef struct io_position
{ int64_t		byteno;		// byte-position in file 
  int64_t		charno;		// character position in file 
  int			lineno;		// lineno in file 
  int			linepos;	// position in line 
  intptr_t		reserved[2];	// future extensions 
} IOPOS;

         * 
typedef struct io_stream{ 
  char		       *bufp;		    // `here'
  char		       *limitp;		    // read/write limit 
  char		       *buffer;		    // the buffer 
  char		       *unbuffer;	    // Sungetc buffer 
  int			    lastc;		    // last character written 
  int			    magic;		    // magic number SIO_MAGIC 
  int  			    bufsize;	    // size of the buffer 
  int			    flags;		    // Status flags 
  IOPOS			    posbuf;		    // location in file 
  IOPOS *		    position;	    // pointer to above 
  void		       *handle;		    // function's handle 
  IOFUNCTIONS	   *functions;	    // open/close/read/write/seek 
  int		        locks;		    // lock/unlock count 
  IOLOCK *		    mutex;		    // stream mutex 
					// SWI-Prolog 4.0.7 
  void			    (*close_hook)(void* closure);
  void *		    closure;
					// SWI-Prolog 5.1.3 
  int			    timeout;	    // timeout (milliseconds) 
					// SWI-Prolog 5.4.4 
  char *		    message;	    // error/warning message 
  IOENC			    encoding;	    // character encoding used 
  struct io_stream *	tee;		// copy data to this stream 
  mbstate_t *		mbstate;	    // ENC_ANSI decoding 
  struct io_stream *	upstream;	// stream providing our input 
  struct io_stream *	downstream;	// stream providing our output 
  unsigned		    newline : 2;	// Newline mode 
  void *		    exception;	    // pending exception (record_t) 
  intptr_t		    reserved[2];	// reserved for extension 
} IOSTREAM;

         */

        #endregion structurs


        [DllImport(DllFileName)]
        internal static extern int Slinesize();


        // from pl-stream.h
        // PL_EXPORT(IOSTREAM *)	S__getiob(void);	/* get DLL's __iob[] address */
        /// <summary>
        /// 0 -> Sinput
        /// 1 -> Soutput
        /// 2 -> Serror
        /// </summary>
        /// <returns>a array of IOSTREAM * pointers</returns>
        [DllImport(DllFileName)]
        internal static extern IntPtr S__getiob();


        // from pl-stream.h
        // PL_EXPORT(IOSTREAM *)	Snew(void *handle, int flags, IOFUNCTIONS *functions);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="flags">defined in pl-stream.h all with prefix SIO_</param>
        /// <param name="functions">A set of function pointers see IOFUNCTIONS in pl-stream.h</param>
        /// <returns> a SWI-PROLOG IOSTREAM defined in pl-stream.h</returns>
        [DllImport(DllFileName)]
        internal static extern IntPtr Snew(IntPtr handle, int flags, IntPtr functions);

        // from pl-itf.h
        // PL_EXPORT(int)  	PL_unify_stream(term_t t, IOSTREAM *s);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="t"></param>
        /// <param name="iostream">the return value from Snew</param>
        /// <returns></returns>
        [DllImport(DllFileName)]
        internal static extern int PL_unify_stream(uint t, IntPtr iostream);



        [DllImport(DllFileName)]
        internal static extern FRG PL_foreign_control(IntPtr ptr);

	    [DllImport(DllFileName)]
        internal static extern int PL_foreign_context(IntPtr control);

	    [DllImport(DllFileName)]
        internal static extern void _PL_retry(int control);

	    [DllImport(DllFileName)]
        internal static extern void _PL_retry_address(IntPtr control);

	    [DllImport(DllFileName)]
        internal extern static IntPtr PL_foreign_context_address(IntPtr control);

        [DllImport(DllFileName)]
        internal extern static int PL_toplevel();

	    [DllImport(DllFileName)]
	    internal static extern int PL_write_term(IntPtr iostream, uint term, int precedence, int flags);

	} // class SafeNativeMethods

} // namespace SbsSW.SwiPlCs
