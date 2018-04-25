﻿using LanguageExt;
using static LanguageExt.Prelude;
using System;
using System.Reactive.Linq;
using System.Threading;

using Xunit;

namespace LanguageExtTests
{
    public class DelayTests
    {
 #if !CI
        [Fact]
        public void DelayTest1()
        {
            var span = TimeSpan.FromMilliseconds(500);
            var till = DateTime.Now.Add(span);
            var v = 0;

            delay(() => 1, span).Subscribe(x => v = x);

            while( DateTime.Now < till )
            {
                Assert.True(v == 0);
                Thread.Sleep(10);
            }

            while (DateTime.Now < till.AddMilliseconds(100))
            {
                Thread.Sleep(10);
            }

            Assert.True(v == 1);
        }
#endif
    }
}
