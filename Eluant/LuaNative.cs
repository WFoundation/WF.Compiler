//
// LuaApi.cs
//
// Authors:
//       Chris Howie <me@chrishowie.com>
//       Dirk Weltz <web@weltz-online.de>
//       Brice Clocher <contact@cybisoft.net>
//
// Copyright (c) 2013 Chris Howie
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Eluant
{
    internal static class LuaNative
    {
        public enum LuaType : int
        {
            None = -1,

            Nil = 0,
            Boolean = 1,
            LightUserdata = 2,
            Number = 3,
            String = 4,
            Table = 5,
            Function = 6,
            Userdata = 7,
            Thread = 8,
        }

        public enum LuaGcOperation : int
        {
            Stop = 0,
            Restart = 1,
            Collect = 2,
            Count = 3,
            Countb = 4,
            Step = 5,
            SetPause = 6,
            SetStepMul = 7,
        }

#if !USE_KOPILUA

	#if MONOTOUCH
		const string LUA_DLL = "__Internal";
	#else
        const string LUA_DLL = "lua5.1.dll";
	#endif

        public const int LUA_REGISTRYINDEX = -10000;
        public const int LUA_ENVIRONINDEX = -10001;
        public const int LUA_GLOBALSINDEX = -10002;

        public const int LUA_MULTRET = -1;

        public static int lua_upvalueindex(int i)
        {
            return LUA_GLOBALSINDEX - i;
        }

        public delegate int lua_CFunction(IntPtr L);

        public static int abs_index(IntPtr L, int i)
        {
            return (i > 0 || i <= LUA_REGISTRYINDEX) ? i : lua_gettop(L) + i + 1;
        }

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_newstate")]
		public static extern IntPtr lua_newstate(LuaRuntime.LuaAllocator f, IntPtr ud);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_atpanic")]
		internal static extern void lua_atpanic(IntPtr luaState, IntPtr panicf);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_checkstack")]
		public static extern int lua_checkstack(IntPtr L, int extra);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_close")]
		public static extern void lua_close(IntPtr L);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_concat")]
		public static extern void lua_concat(IntPtr L, int n);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_createtable")]
		public static extern void lua_createtable(IntPtr L, int narr, int nrec);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_equal")]
		public static extern int lua_equal(IntPtr L, int index1, int index2);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_error")]
		public static extern void lua_error(IntPtr luaState);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_gc")]
		public static extern int lua_gc(IntPtr L, LuaGcOperation what, int data);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_getfield")]
		public static extern void lua_getfield(IntPtr L, int index, [MarshalAs(UnmanagedType.LPStr)] string k);

		public static void lua_getglobal(IntPtr L, string name)
		{
			lua_getfield(L, LUA_GLOBALSINDEX, name);
		}

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_getmetatable")]
		public static extern int lua_getmetatable(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_gettable")]
		public static extern void lua_gettable(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_gettop")]
		public static extern int lua_gettop(IntPtr L);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_insert")]
		public static extern int lua_insert(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_iscfunction")]
		public static extern int lua_iscfunction(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_lessthan")]
		public static extern int lua_lessthan(IntPtr L, int index1, int index2);

		public static void lua_newtable(IntPtr L)
		{
			lua_createtable(L, 0, 0);
		}

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_newthread")]
		public static extern IntPtr lua_newthread(IntPtr L);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_newuserdata")]
		public static extern IntPtr lua_newuserdata(IntPtr L, UIntPtr size);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_next")]
		public static extern int lua_next(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_objlen")]
		public static extern UIntPtr lua_objlen(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pcall")]
		public static extern int lua_pcall(IntPtr L, int nargs, int nresults, int errfunc);

		public static void lua_pop(IntPtr L, int n)
		{
			lua_settop(L, -n - 1);
		}

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushboolean")]
		public static extern void lua_pushboolean(IntPtr L, int b);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushcclosure")]
		public static extern void lua_pushcclosure(IntPtr L, lua_CFunction fn, int n);

		public static void lua_pushcfunction(IntPtr L, lua_CFunction f)
		{
			lua_pushcclosure(L, f, 0);
		}

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushlightuserdata")]
		public static extern void lua_pushlightuserdata(IntPtr L, IntPtr p);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushlstring")]
		public static extern void lua_pushlstring(IntPtr L, [MarshalAs(UnmanagedType.LPStr)] string s, UIntPtr len);

		public static void lua_pushstring(IntPtr L, string s)
		{
			if (s == null)
			{
				lua_pushnil(L);
			}
			else
			{
				lua_pushlstring(L, s, new UIntPtr(unchecked((ulong)s.Length)));
			}
		}

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushnil")]
		public static extern void lua_pushnil(IntPtr L);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushnumber")]
		public static extern void lua_pushnumber(IntPtr L, double n);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushthread")]
		public static extern int lua_pushthread(IntPtr L);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_pushvalue")]
		public static extern void lua_pushvalue(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_rawequal")]
		public static extern int lua_rawequal(IntPtr L, int index1, int index2);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_rawget")]
		public static extern void lua_rawget(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_rawgeti")]
		public static extern void lua_rawgeti(IntPtr L, int index, int n);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_rawset")]
		public static extern void lua_rawset(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_rawseti")]
		public static extern void lua_rawseti(IntPtr L, int index, int n);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_remove")]
		public static extern void lua_remove(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_replace")]
		public static extern void lua_replace(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_resume")]
		public static extern int lua_resume(IntPtr L, int narg);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_setallocf")]
		public static extern void lua_setallocf(IntPtr L, LuaRuntime.LuaAllocator f, IntPtr ud);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_setfield")]
		public static extern void lua_setfield(IntPtr L, int index, [MarshalAs(UnmanagedType.LPStr)] string k);

		public static void lua_setglobal(IntPtr L, string name)
		{
			lua_setfield(L, LUA_GLOBALSINDEX, name);
		}

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_setmetatable")]
		public static extern int lua_setmetatable(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_settable")]
		public static extern void lua_settable(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_settop")]
		public static extern void lua_settop(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_status")]
		public static extern int lua_status(IntPtr L);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_toboolean")]
		public static extern int lua_toboolean(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_tocfunction")]
		public static extern lua_CFunction lua_tocfunction(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_tonumber")]
		public static extern double lua_tonumber(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_topointer")]
		public static extern IntPtr lua_topointer(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_tolstring")]
		public static extern IntPtr lua_tolstring(IntPtr L, int index, ref UIntPtr len);

		public static string lua_tostring(IntPtr L, int index)
		{
			UIntPtr len = UIntPtr.Zero;

			var stringPtr = lua_tolstring(L, index, ref len);
			if (stringPtr == IntPtr.Zero)
			{
				return null;
			}

			int length = checked((int)len.ToUInt32 ());
			// Changed, because the function fails, if the result is a binary chunk
			byte [] buff = new byte [length];
			Marshal.Copy (stringPtr, buff, 0, length);
			// Are the first four bytes "ESC Lua". If yes, than it is a binary chunk.
			// Didn't check on version of Lua, because it isn't relevant.
			if (length > 3 && buff [0] == 0x1B && buff [1] == 0x4C && buff [2] == 0x75 && buff [3] == 0x61) {
				// It is a binary chunk
				StringBuilder s = new StringBuilder (length);
				foreach (byte b in buff)
					s.Append ((char)b);
				return s.ToString ();
			} else {
				return Marshal.PtrToStringAnsi (stringPtr, length);
			}
		}

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_tothread")]
		public static extern IntPtr lua_tothread(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_touserdata")]
		public static extern IntPtr lua_touserdata(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_type")]
		public static extern LuaType lua_type(IntPtr L, int index);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_typename")]
		[return: MarshalAs(UnmanagedType.LPStr)]
		public static extern string lua_typename(IntPtr L, LuaType tp);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_xmove")]
		public static extern void lua_xmove(IntPtr from, IntPtr to, int n);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lua_yield")]
		public static extern int lua_yield(IntPtr L, int nresults);

		// Aux lib.

		public static void luaL_getmetatable(IntPtr L, string name)
		{
			lua_getfield(L, LUA_REGISTRYINDEX, name);
		}

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_loadstring")]
		public static extern int luaL_loadstring(IntPtr L, [MarshalAs(UnmanagedType.LPStr)] string s);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_loadbuffer")]
		public static extern int luaL_loadbuffer(IntPtr L, [MarshalAs(UnmanagedType.LPStr)] string s, int sz, [MarshalAs(UnmanagedType.LPStr)] string name);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_loadbuffer")]
		public static extern int luaL_loadbuffer(IntPtr L, byte[] s, int sz, [MarshalAs(UnmanagedType.LPStr)] string name);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_newmetatable")]
		public static extern int luaL_newmetatable(IntPtr L, [MarshalAs(UnmanagedType.LPStr)] string tname);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_newstate")]
		public static extern IntPtr luaL_newstate();

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_openlibs")]
		public static extern void luaL_openlibs(IntPtr L);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_ref")]
		public static extern int luaL_ref(IntPtr L, int t);

		[DllImport(LUA_DLL, CallingConvention = CallingConvention.Cdecl, EntryPoint = "luaL_unref")]
		public static extern void luaL_unref(IntPtr L, int t, int r);
#endif
	}
}

