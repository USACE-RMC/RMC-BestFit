using System.Globalization;
using System.Reflection;
using System.Text;

namespace RMC.BestFit.Tests.CoreInfrastructure;

/// <summary>
/// Produces a stable, binary-compatibility-oriented description of a public .NET API.
/// </summary>
internal static class PublicApiSnapshot
{
    /// <summary>
    /// Creates the canonical public API representation for an assembly.
    /// </summary>
    /// <param name="assembly">Assembly whose exported surface is captured.</param>
    /// <returns>A newline-delimited, ordinally sorted API representation.</returns>
    public static string Create(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var lines = new List<string>();
        const BindingFlags declaredPublic = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (Type type in assembly.GetExportedTypes().OrderBy(TypeName, StringComparer.Ordinal))
        {
            lines.Add($"TYPE {TypeKind(type)} {TypeName(type)}");
            if (type.BaseType is not null)
            {
                lines.Add($"  BASE {TypeName(type.BaseType)}");
            }

            foreach (Type implementedInterface in type.GetInterfaces().OrderBy(TypeName, StringComparer.Ordinal))
            {
                lines.Add($"  INTERFACE {TypeName(implementedInterface)}");
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(declaredPublic).OrderBy(DescribeConstructor, StringComparer.Ordinal))
            {
                lines.Add($"  {DescribeConstructor(constructor)}");
            }

            foreach (MethodInfo method in type.GetMethods(declaredPublic).Where(candidate => !candidate.IsSpecialName).OrderBy(DescribeMethod, StringComparer.Ordinal))
            {
                lines.Add($"  {DescribeMethod(method)}");
            }

            foreach (PropertyInfo property in type.GetProperties(declaredPublic).OrderBy(DescribeProperty, StringComparer.Ordinal))
            {
                lines.Add($"  {DescribeProperty(property)}");
            }

            foreach (FieldInfo field in type.GetFields(declaredPublic).OrderBy(DescribeField, StringComparer.Ordinal))
            {
                lines.Add($"  {DescribeField(field)}");
            }

            foreach (EventInfo eventInfo in type.GetEvents(declaredPublic).OrderBy(DescribeEvent, StringComparer.Ordinal))
            {
                lines.Add($"  {DescribeEvent(eventInfo)}");
            }
        }

        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// Describes a constructor signature.
    /// </summary>
    /// <param name="constructor">Constructor to describe.</param>
    /// <returns>The canonical constructor signature.</returns>
    private static string DescribeConstructor(ConstructorInfo constructor) =>
        $"CTOR {TypeName(constructor.DeclaringType!)}({DescribeParameters(constructor.GetParameters())})";

    /// <summary>
    /// Describes a method signature.
    /// </summary>
    /// <param name="method">Method to describe.</param>
    /// <returns>The canonical method signature.</returns>
    private static string DescribeMethod(MethodInfo method)
    {
        string genericSuffix = method.IsGenericMethodDefinition ? $"``{method.GetGenericArguments().Length}" : string.Empty;
        return $"METHOD {TypeName(method.ReturnType)} {method.Name}{genericSuffix}({DescribeParameters(method.GetParameters())})";
    }

    /// <summary>
    /// Describes a property signature and public accessor availability.
    /// </summary>
    /// <param name="property">Property to describe.</param>
    /// <returns>The canonical property signature.</returns>
    private static string DescribeProperty(PropertyInfo property)
    {
        string index = property.GetIndexParameters().Length == 0 ? string.Empty : $"[{DescribeParameters(property.GetIndexParameters())}]";
        string accessors = $"{{{(property.GetMethod?.IsPublic == true ? "get;" : string.Empty)}{(property.SetMethod?.IsPublic == true ? "set;" : string.Empty)}}}";
        return $"PROPERTY {TypeName(property.PropertyType)} {property.Name}{index} {accessors}";
    }

    /// <summary>
    /// Describes a public field, including literal enum values.
    /// </summary>
    /// <param name="field">Field to describe.</param>
    /// <returns>The canonical field signature.</returns>
    private static string DescribeField(FieldInfo field)
    {
        string value = field.IsLiteral
            ? $" = {Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture)}"
            : string.Empty;
        return $"FIELD {TypeName(field.FieldType)} {field.Name}{value}";
    }

    /// <summary>
    /// Describes an event signature.
    /// </summary>
    /// <param name="eventInfo">Event to describe.</param>
    /// <returns>The canonical event signature.</returns>
    private static string DescribeEvent(EventInfo eventInfo) =>
        $"EVENT {TypeName(eventInfo.EventHandlerType!)} {eventInfo.Name}";

    /// <summary>
    /// Describes a parameter sequence, including ref/out and optional modifiers.
    /// </summary>
    /// <param name="parameters">Parameters to describe.</param>
    /// <returns>The canonical parameter sequence.</returns>
    private static string DescribeParameters(IEnumerable<ParameterInfo> parameters) =>
        string.Join(", ", parameters.Select(parameter =>
        {
            string modifier = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : string.Empty;
            string optional = parameter.IsOptional ? " optional" : string.Empty;
            Type parameterType = parameter.ParameterType.IsByRef ? parameter.ParameterType.GetElementType()! : parameter.ParameterType;
            return $"{modifier}{TypeName(parameterType)}{optional}";
        }));

    /// <summary>
    /// Returns a stable name for a reflected type.
    /// </summary>
    /// <param name="type">Type to name.</param>
    /// <returns>A canonical type name.</returns>
    private static string TypeName(Type type)
    {
        if (type.IsArray)
        {
            return $"{TypeName(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]";
        }

        if (type.IsPointer)
        {
            return $"{TypeName(type.GetElementType()!)}*";
        }

        if (type.IsGenericParameter)
        {
            return $"`{type.GenericParameterPosition}:{type.Name}";
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        Type definition = type.GetGenericTypeDefinition();
        string definitionName = (definition.FullName ?? definition.Name).Split('`')[0];
        return $"{definitionName}<{string.Join(",", type.GetGenericArguments().Select(TypeName))}>";
    }

    /// <summary>
    /// Classifies the reflected public type.
    /// </summary>
    /// <param name="type">Type to classify.</param>
    /// <returns>A stable type-kind label.</returns>
    private static string TypeKind(Type type) =>
        type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsValueType ? "struct" : type.IsAbstract && type.IsSealed ? "static-class" : type.IsAbstract ? "abstract-class" : "class";
}