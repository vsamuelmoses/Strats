﻿using System;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace LanguageExt
{
    /// <summary>
    /// Common functions that acts like a functional language's 'prelude'; that is usually
    /// a standard set of functions for dealing with built-in types.  In our case we have a
    /// set of core types:
    /// 
    ///     Lst T
    ///     Map T
    ///     Option T
    ///     OptionUnsafe T
    ///     Either L R
    ///     EitherUnsafe L R
    ///     TryOption T
    ///     Try T
    ///     Reader E T
    ///     Writer W T
    ///     State S cT
    ///     Unit
    /// 
    /// </summary>
    public static partial class Prelude
    {
        /// <summary>
        /// 'No value' state of Option T.
        /// </summary>
        public static OptionNone None =>
            OptionNone.Default;

        /// <summary>
        /// Unit constructor
        /// </summary>
        public static Unit unit =>
            Unit.Default;

        /// <summary>
        /// Takes any value, ignores it, returns a unit
        /// </summary>
        /// <param name="anything">Value to ignore</param>
        /// <returns>Unit</returns>
        public static Unit ignore<T>(T anything) =>
            unit;

        /// <summary>
        /// Projects a value into a lambda
        /// Useful when one needs to declare a local variable which breaks your
        /// expression.  This allows you to keep the expression going.
        /// </summary>
        [Pure]
        public static R map<T, R>(T value, Func<T, R> project) =>
            project(value);

        /// <summary>
        /// Projects values into a lambda
        /// Useful when one needs to declare a local variable which breaks your
        /// expression.  This allows you to keep the expression going.
        /// </summary>
        [Pure]
        public static R map<T1, T2, R>(T1 value1, T2 value2, Func<T1, T2, R> project) =>
            project(value1, value2);

        /// <summary>
        /// Projects values into a lambda
        /// Useful when one needs to declare a local variable which breaks your
        /// expression.  This allows you to keep the expression going.
        /// </summary>
        [Pure]
        public static R map<T1, T2, T3, R>(T1 value1, T2 value2, T3 value3, Func<T1, T2, T3, R> project) =>
            project(value1, value2, value3);

        /// <summary>
        /// Projects values into a lambda
        /// Useful when one needs to declare a local variable which breaks your
        /// expression.  This allows you to keep the expression going.
        /// </summary>
        [Pure]
        public static R map<T1, T2, T3, T4, R>(T1 value1, T2 value2, T3 value3, T4 value4, Func<T1, T2, T3, T4, R> project) =>
            project(value1, value2, value3, value4);

        /// <summary>
        /// Projects values into a lambda
        /// Useful when one needs to declare a local variable which breaks your
        /// expression.  This allows you to keep the expression going.
        /// </summary>
        [Pure]
        public static R map<T1, T2, T3, T4, T5, R>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, Func<T1, T2, T3, T4, T5, R> project) =>
            project(value1, value2, value3, value4, value5);

        /// <summary>
        /// Use with the 'match' function to match values and map a result
        /// </summary>
        [Pure]
        public static Func<T, Option<R>> with<T, R>(T value, Func<T, R> map) =>
            (T input) =>
                EqualityComparer<T>.Default.Equals(input, value)
                    ? Some(map(input))
                    : None;

        /// <summary>
        /// Use with the 'match' function to match values and map a result
        /// </summary>
        [Pure]
        public static Func<Exception, Option<R>> with<T, R>(Func<T, R> map)
            where T : Exception =>
            (Exception input) =>
                input is T
                    ? Some(map((T)input))
                    : None;

        /// <summary>
        /// Use with the 'match' function to catch a non-matched value and map a result
        /// </summary>
        [Pure]
        public static Func<T, Option<R>> otherwise<T, R>(Func<T, R> map) =>
            (T input) => Some(map(input));

        /// <summary>
        /// Pattern matching for values
        /// </summary>
        /// <typeparam name="T">Value type to match</typeparam>
        /// <typeparam name="R">Result of expression</typeparam>
        /// <param name="value">Value to match</param>
        /// <param name="clauses">Clauses to test</param>
        /// <returns>Result</returns>
        [Pure]
        public static R match<T, R>(T value, params Func<T, Option<R>>[] clauses)
        {
            foreach (var clause in clauses)
            {
                var res = clause(value);
                if (res.IsSome) return res.IfNone(default(R));
            }
            throw new Exception("Match not exhaustive");
        }

        /// <summary>
        /// Pattern matching for values
        /// </summary>
        /// <typeparam name="T">Value type to match</typeparam>
        /// <typeparam name="R">Result of expression</typeparam>
        /// <param name="value">Value to match</param>
        /// <param name="clauses">Clauses to test</param>
        /// <returns>Result</returns>
        [Pure]
        public static Func<T, R> function<T, R>(params Func<T, Option<R>>[] clauses) => (T value) =>
         {
             foreach (var clause in clauses)
             {
                 var res = clause(value);
                 if (res.IsSome) return res.IfNone(default(R));
             }
             throw new Exception("Match not exhaustive");
         };

        /// <summary>
        /// Identity function
        /// </summary>
        [Pure]
        public static T identity<T>(T x) => x;

        /// <summary>
        /// Raises a lazy Exception with the message provided
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <returns>Action that when executed throws</returns>
        public static Action failwith(string message) =>
            () => { throw new Exception(message); };

        /// <summary>
        /// Raises an Exception with the message provided
        /// </summary>
        /// <typeparam name="R">The return type of the expression this function is being used in.
        /// This allows exceptions to be thrown in ternary operators, or LINQ expressions for
        /// example</typeparam>
        /// <param name="message">Exception message</param>
        /// <returns>Throws an exception</returns>
        public static R failwith<R>(string message)
        {
            throw new Exception(message);
        }

#if !COREFX
        /// <summary>
        /// Raises an ApplicationException with the message provided
        /// </summary>
        /// <typeparam name="R">The return type of the expression this function is being used in.
        /// This allows exceptions to be thrown in ternary operators, or LINQ expressions for
        /// example</typeparam>
        /// <param name="message">ApplicationException message</param>
        /// <returns>Throws an ApplicationException</returns>
        public static R raiseapp<R>(string message)
        {
            throw new ApplicationException(message);
        }
#endif
        /// <summary>
        /// Raise an exception
        /// </summary>
        /// <typeparam name="R">The return type of the expression this function is being used in.
        /// This allows exceptions to be thrown in ternary operators, or LINQ expressions for
        /// example</typeparam>
        /// <param name="ex">Exception to throw</param>
        /// <returns>Throws an exception</returns>
        public static R raise<R>(Exception ex)
        {
            throw ex;
        }

        /// <summary>
        /// Identifies an exception as being of type E
        /// </summary>
        /// <typeparam name="E">Type to match</typeparam>
        /// <param name="e">Exception to test</param>
        /// <returns>True if e is of type E</returns>
        [Pure]
        public static bool exceptionIs<E>(Exception e)
        {
            if (e is E) return true;
            if (e.InnerException == null) return false;
            return exceptionIs<E>(e.InnerException);
        }

        /// <summary>
        /// Calculate a hash-code for an enumerable
        /// </summary>
        public static int hash<T>(IEnumerable<T> xs)
        {
            if (xs == null) return 0;
            unchecked
            {
                int hash = 1;
                foreach(var x in xs)
                {
                    hash = ReferenceEquals(x, null)
                        ? hash * 31
                        : hash * 31 + x.GetHashCode();
                }
                return hash;
            }
        }

        /// <summary>
        /// Not function, for prettifying code and removing the need to 
        /// use the ! operator.
        /// </summary>
        /// <param name="value">Value to perform the not operation on</param>
        /// <returns>!value</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool not(bool value) =>
            !value;

        /// <summary>
        /// Returns true if the value is equal to this type's
        /// default value.
        /// </summary>
        /// <example>
        ///     isDefault(0)  // true
        ///     isDefault(1)  // false
        /// </example>
        /// <returns>True if the value is equal to this type's
        /// default value</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool isDefault<T>(T value) =>
            EqualityComparer<T>.Default.Equals(value, default(T));

        /// <summary>
        /// Returns true if the value is not equal to this type's
        /// default value.
        /// </summary>
        /// <example>
        ///     notDefault(0)  // false
        ///     notDefault(1)  // true
        /// </example>
        /// <returns>True if the value is not equal to this type's
        /// default value</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool notDefault<T>(T value) =>
            !isDefault(value);

        /// <summary>
        /// Returns true if the value is null, and does so without
        /// boxing of any value-types.  Value-types will always
        /// return false.
        /// </summary>
        /// <example>
        ///     int x = 0;
        ///     string y = null;
        ///     
        ///     isnull(x)  // false
        ///     isnull(y)  // true
        /// </example>
        /// <returns>True if the value is null, and does so without
        /// boxing of any value-types.  Value-types will always
        /// return false.</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool isnull<T>(T value) =>
            Check<T>.IsNull(value);

        /// <summary>
        /// Returns true if the value is not null, and does so without
        /// boxing of any value-types.  Value-types will always return 
        /// true.
        /// </summary>
        /// <example>
        ///     int x = 0;
        ///     string y = null;
        ///     string z = "Hello";
        ///     
        ///     notnull(x)  // true
        ///     notnull(y)  // false
        ///     notnull(z)  // true
        /// </example>
        /// <returns>True if the value is null, and does so without
        /// boxing of any value-types.  Value-types will always
        /// return false.</returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool notnull<T>(T value) =>
            !Check<T>.IsNull(value);

        /// <summary>
        /// Convert a value to string
        /// </summary>
        [Pure]
        public static string toString<T>(T value) =>
            value?.ToString();

        static class Check<T>
        {
            static bool IsValueType;

            static Check()
            {
                IsValueType = typeof(T).GetTypeInfo().IsValueType;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsNull(T value) =>
                !IsValueType && EqualityComparer<T>.Default.Equals(value, default(T));
        }
   }
}