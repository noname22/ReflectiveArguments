﻿using System;
using System.Reflection;

namespace ReflectiveArguments
{
    enum ArgumentType
    {
        Explicit, Implicit
    }

    class Argument
    {
        public ArgumentType ArgumentType { get; set; }
        public Type DataType { get; set; }
        public string Name { get; set; }
        public string KebabName => Name.ToKebabCase();
        public string SnakeName => Name.ToUpperSnakeCase();
        public string Description { get; set; }
        public object DefaultValue { get; set; }

        public object ParseValue(string value)
        {
            try
            {
                if (DataType.IsEnum)
                {
                    return Enum.Parse(DataType, value);
                }

                return Convert.ChangeType(value, DataType);
            }
            catch (Exception ex)
            {
                throw new ParsingException($"Error when parsing argument {KebabName}: {ex.Message}", ex);
            }
        }

        public static Argument FromParameterInfo(ParameterInfo info) => new Argument
        {
            ArgumentType = info.HasDefaultValue ? ArgumentType.Explicit : ArgumentType.Implicit,
            DataType = info.ParameterType,
            DefaultValue = info.DefaultValue,
            Description = string.Empty,
            Name = info.Name
        };
    }
}