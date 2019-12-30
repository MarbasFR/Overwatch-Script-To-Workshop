using System;
using System.Linq;
using System.Collections.Generic;
using Deltin.Deltinteger.LanguageServer;

namespace Deltin.Deltinteger.Parse
{
    public class CallInfo
    {
        public IApplyBlock Function { get; }
        private ScriptFile Script { get; }
        private Dictionary<IApplyBlock, List<DocRange>> Calls { get; } = new Dictionary<IApplyBlock, List<DocRange>>();

        public CallInfo(IApplyBlock function, ScriptFile script)
        {
            Function = function;
            Script = script;
        }

        public void Call(IApplyBlock callBlock, DocRange range)
        {
            if (!Calls.ContainsKey(callBlock)) Calls.Add(callBlock, new List<DocRange>());
            Calls[callBlock].Add(range);
        }

        public void CheckRecursion()
        {
            foreach (var call in Calls)
                if (DoesTreeCall(Function, call.Key))
                    foreach (DocRange range in call.Value)
                        Script.Diagnostics.Error("Recursion is not allowed here.", range);
        }

        private bool DoesTreeCall(IApplyBlock function, IApplyBlock currentCheck)
        {
            if (function == currentCheck)
            {
                if (function is DefinedMethod && ((DefinedMethod)function).IsRecursive)
                    return false;
                else
                    return true;
            }

            foreach (var call in currentCheck.CallInfo.Calls)
                if (DoesTreeCall(function, call.Key))
                    return true;
            return false;
        }
    }
}