﻿using System;
using System.Collections.Generic;
using Cogbot;
using MushDLR223.ScriptEngines;
using OpenMetaverse;

namespace Cogbot.World
{
    public class SimGeneric : BotMentalAspect
    {
        public ICollection<NamedParam> GetInfoMap()
        {
            return WorldObjects.GetMemberValues(_type, _value);
        }

        public object Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
            }
        }

        public override string ToString()
        {
            if (_value != null)
            {
                string name = _value.ToString();
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return _type + "-" + ID;
        }

        private UUID _id = UUID.Zero;
        public UUID ID
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
            }
        }
        private object _value;
        private readonly string _type;

        public SimGeneric(String type, UUID g)
        {
            _type = type;
            ID = g;
        }

        public FirstOrderTerm GetTerm()
        {
            throw new NotImplementedException();
        }

        public string GetGenericName()
        {
            return "Sim"+_type;
        }
    }
}
