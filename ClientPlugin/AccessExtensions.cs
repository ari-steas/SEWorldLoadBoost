﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientPlugin
{
    static class AccessExtensions
    {
        public static object Call(this object o, string methodName, params object[] args)
        {
            var mi = o.GetType ().GetMethod (methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance );
            if (mi != null) {
                return mi.Invoke (o, args);
            }
            return null;
        }
    }
}
