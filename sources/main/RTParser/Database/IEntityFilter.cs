﻿namespace RTParser.Database
{
    public interface IEntityFilter
    {
        bool IsExcludedSubject(string words);
        bool IsExcludedValue(string words);
    }
}