﻿using System;
using System.Xml;

namespace RTParser.Database
{
    public interface ITripleStore
    {
        int assertTriple(string subject, string relation, string value);
        int retractTriple(string subject, string relation, string value);
        int retractAllTriple(string subject, string relation);
        int updateTriple(string subject, string relation, string value);        
        String queryTriple(string subject, string relation, XmlNode templateNode);

        bool IsExcludedRelation(string relation);
        IEntityFilter EntityFilter { get; }
    }
}