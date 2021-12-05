using PELoader;
using System;

namespace ILInterpreter
{
    public class PlugAttribute : Attribute
    {
        private string _className;
        private SigFlags _flags;
        private ElementType _retType;
        private ElementType[] _paramType;

        public string ClassName { get { return _className; } }
        public SigFlags Flags { get { return _flags; } }
        public ElementType RetType { get { return _retType; } }
        public ElementType[] ParamType { get { return _paramType; } }

        public PlugAttribute(string className, ElementType.EType retType, ElementType.EType[] paramType, SigFlags flags = SigFlags.DEFAULT)
        {
            _className = className;
            _flags = flags;
            _retType = new ElementType(retType);
            _paramType = new ElementType[paramType.Length];
            for (int i = 0; i < paramType.Length; i++) _paramType[i] = new ElementType(paramType[i]);
        }

        public PlugAttribute(string className, ElementType.EType[] paramType, SigFlags flags = SigFlags.DEFAULT)
        {
            _className = className;
            _flags = flags;
            _retType = new ElementType(ElementType.EType.Void);
            _paramType = new ElementType[paramType.Length];
            for (int i = 0; i < paramType.Length; i++) _paramType[i] = new ElementType(paramType[i]);
        }

        public PlugAttribute(string className, ElementType.EType retType, SigFlags flags = SigFlags.DEFAULT)
        {
            _className = className;
            _flags = flags;
            _retType = new ElementType(retType);
            _paramType = null;
        }

        public PlugAttribute(string className, SigFlags flags = SigFlags.DEFAULT)
        {
            _className = className;
            _flags = flags;
            _retType = new ElementType(ElementType.EType.Void);
            _paramType = null;
        }

        public bool IsEquivalent(MemberRefLayout member)
        {
            var signature = member.MemberSignature;

            if (signature.Flags != Flags) return false;
            if (signature.RetType.Type != RetType.Type/* || signature.RetType.Token != RetType.Token*/) return false;
            if (!member.ToString().StartsWith(_className)) return false;

            // if neither takes any params then finish up here
            if (signature.ParamCount == 0 && (ParamType == null || ParamType.Length == 0)) return true;

            // otherwise we must compare params
            if (ParamType == null) return false;
            if (signature.ParamCount != ParamType.Length) return false;
            for (int i = 0; i < ParamType.Length; i++)
            {
                if (signature.Params[i].Type != ParamType[i].Type || signature.Params[i].Token != ParamType[i].Token) return false;
            }
            return true;
        }
    }
}
