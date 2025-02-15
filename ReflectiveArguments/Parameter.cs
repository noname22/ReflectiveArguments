using System;
using System.ComponentModel;
using System.Reflection;

namespace ReflectiveArguments;

enum ParameterType
{
    Option, Argument
}

class Parameter
{
    public ParameterType ParameterType { get; set; }
    public Type DataType { get; set; }
    public string Name { get; set; }
    public string KebabName => Name.ToKebabCase();
    public string Description { get; set; }
    public object DefaultValue { get; set; }
    public bool AcceptsMany { get; set; }

    public object ParseValue(string value, Command command)
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
            throw new ParsingException($"Error when parsing argument {KebabName}: {ex.Message}", command, ex);
        }
    }

    public static Parameter FromParameterInfo(ParameterInfo info) => new Parameter
    {
        AcceptsMany = info.ParameterType.IsArray,
        ParameterType = info.HasDefaultValue ? ParameterType.Option : ParameterType.Argument,
        DataType = info.ParameterType.IsArray ? info.ParameterType.GetElementType() : info.ParameterType,
        DefaultValue = info.DefaultValue,
        Description = info.GetCustomAttribute<DescriptionAttribute>(false)?.Description ?? string.Empty,
        Name = info.Name,
    };
}
