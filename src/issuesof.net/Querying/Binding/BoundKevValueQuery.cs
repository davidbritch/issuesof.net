﻿namespace IssuesOfDotNet.Querying
{
    public sealed class BoundKevValueQuery : BoundQuery
    {
        public BoundKevValueQuery(bool isNegated, string key, string value)
        {
            IsNegated = isNegated;
            Key = key;
            Value = value;
        }

        public bool IsNegated { get; }
        public string Key { get; }
        public string Value { get; }
    }
}