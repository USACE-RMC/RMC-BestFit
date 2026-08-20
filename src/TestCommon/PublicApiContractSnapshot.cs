#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RMC.BestFit.TestCommon;

/// <summary>
/// Produces a stable signature snapshot for the externally visible contract of a .NET assembly.
/// </summary>
/// <remarks>
/// The snapshot includes exported types and their declared public or protected members. Inherited
/// framework members and compiler-generated implementation details are intentionally excluded.
/// </remarks>
internal static class PublicApiContractSnapshot
{
    /// <summary>
    /// Creates the canonical public and protected API representation for an assembly.
    /// </summary>
    /// <param name="assembly">Assembly whose exported contract is captured.</param>
    /// <returns>A newline-delimited, ordinally sorted API representation.</returns>
    public static string Create(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var lines = new List<string>();
        const BindingFlags declaredMembers = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (Type type in assembly.GetExportedTypes()
                     .Where(candidate => !IsCompilerGenerated(candidate))
                     .OrderBy(TypeName, StringComparer.Ordinal))
        {
            lines.Add($"TYPE {TypeKind(type)} {TypeName(type)}{DescribeGenericConstraints(type.GetGenericArguments())}");
            if (type.BaseType is not null)
            {
                lines.Add($"  BASE {TypeName(type.BaseType)}");
            }

            foreach (Type implementedInterface in type.GetInterfaces().OrderBy(TypeName, StringComparer.Ordinal))
            {
                lines.Add($"  INTERFACE {TypeName(implementedInterface)}");
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(declaredMembers)
                         .Where(candidate => IsContractVisibility(candidate) && !IsCompilerGenerated(candidate))
                         .OrderBy(DescribeConstructor, StringComparer.Ordinal))
            {
                lines.Add($"  {DescribeConstructor(constructor)}");
            }

            foreach (MethodInfo method in type.GetMethods(declaredMembers)
                         .Where(candidate => !candidate.IsSpecialName && IsContractVisibility(candidate) && !IsCompilerGenerated(candidate))
                         .OrderBy(DescribeMethod, StringComparer.Ordinal))
            {
                lines.Add($"  {DescribeMethod(method)}");
            }

            foreach (PropertyInfo property in type.GetProperties(declaredMembers)
                         .Where(IsContractProperty)
                         .OrderBy(DescribeProperty, StringComparer.Ordinal))
            {
                lines.Add($"  {DescribeProperty(property)}");
            }

            foreach (FieldInfo field in type.GetFields(declaredMembers)
                         .Where(candidate => IsContractVisibility(candidate) && !IsCompilerGenerated(candidate))
                         .OrderBy(DescribeField, StringComparer.Ordinal))
            {
                lines.Add($"  {DescribeField(field)}");
            }

            foreach (EventInfo eventInfo in type.GetEvents(declaredMembers)
                         .Where(IsContractEvent)
                         .OrderBy(DescribeEvent, StringComparer.Ordinal))
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
        $"CTOR {Visibility(constructor)} {TypeName(constructor.DeclaringType!)}({DescribeParameters(constructor.GetParameters())})";

    /// <summary>
    /// Describes a method signature and its generic constraints.
    /// </summary>
    /// <param name="method">Method to describe.</param>
    /// <returns>The canonical method signature.</returns>
    private static string DescribeMethod(MethodInfo method)
    {
        string genericSuffix = method.IsGenericMethodDefinition
            ? $"``{method.GetGenericArguments().Length}{DescribeGenericConstraints(method.GetGenericArguments())}"
            : string.Empty;
        return $"METHOD {Visibility(method)} {MethodModifiers(method)}{TypeName(method.ReturnType)} {method.Name}{genericSuffix}({DescribeParameters(method.GetParameters())})";
    }

    /// <summary>
    /// Describes a property signature and the visibility of each contract accessor.
    /// </summary>
    /// <param name="property">Property to describe.</param>
    /// <returns>The canonical property signature.</returns>
    private static string DescribeProperty(PropertyInfo property)
    {
        string index = property.GetIndexParameters().Length == 0
            ? string.Empty
            : $"[{DescribeParameters(property.GetIndexParameters())}]";
        string getter = DescribeAccessor(property.GetMethod, "get;");
        string setter = DescribeAccessor(property.SetMethod, "set;");
        return $"PROPERTY {TypeName(property.PropertyType)} {property.Name}{index} {{{getter}{setter}}}";
    }

    /// <summary>
    /// Describes a public or protected property accessor.
    /// </summary>
    /// <param name="accessor">Accessor method, if present.</param>
    /// <param name="label">Accessor label.</param>
    /// <returns>The accessor description, or an empty string when it is outside the contract.</returns>
    private static string DescribeAccessor(MethodInfo? accessor, string label) =>
        accessor is not null && IsContractVisibility(accessor) ? $"{Visibility(accessor)} {label}" : string.Empty;

    /// <summary>
    /// Describes a field, including literal values.
    /// </summary>
    /// <param name="field">Field to describe.</param>
    /// <returns>The canonical field signature.</returns>
    private static string DescribeField(FieldInfo field)
    {
        string value = field.IsLiteral ? $" = {FormatConstant(field.GetRawConstantValue())}" : string.Empty;
        return $"FIELD {Visibility(field)} {TypeName(field.FieldType)} {field.Name}{value}";
    }

    /// <summary>
    /// Describes an event and its visible add/remove accessors.
    /// </summary>
    /// <param name="eventInfo">Event to describe.</param>
    /// <returns>The canonical event signature.</returns>
    private static string DescribeEvent(EventInfo eventInfo)
    {
        string add = DescribeAccessor(eventInfo.AddMethod, "add;");
        string remove = DescribeAccessor(eventInfo.RemoveMethod, "remove;");
        return $"EVENT {TypeName(eventInfo.EventHandlerType!)} {eventInfo.Name} {{{add}{remove}}}";
    }

    /// <summary>
    /// Describes a parameter sequence, including direction, optionality, and default values.
    /// </summary>
    /// <param name="parameters">Parameters to describe.</param>
    /// <returns>The canonical parameter sequence.</returns>
    private static string DescribeParameters(IEnumerable<ParameterInfo> parameters) =>
        string.Join(", ", parameters.Select(parameter =>
        {
            string modifier = parameter.IsOut
                ? "out "
                : parameter.ParameterType.IsByRef && parameter.IsIn
                    ? "in "
                    : parameter.ParameterType.IsByRef
                        ? "ref "
                        : string.Empty;
            Type parameterType = parameter.ParameterType.IsByRef
                ? parameter.ParameterType.GetElementType()!
                : parameter.ParameterType;
            string optional = parameter.IsOptional ? $" optional={FormatConstant(parameter.DefaultValue)}" : string.Empty;
            return $"{modifier}{TypeName(parameterType)}{optional}";
        }));

    /// <summary>
    /// Describes generic parameter constraints in declaration order.
    /// </summary>
    /// <param name="genericArguments">Generic arguments to inspect.</param>
    /// <returns>A canonical constraint suffix.</returns>
    private static string DescribeGenericConstraints(IEnumerable<Type> genericArguments)
    {
        var descriptions = new List<string>();
        foreach (Type argument in genericArguments.Where(candidate => candidate.IsGenericParameter))
        {
            var constraints = new List<string>();
            GenericParameterAttributes attributes = argument.GenericParameterAttributes;
            if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
            {
                constraints.Add("class");
            }

            if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
            {
                constraints.Add("struct");
            }

            constraints.AddRange(argument.GetGenericParameterConstraints().Select(TypeName).OrderBy(value => value, StringComparer.Ordinal));
            if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
            {
                constraints.Add("new()");
            }

            if (constraints.Count > 0)
            {
                descriptions.Add($"{argument.Name}:{string.Join("&", constraints)}");
            }
        }

        return descriptions.Count == 0 ? string.Empty : $" WHERE[{string.Join(";", descriptions)}]";
    }

    /// <summary>
    /// Formats a reflected constant using invariant, unambiguous text.
    /// </summary>
    /// <param name="value">Constant value.</param>
    /// <returns>The canonical constant representation.</returns>
    private static string FormatConstant(object? value) => value switch
    {
        null => "null",
        Missing => "missing",
        string text => $"\"{text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
        char character => $"'{character}'",
        bool boolean => boolean ? "true" : "false",
        Enum enumValue => Convert.ToString(enumValue, CultureInfo.InvariantCulture) ?? enumValue.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

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
    /// Classifies the reflected exported type.
    /// </summary>
    /// <param name="type">Type to classify.</param>
    /// <returns>A stable type-kind label.</returns>
    private static string TypeKind(Type type) => type.IsEnum
        ? "enum"
        : type.IsInterface
            ? "interface"
            : type.IsValueType
                ? "struct"
                : type.IsAbstract && type.IsSealed
                    ? "static-class"
                    : type.IsAbstract
                        ? "abstract-class"
                        : "class";

    /// <summary>
    /// Returns source-level method modifiers that affect substitutability.
    /// </summary>
    /// <param name="method">Method to inspect.</param>
    /// <returns>A canonical modifier prefix.</returns>
    private static string MethodModifiers(MethodInfo method)
    {
        if (method.IsAbstract)
        {
            return "abstract ";
        }

        if (method.IsVirtual && method.GetBaseDefinition() == method)
        {
            return method.IsFinal ? "sealed-virtual " : "virtual ";
        }

        if (method.IsVirtual)
        {
            return method.IsFinal ? "sealed-override " : "override ";
        }

        return method.IsStatic ? "static " : string.Empty;
    }

    /// <summary>
    /// Determines whether a method or constructor is public or protected.
    /// </summary>
    /// <param name="method">Method to inspect.</param>
    /// <returns><c>true</c> when the member is part of the external inheritance contract.</returns>
    private static bool IsContractVisibility(MethodBase method) =>
        method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly || method.IsFamilyAndAssembly;

    /// <summary>
    /// Determines whether a field is public or protected.
    /// </summary>
    /// <param name="field">Field to inspect.</param>
    /// <returns><c>true</c> when the field is part of the external inheritance contract.</returns>
    private static bool IsContractVisibility(FieldInfo field) =>
        field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly || field.IsFamilyAndAssembly;

    /// <summary>
    /// Determines whether a property has at least one contract-visible accessor.
    /// </summary>
    /// <param name="property">Property to inspect.</param>
    /// <returns><c>true</c> when the property is externally visible.</returns>
    private static bool IsContractProperty(PropertyInfo property) =>
        !IsCompilerGenerated(property) &&
        ((property.GetMethod is not null && IsContractVisibility(property.GetMethod)) ||
         (property.SetMethod is not null && IsContractVisibility(property.SetMethod)));

    /// <summary>
    /// Determines whether an event has at least one contract-visible accessor.
    /// </summary>
    /// <param name="eventInfo">Event to inspect.</param>
    /// <returns><c>true</c> when the event is externally visible.</returns>
    private static bool IsContractEvent(EventInfo eventInfo) =>
        !IsCompilerGenerated(eventInfo) &&
        ((eventInfo.AddMethod is not null && IsContractVisibility(eventInfo.AddMethod)) ||
         (eventInfo.RemoveMethod is not null && IsContractVisibility(eventInfo.RemoveMethod)));

    /// <summary>
    /// Returns a canonical accessibility label for a method or constructor.
    /// </summary>
    /// <param name="method">Member to inspect.</param>
    /// <returns>The accessibility label.</returns>
    private static string Visibility(MethodBase method) => method.IsPublic
        ? "public"
        : method.IsFamilyOrAssembly
            ? "protected-internal"
            : method.IsFamilyAndAssembly
                ? "private-protected"
                : "protected";

    /// <summary>
    /// Returns a canonical accessibility label for a field.
    /// </summary>
    /// <param name="field">Field to inspect.</param>
    /// <returns>The accessibility label.</returns>
    private static string Visibility(FieldInfo field) => field.IsPublic
        ? "public"
        : field.IsFamilyOrAssembly
            ? "protected-internal"
            : field.IsFamilyAndAssembly
                ? "private-protected"
                : "protected";

    /// <summary>
    /// Detects compiler-generated implementation artifacts.
    /// </summary>
    /// <param name="member">Member to inspect.</param>
    /// <returns><c>true</c> when the compiler generated the member.</returns>
    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) || member.Name.Contains('<', StringComparison.Ordinal);
}
